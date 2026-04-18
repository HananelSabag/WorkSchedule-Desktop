using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using WorkSchedule.Models;
using WorkSchedule.Views.Pages;

namespace WorkSchedule.Views;

public partial class MainWindow : Window
{
    private Button? _activeNavButton;
    private bool    _closingAllowed;

    // Stack tracks where we came from inside a schedule flow so Back works correctly
    private readonly Stack<Page> _flowHistory = new();

    public MainWindow()
    {
        InitializeComponent();
        NavigateTo(NavHome, new HomePage());
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshSidebarStatusAsync();
    }

    // ── Public helpers called by flow pages ──────────────────────────────

    // Called when entering a flow (Blocking → Manual/Auto → Preview)
    // step1 = previous step label, step2 = current step label
    public void EnterFlowPage(Page page, string step1, string step2, bool pushCurrent = true)
    {
        if (pushCurrent && ContentFrame.Content is Page current)
            _flowHistory.Push(current);

        FlowStep1.Text = step1;
        FlowStep2.Text = step2;
        FlowBar.Visibility = Visibility.Visible;

        // Deactivate sidebar highlight when in flow
        if (_activeNavButton != null)
            _activeNavButton.Style = (Style)FindResource("NavItemStyle");
        _activeNavButton = null;

        ContentFrame.Navigate(page);
    }

    // Shows draft banner on the sidebar
    public void ShowDraftBanner(bool show) =>
        DraftBanner.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

    // Updates the active template name shown under the logo
    public void SetTemplateName(string name) =>
        ActiveTemplateName.Text = name;

    // ── Sidebar navigation ────────────────────────────────────────────────

    public void NavigateTo(Button navButton, Page page)
    {
        if (_activeNavButton != null)
            _activeNavButton.Style = (Style)FindResource("NavItemStyle");

        navButton.Style = (Style)FindResource("NavItemActiveStyle");
        _activeNavButton = navButton;

        // Leaving the flow — save draft and hide breadcrumb bar
        if (FlowBar.Visibility == Visibility.Visible && !ScheduleFlowState.Current.IsEditingExisting)
            _ = ScheduleFlowState.SaveDraftAsync(App.Database);

        FlowBar.Visibility = Visibility.Collapsed;
        _flowHistory.Clear();

        ContentFrame.Navigate(page);
    }

    private void NavHome_Click(object sender, RoutedEventArgs e)      => GoHome();
    private void NavEmployees_Click(object sender, RoutedEventArgs e) => GoEmployees();
    private void NavTemplate_Click(object sender, RoutedEventArgs e)  => GoTemplate();
    private void NavHistory_Click(object sender, RoutedEventArgs e)   => GoHistory();
    private void NavSettings_Click(object sender, RoutedEventArgs e)  => GoSettings();

    // Public so any page can trigger sidebar navigation
    public void GoHome()      => NavigateTo(NavHome,      new HomePage());
    public void GoEmployees() => NavigateTo(NavEmployees, new EmployeesPage());
    public void GoTemplate()  => NavigateTo(NavTemplate,  new TemplatePage());
    public void GoSettings()  => NavigateTo(NavSettings,  new SettingsPage());

    public void GoHistory()
    {
        NavigateTo(NavHistory, new HistoryPage());
        _ = RefreshSidebarStatusAsync();
    }

    public void GoPreview(int scheduleId)
    {
        NavigateTo(NavHistory, new PreviewPage(scheduleId));
        _ = RefreshSidebarStatusAsync();
    }

    // ── Flow navigation ───────────────────────────────────────────────────

    private void FlowBack_Click(object sender, RoutedEventArgs e)
    {
        if (_flowHistory.TryPop(out var previous))
        {
            ContentFrame.Navigate(previous);

            // If no more history, we're back at the start of the flow (Home)
            if (_flowHistory.Count == 0)
            {
                FlowBar.Visibility = Visibility.Collapsed;
                // Re-highlight Home in sidebar
                NavHome.Style = (Style)FindResource("NavItemActiveStyle");
                _activeNavButton = NavHome;
            }
        }
        else
        {
            NavigateTo(NavHome, new HomePage());
        }
    }

    private async void ContinueDraft_Click(object sender, RoutedEventArgs e) =>
        await ContinueDraftAsync();

    public async Task ContinueDraftAsync()
    {
        var loaded = await ScheduleFlowState.LoadDraftAsync(App.Database);
        if (!loaded) { ShowDraftBanner(false); return; }

        var template = await App.Database.ShiftTemplates
            .Include(t => t.ShiftRows).Include(t => t.DayColumns)
            .FirstOrDefaultAsync(t => t.IsActive);
        var employees = await App.Database.Employees.ToListAsync();

        if (template != null)
        {
            var days   = template.DayColumns.Where(d => d.IsEnabled).OrderBy(d => d.DayIndex).ToList();
            var shifts = template.ShiftRows.OrderBy(r => r.OrderIndex).ToList();
            ScheduleFlowState.Current.ComputeShabbatBlocks(employees, days, shifts);
        }

        EnterFlowPage(new ManualSchedulePage(), "המשך טיוטה", "שיבוץ ידני", pushCurrent: false);
    }

    // ── Window lifecycle ──────────────────────────────────────────────────

    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_closingAllowed) return;
        if (FlowBar.Visibility != Visibility.Visible) return;
        if (ScheduleFlowState.Current.IsEditingExisting) return;

        e.Cancel = true;
        await ScheduleFlowState.SaveDraftAsync(App.Database);
        _closingAllowed = true;
        Close();
    }

    private async Task RefreshSidebarStatusAsync()
    {
        var template = await App.Database.ShiftTemplates.FirstOrDefaultAsync(t => t.IsActive);
        ActiveTemplateName.Text = template?.Name ?? "אין תבנית פעילה";

        var hasDraft = await App.Database.Schedules.AnyAsync(s => s.WeekStart == Schedule.DraftMarker);
        ShowDraftBanner(hasDraft);
    }
}
