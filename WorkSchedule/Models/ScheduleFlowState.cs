using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WorkSchedule.Data;

namespace WorkSchedule.Models;

public enum BlockMode { Cannot, CanOnly }
public enum CellBlockState { None, Cannot, CanOnly, Shabbat }

// Singleton shared state for the active scheduling flow (blocking → assignment → preview)
public sealed class ScheduleFlowState
{
    public static ScheduleFlowState Current { get; private set; } = new();

    public static void Begin(int? existingId = null)
    {
        // Calculate the coming Sunday → Saturday (next full work week)
        int dow = (int)DateTime.Today.DayOfWeek; // 0=Sunday
        int daysToNextSunday = dow == 0 ? 7 : 7 - dow;
        var start = DateTime.Today.AddDays(daysToNextSunday);
        Current = new ScheduleFlowState
        {
            ExistingScheduleId = existingId,
            WeekStartDate      = start,
            WeekEndDate        = start.AddDays(6)
        };
    }

    public int?     ExistingScheduleId { get; init; }
    public bool     IsEditingExisting  => ExistingScheduleId.HasValue;
    public DateTime WeekStartDate      { get; init; } = DateTime.Today;
    public DateTime WeekEndDate        { get; init; } = DateTime.Today.AddDays(6);

    // "20/04 - 26/04" — used as default schedule name and in headers
    public string WeekLabel => $"{WeekStartDate:dd/MM} - {WeekEndDate:dd/MM}";

    // Key format: "{dayListIndex}_{shiftListIndex}" (0-based within active lists)
    public Dictionary<int, HashSet<string>> CannotBlocks { get; } = [];
    public Dictionary<int, HashSet<string>> CanOnlyBlocks  { get; } = [];
    public Dictionary<int, HashSet<string>> ShabbatBlocks  { get; } = [];

    // Schedule cell content: cellKey → free text (e.g. "חננאל" or "חננאל עד 19")
    public Dictionary<string, string> CellTexts { get; } = [];

    // Populate Shabbat auto-blocks from active template + employee flags
    public void ComputeShabbatBlocks(
        IList<Employee>   employees,
        IList<DayColumn>  activeDays,
        IList<ShiftRow>   activeShifts)
    {
        ShabbatBlocks.Clear();

        foreach (var emp in employees.Where(e => e.ShabbatObserver))
        {
            var keys = new HashSet<string>();
            for (int di = 0; di < activeDays.Count; di++)
            {
                var dayName = activeDays[di].DayNameHebrew;
                bool friday   = dayName.Contains("שישי");
                bool saturday = dayName.Contains("שבת");
                if (!friday && !saturday) continue;

                for (int si = 0; si < activeShifts.Count; si++)
                {
                    var s = activeShifts[si].ShiftName;
                    bool block = (friday   && (s.Contains("צהריים") || s.Contains("לילה")))
                              || (saturday && (s.Contains("בוקר")   || s.Contains("צהריים")));
                    if (block) keys.Add($"{di}_{si}");
                }
            }
            if (keys.Count == 0) continue;

            ShabbatBlocks[emp.Id] = keys;
            if (!CannotBlocks.ContainsKey(emp.Id)) CannotBlocks[emp.Id] = [];
            foreach (var k in keys) CannotBlocks[emp.Id].Add(k);
        }
    }

    public bool IsShabbatLocked(int empId, string key) =>
        ShabbatBlocks.TryGetValue(empId, out var sb) && sb.Contains(key);

    public CellBlockState GetCellState(int empId, string key)
    {
        if (ShabbatBlocks.TryGetValue(empId, out var sb) && sb.Contains(key))  return CellBlockState.Shabbat;
        if (CannotBlocks .TryGetValue(empId, out var cb) && cb.Contains(key))  return CellBlockState.Cannot;
        if (CanOnlyBlocks.TryGetValue(empId, out var co) && co.Contains(key))  return CellBlockState.CanOnly;
        return CellBlockState.None;
    }

    // Toggle a cell block in the given mode. Switching mode clears the other type.
    public void ToggleBlock(int empId, string key, BlockMode mode)
    {
        if (IsShabbatLocked(empId, key)) return;

        if (mode == BlockMode.Cannot)
        {
            CanOnlyBlocks.Remove(empId);
            if (!CannotBlocks.ContainsKey(empId)) CannotBlocks[empId] = [];
            if (!CannotBlocks[empId].Remove(key))  CannotBlocks[empId].Add(key);
        }
        else
        {
            // Remove user-set Cannot blocks, preserving Shabbat
            if (CannotBlocks.TryGetValue(empId, out var cb))
            {
                var shabbat = ShabbatBlocks.GetValueOrDefault(empId, []);
                foreach (var k in cb.Except(shabbat).ToList()) cb.Remove(k);
            }
            if (!CanOnlyBlocks.ContainsKey(empId)) CanOnlyBlocks[empId] = [];
            if (!CanOnlyBlocks[empId].Remove(key))  CanOnlyBlocks[empId].Add(key);
        }
    }

