using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WorkSchedule.Models;

// Mirrors Android ShiftTemplate entity — only one active at a time
[Table("shift_templates")]
public class ShiftTemplate
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    // Number of shift rows (2-8)
    [Column("row_count")]
    public int RowCount { get; set; }

    // Number of working day columns (4-7)
    [Column("column_count")]
    public int ColumnCount { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_date")]
    public long CreatedDate { get; set; }

    [Column("last_modified")]
    public long LastModified { get; set; }

    // Navigation properties (EF Core equivalent of Room relations)
    public List<ShiftRow> ShiftRows { get; set; } = [];
    public List<DayColumn> DayColumns { get; set; } = [];
}

// Mirrors Android ShiftRow entity
[Table("shift_rows")]
public class ShiftRow
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("template_id")]
    public int TemplateId { get; set; }

    // Position in table (0-based)
    [Column("order_index")]
    public int OrderIndex { get; set; }

    // e.g. "בוקר"
    [Column("shift_name")]
    public string ShiftName { get; set; } = string.Empty;

    // e.g. "06:45-15:00"
    [Column("shift_hours")]
    public string ShiftHours { get; set; } = string.Empty;

    [NotMapped]
    public string DisplayName => $"{ShiftName} ({ShiftHours})";

    public ShiftTemplate? Template { get; set; }
}

// Mirrors Android DayColumn entity
[Table("day_columns")]
public class DayColumn
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("template_id")]
    public int TemplateId { get; set; }

    // 0=Sunday ... 6=Saturday
    [Column("day_index")]
    public int DayIndex { get; set; }

    [Column("day_name_hebrew")]
    public string DayNameHebrew { get; set; } = string.Empty;

    [Column("day_name_english")]
    public string DayNameEnglish { get; set; } = string.Empty;

    [Column("is_enabled")]
    public bool IsEnabled { get; set; }

    public ShiftTemplate? Template { get; set; }
}
