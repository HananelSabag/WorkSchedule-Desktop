using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace WorkSchedule.Views;

/// <summary>
/// Custom Hebrew-styled dialog that replaces system MessageBox throughout the app.
/// </summary>
public static class AppDialog
{
    // ── Public API ─────────────────────────────────────────────────────────

    public static void ShowInfo(string message, string title) =>
        Build(message, title, DialogKind.Info, DialogButtons.Ok).ShowDialog();

    public static void ShowSuccess(string message, string title) =>
        Build(message, title, DialogKind.Success, DialogButtons.Ok).ShowDialog();

    public static void ShowError(string message, string title) =>
        Build(message, title, DialogKind.Error, DialogButtons.Ok).ShowDialog();

    public static void ShowWarning(string message, string title) =>
        Build(message, title, DialogKind.Warning, DialogButtons.Ok).ShowDialog();

    public static bool Confirm(string message, string title) =>
        Build(message, title, DialogKind.Question, DialogButtons.YesNo).ShowDialog() == true;

    public static MessageBoxResult Ask(string message, string title) =>
        BuildAndRun(message, title, DialogKind.Question, DialogButtons.YesNoCancel);

    // ── Internal ───────────────────────────────────────────────────────────

    private enum DialogKind   { Info, Success, Warning, Error, Question }
    private enum DialogButtons { Ok, YesNo, YesNoCancel, OkCancel }

    private static MessageBoxResult BuildAndRun(string message, string title, DialogKind kind, DialogButtons buttons)
    {
        var win = Build(message, title, kind, buttons);
        win.ShowDialog();
        return (MessageBoxResult)(win.Tag ?? MessageBoxResult.Cancel);
    }

    private static Window Build(string message, string title, DialogKind kind, DialogButtons buttons)
    {
        // ── Root window ──
        var win = new Window
        {
            Width                  = 460,
            SizeToContent          = SizeToContent.Height,
            WindowStartupLocation  = WindowStartupLocation.CenterOwner,
            ResizeMode             = ResizeMode.NoResize,
            WindowStyle            = WindowStyle.None,
            AllowsTransparency     = true,
            Background             = Brushes.Transparent,
            FlowDirection          = FlowDirection.RightToLeft,
            FontFamily             = new FontFamily("Segoe UI"),
            Tag                    = MessageBoxResult.Cancel,
            Owner                  = Application.Current?.MainWindow
        };

        // ── Outer border with shadow ──
        var outer = new Border
        {
            CornerRadius    = new CornerRadius(12),
            Background      = Brushes.White,
            BorderThickness = new Thickness(0),
            Margin          = new Thickness(12),
            Effect          = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 4, Opacity = 0.28, Color = Colors.Black }
        };

        // ── Main layout ──
        var root = new StackPanel();
        outer.Child = root;

        // ── Colored top strip ──
        var (accentColor, iconText) = kind switch
        {
            DialogKind.Success  => (Color.FromRgb(0x16, 0xA3, 0x4A), "✓"),
            DialogKind.Warning  => (Color.FromRgb(0xD9, 0x77, 0x06), "!"),
            DialogKind.Error    => (Color.FromRgb(0xDC, 0x26, 0x26), "✕"),
            DialogKind.Question => (Color.FromRgb(0x1E, 0x3A, 0x5F), "?"),
            _                   => (Color.FromRgb(0x1E, 0x3A, 0x5F), "i"),
        };

        var strip = new Border
        {
            Background    = new SolidColorBrush(accentColor),
            CornerRadius  = new CornerRadius(12, 12, 0, 0),
            Padding       = new Thickness(24, 20, 24, 20)
        };

