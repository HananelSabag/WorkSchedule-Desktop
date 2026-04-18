using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using Newtonsoft.Json;
using WorkSchedule.Models;

namespace WorkSchedule.Views.Pages;

public partial class PreviewPage : Page
{
    private readonly int _scheduleId;
    private Schedule?    _schedule;
    private List<DayColumn>  _activeDays   = [];
    private List<ShiftRow>   _activeShifts = [];

    // Human-key format: "dayNameHebrew|shiftName" → text
    private Dictionary<string, string> _cellTexts = [];
    private string? _editingHumanKey;

    // ── Brushes ──────────────────────────────────────────────────────────
    private static readonly SolidColorBrush BrHeaderBg    = new(Color.FromRgb(0x1E, 0x3A, 0x5F));
    private static readonly SolidColorBrush BrShiftLabelBg = new(Color.FromRgb(0xEF, 0xF6, 0xFF));
    private static readonly SolidColorBrush BrAssignedBg  = new(Color.FromRgb(0xF8, 0xFA, 0xFF));
    private static readonly SolidColorBrush BrEmptyBg     = Brushes.White;
    private static readonly SolidColorBrush BrBorder      = new(Color.FromRgb(0xE2, 0xE8, 0xF0));
    private static readonly SolidColorBrush BrTextDark    = new(Color.FromRgb(0x1E, 0x29, 0x3B));
    private static readonly SolidColorBrush BrTextMuted   = new(Color.FromRgb(0x64, 0x74, 0x8B));

    public PreviewPage(int scheduleId)
    {
        _scheduleId = scheduleId;
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _schedule = await App.Database.Schedules.FindAsync(_scheduleId);
        if (_schedule == null) return;

        ScheduleTitle.Text = _schedule.WeekStart;

        // Parse cell data
        _cellTexts = string.IsNullOrEmpty(_schedule.ScheduleData) || _schedule.ScheduleData == "{}"
            ? []
            : JsonConvert.DeserializeObject<Dictionary<string, string>>(_schedule.ScheduleData) ?? [];

        // Load active template for grid structure
        var template = await App.Database.ShiftTemplates
            .Include(t => t.ShiftRows)
            .Include(t => t.DayColumns)
            .FirstOrDefaultAsync(t => t.IsActive);

        if (template != null)
        {
            _activeDays   = template.DayColumns.Where(d => d.IsEnabled).OrderBy(d => d.DayIndex).ToList();
            _activeShifts = template.ShiftRows.OrderBy(r => r.OrderIndex).ToList();
        }

        BuildPreviewGrid();
    }

    // ── Grid builder ──────────────────────────────────────────────────────

