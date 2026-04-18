using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using WorkSchedule.Models;

namespace WorkSchedule.Views.Pages;

// VM for each shift row — fires PropertyChanged so preview rebuilds live
public class ShiftRowVm : INotifyPropertyChanged
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public int OrderIndex { get; set; }
    public int OrderNumber => OrderIndex + 1;

    private string _shiftName = "";
    public string ShiftName
    {
        get => _shiftName;
        set { _shiftName = value; OnPropertyChanged(); }
    }

    private string _shiftHours = "";
    public string ShiftHours
    {
        get => _shiftHours;
        set { _shiftHours = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class TemplatePage : Page
{
    private ShiftTemplate? _template;
    private ObservableCollection<ShiftRowVm> _shifts = [];
    private List<DayColumn> _days = [];

    // ── Colors shared between preview and the future image export ──────────
    private static readonly SolidColorBrush HeaderBg    = new(Color.FromRgb(0x3B, 0x82, 0xF6)); // primary blue
    private static readonly SolidColorBrush ShiftLabelBg = new(Color.FromRgb(0xEF, 0xF6, 0xFF)); // light blue
    private static readonly SolidColorBrush CellBg       = new(Color.FromRgb(0xFF, 0xFF, 0xFF)); // white
    private static readonly SolidColorBrush BorderCol     = new(Color.FromRgb(0xE2, 0xE8, 0xF0)); // slate-200
    private static readonly SolidColorBrush TextDark      = new(Color.FromRgb(0x1E, 0x29, 0x3B));
    private static readonly SolidColorBrush TextWhite     = Brushes.White;
    private static readonly SolidColorBrush TextMuted     = new(Color.FromRgb(0x64, 0x74, 0x8B));

    public TemplatePage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _template = await App.Database.ShiftTemplates
                             .Include(t => t.ShiftRows)
                             .Include(t => t.DayColumns)
                             .FirstOrDefaultAsync(t => t.IsActive);

        if (_template == null) return;

        TemplateNameBox.Text = _template.Name;

        _days = _template.DayColumns.OrderBy(d => d.DayIndex).ToList();
        DaysList.ItemsSource = _days;

        _shifts = new ObservableCollection<ShiftRowVm>(
            _template.ShiftRows
                     .OrderBy(r => r.OrderIndex)
                     .Select(r => new ShiftRowVm
                     {
                         Id = r.Id, TemplateId = r.TemplateId,
                         OrderIndex = r.OrderIndex,
                         ShiftName  = r.ShiftName,
                         ShiftHours = r.ShiftHours
                     }));

        // Subscribe to collection and item changes → rebuild preview live
        _shifts.CollectionChanged += OnShiftsChanged;
        foreach (var vm in _shifts)
            vm.PropertyChanged += OnShiftItemChanged;

        ShiftsList.ItemsSource = _shifts;
        BuildPreview();
    }

    // ── Event wiring ───────────────────────────────────────────────────────

    private void OnShiftsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Subscribe to newly added VMs
        if (e.NewItems != null)
            foreach (ShiftRowVm vm in e.NewItems)
                vm.PropertyChanged += OnShiftItemChanged;
        BuildPreview();
    }

    private void OnShiftItemChanged(object? sender, PropertyChangedEventArgs e)
        => BuildPreview();

    // Called by TextChanged on TemplateNameBox (day toggle events wired in XAML)
    private void Config_Changed(object sender, TextChangedEventArgs e) => BuildPreview();

    private void DayToggle_Changed(object sender, RoutedEventArgs e) => BuildPreview();

    // ── Preview builder ────────────────────────────────────────────────────

    private void BuildPreview()
    {
        var activeDays   = _days.Where(d => d.IsEnabled).ToList();
        var activeShifts = _shifts.ToList();

        if (activeDays.Count == 0 || activeShifts.Count == 0)
        {
            PreviewHost.Content = BuildEmptyPreview();
            return;
        }

        // Grid: row 0 = day headers, rows 1..n = shifts
        // Col 0 = shift label, cols 1..m = day cells
        var grid = new Grid { FlowDirection = FlowDirection.RightToLeft };

        // Column definitions: shift-label col (fixed) + one per active day
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        foreach (var _ in activeDays)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Row definitions: header + one per shift
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        foreach (var _ in activeShifts)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) });

        // ── Header row ─────────────────────────────────────────────────────

        // Top-left corner cell (empty)
        grid.Children.Add(MakeCell("", 0, 0, HeaderBg, TextWhite, FontWeights.SemiBold, 12, isBorder: true));

        // Day name headers
        for (int c = 0; c < activeDays.Count; c++)
            grid.Children.Add(MakeCell(activeDays[c].DayNameHebrew, 0, c + 1,
                                       HeaderBg, TextWhite, FontWeights.SemiBold, 13, isBorder: true));

        // ── Shift rows ─────────────────────────────────────────────────────
        for (int r = 0; r < activeShifts.Count; r++)
        {
            var shift = activeShifts[r];

            // Shift label cell (name + hours)
            var labelCell = MakeShiftLabelCell(shift.ShiftName, shift.ShiftHours, r + 1);
            grid.Children.Add(labelCell);

            // Empty assignment cells for each day
            for (int c = 0; c < activeDays.Count; c++)
                grid.Children.Add(MakeCell("", r + 1, c + 1, CellBg, TextDark,
                                           FontWeights.Normal, 12, isBorder: true));
        }

        PreviewHost.Content = grid;
    }

    // Creates a simple text cell at [row, col]
    private static UIElement MakeCell(string text, int row, int col,
                                      Brush bg, Brush fg, FontWeight weight,
                                      double fontSize, bool isBorder)
    {
        var border = new Border
        {
            Background      = bg,
            BorderBrush     = BorderCol,
            BorderThickness = new Thickness(0, 0, 1, 1),
        };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);

        if (!string.IsNullOrEmpty(text))
        {
            border.Child = new TextBlock
            {
                Text                = text,
                Foreground          = fg,
                FontWeight          = weight,
                FontSize            = fontSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
                TextWrapping        = TextWrapping.Wrap,
                Margin              = new Thickness(4, 2, 4, 2),
                FontFamily          = new FontFamily("Segoe UI")
            };
        }

        return border;
    }

    // Shift label cell — blue-tinted background, name on top + hours below
    private static UIElement MakeShiftLabelCell(string name, string hours, int row)
    {
        var border = new Border
        {
            Background      = ShiftLabelBg,
            BorderBrush     = BorderCol,
            BorderThickness = new Thickness(0, 0, 1, 1),
        };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, 0);

        var stack = new StackPanel
        {
            VerticalAlignment   = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin              = new Thickness(6, 4, 6, 4)
        };

        stack.Children.Add(new TextBlock
        {
            Text                = string.IsNullOrWhiteSpace(name) ? "—" : name,
            Foreground          = TextDark,
            FontWeight          = FontWeights.SemiBold,
            FontSize            = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment       = TextAlignment.Center,
            FontFamily          = new FontFamily("Segoe UI")
        });

        if (!string.IsNullOrWhiteSpace(hours))
        {
            stack.Children.Add(new TextBlock
            {
                Text                = hours,
                Foreground          = TextMuted,
                FontSize            = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
                FontFamily          = new FontFamily("Segoe UI"),
                Margin              = new Thickness(0, 2, 0, 0)
            });
        }

        border.Child = stack;
        return border;
    }

    private static UIElement BuildEmptyPreview()
    {
        return new TextBlock
        {
            Text                = "הגדר לפחות יום עבודה אחד ומשמרת אחת כדי לראות תצוגה מקדימה",
            FontSize            = 13,
            Foreground          = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            TextAlignment       = TextAlignment.Center,
            TextWrapping        = TextWrapping.Wrap,
            Margin              = new Thickness(24),
            FontFamily          = new FontFamily("Segoe UI")
        };
    }

    // ── CRUD ───────────────────────────────────────────────────────────────

    private void AddShift_Click(object sender, RoutedEventArgs e)
    {
        var vm = new ShiftRowVm
        {
            TemplateId = _template?.Id ?? 0,
            OrderIndex = _shifts.Count,
            ShiftName  = "",
            ShiftHours = ""
        };
        vm.PropertyChanged += OnShiftItemChanged;
        _shifts.Add(vm);
    }

    private void RemoveShift_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ShiftRowVm vm })
            _shifts.Remove(vm);
    }

    private void MoveShiftUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ShiftRowVm vm }) return;
        int idx = _shifts.IndexOf(vm);
        if (idx > 0) { _shifts.Move(idx, idx - 1); RefreshOrderNumbers(); }
    }

    private void MoveShiftDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ShiftRowVm vm }) return;
        int idx = _shifts.IndexOf(vm);
        if (idx < _shifts.Count - 1) { _shifts.Move(idx, idx + 1); RefreshOrderNumbers(); }
    }

    private void RefreshOrderNumbers()
    {
        for (int i = 0; i < _shifts.Count; i++)
            _shifts[i].OrderIndex = i;
    }

    private async void SaveTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (_template == null) return;

        var name = TemplateNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            AppDialog.ShowWarning("נא להזין שם לתבנית.", "שגיאה");
            return;
        }
        if (_shifts.Count == 0)
        {
            AppDialog.ShowWarning("חייבת להיות לפחות משמרת אחת.", "שגיאה");
            return;
        }

        _template.Name         = name;
        _template.RowCount     = _shifts.Count;
        _template.ColumnCount  = _days.Count(d => d.IsEnabled);
        _template.LastModified = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var existingRows = await App.Database.ShiftRows
                                    .Where(r => r.TemplateId == _template.Id)
                                    .ToListAsync();
        App.Database.ShiftRows.RemoveRange(existingRows);

        for (int i = 0; i < _shifts.Count; i++)
        {
            var vm = _shifts[i];
            App.Database.ShiftRows.Add(new ShiftRow
            {
                TemplateId = _template.Id,
                OrderIndex = i,
                ShiftName  = vm.ShiftName.Trim(),
                ShiftHours = vm.ShiftHours.Trim()
            });
        }

        await App.Database.SaveChangesAsync();

        if (Window.GetWindow(this) is MainWindow win)
            win.SetTemplateName(_template.Name);

        AppDialog.ShowSuccess("התבנית נשמרה בהצלחה!", "שמירה");
        await LoadAsync();
    }
}
