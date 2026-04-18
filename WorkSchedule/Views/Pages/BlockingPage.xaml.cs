using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using WorkSchedule.Models;

namespace WorkSchedule.Views.Pages;

public class EmployeeBlockingVm : INotifyPropertyChanged
{
    public Employee Employee { get; init; } = null!;

    private string _blockSummary = "ללא חסימות";
    public string BlockSummary
    {
        get => _blockSummary;
        set { _blockSummary = value; OnPropertyChanged(); }
    }

    public void RefreshSummary()
    {
        int n = ScheduleFlowState.Current.GetUserBlockCount(Employee.Id);
        bool hasShabbat = ScheduleFlowState.Current.ShabbatBlocks.ContainsKey(Employee.Id);
        var mode = ScheduleFlowState.Current.GetNaturalMode(Employee.Id);
        string text = n > 0
            ? $"{n} חסימות ({(mode == BlockMode.CanOnly ? "יכול רק" : "לא יכול")})"
            : "ללא חסימות";
        if (hasShabbat) text += " + שבת";
        BlockSummary = text;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public partial class BlockingPage : Page
{
    private List<DayColumn>  _activeDays   = [];
    private List<ShiftRow>   _activeShifts = [];
    private List<Employee>   _employees    = [];
    private ObservableCollection<EmployeeBlockingVm> _employeeVms = [];

    private EmployeeBlockingVm? _selectedVm;
    private BlockMode _currentMode = BlockMode.Cannot;

    // ── Brushes ──────────────────────────────────────────────────────────
    private static readonly SolidColorBrush BrHeaderBg    = new(Color.FromRgb(0x3B, 0x82, 0xF6));
    private static readonly SolidColorBrush BrShiftLabelBg = new(Color.FromRgb(0xEF, 0xF6, 0xFF));
    private static readonly SolidColorBrush BrCannotBg    = new(Color.FromRgb(0xFE, 0xE2, 0xE2));
    private static readonly SolidColorBrush BrCannotFg    = new(Color.FromRgb(0xDC, 0x26, 0x26));
    private static readonly SolidColorBrush BrCanOnlyBg   = new(Color.FromRgb(0xDB, 0xEA, 0xFE));
    private static readonly SolidColorBrush BrCanOnlyFg   = new(Color.FromRgb(0x1D, 0x4E, 0xD8));
    private static readonly SolidColorBrush BrShabbatBg   = new(Color.FromRgb(0xF1, 0xF5, 0xF9));
    private static readonly SolidColorBrush BrShabbatFg   = new(Color.FromRgb(0x94, 0xA3, 0xB8));
    private static readonly SolidColorBrush BrDayHoverBg  = new(Color.FromRgb(0xBF, 0xDB, 0xFE));
    private static readonly SolidColorBrush BrCellBg      = Brushes.White;
    private static readonly SolidColorBrush BrBorder      = new(Color.FromRgb(0xE2, 0xE8, 0xF0));
    private static readonly SolidColorBrush BrTextDark    = new(Color.FromRgb(0x1E, 0x29, 0x3B));
    private static readonly SolidColorBrush BrTextMuted   = new(Color.FromRgb(0x64, 0x74, 0x8B));

    public BlockingPage()
    {
        InitializeComponent();
        // Wire mode chip clicks
        CannotChip.MouseLeftButtonDown  += (_, _) => SetMode(BlockMode.Cannot);
        CanOnlyChip.MouseLeftButtonDown += (_, _) => SetMode(BlockMode.CanOnly);
        Loaded += async (_, _) => await LoadAsync();
    }

    // ── Load ─────────────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        var template = await App.Database.ShiftTemplates
            .Include(t => t.ShiftRows)
            .Include(t => t.DayColumns)
            .FirstOrDefaultAsync(t => t.IsActive);

        _employees = await App.Database.Employees.OrderBy(e => e.Name).ToListAsync();

        if (template != null)
        {
            _activeDays   = template.DayColumns.Where(d => d.IsEnabled).OrderBy(d => d.DayIndex).ToList();
            _activeShifts = template.ShiftRows.OrderBy(r => r.OrderIndex).ToList();
        }

        ScheduleFlowState.Current.ComputeShabbatBlocks(_employees, _activeDays, _activeShifts);

        _employeeVms = new ObservableCollection<EmployeeBlockingVm>(
            _employees.Select(e => new EmployeeBlockingVm { Employee = e }));

        EmployeeChips.ItemsSource = _employeeVms;

        RefreshModeChips();
        BuildBlockingGrid(); // show empty grid first
    }

    // ── Employee chip selection ───────────────────────────────────────────

    private void EmployeeChip_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EmployeeChips.SelectedItem is not EmployeeBlockingVm vm) return;
        _selectedVm = vm;
        _currentMode = ScheduleFlowState.Current.GetNaturalMode(vm.Employee.Id);

