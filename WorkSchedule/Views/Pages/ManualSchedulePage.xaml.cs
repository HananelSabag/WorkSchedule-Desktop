using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WorkSchedule.Models;

namespace WorkSchedule.Views.Pages;

// VM for chips — shows name + assignment count
public class EmployeeScheduleVm : INotifyPropertyChanged
{
    public Employee Employee { get; init; } = null!;

    private int _assignmentCount;
    public int AssignmentCount
    {
        get => _assignmentCount;
        set { _assignmentCount = value; OnPropertyChanged(nameof(DisplayLabel)); }
    }

    public string DisplayLabel =>
        _assignmentCount > 0 ? $"{Employee.Name} ({_assignmentCount})" : Employee.Name;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public partial class ManualSchedulePage : Page
{
    private List<DayColumn>  _activeDays   = [];
    private List<ShiftRow>   _activeShifts = [];
    private List<EmployeeScheduleVm> _employeeVms = [];

    private EmployeeScheduleVm? _selectedVm;
    private string? _editingCellKey; // key of cell being edited in the free-text overlay

    // ── Brushes ──────────────────────────────────────────────────────────
    private static readonly SolidColorBrush BrHeaderBg     = new(Color.FromRgb(0x1E, 0x3A, 0x5F)); // dark navy
    private static readonly SolidColorBrush BrShiftLabelBg = new(Color.FromRgb(0xEF, 0xF6, 0xFF));
    private static readonly SolidColorBrush BrAssignedBg   = new(Color.FromRgb(0xEF, 0xF6, 0xFF));
    private static readonly SolidColorBrush BrAssignedFg   = new(Color.FromRgb(0x1E, 0x29, 0x3B));
    private static readonly SolidColorBrush BrCannotHintBg = new(Color.FromRgb(0xFF, 0xEE, 0xEE));
    private static readonly SolidColorBrush BrCanOnlyHintBg= new(Color.FromRgb(0xF0, 0xF9, 0xFF));
    private static readonly SolidColorBrush BrEmptyBg      = Brushes.White;
    private static readonly SolidColorBrush BrBorder       = new(Color.FromRgb(0xE2, 0xE8, 0xF0));
    private static readonly SolidColorBrush BrTextDark     = new(Color.FromRgb(0x1E, 0x29, 0x3B));
    private static readonly SolidColorBrush BrTextMuted    = new(Color.FromRgb(0x64, 0x74, 0x8B));

    public ManualSchedulePage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
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

        _employeeVms = employees.Select(e => new EmployeeScheduleVm { Employee = e }).ToList();
        RefreshAssignmentCounts();

        EmployeeChips.ItemsSource = _employeeVms;
        BuildScheduleGrid();
    }

    // ── Employee chip selection ───────────────────────────────────────────

