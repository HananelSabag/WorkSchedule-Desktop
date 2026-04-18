using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WorkSchedule.Models;

namespace WorkSchedule.Views.Pages;

public partial class AutoSchedulePage : Page
{
    private List<DayColumn>  _activeDays   = [];
    private List<ShiftRow>   _activeShifts = [];

    private Dictionary<string, string> _generatedSchedule = [];
    private List<string>               _impossibleShifts  = [];

    // ── Brushes ──────────────────────────────────────────────────────────
    private static readonly SolidColorBrush BrHeaderBg     = new(Color.FromRgb(0x1E, 0x3A, 0x5F));
    private static readonly SolidColorBrush BrShiftLabelBg = new(Color.FromRgb(0xEF, 0xF6, 0xFF));
    private static readonly SolidColorBrush BrAssignedBg   = new(Color.FromRgb(0xEF, 0xF6, 0xFF));
    private static readonly SolidColorBrush BrImpossibleBg = new(Color.FromRgb(0xFF, 0xF0, 0xF0));
    private static readonly SolidColorBrush BrBorder       = new(Color.FromRgb(0xE2, 0xE8, 0xF0));
    private static readonly SolidColorBrush BrTextDark     = new(Color.FromRgb(0x1E, 0x29, 0x3B));
    private static readonly SolidColorBrush BrTextMuted    = new(Color.FromRgb(0x64, 0x74, 0x8B));
    private static readonly SolidColorBrush BrImpossibleFg = new(Color.FromRgb(0xDC, 0x26, 0x26));