    public void ClearUserBlocks(int empId)
    {
        CanOnlyBlocks.Remove(empId);
        if (CannotBlocks.TryGetValue(empId, out var cb))
        {
            var shabbat = ShabbatBlocks.GetValueOrDefault(empId, []);
            foreach (var k in cb.Except(shabbat).ToList()) cb.Remove(k);
        }
    }

    // Count of user-set blocks (excluding auto Shabbat blocks)
    public int GetUserBlockCount(int empId)
    {
        int n = 0;
        if (CannotBlocks .TryGetValue(empId, out var cb)) n += cb.Except(ShabbatBlocks.GetValueOrDefault(empId, [])).Count();
        if (CanOnlyBlocks.TryGetValue(empId, out var co)) n += co.Count;
        return n;
    }

    // Determine the natural mode for an employee based on existing blocks
    public BlockMode GetNaturalMode(int empId) =>
        CanOnlyBlocks.TryGetValue(empId, out var co) && co.Count > 0
            ? BlockMode.CanOnly : BlockMode.Cannot;

    // ── Draft persistence ─────────────────────────────────────────────────

    public static async Task SaveDraftAsync(AppDbContext db)
    {
        if (Current.IsEditingExisting) return;

        var draft  = await db.Schedules.FirstOrDefaultAsync(s => s.WeekStart == Schedule.DraftMarker);
        bool isNew = draft == null;
        if (isNew) draft = new Schedule { WeekStart = Schedule.DraftMarker };

        draft!.ScheduleData = JsonConvert.SerializeObject(Current.CellTexts);

        // Persist only user-set Cannot blocks (exclude auto Shabbat ones)
        var cannotManual = new Dictionary<int, List<string>>();
        foreach (var (empId, keys) in Current.CannotBlocks)
        {
            var shabbat = Current.ShabbatBlocks.GetValueOrDefault(empId, []);
            var manual  = keys.Except(shabbat).ToList();
            if (manual.Count > 0) cannotManual[empId] = manual;
        }
        draft.BlocksData  = JsonConvert.SerializeObject(cannotManual);
        draft.CanOnlyData = JsonConvert.SerializeObject(
            Current.CanOnlyBlocks
                   .Where(kv => kv.Value.Count > 0)
                   .ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
        draft.SavingModeData = "{}";
        draft.CreatedDate    = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (isNew) db.Schedules.Add(draft);
        await db.SaveChangesAsync();
    }

    public static async Task<bool> LoadDraftAsync(AppDbContext db)
    {
        var draft = await db.Schedules.FirstOrDefaultAsync(s => s.WeekStart == Schedule.DraftMarker);
        if (draft == null) return false;

        var state = new ScheduleFlowState();

        if (!string.IsNullOrEmpty(draft.ScheduleData) && draft.ScheduleData != "{}")
            foreach (var (k, v) in JsonConvert.DeserializeObject<Dictionary<string, string>>(draft.ScheduleData) ?? [])
                state.CellTexts[k] = v;

        if (!string.IsNullOrEmpty(draft.BlocksData) && draft.BlocksData != "{}")
            foreach (var (empId, keys) in JsonConvert.DeserializeObject<Dictionary<int, List<string>>>(draft.BlocksData) ?? [])
                state.CannotBlocks[empId] = [..keys];

        if (!string.IsNullOrEmpty(draft.CanOnlyData) && draft.CanOnlyData != "{}")
            foreach (var (empId, keys) in JsonConvert.DeserializeObject<Dictionary<int, List<string>>>(draft.CanOnlyData) ?? [])
                state.CanOnlyBlocks[empId] = [..keys];

        Current = state;
        return true;
    }

    public static async Task DeleteDraftAsync(AppDbContext db)
    {
        var draft = await db.Schedules.FirstOrDefaultAsync(s => s.WeekStart == Schedule.DraftMarker);
        if (draft == null) return;
        db.Schedules.Remove(draft);
        await db.SaveChangesAsync();
    }
}
