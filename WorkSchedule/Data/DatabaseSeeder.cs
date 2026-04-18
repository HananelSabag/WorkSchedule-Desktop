using Microsoft.EntityFrameworkCore;
using WorkSchedule.Models;

namespace WorkSchedule.Data;

// Seeds the default shift template on first launch — mirrors Android's default template creation
public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.ShiftTemplates.AnyAsync())
            return;

        var template = new ShiftTemplate
        {
            Name = "תבנית ברירת מחדל",
            RowCount = 3,
            ColumnCount = 7,
            IsActive = true,
            CreatedDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            LastModified = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ShiftRows =
            [
                new ShiftRow { OrderIndex = 0, ShiftName = "בוקר",   ShiftHours = "07:00-15:00" },
                new ShiftRow { OrderIndex = 1, ShiftName = "צהריים", ShiftHours = "15:00-23:00" },
                new ShiftRow { OrderIndex = 2, ShiftName = "לילה",   ShiftHours = "23:00-07:00" },
            ],
            DayColumns =
            [
                new DayColumn { DayIndex = 0, DayNameHebrew = "ראשון",  DayNameEnglish = "Sunday",    IsEnabled = true },
                new DayColumn { DayIndex = 1, DayNameHebrew = "שני",    DayNameEnglish = "Monday",    IsEnabled = true },
                new DayColumn { DayIndex = 2, DayNameHebrew = "שלישי",  DayNameEnglish = "Tuesday",   IsEnabled = true },
                new DayColumn { DayIndex = 3, DayNameHebrew = "רביעי",  DayNameEnglish = "Wednesday", IsEnabled = true },
                new DayColumn { DayIndex = 4, DayNameHebrew = "חמישי",  DayNameEnglish = "Thursday",  IsEnabled = true },
                new DayColumn { DayIndex = 5, DayNameHebrew = "שישי",   DayNameEnglish = "Friday",    IsEnabled = true },
                new DayColumn { DayIndex = 6, DayNameHebrew = "שבת",    DayNameEnglish = "Saturday",  IsEnabled = true },
            ]
        };

        db.ShiftTemplates.Add(template);
        await db.SaveChangesAsync();
    }
}
