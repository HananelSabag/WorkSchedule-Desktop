namespace WorkSchedule.Models;

// Port of Android GenericScheduleGenerator.kt
// Greedy MRV (Minimum Remaining Values) constraint-satisfaction scheduler with a repair pass
public static class GenericScheduleGenerator
{
    // ── Internal records ──────────────────────────────────────────────────

    private record ParsedShift(
        string ShiftName,
        TimeOnly StartTime,
        TimeOnly EndTime,
        double   DurationHours,
        bool     IsNightShift);

    private record ShiftSlot(int DayIdx, int ShiftIdx, ParsedShift Shift);
    private record Assignment(int EmpId, string EmpName, ShiftSlot Slot);

    // ── Public entry point ────────────────────────────────────────────────

    /// <summary>
    /// Generates a schedule from the current ScheduleFlowState constraints.
    /// Returns (cellTexts, impossibleShifts) where:
    ///   cellTexts       = Dictionary "dayIdx_shiftIdx" → employee name
    ///   impossibleShifts = List of human-readable "dayName|shiftName" strings for unfilled slots
    /// </summary>
    public static (Dictionary<string, string> Schedule, List<string> ImpossibleShifts) Generate(
        List<Employee>   employees,
        List<DayColumn>  activeDays,
        List<ShiftRow>   activeShifts,
        ScheduleFlowState flowState)
    {
        if (employees.Count == 0 || activeDays.Count == 0 || activeShifts.Count == 0)
            return ([], []);

        var parsed = activeShifts.Select(ParseShift).ToList();

        // All (day, shift) pairs that need assignment
        var unassigned = new List<ShiftSlot>();
        for (int d = 0; d < activeDays.Count; d++)
            for (int s = 0; s < activeShifts.Count; s++)
                unassigned.Add(new ShiftSlot(d, s, parsed[s]));

        var empAssignments = employees.ToDictionary(e => e.Id, _ => new List<Assignment>());
        var schedule       = new Dictionary<string, string>();
        var impossible     = new List<ShiftSlot>();

        // ── PHASE 1: MRV greedy assignment ───────────────────────────────
        while (unassigned.Count > 0)
        {
            // Sort by fewest available employees — hardest slot first
            unassigned.Sort((a, b) =>
                CountAvailable(employees, a, empAssignments, flowState, activeDays)
               .CompareTo(CountAvailable(employees, b, empAssignments, flowState, activeDays)));

            var slot = unassigned[0];
            unassigned.RemoveAt(0);

            var available = employees
                .Where(e => CanAssign(e, slot, empAssignments[e.Id], flowState, activeDays))
                .ToList();

            if (available.Count == 0) { impossible.Add(slot); continue; }

            var best = available
                .OrderBy(e => ScoreEmployee(e, slot, empAssignments[e.Id], activeDays))
                .First();

            empAssignments[best.Id].Add(new Assignment(best.Id, best.Name, slot));
            schedule[$"{slot.DayIdx}_{slot.ShiftIdx}"] = best.Name;
        }

        // ── PHASE 2: Repair pass — swap strategy for impossible slots ─────
        var repaired = new List<ShiftSlot>();

        foreach (var impSlot in impossible)
        {
            bool swapDone = false;
            foreach (var emp in employees)
            {
                var st = flowState.GetCellState(emp.Id, $"{impSlot.DayIdx}_{impSlot.ShiftIdx}");
                if (st is CellBlockState.Cannot or CellBlockState.Shabbat) continue;
                if (!PassesCanOnly(emp, impSlot, flowState)) continue;

                foreach (var existing in empAssignments[emp.Id].ToList())
                {
                    // Can emp take the impossible slot without this one assignment?
                    var temp = empAssignments[emp.Id].Where(a => a != existing).ToList();
                    if (!CanAssign(emp, impSlot, temp, flowState, activeDays)) continue;

                    // Find a substitute for the freed slot
                    var sub = employees
                        .Where(r => r.Id != emp.Id)
                        .Where(r =>
                        {
                            var rst = flowState.GetCellState(r.Id, $"{existing.Slot.DayIdx}_{existing.Slot.ShiftIdx}");
                            return rst is not (CellBlockState.Cannot or CellBlockState.Shabbat);
                        })
                        .Where(r => PassesCanOnly(r, existing.Slot, flowState))
                        .Where(r => CanAssign(r, existing.Slot, empAssignments[r.Id], flowState, activeDays))
                        .OrderBy(r => ScoreEmployee(r, existing.Slot, empAssignments[r.Id], activeDays))
                        .FirstOrDefault();

                    if (sub == null) continue;

                    // Perform swap
                    empAssignments[emp.Id].Remove(existing);
                    empAssignments[emp.Id].Add(new Assignment(emp.Id, emp.Name, impSlot));
                    empAssignments[sub.Id].Add(new Assignment(sub.Id, sub.Name, existing.Slot));

                    schedule[$"{impSlot.DayIdx}_{impSlot.ShiftIdx}"]             = emp.Name;
                    schedule[$"{existing.Slot.DayIdx}_{existing.Slot.ShiftIdx}"] = sub.Name;

                    repaired.Add(impSlot);
                    swapDone = true;
                    break;
                }
                if (swapDone) break;
            }
        }

        var stillImpossible = impossible
            .Except(repaired)
            .Select(s => $"{activeDays[s.DayIdx].DayNameHebrew}|{activeShifts[s.ShiftIdx].ShiftName}")
            .ToList();

        return (schedule, stillImpossible);
    }