    private void EmployeeChip_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EmployeeChips.SelectedItem is not EmployeeScheduleVm vm) return;
        _selectedVm = vm;

        NoEmpHint.Visibility        = Visibility.Collapsed;
        SelectedEmpBadge.Visibility = Visibility.Visible;
        SelectedEmployeeLabel.Text  = vm.Employee.Name;
        SelectedEmpInitial.Text     = vm.Employee.Name.Length > 0 ? vm.Employee.Name[0].ToString() : "?";

        BuildScheduleGrid();
    }

    // ── Grid builder ──────────────────────────────────────────────────────

    private void BuildScheduleGrid()
    {
        if (_activeDays.Count == 0 || _activeShifts.Count == 0)
        {
            ScheduleGridHost.Content = MakeEmptyState("הגדר תבנית טבלה עם ימים ומשמרות כדי להמשיך");
            return;
        }

        var grid = new Grid { FlowDirection = FlowDirection.RightToLeft };

        // Columns: shift label (fixed) + one per day (adaptive star)
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110), MinWidth = 90 });
        foreach (var _ in _activeDays)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 80 });

        // Rows: header + one per shift
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
        foreach (var _ in _activeShifts)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(85) });

        // Header row
        grid.Children.Add(MakeStaticCell("סידור עבודה", 0, 0, BrHeaderBg, Brushes.White, FontWeights.Bold, 13));
        for (int c = 0; c < _activeDays.Count; c++)
        {
            var date = ScheduleFlowState.Current.WeekStartDate.AddDays(_activeDays[c].DayIndex).ToString("dd/MM");
            grid.Children.Add(MakeDayHeaderCell(_activeDays[c].DayNameHebrew, date, 0, c + 1));
        }

        // Shift rows
        for (int r = 0; r < _activeShifts.Count; r++)
        {
            grid.Children.Add(MakeShiftLabelCell(_activeShifts[r].ShiftName, _activeShifts[r].ShiftHours, r + 1));
            for (int c = 0; c < _activeDays.Count; c++)
                grid.Children.Add(MakeScheduleCell($"{c}_{r}", r + 1, c + 1));
        }

        ScheduleGridHost.Content = grid;
    }

    private UIElement MakeScheduleCell(string key, int row, int col)
    {
        var text      = ScheduleFlowState.Current.CellTexts.GetValueOrDefault(key, "");
        bool occupied = !string.IsNullOrEmpty(text);

        // Determine background: hint constraint of selected employee
        Brush bg = BrEmptyBg;
        if (!occupied && _selectedVm != null)
        {
            var state = ScheduleFlowState.Current.GetCellState(_selectedVm.Employee.Id, key);
            bg = state switch
            {
                CellBlockState.Cannot  => BrCannotHintBg,
                CellBlockState.CanOnly => BrCanOnlyHintBg,
                _                     => BrEmptyBg
            };
        }
        else if (occupied)
            bg = BrAssignedBg;

        // Collect all employees blocked for this cell (Cannot + Shabbat)
        var blockedNames = new List<string>();
        foreach (var vm in _employeeVms)
        {
            var empId = vm.Employee.Id;
            bool blocked =
                (ScheduleFlowState.Current.CannotBlocks.TryGetValue(empId, out var cb) && cb.Contains(key)) ||
                (ScheduleFlowState.Current.ShabbatBlocks.TryGetValue(empId, out var sb) && sb.Contains(key));
            if (blocked) blockedNames.Add(vm.Employee.Name);
        }

        var cell = new Border
        {
            Background      = bg,
            BorderBrush     = BrBorder,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Cursor          = Cursors.Hand,
            Tag             = key
        };
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, col);

        if (occupied)
        {
            cell.Child = new TextBlock
            {
                Text                = text,
                Foreground          = BrAssignedFg,
                FontSize            = 13,
                FontWeight          = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
                TextWrapping        = TextWrapping.Wrap,
                Margin              = new Thickness(4),
                FontFamily          = new FontFamily("Segoe UI")
            };
        }
        else if (blockedNames.Count > 0)
        {
            // Inner grid: empty top area + blocked names at bottom
            var inner = new Grid();
            inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var blockedTb = new TextBlock
            {
                Text                = string.Join(", ", blockedNames),
                Foreground          = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
                FontSize            = 9,
                FontStyle           = FontStyles.Italic,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
                TextWrapping        = TextWrapping.Wrap,
                Margin              = new Thickness(2, 0, 2, 4),
                FontFamily          = new FontFamily("Segoe UI")
            };
            Grid.SetRow(blockedTb, 1);
            inner.Children.Add(blockedTb);
            cell.Child = inner;
        }

        cell.MouseLeftButtonDown  += Cell_LeftClick;
        cell.MouseRightButtonDown += Cell_RightClick;

        // Hover
        cell.MouseEnter += (s, _) =>
        {
            if (s is Border b && !occupied)
                b.Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
        };
        cell.MouseLeave += (s, _) =>
        {
            if (s is Border b && !occupied) b.Background = bg;
        };

        return cell;
    }

    // ── Cell interactions ─────────────────────────────────────────────────

    private void Cell_LeftClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: string key }) return;

        if (_selectedVm == null)
        {
            ShowHint("בחר עובד מהחיפים למטה תחילה");
            return;
        }

        var current = ScheduleFlowState.Current.CellTexts.GetValueOrDefault(key, "");
        var empName = _selectedVm.Employee.Name;

        // Toggle off if same employee name is already the only content
        if (current == empName)
        {
            ScheduleFlowState.Current.CellTexts.Remove(key);
            RefreshAssignmentCounts();
            BuildScheduleGrid();
            return;
        }

        // Check constraints
        var state = ScheduleFlowState.Current.GetCellState(_selectedVm.Employee.Id, key);
        if (state == CellBlockState.Cannot || state == CellBlockState.Shabbat)
        {
            var warning = state == CellBlockState.Shabbat
                ? $"{empName} מסומן כשומר שבת ולא יכול לעבוד במשמרת זו.\nהאם לשבץ בכל זאת?"
                : $"{empName} מסומן כ\"לא יכול\" לעבוד במשמרת זו.\nהאם לשבץ בכל זאת?";
            if (!AppDialog.Confirm(warning, "אזהרת חסימה")) return;
        }
        else if (state == CellBlockState.CanOnly)
        {
            // This cell IS in their can-only list — that's fine, no warning
        }
        else if (ScheduleFlowState.Current.CanOnlyBlocks.TryGetValue(_selectedVm.Employee.Id, out var co) && co.Count > 0)
        {
            // Employee has can-only blocks and this cell is NOT one of them
            if (!co.Contains(key))
            {
                if (!AppDialog.Confirm(
                    $"{empName} מוגדר כ\"יכול רק\" במשמרות מסוימות ומשמרת זו אינה ביניהן.\nהאם לשבץ בכל זאת?",
                    "אזהרת חסימה")) return;
            }
        }

        ScheduleFlowState.Current.CellTexts[key] = empName;
        RefreshAssignmentCounts();
        BuildScheduleGrid();
    }

    private void Cell_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: string key }) return;
        var current = ScheduleFlowState.Current.CellTexts.GetValueOrDefault(key, "");
        OpenTextEdit(key, current);
        e.Handled = true;
    }

    // ── Free text overlay ─────────────────────────────────────────────────

    private void OpenTextEdit(string key, string currentText)
    {
        _editingCellKey = key;

        // Determine a nice title
        var parts = key.Split('_');
        string cellName = "";
        if (parts.Length == 2 && int.TryParse(parts[0], out int ci) && int.TryParse(parts[1], out int ri))
        {
            var dayName   = ci < _activeDays.Count   ? _activeDays[ci].DayNameHebrew   : "";
            var shiftName = ri < _activeShifts.Count ? _activeShifts[ri].ShiftName     : "";
            cellName = $"{dayName} — {shiftName}";
        }

        TextEditTitle.Text = $"עריכה: {cellName}";
        TextEditBox.Text   = currentText;
        TextEditOverlay.Visibility = Visibility.Visible;
        TextEditBox.Focus();
        TextEditBox.SelectAll();
    }

    private void TextEditConfirm_Click(object sender, RoutedEventArgs e) => CommitTextEdit();
    private void TextEditCancel_Click(object sender, RoutedEventArgs e)  => CloseTextEdit();

    private void TextEditClear_Click(object sender, RoutedEventArgs e)
    {
        if (_editingCellKey != null)
            ScheduleFlowState.Current.CellTexts.Remove(_editingCellKey);
        CloseTextEdit();
        RefreshAssignmentCounts();
        BuildScheduleGrid();
    }

    private void TextEditBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)  CommitTextEdit();
        if (e.Key == Key.Escape) CloseTextEdit();
    }

    private void CommitTextEdit()
    {
        if (_editingCellKey == null) { CloseTextEdit(); return; }
        var text = TextEditBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
            ScheduleFlowState.Current.CellTexts.Remove(_editingCellKey);
        else
            ScheduleFlowState.Current.CellTexts[_editingCellKey] = text;

        CloseTextEdit();
        RefreshAssignmentCounts();
        BuildScheduleGrid();
    }

    private void CloseTextEdit()
    {
        TextEditOverlay.Visibility = Visibility.Collapsed;
        _editingCellKey = null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void RefreshAssignmentCounts()
    {
        foreach (var vm in _employeeVms)
            vm.AssignmentCount = ScheduleFlowState.Current.CellTexts.Values
                .Count(v => v == vm.Employee.Name || v.StartsWith(vm.Employee.Name + " "));
        // PropertyChanged fires on each VM — chips update without resetting ItemsSource
    }

    private static void ShowHint(string msg) => AppDialog.ShowInfo(msg, "שיבוץ ידני");

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
            Background      = bg,
            BorderBrush     = BrBorder,
            BorderThickness = new Thickness(0, 0, 1, 1)
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
            Background      = BrShiftLabelBg,
            BorderBrush     = BrBorder,
            BorderThickness = new Thickness(0, 0, 1, 1)
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

    private static UIElement MakeEmptyState(string msg) =>
        new TextBlock
        {
            Text = msg, FontSize = 14, Foreground = BrTextMuted,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            TextAlignment       = TextAlignment.Center,
            TextWrapping        = TextWrapping.Wrap,
            Margin              = new Thickness(24),
            FontFamily          = new FontFamily("Segoe UI")
        };

    // ── Navigation ────────────────────────────────────────────────────────

    private void BackToBlocking_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow win)
            win.EnterFlowPage(new BlockingPage(), "סידור חדש", "חסימות", pushCurrent: false);
    }

    // ── Save ──────────────────────────────────────────────────────────────

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ScheduleFlowState.Current.CellTexts.Count == 0)
        {
            if (!AppDialog.Confirm("הסידור ריק — לשמור בכל זאת?", "שמירה")) return;
        }

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

        // Build schedule data: dayName|shiftName → text
        var scheduleData = new Dictionary<string, string>();
        foreach (var (key, text) in ScheduleFlowState.Current.CellTexts)
        {
            var parts = key.Split('_');
            if (parts.Length != 2) continue;
            if (!int.TryParse(parts[0], out int ci) || !int.TryParse(parts[1], out int ri)) continue;
            if (ci >= _activeDays.Count || ri >= _activeShifts.Count) continue;
            scheduleData[$"{_activeDays[ci].DayNameHebrew}|{_activeShifts[ri].ShiftName}"] = text;
        }

        var json = JsonConvert.SerializeObject(scheduleData);

        if (overwriteId.HasValue)
        {
            var existing = await App.Database.Schedules.FindAsync(overwriteId.Value);
            if (existing != null)
            {
                existing.ScheduleData = json;
                existing.CreatedDate  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                await App.Database.SaveChangesAsync();
                await ScheduleFlowState.DeleteDraftAsync(App.Database);
                if (Window.GetWindow(this) is MainWindow w) w.GoPreview(overwriteId.Value);
                return;
            }
        }

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
        await ScheduleFlowState.DeleteDraftAsync(App.Database);

        if (Window.GetWindow(this) is MainWindow win) win.GoPreview(schedule.Id);
    }

    // Returns (finalName, overwriteId):
    //   finalName=null  → user cancelled (reopen overlay)
    //   overwriteId set → update existing record
    //   overwriteId null → create new record with finalName
    private static async Task<(string? FinalName, int? OverwriteId)> ResolveNameConflictAsync(string name)
    {
        var existing = await App.Database.Schedules
            .Where(s => s.WeekStart == name && s.WeekStart != Schedule.DraftMarker)
            .FirstOrDefaultAsync();

        if (existing == null) return (name, null);

        // Generate a unique copy name
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
            MessageBoxResult.No  => (name,     existing.Id),
            _                    => (null,     null)
        };
    }
}
