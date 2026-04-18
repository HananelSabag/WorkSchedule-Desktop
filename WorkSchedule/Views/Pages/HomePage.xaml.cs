using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using WorkSchedule.Models;

namespace WorkSchedule.Views.Pages;

public partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        var db = App.Database;

        var employeeCount = await db.Employees.CountAsync();
        var hasTemplate   = await db.ShiftTemplates.AnyAsync(t => t.IsActive);
        var schedules     = await db.Schedules
                                    .Where(s => s.WeekStart != Schedule.DraftMarker)
                                    .OrderByDescending(s => s.CreatedDate)
                                    .Take(5)
                                    .ToListAsync();
        var hasDraft      = await db.Schedules.AnyAsync(s => s.WeekStart == Schedule.DraftMarker);

        EmployeesCountLabel.Text = employeeCount.ToString();
        SchedulesCountLabel.Text = schedules.Count.ToString();
        LastScheduleLabel.Text   = schedules.FirstOrDefault()?.WeekStart ?? "—";

        if (schedules.Count > 0)
        {
            RecentSchedulesList.ItemsSource = schedules;
            RecentSchedulesList.Visibility  = Visibility.Visible;
            EmptyHistoryHint.Visibility     = Visibility.Collapsed;
        }

        ShowSetupWarning(!hasTemplate, employeeCount == 0);

        // Guard: "New Schedule" disabled until template + employees exist
        bool canCreate = hasTemplate && employeeCount > 0;
        NewScheduleBtn.IsHitTestVisible = canCreate;
        NewScheduleBtn.Opacity          = canCreate ? 1.0 : 0.45;
        NewScheduleBtn.Cursor           = canCreate ? Cursors.Hand : Cursors.Arrow;

        if (Window.GetWindow(this) is MainWindow win)
            win.ShowDraftBanner(hasDraft);
    }

    private void ShowSetupWarning(bool noTemplate, bool noEmployees)
    {
        if (!noTemplate && !noEmployees) { SetupWarningBorder.Visibility = Visibility.Collapsed; return; }

        SetupWarningBorder.Visibility = Visibility.Visible;
        SetupWarningText.Text = (noTemplate && noEmployees)
            ? "לפני שמתחילים — הגדר תבנית טבלה והוסף עובדים."
            : noTemplate ? "טרם הוגדרה תבנית טבלה. הגדר אחת לפני יצירת סידור."
                         : "אין עובדים במערכת. הוסף עובדים לפני יצירת סידור.";

        GoToTemplateBtn.Visibility  = noTemplate  ? Visibility.Visible : Visibility.Collapsed;
        GoToEmployeesBtn.Visibility = noEmployees ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void NewSchedule_Click(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is not MainWindow win) return;

        var hasDraft = await App.Database.Schedules
            .AnyAsync(s => s.WeekStart == Schedule.DraftMarker);

        if (hasDraft)
        {
            var result = AppDialog.Ask(
                "יש טיוטה שמורה מסידור קודם.\n\nכן  ← המשך את הטיוטה\nלא  ← התחל סידור חדש (ימחק את הטיוטה)\nביטול ← חזור",
                "טיוטה קיימת");

            if (result == MessageBoxResult.Cancel) return;

            if (result == MessageBoxResult.Yes)
            {
                await win.ContinueDraftAsync();
                return;
            }

            // No → delete draft and start fresh
            await ScheduleFlowState.DeleteDraftAsync(App.Database);
            win.ShowDraftBanner(false);
        }

        ScheduleFlowState.Begin();
        win.EnterFlowPage(new BlockingPage(), "סידור חדש", "חסימות", pushCurrent: false);
    }

    private void GoToTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow win) win.GoTemplate();
    }

    private void GoToEmployees_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow win) win.GoEmployees();
    }
}