    public AutoSchedulePage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAndGenerateAsync();
    }

    // ── Load + generate ───────────────────────────────────────────────────

    private async Task LoadAndGenerateAsync()
    {
        var template = await App.Database.ShiftTemplates
            .Include(t => t.ShiftRows)
            .Include(t => t.DayColumns)
            .FirstOrDefaultAsync(t => t.IsActive);

        var employees = await App.Database.Employees.OrderBy(e => e.Name).ToListAsync();

        if (template != null)
        {
            _activeDays   = template.DayColumns.Where(d => d.IsEnabled).OrderBy(d => d.DayIndex).ToList();
            _activeShifts = template.ShiftRows.OrderBy(r => r.OrderIndex).ToList();
        }

        if (_activeDays.Count == 0 || _activeShifts.Count == 0 || employees.Count == 0)
        {
            ShowError("לא ניתן להריץ את האלגוריתם — בדוק שהוגדרו תבנית ועובדים.");
            return;
        }

        // Run on background thread so the UI stays responsive
        (_generatedSchedule, _impossibleShifts) = await Task.Run(() =>
            GenericScheduleGenerator.Generate(
                employees, _activeDays, _activeShifts, ScheduleFlowState.Current));

        ShowResults();
    }

    private void ShowResults()
    {
        int total  = _activeDays.Count * _activeShifts.Count;
        int filled = _generatedSchedule.Count;
        int empty  = _impossibleShifts.Count;

        FilledCount.Text = filled.ToString();
        EmptyCount.Text  = empty.ToString();
        TotalCount.Text  = total.ToString();

        if (empty == 0)
        {
            StatusIcon.Kind       = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle;
            StatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
            StatusLabel.Text      = "הסידור הושלם בהצלחה!";
        }
        else
        {
            StatusIcon.Kind       = MaterialDesignThemes.Wpf.PackIconKind.AlertCircle;
            StatusIcon.Foreground = BrImpossibleFg;
            StatusLabel.Text      = $"הסידור נוצר עם {empty} משמרות פתוחות";

            ImpossibleCard.Visibility  = Visibility.Visible;
            ImpossibleList.ItemsSource = _impossibleShifts
                .Select(k =>
                {
                    var p = k.Split('|');
                    return p.Length == 2 ? $"{p[0]} — {p[1]}" : k;
                })
                .ToList();
        }

        BuildPreviewGrid();

        LoadingPanel.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility  = Visibility.Visible;
        SaveBtn.IsEnabled       = true;
    }

    private void ShowError(string msg)
    {
        LoadingPanel.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility  = Visibility.Visible;
        ScheduleGridHost.Content = new TextBlock
        {
            Text                = msg,
            FontSize            = 14,
            Foreground          = BrImpossibleFg,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            TextAlignment       = TextAlignment.Center,
            TextWrapping        = TextWrapping.Wrap,
            Margin              = new Thickness(24),
            FontFamily          = new FontFamily("Segoe UI")
        };
    }

    // ── Read-only schedule preview grid ───────────────────────────────────

    private void BuildPreviewGrid()
    {
        var grid = new Grid { FlowDirection = FlowDirection.RightToLeft };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110), MinWidth = 90 });
        foreach (var _ in _activeDays)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 80 });

        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
        foreach (var _ in _activeShifts)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(85) });

        grid.Children.Add(MakeStaticCell("סידור עבודה", 0, 0, BrHeaderBg, Brushes.White, FontWeights.Bold, 13));
        for (int c = 0; c < _activeDays.Count; c++)
        {
            var date = ScheduleFlowState.Current.WeekStartDate.AddDays(_activeDays[c].DayIndex).ToString("dd/MM");
            grid.Children.Add(MakeDayHeaderCell(_activeDays[c].DayNameHebrew, date, 0, c + 1));
        }

        for (int r = 0; r < _activeShifts.Count; r++)
        {
            grid.Children.Add(MakeShiftLabelCell(_activeShifts[r].ShiftName, _activeShifts[r].ShiftHours, r + 1));
            for (int c = 0; c < _activeDays.Count; c++)
            {
                var key  = $"{c}_{r}";
                var text = _generatedSchedule.GetValueOrDefault(key, "");
                bool imp = _impossibleShifts.Any(s =>
                {
                    var p = s.Split('|');
                    return p.Length == 2
                        && p[0] == _activeDays[c].DayNameHebrew
                        && p[1] == _activeShifts[r].ShiftName;
                });
                grid.Children.Add(MakePreviewCell(text, imp, r + 1, c + 1));
            }
        }

        ScheduleGridHost.Content = grid;
    }

    private static UIElement MakePreviewCell(string text, bool impossible, int row, int col)
    {
        bool hasContent = !string.IsNullOrEmpty(text);
        var  bg         = (Brush)(impossible ? BrImpossibleBg : hasContent ? BrAssignedBg : Brushes.White);

        var cell = new Border
        {
            Background      = bg,
            BorderBrush     = BrBorder,
            BorderThickness = new Thickness(0, 0, 1, 1)
        };
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, col);

        if (hasContent)
            cell.Child = new TextBlock
            {
                Text                = text,
                Foreground          = BrTextDark,
                FontSize            = 13,
                FontWeight          = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
                TextWrapping        = TextWrapping.Wrap,
                Margin              = new Thickness(4),
                FontFamily          = new FontFamily("Segoe UI")
            };
        else if (impossible)
            cell.Child = new TextBlock
            {
                Text                = "—",
                Foreground          = BrImpossibleFg,
                FontSize            = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                FontFamily          = new FontFamily("Segoe UI")
            };

        return cell;
    }

    // ── Navigation ────────────────────────────────────────────────────────

    private void BackToBlocks_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow win)
            win.EnterFlowPage(new BlockingPage(), "סידור חדש", "חסימות", pushCurrent: false);
    }

    // ── Save overlay ──────────────────────────────────────────────────────

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveNameBox.Text = ScheduleFlowState.Current.WeekLabel;
        SaveOverlay.Visibility = Visibility.Visible;
        SaveNameBox.Focus();
        SaveNameBox.SelectAll();
    }

    private void SaveNameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)  _ = CommitSaveAsync();
        if (e.Key == Key.Escape) SaveOverlay.Visibility = Visibility.Collapsed;
    }

    private void SaveConfirm_Click(object sender, RoutedEventArgs e) => _ = CommitSaveAsync();
    private void SaveCancel_Click(object sender, RoutedEventArgs e)  => SaveOverlay.Visibility = Visibility.Collapsed;

    private async Task CommitSaveAsync()
    {
        var name = SaveNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) name = $"סידור {DateTime.Now:dd/MM/yyyy}";

        var (finalName, overwriteId) = await ResolveNameConflictAsync(name);
        if (finalName == null) { SaveOverlay.Visibility = Visibility.Visible; return; }

        SaveOverlay.Visibility = Visibility.Collapsed;

        // Convert cellKey → humanKey for DB storage
        var scheduleData = new Dictionary<string, string>();
        foreach (var (key, text) in _generatedSchedule)
        {
            var parts = key.Split('_');
            if (parts.Length != 2) continue;
            if (!int.TryParse(parts[0], out int ci) || !int.TryParse(parts[1], out int ri)) continue;
            if (ci >= _activeDays.Count || ri >= _activeShifts.Count) continue;
            scheduleData[$"{_activeDays[ci].DayNameHebrew}|{_activeShifts[ri].ShiftName}"] = text;
        }

        var json = JsonConvert.SerializeObject(scheduleData);
        int savedId;

        if (overwriteId.HasValue)
        {
            var existing = await App.Database.Schedules.FindAsync(overwriteId.Value);
            if (existing != null)
            {
                existing.ScheduleData = json;
                existing.CreatedDate  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                await App.Database.SaveChangesAsync();
                savedId = existing.Id;
            }
            else savedId = 0;
        }
        else
        {
            var schedule = new Schedule
            {
                WeekStart      = finalName,
                ScheduleData   = json,
                BlocksData     = "{}",
                CanOnlyData    = "{}",
                SavingModeData = "{}",
                CreatedDate    = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            App.Database.Schedules.Add(schedule);
            await App.Database.SaveChangesAsync();
            savedId = schedule.Id;
        }

        await ScheduleFlowState.DeleteDraftAsync(App.Database);

        if (Window.GetWindow(this) is MainWindow win && savedId > 0)
            win.NavigateTo(win.NavHistory, new PreviewPage(savedId));
    }

    private static async Task<(string? FinalName, int? OverwriteId)> ResolveNameConflictAsync(string name)
    {
        var existing = await App.Database.Schedules
            .Where(s => s.WeekStart == name && s.WeekStart != Schedule.DraftMarker)
            .FirstOrDefaultAsync();

        if (existing == null) return (name, null);

        int suffix = 1;
        string copyName;
        do { copyName = $"{name} ({suffix++})"; }
        while (await App.Database.Schedules.AnyAsync(s => s.WeekStart == copyName));

        var result = AppDialog.Ask(
            $"כבר קיים סידור בשם \"{name}\".\n\n" +
            $"כן  ← שמור עותק: \"{copyName}\"\n" +
            $"לא  ← דרוס את הסידור הקיים\n" +
            $"ביטול ← חזור לעריכת השם",
            "שם כבר קיים");

        return result switch
        {
            MessageBoxResult.Yes => (copyName, null),
            MessageBoxResult.No  => (name, existing.Id),
            _                    => (null, null)
        };
    }

    // ── Cell factories ────────────────────────────────────────────────────

    private static UIElement MakeDayHeaderCell(string dayName, string date, int row, int col)
    {
        var border = new Border { Background = BrHeaderBg, BorderBrush = BrBorder, BorderThickness = new Thickness(0, 0, 1, 1) };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        sp.Children.Add(new TextBlock
        {
            Text = dayName, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold,
            FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center, FontFamily = new FontFamily("Segoe UI")
        });
        sp.Children.Add(new TextBlock
        {
            Text = date, Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 2, 0, 0),
            FontFamily = new FontFamily("Segoe UI")
        });
        border.Child = sp;
        return border;
    }

    private static UIElement MakeStaticCell(string text, int row, int col,
                                             Brush bg, Brush fg, FontWeight weight, double size)
    {
        var border = new Border
        {
            Background = bg, BorderBrush = BrBorder, BorderThickness = new Thickness(0, 0, 1, 1)
        };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);
        if (!string.IsNullOrEmpty(text))
            border.Child = new TextBlock
            {
                Text = text, Foreground = fg, FontWeight = weight, FontSize = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
                TextWrapping        = TextWrapping.Wrap,
                Margin              = new Thickness(4, 2, 4, 2),
                FontFamily          = new FontFamily("Segoe UI")
            };
        return border;
    }

    private static UIElement MakeShiftLabelCell(string name, string hours, int row)
    {
        var border = new Border
        {
            Background = BrShiftLabelBg, BorderBrush = BrBorder, BorderThickness = new Thickness(0, 0, 1, 1)
        };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, 0);

        var sp = new StackPanel
        {
            VerticalAlignment   = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin              = new Thickness(6, 4, 6, 4)
        };
        sp.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(name) ? "—" : name,
            Foreground = BrTextDark, FontWeight = FontWeights.SemiBold, FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center,
            FontFamily = new FontFamily("Segoe UI")
        });
        if (!string.IsNullOrWhiteSpace(hours))
            sp.Children.Add(new TextBlock
            {
                Text = hours, Foreground = BrTextMuted, FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0), FontFamily = new FontFamily("Segoe UI")
            });
        border.Child = sp;
        return border;
    }
}
