using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Peeko.App.Animation;
using Peeko.Core.Peeks;
using Peeko.Core.Productivity;

namespace Peeko.App.Productivity;

public partial class ClipboardModule : UserControl
{
    private ClipboardController? _clipboard;
    private bool _dirty = true;
    private bool _visible;

    public ClipboardModule()
    {
        InitializeComponent();
    }

    public event Action<PeekRequest>? PeekRequested;

    public void Attach(ClipboardController clipboard)
    {
        _clipboard = clipboard;
        clipboard.History.Changed += () =>
        {
            // Salinan baru saat tab tidak terlihat: susun ulang nanti saja.
            _dirty = true;
            if (_visible) Rebuild();
        };
    }

    public void SetVisible(bool visible)
    {
        _visible = visible;
        if (visible && _dirty) Rebuild();
    }

    private void Rebuild()
    {
        _dirty = false;
        if (_clipboard is null) return;
        var items = _clipboard.History.Items;
        List.Children.Clear();
        foreach (var item in items) List.Children.Add(Row(item));
        Empty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        int pinned = items.Count(i => i.Pinned);
        CountText.Text = items.Count == 0 ? "" : pinned > 0 ? $"{items.Count} · {pinned} di-pin" : $"{items.Count}";
        ClearButton.Visibility = items.Count > pinned ? Visibility.Visible : Visibility.Collapsed;
    }

    private FrameworkElement Row(ClipItem item)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new TextBlock
        {
            Text = ClipboardHistory.Preview(item.Text),
            Style = (Style)FindResource("Peeko.Text.Body"),
            FontSize = 12.5,
            Foreground = B(item.Pinned ? "TextPrimary" : "TextSecondary"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var pin = IconButton("Peeko.Icon.Pin", item.Pinned ? "Lepas pin" : "Pin supaya tetap tersimpan", item.Pinned ? "Accent" : "TextTertiary");
        pin.Opacity = item.Pinned ? 1 : 0;
        pin.Click += (_, _) => _clipboard?.History.TogglePin(item.Id);
        Grid.SetColumn(pin, 1);
        grid.Children.Add(pin);

        var remove = IconButton("Peeko.Icon.Close", "Hapus", "TextTertiary");
        remove.Opacity = 0;
        remove.Click += (_, _) => _clipboard?.History.Remove(item.Id);
        Grid.SetColumn(remove, 2);
        grid.Children.Add(remove);

        var row = new Border
        {
            Height = 30,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 0, 4, 0),
            Margin = new Thickness(0, 0, 0, 2),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = item.Text.Length > 600 ? item.Text[..600] + "…" : item.Text,
            Child = grid,
        };
        row.MouseEnter += (_, _) =>
        {
            row.Background = B("SurfaceRaised");
            pin.Animate(OpacityProperty, Motion.To(1, Motion.Fast, Motion.Out));
            remove.Animate(OpacityProperty, Motion.To(1, Motion.Fast, Motion.Out));
        };
        row.MouseLeave += (_, _) =>
        {
            row.Background = Brushes.Transparent;
            if (!item.Pinned) pin.Animate(OpacityProperty, Motion.To(0, Motion.Fast, Motion.Out));
            remove.Animate(OpacityProperty, Motion.To(0, Motion.Fast, Motion.Out));
        };
        row.MouseLeftButtonUp += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject d && FindParent<Button>(d) is not null) return;
            if (_clipboard?.CopyBack(item) == true)
                PeekRequested?.Invoke(new PeekRequest(PeekKind.FileAction) { Flag = true, Text = "Disalin", Detail = ClipboardHistory.Preview(item.Text) });
        };
        return row;
    }

    private Button IconButton(string icon, string tooltip, string brush) => new()
    {
        Style = (Style)FindResource("Peeko.Button.Icon"),
        Tag = FindResource(icon),
        Width = 24,
        Height = 24,
        Foreground = B(brush),
        ToolTip = tooltip,
        Margin = new Thickness(2, 0, 0, 0),
    };

    private static T? FindParent<T>(DependencyObject node) where T : DependencyObject
    {
        for (var n = node; n is not null; n = VisualTreeHelper.GetParent(n))
            if (n is T t) return t;
        return null;
    }

    private void OnClear(object sender, RoutedEventArgs e) => _clipboard?.History.Clear();

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Peeko.Brush.{token}"];
}