        NoEmpHint.Visibility       = Visibility.Collapsed;
        SelectedEmpBadge.Visibility = Visibility.Visible;
        SelectedEmployeeLabel.Text = vm.Employee.Name;
        SelectedEmpInitial.Text    = vm.Employee.Name.Length > 0 ? vm.Employee.Name[0].ToString() : "?";
        ClearBlocksBtn.Visibility  = Visibility.Visible;

        RefreshModeChips();
        BuildBlockingGrid();
    }

    // ── Mode switching ────────────────────────────────────────────────────

    private void SetMode(BlockMode mode)
    {
        _currentMode = mode;
        RefreshModeChips();
        // No rebuild needed — mode only affects next click
    }

    private void RefreshModeChips()
    {
        bool isCannot = _currentMode == BlockMode.Cannot;

        // Cannot chip — filled red when active
        CannotChip.Background   = isCannot ? BrCannotFg  : Brushes.White;
        CannotChip.BorderBrush  = BrCannotFg;
        CannotText.Foreground   = isCannot ? Brushes.White : BrCannotFg;
        CannotIcon.Foreground   = isCannot ? Brushes.White : BrCannotFg;

        // CanOnly chip — filled blue when active
        CanOnlyChip.Background  = isCannot ? Brushes.White : BrCanOnlyFg;
        CanOnlyChip.BorderBrush = BrCanOnlyFg;
        CanOnlyText.Foreground  = isCannot ? BrCanOnlyFg  : Brushes.White;
        CanOnlyIcon.Foreground  = isCannot ? BrCanOnlyFg  : Brushes.White;
    }

    private void ClearBlocks_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedVm == null) return;
        ScheduleFlowState.Current.ClearUserBlocks(_selectedVm.Employee.Id);
        _currentMode = BlockMode.Cannot;
        RefreshModeChips();
        _selectedVm.RefreshSummary();
        BuildBlockingGrid();
    }

    // ── Grid builder ──────────────────────────────────────────────────────

    private void BuildBlockingGrid()
    {
        if (_activeDays.Count == 0 || _activeShifts.Count == 0)
        {
            BlockingGridHost.Content = MakeEmptyState("הגדר תבנית טבלה עם ימים ומשמרות כדי להמשיך");
            return;
        }

        // Use -1 when no employee is selected — cells will be neutral/non-clickable
        var empId   = _selectedVm?.Employee.Id   ?? -1;
        var empName = _selectedVm?.Employee.Name ?? "";

        var grid = new Grid { FlowDirection = FlowDirection.RightToLeft };

        // Columns: shift-label (fixed) + one per day (adaptive star)
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110), MinWidth = 90 });
        foreach (var _ in _activeDays)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 80 });

        // Rows: header + one per shift
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
        foreach (var _ in _activeShifts)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(85) });

        // Top-left corner cell
        grid.Children.Add(MakeStaticCell("", 0, 0, BrHeaderBg, Brushes.White, FontWeights.Normal, 12));

        // Day header cells — clickable (click = block entire column)
        for (int c = 0; c < _activeDays.Count; c++)
        {
            bool wholeDay = IsWholeDayBlocked(empId, c);
            var date = ScheduleFlowState.Current.WeekStartDate.AddDays(_activeDays[c].DayIndex).ToString("dd/MM");
            grid.Children.Add(MakeDayHeaderCell(_activeDays[c].DayNameHebrew, date, 0, c + 1, wholeDay, c));
        }

        // Shift rows
        for (int r = 0; r < _activeShifts.Count; r++)
        {
            var shift = _activeShifts[r];
            grid.Children.Add(MakeShiftLabelCell(shift.ShiftName, shift.ShiftHours, r + 1));

            for (int c = 0; c < _activeDays.Count; c++)
            {
                var key   = $"{c}_{r}";
                var state = ScheduleFlowState.Current.GetCellState(empId, key);
                grid.Children.Add(MakeBlockCell(key, state, empName, r + 1, c + 1));
            }
        }

        BlockingGridHost.Content = grid;
    }

    // Returns true if all non-Shabbat shifts in this day column have a user block
    private bool IsWholeDayBlocked(int empId, int dayIdx)
    {
        bool anyUserBlockable = false;
        for (int r = 0; r < _activeShifts.Count; r++)
        {
            var key = $"{dayIdx}_{r}";
            if (ScheduleFlowState.Current.IsShabbatLocked(empId, key)) continue;
            anyUserBlockable = true;
            if (ScheduleFlowState.Current.GetCellState(empId, key) == CellBlockState.None)
                return false;
        }
        return anyUserBlockable; // false if all cells are Shabbat-locked (nothing to toggle)
    }

    // ── Cell factories ────────────────────────────────────────────────────

    private UIElement MakeDayHeaderCell(string dayName, string date, int row, int col,
                                         bool wholeBlocked, int dayColIndex)
    {
        var cell = new Border
        {
            Background      = wholeBlocked ? BrCannotFg : BrHeaderBg,
            BorderBrush     = BrBorder,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Cursor          = Cursors.Hand,
            Tag             = dayColIndex,
            ToolTip         = "לחץ לחסימת/שחרור כל המשמרות של יום זה"
        };
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, col);

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
        cell.Child = sp;

        cell.MouseLeftButtonDown += DayHeader_Click;

        // Hover effect
        cell.MouseEnter += (s, _) => { if (s is Border b && !wholeBlocked) b.Background = BrDayHoverBg; };
        cell.MouseLeave += (s, _) => { if (s is Border b) b.Background = wholeBlocked ? BrCannotFg : BrHeaderBg; };

        return cell;
    }

    private void DayHeader_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: int dayIdx } || _selectedVm == null) return;
        var empId = _selectedVm.Employee.Id;
        bool allBlocked = IsWholeDayBlocked(empId, dayIdx);

        if (allBlocked)
        {
            // Clear all user-set blocks in this column (Shabbat stays)
            for (int r = 0; r < _activeShifts.Count; r++)
            {
                var key = $"{dayIdx}_{r}";
                if (ScheduleFlowState.Current.IsShabbatLocked(empId, key)) continue;
                ScheduleFlowState.Current.CannotBlocks.GetValueOrDefault(empId)?.Remove(key);
                ScheduleFlowState.Current.CanOnlyBlocks.GetValueOrDefault(empId)?.Remove(key);
            }
        }
        else
        {
            // Force-fill the whole column with the current mode, clearing the opposite
            if (_currentMode == BlockMode.Cannot)
            {
                ScheduleFlowState.Current.CanOnlyBlocks.Remove(empId);
                if (!ScheduleFlowState.Current.CannotBlocks.ContainsKey(empId))
                    ScheduleFlowState.Current.CannotBlocks[empId] = [];
            }
            else
            {
                if (ScheduleFlowState.Current.CannotBlocks.TryGetValue(empId, out var cb))
                {
                    var shabbat = ScheduleFlowState.Current.ShabbatBlocks.GetValueOrDefault(empId, []);
                    foreach (var k in cb.Except(shabbat).ToList()) cb.Remove(k);
                }
                if (!ScheduleFlowState.Current.CanOnlyBlocks.ContainsKey(empId))
                    ScheduleFlowState.Current.CanOnlyBlocks[empId] = [];
            }

            for (int r = 0; r < _activeShifts.Count; r++)
            {
                var key = $"{dayIdx}_{r}";
                if (ScheduleFlowState.Current.IsShabbatLocked(empId, key)) continue;
                if (_currentMode == BlockMode.Cannot)
                    ScheduleFlowState.Current.CannotBlocks[empId].Add(key);
                else
                    ScheduleFlowState.Current.CanOnlyBlocks[empId].Add(key);
            }
        }

        _selectedVm.RefreshSummary();
        BuildBlockingGrid();
    }

    private UIElement MakeBlockCell(string key, CellBlockState state, string empName,
                                     int row, int col)
    {
        Brush bg; bool clickable; string? tip;
        switch (state)
        {
            case CellBlockState.Cannot:
                bg = BrCannotBg; clickable = true; tip = "לא יכול — לחץ להסרה";
                break;
            case CellBlockState.CanOnly:
                bg = BrCanOnlyBg; clickable = true; tip = "יכול רק — לחץ להסרה";
                break;
            case CellBlockState.Shabbat:
                bg = BrShabbatBg; clickable = false; tip = "חסום אוטומטי – שומר שבת";
                break;
            default:
                bg = BrCellBg;
                clickable = _selectedVm != null;
                tip = _selectedVm == null ? "בחר עובד מהחיפים" : null;
                break;
        }

        // Collect OTHER employees blocked in this cell
        var otherCannot  = new List<string>();
        var otherCanOnly = new List<string>();
        var otherShabbat = new List<string>();
        foreach (var emp in _employees)
        {
            if (_selectedVm != null && emp.Id == _selectedVm.Employee.Id) continue;
            var s = ScheduleFlowState.Current.GetCellState(emp.Id, key);
            if      (s == CellBlockState.Shabbat) otherShabbat.Add(emp.Name);
            else if (s == CellBlockState.Cannot)  otherCannot.Add(emp.Name);
            else if (s == CellBlockState.CanOnly)  otherCanOnly.Add(emp.Name);
        }
        bool hasOthers = otherCannot.Count > 0 || otherCanOnly.Count > 0 || otherShabbat.Count > 0;

        var cell = new Border
        {
            Background      = bg,
            BorderBrush     = BrBorder,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Cursor          = clickable ? Cursors.Hand : Cursors.Arrow,
            Tag             = key,
            ToolTip         = tip
        };
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, col);

        // Inner layout: main area (selected emp) + others strip at bottom
        var inner = new Grid();
        inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ── Main area: selected employee state ────────────────────────────
        if (!string.IsNullOrEmpty(empName) && state != CellBlockState.None)
        {
            Brush fg = state switch
            {
                CellBlockState.Cannot  => BrCannotFg,
                CellBlockState.CanOnly => BrCanOnlyFg,
                _                      => BrShabbatFg
            };
            string mainText = state == CellBlockState.Shabbat ? "🔒" : empName;
            Grid.SetRow(new TextBlock(), 0);
            var mainTb = new TextBlock
            {
                Text                = mainText,
                Foreground          = fg,
                FontSize            = state == CellBlockState.Shabbat ? 14 : 13,
                FontWeight          = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
                TextWrapping        = TextWrapping.Wrap,
                Margin              = new Thickness(4, 4, 4, 0),
                FontFamily          = new FontFamily("Segoe UI")
            };
            Grid.SetRow(mainTb, 0);
            inner.Children.Add(mainTb);
        }

        // ── Others strip ──────────────────────────────────────────────────
        if (hasOthers)
        {
            var strip = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(2, 0, 2, 4)
            };

            foreach (var n in otherCannot)
                strip.Children.Add(MakeOtherChip(n, BrCannotFg));
            foreach (var n in otherCanOnly)
                strip.Children.Add(MakeOtherChip(n, BrCanOnlyFg));
            foreach (var n in otherShabbat)
                strip.Children.Add(MakeOtherChip(n, BrShabbatFg));

            Grid.SetRow(strip, 1);
            inner.Children.Add(strip);
        }

        cell.Child = inner;

        if (clickable)
        {
            cell.MouseLeftButtonDown += Cell_Click;
            cell.MouseEnter += (s, _) =>
            {
                if (s is Border b && state == CellBlockState.None)
                    b.Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
            };
            cell.MouseLeave += (s, _) =>
            {
                if (s is Border b && state == CellBlockState.None) b.Background = BrCellBg;
            };
        }

        return cell;
    }

    private static TextBlock MakeOtherChip(string name, Brush fg) =>
        new()
        {
            Text        = name,
            Foreground  = fg,
            FontSize    = 9,
            FontStyle   = FontStyles.Italic,
            Margin      = new Thickness(2, 0, 2, 0),
            FontFamily  = new FontFamily("Segoe UI"),
            VerticalAlignment = VerticalAlignment.Center
        };

    private void Cell_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: string key } || _selectedVm == null) return;
        ScheduleFlowState.Current.ToggleBlock(_selectedVm.Employee.Id, key, _currentMode);
        _currentMode = ScheduleFlowState.Current.GetNaturalMode(_selectedVm.Employee.Id);
        RefreshModeChips();
        _selectedVm.RefreshSummary();
        BuildBlockingGrid();
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

    private static UIElement MakeEmptyState(string msg)
    {
        return new TextBlock
        {
            Text = msg, FontSize = 14, Foreground = BrTextMuted,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            TextAlignment       = TextAlignment.Center,
            TextWrapping        = TextWrapping.Wrap,
            Margin              = new Thickness(24),
            FontFamily          = new FontFamily("Segoe UI")
        };
    }

    // ── Flow navigation ───────────────────────────────────────────────────

    private void Manual_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow win)
            win.EnterFlowPage(new ManualSchedulePage(), "חסימות", "שיבוץ ידני");
    }

    private void Auto_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow win)
            win.EnterFlowPage(new AutoSchedulePage(), "חסימות", "שיבוץ אוטומטי");
    }
}