    // ── Constraint checking ───────────────────────────────────────────────

    private static int CountAvailable(
        List<Employee> employees,
        ShiftSlot slot,
        Dictionary<int, List<Assignment>> empAssignments,
        ScheduleFlowState flowState,
        List<DayColumn> activeDays) =>
        employees.Count(e => CanAssign(e, slot, empAssignments[e.Id], flowState, activeDays));

    private static bool CanAssign(
        Employee emp,
        ShiftSlot slot,
        List<Assignment> current,
        ScheduleFlowState flowState,
        List<DayColumn> activeDays)
    {
        // Hard rule 1: Cannot / Shabbat block
        var state = flowState.GetCellState(emp.Id, $"{slot.DayIdx}_{slot.ShiftIdx}");
        if (state is CellBlockState.Cannot or CellBlockState.Shabbat) return false;

        // Hard rule 2: Can-only constraint
        if (!PassesCanOnly(emp, slot, flowState)) return false;

        // Hard rule 3: Max hours per day (12h normal, 16h Mitgaber)
        double sameDayHours = current
            .Where(a => a.Slot.DayIdx == slot.DayIdx)
            .Sum(a => a.Slot.Shift.DurationHours);
        double maxHours = emp.IsMitgaber ? 16.0 : 12.0;
        if (sameDayHours + slot.Shift.DurationHours > maxHours) return false;

        // Hard rule 4: No overlapping shifts on the same day
        if (current.Where(a => a.Slot.DayIdx == slot.DayIdx)
                   .Any(a => ShiftsOverlap(a.Slot.Shift, slot.Shift)))
            return false;

        // Hard rule 5: Minimum rest between calendar-adjacent days
        double minRest = emp.IsMitgaber ? 8.0 : 11.0;
        if (ViolatesRest(slot, current, activeDays, minRest)) return false;

        return true;
    }

    private static bool PassesCanOnly(Employee emp, ShiftSlot slot, ScheduleFlowState flowState) =>
        !flowState.CanOnlyBlocks.TryGetValue(emp.Id, out var co) || co.Count == 0
            || co.Contains($"{slot.DayIdx}_{slot.ShiftIdx}");

    // ── Overlap detection ─────────────────────────────────────────────────

    private static bool ShiftsOverlap(ParsedShift a, ParsedShift b)
    {
        int aS = ToMinutes(a.StartTime), aE = ToMinutes(a.EndTime);
        int bS = ToMinutes(b.StartTime), bE = ToMinutes(b.EndTime);

        bool aOver = aE <= aS;  // overnight span
        bool bOver = bE <= bS;

        if (aOver && bOver) return true;
        if (aOver)  return bS > aS || bE < aE;
        if (bOver)  return aS > bS || aE < bE;
        return aS < bE && bS < aE;
    }

    private static int ToMinutes(TimeOnly t) => t.Hour * 60 + t.Minute;

    // ── Rest-period check ─────────────────────────────────────────────────

