using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using WorkSchedule.Models;

namespace WorkSchedule.Views.Pages;

public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        DbPathLabel.Text = Path.Combine(appData, "WorkSchedule", "workschedule.db");
    }

    private async void DeleteAllSchedules_Click(object sender, RoutedEventArgs e)
    {
        if (!AppDialog.Confirm("למחוק את כל הסידורים השמורים? פעולה זו אינה ניתנת לביטול.", "מחיקת כל הסידורים"))
            return;

        var schedules = await App.Database.Schedules
            .Where(s => s.WeekStart != Schedule.DraftMarker)
            .ToListAsync();

        App.Database.Schedules.RemoveRange(schedules);
        await App.Database.SaveChangesAsync();

        AppDialog.ShowSuccess($"נמחקו {schedules.Count} סידורים.", "מחיקה הושלמה");
    }

    private async void DeleteDraft_Click(object sender, RoutedEventArgs e)
    {
        var hasDraft = await App.Database.Schedules.AnyAsync(s => s.WeekStart == Schedule.DraftMarker);
        if (!hasDraft)
        {
            AppDialog.ShowInfo("אין טיוטה שמורה.", "מחיקת טיוטה");
            return;
        }

        await ScheduleFlowState.DeleteDraftAsync(App.Database);

        if (Window.GetWindow(this) is MainWindow win)
            win.ShowDraftBanner(false);

        AppDialog.ShowSuccess("הטיוטה נמחקה.", "מחיקת טיוטה");
    }
}