        var stripContent = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        // Icon circle
        var iconCircle = new Border
        {
            Width        = 36, Height = 36, CornerRadius = new CornerRadius(18),
            Background   = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
            Margin       = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        iconCircle.Child = new TextBlock
        {
            Text = iconText, Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        stripContent.Children.Add(iconCircle);

        // Title
        stripContent.Children.Add(new TextBlock
        {
            Text = title, Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        });

        strip.Child = stripContent;
        root.Children.Add(strip);

        // ── Message body ──
        var body = new Border { Padding = new Thickness(28, 22, 28, 8) };
        body.Child = new TextBlock
        {
            Text = message, FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
            TextWrapping = TextWrapping.Wrap, LineHeight = 22
        };
        root.Children.Add(body);

        // ── Buttons ──
        var btnRow = new Border { Padding = new Thickness(24, 12, 24, 22) };
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        btnRow.Child = btnPanel;

        void AddBtn(string label, MessageBoxResult tag, bool isPrimary)
        {
            var fg = isPrimary
                ? Brushes.White
                : new SolidColorBrush(Color.FromRgb(0x3B, 0x4A, 0x60));
            var bg = isPrimary
                ? new SolidColorBrush(accentColor)
                : Brushes.Transparent;
            var border = isPrimary
                ? Brushes.Transparent
                : new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));

            var btn = new Button
            {
                Content         = label,
                FontSize        = 14,
                FontWeight      = isPrimary ? FontWeights.SemiBold : FontWeights.Normal,
                FontFamily      = new FontFamily("Segoe UI"),
                Margin          = new Thickness(8, 0, 0, 0),
                Cursor          = System.Windows.Input.Cursors.Hand,
                MinWidth        = 90,
                MinHeight       = 40,
                Background      = bg,
                Foreground      = fg,
                BorderThickness = new Thickness(1.5),
                BorderBrush     = border,
                Template        = MakeButtonTemplate(bg, fg, (SolidColorBrush)border),
            };

            var capturedTag = tag;
            btn.Click += (_, _) => { win.Tag = capturedTag; win.DialogResult = isPrimary || tag == MessageBoxResult.OK; };
            btnPanel.Children.Add(btn);
        }

        switch (buttons)
        {
            case DialogButtons.Ok:
                AddBtn("אישור", MessageBoxResult.OK, true);
                break;
            case DialogButtons.YesNo:
                AddBtn("כן", MessageBoxResult.Yes, true);
                AddBtn("לא",  MessageBoxResult.No,  false);
                break;
            case DialogButtons.YesNoCancel:
                AddBtn("כן",    MessageBoxResult.Yes,    true);
                AddBtn("לא",    MessageBoxResult.No,     false);
                AddBtn("ביטול", MessageBoxResult.Cancel, false);
                break;
            case DialogButtons.OkCancel:
                AddBtn("אישור", MessageBoxResult.OK,     true);
                AddBtn("ביטול", MessageBoxResult.Cancel, false);
                break;
        }

        root.Children.Add(btnRow);
        win.Content = outer;
        return win;
    }

    private static ControlTemplate MakeButtonTemplate(Brush bg, Brush fg, SolidColorBrush border)
    {
        var template = new ControlTemplate(typeof(Button));
        var bdFactory = new FrameworkElementFactory(typeof(Border));
        bdFactory.SetValue(Border.BackgroundProperty,       bg);
        bdFactory.SetValue(Border.BorderBrushProperty,      border);
        bdFactory.SetValue(Border.BorderThicknessProperty,  new Thickness(1.5));
        bdFactory.SetValue(Border.CornerRadiusProperty,     new CornerRadius(8));
        bdFactory.SetValue(Border.PaddingProperty,          new Thickness(24, 10, 24, 13));

        var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
        tbFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        tbFactory.SetValue(TextBlock.VerticalAlignmentProperty,   VerticalAlignment.Center);
        tbFactory.SetValue(TextBlock.ForegroundProperty,          fg);
        tbFactory.SetValue(TextBlock.FontSizeProperty,            14.0);
        tbFactory.SetValue(TextBlock.FontFamilyProperty,          new FontFamily("Segoe UI"));
        tbFactory.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding("Content") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });

        bdFactory.AppendChild(tbFactory);
        template.VisualTree = bdFactory;
        return template;
    }
}
