using System.Windows;
using System.Windows.Controls;
using Peeko.Core.Productivity;

namespace Peeko.App.Productivity;

public partial class TaskPill : UserControl
{
    public TaskPill()
    {
        InitializeComponent();
    }

    /// <summary>Kotak centang di pil diklik.</summary>
    public event Action? CompleteClicked;

    /// <summary>Chip "+10 mnt" diklik.</summary>
    public event Action? SnoozeClicked;

    public void Update(TodoItem? item)
    {
        if (item?.Due is not { } due) return;
        TaskText.Text = item.Text;
        var left = due - DateTime.Now;
        bool isDue = left <= TimeSpan.Zero;
        LeftText.Visibility = isDue ? Visibility.Collapsed : Visibility.Visible;
        SnoozeButton.Visibility = isDue ? Visibility.Visible : Visibility.Collapsed;
        LeftText.Text = Prayer.PrayerText.Left(left, compact: true);
    }

    private void OnCheck(object sender, RoutedEventArgs e) => CompleteClicked?.Invoke();

    private void OnSnooze(object sender, RoutedEventArgs e) => SnoozeClicked?.Invoke();
}