    private static bool ViolatesRest(
        ShiftSlot slot, List<Assignment> current,
        List<DayColumn> activeDays, double minRest)
    {
        int calDay  = activeDays[slot.DayIdx].DayIndex;
        int calPrev = (calDay + 6) % 7;
        int calNext = (calDay + 1) % 7;

        int prevIdx = activeDays.FindIndex(d => d.DayIndex == calPrev);
        if (prevIdx >= 0)
            foreach (var a in current.Where(a => a.Slot.DayIdx == prevIdx))
                if (RestHours(a.Slot.Shift.EndTime, slot.Shift.StartTime) < minRest)
                    return true;

        // Night shifts may violate the rest rule towards the next day too
        if (slot.Shift.IsNightShift)
        {
            int nextIdx = activeDays.FindIndex(d => d.DayIndex == calNext);
            if (nextIdx >= 0)
                foreach (var a in current.Where(a => a.Slot.DayIdx == nextIdx))
                    if (RestHours(slot.Shift.EndTime, a.Slot.Shift.StartTime) < minRest)
                        return true;
        }

        return false;
    }

    // Hours between shift-end and next-shift-start (handles same-day and cross-midnight)
    private static double RestHours(TimeOnly end, TimeOnly start)
    {
        double h = start > end
            ? (start.ToTimeSpan() - end.ToTimeSpan()).TotalHours
            : (TimeSpan.FromHours(24) - end.ToTimeSpan() + start.ToTimeSpan()).TotalHours;
        return h;
    }

    // ── Soft-constraint scoring (lower = preferred) ───────────────────────

    private static int ScoreEmployee(
        Employee emp, ShiftSlot slot,
        List<Assignment> current, List<DayColumn> activeDays)
    {
        int score = current.Count * 10;  // prefer employees with fewer assignments

        // Soft 1: penalise short rest (< 13 h) on previous calendar day
        if (!emp.IsMitgaber)
        {
            int prevIdx = activeDays.FindIndex(d => d.DayIndex == (activeDays[slot.DayIdx].DayIndex + 6) % 7);
            if (prevIdx >= 0)
                foreach (var a in current.Where(a => a.Slot.DayIdx == prevIdx))
                    if (RestHours(a.Slot.Shift.EndTime, slot.Shift.StartTime) < 13.0)
                        score += 5;
        }

        // Soft 2: group night shifts together
        if (slot.Shift.IsNightShift && !current.Any(a => a.Slot.Shift.IsNightShift))
            score += 3;

        // Soft 3: penalise long consecutive-day streaks
        int streak = ConsecutiveDays(slot.DayIdx, current, activeDays);
        score += streak switch { >= 5 => 25, >= 3 => 10, >= 2 => 4, _ => 0 };

        return score;
    }

    private static int ConsecutiveDays(int dayIdx, List<Assignment> current, List<DayColumn> activeDays)
    {
        int calDay = activeDays[dayIdx].DayIndex;
        int streak = 0;
        for (int i = 1; i <= 6; i++)
        {
            int prev = (calDay - i + 7) % 7;
            if (current.Any(a => activeDays[a.Slot.DayIdx].DayIndex == prev))
                streak++;
            else
                break;
        }
        return streak;
    }

    // ── Shift parsing ─────────────────────────────────────────────────────

    private static ParsedShift ParseShift(ShiftRow row)
    {
        try
        {
            var halves = row.ShiftHours.Split('-');
            var sp     = halves[0].Trim().Split(':');
            var ep     = halves[1].Trim().Split(':');

            int sh = int.Parse(sp[0]), sm = sp.Length > 1 ? int.Parse(sp[1]) : 0;
            int eh = int.Parse(ep[0]), em = ep.Length > 1 ? int.Parse(ep[1]) : 0;

            var start = new TimeOnly(sh, sm);
            var end   = new TimeOnly(eh, em);

            double dur = end > start
                ? (end.ToTimeSpan() - start.ToTimeSpan()).TotalHours
                : (TimeSpan.FromHours(24) - start.ToTimeSpan() + end.ToTimeSpan()).TotalHours;

            bool isNight = sh >= 18 || eh <= 6;

            return new ParsedShift(row.ShiftName, start, end, dur, isNight);
        }
        catch
        {
            return new ParsedShift(row.ShiftName, new TimeOnly(8, 0), new TimeOnly(16, 0), 8.0, false);
        }
    }
}
