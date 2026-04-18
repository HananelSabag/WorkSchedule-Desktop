using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using WorkSchedule.Models;

namespace WorkSchedule.Views.Pages;

public partial class EmployeesPage : Page
{
    public EmployeesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var employees = await App.Database.Employees.OrderBy(e => e.Name).ToListAsync();

        SubtitleLabel.Text = employees.Count switch
        {
            0 => "אין עובדים עדיין",
            1 => "עובד אחד",
            _ => $"{employees.Count} עובדים"
        };

        EmployeesList.ItemsSource = employees;
        EmployeesList.Visibility  = employees.Count > 0 ? Visibility.Visible  : Visibility.Collapsed;
        EmptyState.Visibility     = employees.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    // ── Add dialog ────────────────────────────────────────────────────────

    private void AddEmployee_Click(object sender, RoutedEventArgs e)
    {
        EmployeeNameBox.Clear();
        DialogShabbatToggle.IsChecked  = false;
        DialogMitgaberToggle.IsChecked = false;
        DialogOverlay.Visibility = Visibility.Visible;
        EmployeeNameBox.Focus();
    }

    private void CancelDialog_Click(object sender, RoutedEventArgs e)
        => DialogOverlay.Visibility = Visibility.Collapsed;

    private void EmployeeNameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)  SaveEmployee_Click(sender, e);
        if (e.Key == Key.Escape) CancelDialog_Click(sender, e);
    }

    private async void SaveEmployee_Click(object sender, RoutedEventArgs e)
    {
        var name = EmployeeNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;

        App.Database.Employees.Add(new Employee
        {
            Name            = name,
            ShabbatObserver = DialogShabbatToggle.IsChecked == true,
            IsMitgaber      = DialogMitgaberToggle.IsChecked == true
        });

        await App.Database.SaveChangesAsync();
        DialogOverlay.Visibility = Visibility.Collapsed;
        await RefreshAsync();
    }

    // ── Inline toggles ────────────────────────────────────────────────────

    private async void ShabbatToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Tag: Employee emp })
        {
            emp.ShabbatObserver = e.RoutedEvent.Name == "Checked";
            await App.Database.SaveChangesAsync();
            await RefreshAsync();
        }
    }

    private async void MitgaberToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Tag: Employee emp })
        {
            emp.IsMitgaber = e.RoutedEvent.Name == "Checked";
            await App.Database.SaveChangesAsync();
            await RefreshAsync();
        }
    }

    // ── Delete ────────────────────────────────────────────────────────────

    private async void DeleteEmployee_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Employee emp }) return;

        if (!AppDialog.Confirm($"למחוק את {emp.Name}?", "מחיקת עובד")) return;

        App.Database.Employees.Remove(emp);
        await App.Database.SaveChangesAsync();
        await RefreshAsync();
    }
}
