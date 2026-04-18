using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using WorkSchedule.Models;

namespace WorkSchedule.Views.Pages;

public class ScheduleHistoryItem
{
    public int    Id                 { get; init; }
    public string WeekStart          { get; init; } = "";
    public string CreatedDateDisplay { get; init; } = "";
}

public partial class HistoryPage : Page
{
    private ScheduleHistoryItem? _renamingItem;

    public HistoryPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var schedules = await App.Database.Schedules
            .Where(s => s.WeekStart != Schedule.DraftMarker)
            .OrderByDescending(s => s.CreatedDate)
            .ToListAsync();

        SubtitleLabel.Text = schedules.Count > 0
            ? $"{schedules.Count} סידורים שמורים"
            : "אין סידורים שמורים עדיין";

        if (schedules.Count == 0)
        {
            EmptyState.Visibility   = Visibility.Visible;
            ScheduleList.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyState.Visibility   = Visibility.Collapsed;
        ScheduleList.Visibility = Visibility.Visible;

        ScheduleList.ItemsSource = schedules.Select(s => new ScheduleHistoryItem
        {
            Id                 = s.Id,
            WeekStart          = s.WeekStart,
            CreatedDateDisplay = FormatDate(s.CreatedDate)
        }).ToList();
    }

    private static string FormatDate(long unixMs)
    {
        var dt = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).ToLocalTime();
        return $"נשמר ב-{dt:dd/MM/yyyy} בשעה {dt:HH:mm}";
    }

    // ── Open ─────────────────────────────────────────────────────────────

    private void OpenSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ScheduleHistoryItem item }) return;
        if (Window.GetWindow(this) is MainWindow win)
            win.NavigateTo(win.NavHistory, new PreviewPage(item.Id));
    }

    // ── Delete ────────────────────────────────────────────────────────────

    private async void DeleteSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ScheduleHistoryItem item }) return;

        if (!AppDialog.Confirm($"למחוק את הסידור \"{item.WeekStart}\"?\nפעולה זו לא ניתנת לביטול.", "מחיקת סידור"))
            return;

        var schedule = await App.Database.Schedules.FindAsync(item.Id);
        if (schedule == null) return;

        App.Database.Schedules.Remove(schedule);
        await App.Database.SaveChangesAsync();
        await LoadAsync();
    }

    // ── Rename ────────────────────────────────────────────────────────────

    private void RenameSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ScheduleHistoryItem item }) return;
        _renamingItem  = item;
        RenameBox.Text = item.WeekStart;
        RenameOverlay.Visibility = Visibility.Visible;
        RenameBox.Focus();
        RenameBox.SelectAll();
    }

    private void RenameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)  _ = CommitRenameAsync();
        if (e.Key == Key.Escape) CloseRenameOverlay();
    }

    private void RenameConfirm_Click(object sender, RoutedEventArgs e) => _ = CommitRenameAsync();
    private void RenameCancel_Click(object sender, RoutedEventArgs e)  => CloseRenameOverlay();

    private async Task CommitRenameAsync()
    {
        if (_renamingItem == null) { CloseRenameOverlay(); return; }

        var newName = RenameBox.Text.Trim();
        if (string.IsNullOrEmpty(newName)) { CloseRenameOverlay(); return; }

        var schedule = await App.Database.Schedules.FindAsync(_renamingItem.Id);
        if (schedule != null)
        {
            schedule.WeekStart = newName;
            await App.Database.SaveChangesAsync();
        }

        CloseRenameOverlay();
        await LoadAsync();
    }

    private void CloseRenameOverlay()
    {
        RenameOverlay.Visibility = Visibility.Collapsed;
        _renamingItem = null;
    }
}
