using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WorkSchedule.Models;

// Mirrors the Android Room @Entity Employee
[Table("employees")]
public class Employee
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    // Automatic Shabbat blocks applied when true (Friday afternoon/night + all Saturday)
    [Column("shabbat_observer")]
    public bool ShabbatObserver { get; set; }

    // 16h/day max instead of 12h; more scheduling flexibility
    [Column("is_mitgaber")]
    public bool IsMitgaber { get; set; }
}