    private void BuildPreviewGrid()
    {
        if (_activeDays.Count == 0 || _activeShifts.Count == 0)
        {
            ScheduleGridHost.Content = new TextBlock
            {
                Text = "הגדר תבנית טבלה כדי להציג את הסידור",
                FontSize = 14, Foreground = BrTextMuted,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI")
            };
            return;
        }

        var grid = new Grid { FlowDirection = FlowDirection.RightToLeft };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110), MinWidth = 90 });
        foreach (var _ in _activeDays)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 80 });

        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });
        foreach (var _ in _activeShifts)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(85) });

        // Header
        grid.Children.Add(MakeStaticCell("סידור עבודה", 0, 0, BrHeaderBg, Brushes.White, FontWeights.Bold, 13));
        for (int c = 0; c < _activeDays.Count; c++)
            grid.Children.Add(MakeStaticCell(_activeDays[c].DayNameHebrew, 0, c + 1,
                                             BrHeaderBg, Brushes.White, FontWeights.SemiBold, 13));

        // Shift rows
        for (int r = 0; r < _activeShifts.Count; r++)
        {
            grid.Children.Add(MakeShiftLabelCell(_activeShifts[r].ShiftName, _activeShifts[r].ShiftHours, r + 1));
            for (int c = 0; c < _activeDays.Count; c++)
            {
                var humanKey = $"{_activeDays[c].DayNameHebrew}|{_activeShifts[r].ShiftName}";
                var text     = _cellTexts.GetValueOrDefault(humanKey, "");
                grid.Children.Add(MakeEditableCell(humanKey, text, r + 1, c + 1));
            }
        }

        ScheduleGridHost.Content = grid;
    }

    private UIElement MakeEditableCell(string humanKey, string text, int row, int col)
    {
        bool hasContent = !string.IsNullOrEmpty(text);
        var cell = new Border
        {
            Background      = hasContent ? BrAssignedBg : BrEmptyBg,
            BorderBrush     = BrBorder,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Cursor          = Cursors.Hand,
            Tag             = humanKey,
            ToolTip         = "לחץ לעריכה"
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

        cell.MouseLeftButtonDown += Cell_Click;
        cell.MouseEnter += (s, _) =>
        {
            if (s is Border b) b.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF7, 0xFF));
        };
        cell.MouseLeave += (s, _) =>
        {
            if (s is Border b) b.Background = hasContent ? BrAssignedBg : BrEmptyBg;
        };

        return cell;
    }

    private void Cell_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: string humanKey }) return;
        var current = _cellTexts.GetValueOrDefault(humanKey, "");
        OpenTextEdit(humanKey, current);
    }

    // ── Text edit overlay ─────────────────────────────────────────────────

    private void OpenTextEdit(string humanKey, string current)
    {
        _editingHumanKey = humanKey;
        var parts = humanKey.Split('|');
        TextEditTitle.Text = parts.Length == 2 ? $"עריכה: {parts[0]} — {parts[1]}" : "עריכת תא";
        TextEditBox.Text   = current;
        TextEditOverlay.Visibility = Visibility.Visible;
        TextEditBox.Focus();
        TextEditBox.SelectAll();
    }

    private void TextEditConfirm_Click(object sender, RoutedEventArgs e) => CommitTextEdit();
    private void TextEditCancel_Click(object sender, RoutedEventArgs e)  => CloseTextEdit();

    private void TextEditClear_Click(object sender, RoutedEventArgs e)
    {
        if (_editingHumanKey != null) _cellTexts.Remove(_editingHumanKey);
        CloseTextEdit();
        _ = SaveToDatabaseAsync();
        BuildPreviewGrid();
    }

    private void TextEditBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)  CommitTextEdit();
        if (e.Key == Key.Escape) CloseTextEdit();
    }

    private void CommitTextEdit()
    {
        if (_editingHumanKey == null) { CloseTextEdit(); return; }
        var text = TextEditBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
            _cellTexts.Remove(_editingHumanKey);
        else
            _cellTexts[_editingHumanKey] = text;

        CloseTextEdit();
        _ = SaveToDatabaseAsync();
        BuildPreviewGrid();
    }

    private void CloseTextEdit()
    {
        TextEditOverlay.Visibility = Visibility.Collapsed;
        _editingHumanKey = null;
    }

    private async Task SaveToDatabaseAsync()
    {
        if (_schedule == null) return;
        _schedule.ScheduleData = JsonConvert.SerializeObject(_cellTexts);
        await App.Database.SaveChangesAsync();
    }

    // ── Static cell builders ──────────────────────────────────────────────

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
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(6, 4, 6, 4)
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

    // ── Export ────────────────────────────────────────────────────────────

    private void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        if (_activeDays.Count == 0 || _activeShifts.Count == 0)
        {
            AppDialog.ShowWarning("אין תוכן לייצוא.", "שגיאה");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title    = "שמור תמונה",
            Filter   = "PNG image|*.png",
            FileName = $"סידור-{SanitizeFileName(_schedule?.WeekStart ?? "export")}.png"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            // Build a dedicated LTR export grid (avoids RTL→bitmap mirroring bug)
            // Days are in reversed order so the visual result matches the RTL on-screen layout
            var weekStart = ParseWeekStart();
            var exportGrid = BuildExportGrid(weekStart);

            // Measure off-screen
            exportGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            exportGrid.Arrange(new Rect(exportGrid.DesiredSize));
            exportGrid.UpdateLayout();

            var dpi   = 150.0;
            var scale = dpi / 96.0;
            var w = (int)(exportGrid.DesiredSize.Width  * scale);
            var h = (int)(exportGrid.DesiredSize.Height * scale);

            if (w == 0 || h == 0)
            {
                AppDialog.ShowWarning("לא ניתן לייצא — הגריד ריק.", "שגיאה");
                return;
            }

            var bitmap = new RenderTargetBitmap(w, h, dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(exportGrid);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);

            AppDialog.ShowSuccess($"התמונה נשמרה בהצלחה!\n{dialog.FileName}", "ייצוא PNG");
        }
        catch (Exception ex)
        {
            AppDialog.ShowError($"שגיאה בייצוא:\n{ex.Message}", "שגיאה");
        }
    }

    // Build a FlowDirection.LeftToRight grid for PNG export.
    // Days are in REVERSE order so the visual result matches the RTL on-screen layout
    // (Sunday appears on the right, Saturday on the left when the PNG is viewed normally).
    private Grid BuildExportGrid(DateTime? weekStart)
    {
        var grid = new Grid { FlowDirection = FlowDirection.LeftToRight, Background = Brushes.White };

        // Reversed days: Saturday → ... → Sunday (left to right in LTR = same visual as RTL Sun→Sat)
        var reversedDays = _activeDays.AsEnumerable().Reverse().ToList();
        int shiftCol = reversedDays.Count; // shift label goes in the LAST (rightmost) column

        foreach (var _ in reversedDays)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) }); // shift label last

        bool hasDates = weekStart.HasValue;
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(hasDates ? 62 : 46) });
        foreach (var _ in _activeShifts)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(85) });

        // Corner (rightmost header cell)
        grid.Children.Add(MakeExportCornerCell(0, shiftCol));

        // Day header cells
        for (int c = 0; c < reversedDays.Count; c++)
        {
            var day  = reversedDays[c];
            var date = weekStart.HasValue
                ? weekStart.Value.AddDays(day.DayIndex).ToString("dd/MM")
                : "";
            grid.Children.Add(MakeExportDayHeaderCell(day.DayNameHebrew, date, 0, c));
        }

        // Shift rows
        for (int r = 0; r < _activeShifts.Count; r++)
        {
            grid.Children.Add(MakeExportShiftLabelCell(_activeShifts[r].ShiftName, _activeShifts[r].ShiftHours, r + 1, shiftCol));
            for (int c = 0; c < reversedDays.Count; c++)
            {
                var key  = $"{reversedDays[c].DayNameHebrew}|{_activeShifts[r].ShiftName}";
                var text = _cellTexts.GetValueOrDefault(key, "");
                grid.Children.Add(MakeExportDataCell(text, r + 1, c));
            }
        }

        return grid;
    }

    private static UIElement MakeExportCornerCell(int row, int col)
    {
        var b = new Border { Background = BrHeaderBg, BorderBrush = BrBorder, BorderThickness = new Thickness(1, 1, 1, 1) };
        Grid.SetRow(b, row); Grid.SetColumn(b, col);
        b.Child = new TextBlock { Text = "סידור עבודה", Foreground = Brushes.White, FontWeight = FontWeights.Bold,
            FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center, FlowDirection = FlowDirection.RightToLeft, FontFamily = new FontFamily("Segoe UI") };
        return b;
    }

    private static UIElement MakeExportDayHeaderCell(string dayName, string date, int row, int col)
    {
        var b = new Border { Background = BrHeaderBg, BorderBrush = BrBorder, BorderThickness = new Thickness(0, 1, 1, 1) };
        Grid.SetRow(b, row); Grid.SetColumn(b, col);
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        sp.Children.Add(new TextBlock { Text = dayName, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold,
            FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center,
            FlowDirection = FlowDirection.RightToLeft, FontFamily = new FontFamily("Segoe UI") });
        if (!string.IsNullOrEmpty(date))
            sp.Children.Add(new TextBlock { Text = date, Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0), FontFamily = new FontFamily("Segoe UI") });
        b.Child = sp;
        return b;
    }

    private static UIElement MakeExportShiftLabelCell(string name, string hours, int row, int col)
    {
        var b = new Border { Background = BrShiftLabelBg, BorderBrush = BrBorder, BorderThickness = new Thickness(1, 0, 1, 1) };
        Grid.SetRow(b, row); Grid.SetColumn(b, col);
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(4) };
        sp.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(name) ? "—" : name,
            Foreground = BrTextDark, FontWeight = FontWeights.SemiBold, FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center,
            FlowDirection = FlowDirection.RightToLeft, FontFamily = new FontFamily("Segoe UI") });
        if (!string.IsNullOrWhiteSpace(hours))
            sp.Children.Add(new TextBlock { Text = hours, Foreground = BrTextMuted, FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0), FontFamily = new FontFamily("Segoe UI") });
        b.Child = sp;
        return b;
    }

    private static UIElement MakeExportDataCell(string text, int row, int col)
    {
        bool has = !string.IsNullOrEmpty(text);
        var b = new Border { Background = has ? BrAssignedBg : Brushes.White, BorderBrush = BrBorder, BorderThickness = new Thickness(0, 0, 1, 1) };
        Grid.SetRow(b, row); Grid.SetColumn(b, col);
        if (has)
            b.Child = new TextBlock { Text = text, Foreground = BrTextDark, FontSize = 13, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4),
                FlowDirection = FlowDirection.RightToLeft, FontFamily = new FontFamily("Segoe UI") };
        return b;
    }

    // Parse "19/04 - 25/04" from schedule name → return Sunday date
    private DateTime? ParseWeekStart()
    {
        var parts = (_schedule?.WeekStart ?? "").Split(" - ");
        if (parts.Length < 1) return null;
        if (DateTime.TryParseExact(parts[0].Trim(), "dd/MM",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return new DateTime(DateTime.Today.Year, d.Month, d.Day);
        return null;
    }

    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title    = "ייצוא לאקסל",
            Filter   = "Excel|*.xlsx",
            FileName = $"סידור-{SanitizeFileName(_schedule?.WeekStart ?? "export")}.xlsx"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("סידור");

            // Style helpers
            var headerFill = XLColor.FromHtml("#1E3A5F");
            var shiftFill  = XLColor.FromHtml("#EFF6FF");


            // Header row — days in columns 1.._activeDays.Count, shift label in last column
            int shiftExcelCol = _activeDays.Count + 1;
            for (int c = 0; c < _activeDays.Count; c++)
            {
                ws.Cell(1, c + 1).Value = _activeDays[c].DayNameHebrew;
                StyleHeader(ws.Cell(1, c + 1), headerFill);
            }
            ws.Cell(1, shiftExcelCol).Value = "סידור עבודה";
            StyleHeader(ws.Cell(1, shiftExcelCol), headerFill);

            // Shift rows
            for (int r = 0; r < _activeShifts.Count; r++)
            {
                var shift = _activeShifts[r];
                ws.Row(r + 2).Height = 40;

                for (int c = 0; c < _activeDays.Count; c++)
                {
                    var humanKey = $"{_activeDays[c].DayNameHebrew}|{shift.ShiftName}";
                    var text     = _cellTexts.GetValueOrDefault(humanKey, "");
                    var cell     = ws.Cell(r + 2, c + 1);
                    cell.Value   = text;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
                    cell.Style.Font.FontSize = 11;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E2E8F0");
                }

                var labelCell = ws.Cell(r + 2, shiftExcelCol);
                labelCell.Value = $"{shift.ShiftName}\n{shift.ShiftHours}";
                labelCell.Style.Fill.BackgroundColor = shiftFill;
                labelCell.Style.Font.Bold = true;
                labelCell.Style.Font.FontSize = 11;
                labelCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                labelCell.Style.Alignment.WrapText = true;
            }

            ws.Columns().AdjustToContents();
            workbook.SaveAs(dialog.FileName);

            AppDialog.ShowSuccess($"האקסל נשמר בהצלחה!\n{dialog.FileName}", "ייצוא Excel");
        }
        catch (Exception ex)
        {
            AppDialog.ShowError($"שגיאה בייצוא:\n{ex.Message}", "שגיאה");
        }
    }

    private static void StyleHeader(IXLCell cell, XLColor fill)
    {
        cell.Style.Fill.BackgroundColor = fill;
        cell.Style.Font.FontColor = XLColor.White;
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontSize = 12;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        cell.Style.Border.OutsideBorderColor = XLColor.White;
    }

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    // ── WhatsApp share ────────────────────────────────────────────────────

    private void ShareWhatsApp_Click(object sender, RoutedEventArgs e)
    {
        if (_activeDays.Count == 0 || _activeShifts.Count == 0)
        {
            AppDialog.ShowWarning("אין תוכן לשיתוף.", "שגיאה");
            return;
        }

        try
        {
            // Render export grid to PNG and copy to clipboard
            var weekStart  = ParseWeekStart();
            var exportGrid = BuildExportGrid(weekStart);

            exportGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            exportGrid.Arrange(new Rect(exportGrid.DesiredSize));
            exportGrid.UpdateLayout();

            const double dpi   = 150.0;
            const double scale = dpi / 96.0;
            var w = (int)(exportGrid.DesiredSize.Width  * scale);
            var h = (int)(exportGrid.DesiredSize.Height * scale);

            if (w == 0 || h == 0)
            {
                AppDialog.ShowWarning("לא ניתן לייצא — הגריד ריק.", "שגיאה");
                return;
            }

            var bitmap = new RenderTargetBitmap(w, h, dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(exportGrid);

            Clipboard.SetImage(bitmap);

            // Try to launch WhatsApp
            bool launched = TryLaunchWhatsApp();
            string msg = launched
                ? "התמונה הועתקה ללוח.\nפתח שיחה בוואטסאפ והדבק עם Ctrl+V"
                : "התמונה הועתקה ללוח.\nפתח וואטסאפ והדבק עם Ctrl+V";

            AppDialog.ShowSuccess(msg, "שיתוף וואטסאפ");
        }
        catch (Exception ex)
        {
            AppDialog.ShowError($"שגיאה בשיתוף:\n{ex.Message}", "שגיאה");
        }
    }

    private static bool TryLaunchWhatsApp()
    {
        // Check common WhatsApp install paths
        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var paths = new[]
        {
            Path.Combine(localApp, "WhatsApp", "WhatsApp.exe"),
            Path.Combine(localApp, "Programs", "WhatsApp", "WhatsApp.exe"),
        };

        var exePath = paths.FirstOrDefault(File.Exists);
        if (exePath != null)
        {
            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
            return true;
        }

        // Try URI scheme as fallback
        try
        {
            Process.Start(new ProcessStartInfo("whatsapp:") { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
