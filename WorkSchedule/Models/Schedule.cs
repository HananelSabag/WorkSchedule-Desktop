using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WorkSchedule.Models;

// Mirrors Android Schedule entity
// scheduleData, blocksData, canOnlyData stored as JSON strings — same as Android Gson approach
[Table("schedules")]
public class Schedule
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    // Week identifier e.g. "20/04 - 26/04", or "__TEMP_DRAFT__" for auto-save draft
    [Required]
    [Column("week_start")]
    public string WeekStart { get; set; } = string.Empty;

    // JSON: Dictionary<"day-shift", List<string>> — e.g. {"ראשון-בוקר": ["דני","יוסי"]}
    [Column("schedule_data")]
    public string ScheduleData { get; set; } = "{}";

    // JSON: Dictionary<"employee-day-shift", bool> — Cannot blocks
    [Column("blocks_data")]
    public string BlocksData { get; set; } = "{}";

    // JSON: Dictionary<"employee-day-shift", bool> — Can-Only blocks
    [Column("can_only_data")]
    public string CanOnlyData { get; set; } = "{}";

    // JSON: Dictionary<"day", bool> — saving mode per day
    [Column("saving_mode_data")]
    public string SavingModeData { get; set; } = "{}";

    [Column("created_date")]
    public long CreatedDate { get; set; }

    [NotMapped]
    public bool IsDraft => WeekStart == DraftMarker;

    public const string DraftMarker = "__TEMP_DRAFT__";
}
