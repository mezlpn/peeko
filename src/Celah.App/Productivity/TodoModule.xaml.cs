using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Celah.App.Animation;
using Celah.Core.Productivity;

namespace Celah.App.Productivity;

public partial class TodoModule : UserControl
{
    private readonly DispatcherTimer _sinkDelay = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private TodoList? _list;

    public TodoModule()
    {
        InitializeComponent();
        // Tugas yang dicentang turun ke bawah setelah jeda 600 ms supaya centangnya sempat terlihat.
        _sinkDelay.Tick += (_, _) =>
        {
            _sinkDelay.Stop();
            Rebuild();
        };
    }

    /// <summary>
    /// Input teks dibuka/ditutup. Jendela notch normalnya tidak bisa menerima keyboard, jadi
    /// pemiliknya perlu mengaktifkan jendela sementara dan menahan panel tetap terbuka.
    /// </summary>
    public event Action<bool>? TextInputChanged;

    public bool IsEditing => InputRow.Visibility == Visibility.Visible;

    public void Attach(TodoList list)
    {
        _list = list;
        list.Changed += () =>
        {
            if (!_sinkDelay.IsEnabled) Rebuild();
        };
        Rebuild();
    }

    public void Rebuild()
    {
        if (_list is null) return;
        CountText.Text = _list.Count == 0 ? "" : $"{_list.DoneCount} dari {_list.Count} selesai";
        EmptyView.Visibility = _list.Count == 0 && !IsEditing ? Visibility.Visible : Visibility.Collapsed;

        List.Children.Clear();
        foreach (var item in _list.Items) List.Children.Add(BuildRow(item));
    }

    private FrameworkElement BuildRow(TodoItem item)
    {
        var row = new Grid { Height = 32, Margin = new Thickness(0, 0, 0, 2) };
        var bg = new Border { CornerRadius = new CornerRadius(10), Background = Brushes.Transparent };
        row.Children.Add(bg);

        var content = new Grid { Margin = new Thickness(8, 0, 4, 0) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(content);

        // Kotak centang 16 px r5; selesai: isi accent + centang.
        var box = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(item.Done ? 0 : 1.5),
            BorderBrush = B("TextSecondary"),
            Background = item.Done ? B("Accent") : Brushes.Transparent,
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(),
        };
        if (item.Done) box.Child = Check();
        Grid.SetColumn(box, 0);
        content.Children.Add(box);

        var text = new TextBlock
        {
            Text = item.Text,
            Style = (Style)FindResource("Celah.Text.Body"),
            Foreground = item.Done ? B("TextTertiary") : B("TextPrimary"),
            TextDecorations = item.Done ? TextDecorations.Strikethrough : null,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 8, 0),
        };
        Grid.SetColumn(text, 1);
        content.Children.Add(text);

        var delete = new Button
        {
            Style = (Style)FindResource("Celah.Button.Icon"),
            Tag = FindResource("Celah.Icon.Close"),
            Width = 24,
            Height = 24,
            Opacity = 0,
            ToolTip = "Hapus tugas",
        };
        delete.Click += (_, _) => _list?.Remove(item.Id);
        Grid.SetColumn(delete, 2);
        content.Children.Add(delete);

        // Hover: latar baris dan tombol hapus muncul (140 ms).
        row.MouseEnter += (_, _) =>
        {
            bg.Background = B("SurfaceRaised");
            delete.Animate(OpacityProperty, Motion.To(1, Motion.Fast, Motion.Out));
        };
        row.MouseLeave += (_, _) =>
        {
            bg.Background = Brushes.Transparent;
            delete.Animate(OpacityProperty, Motion.To(0, Motion.Fast, Motion.Out));
        };

        // Klik baris/kotak: centang. Kotak membesar 0.8 → 1 (ease-spring 220 ms), teks memudar dan dicoret.
        void Toggle()
        {
            if (_list is null) return;
            bool nowDone = !item.Done;
            box.BorderThickness = new Thickness(nowDone ? 0 : 1.5);
            box.Background = nowDone ? B("Accent") : Brushes.Transparent;
            box.Child = nowDone ? Check() : null;
            text.Foreground = nowDone ? B("TextTertiary") : B("TextPrimary");
            text.TextDecorations = nowDone ? TextDecorations.Strikethrough : null;
            if (!Motion.Reduced)
            {
                var pop = Motion.To(1, Motion.Base, Motion.Spring);
                pop.From = 0.8;
                ((ScaleTransform)box.RenderTransform).Animate(ScaleTransform.ScaleXProperty, pop);
                ((ScaleTransform)box.RenderTransform).Animate(ScaleTransform.ScaleYProperty, pop);
            }
            _sinkDelay.Stop();
            _sinkDelay.Start();
            _list.Toggle(item.Id);
        }
        row.MouseLeftButtonUp += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject src && IsInside(src, delete)) return;
            Toggle();
            e.Handled = true;
        };

        return row;
    }

    private static Path Check() => new()
    {
        Data = (Geometry)Application.Current.Resources["Celah.Icon.Check"],
        Stroke = (Brush)Application.Current.Resources["Celah.Brush.AccentInk"],
        StrokeThickness = 2.6,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        Width = 24,
        Height = 24,
        LayoutTransform = new ScaleTransform(0.5, 0.5),
    };

    private static bool IsInside(DependencyObject node, DependencyObject container)
    {
        for (var n = node; n is not null; n = VisualTreeHelper.GetParent(n))
        {
            if (n == container) return true;
        }
        return false;
    }

    // ── Input ───────────────────────────────────────────────────────────────

    private void OnAddClick(object sender, RoutedEventArgs e) => BeginInput();

    public void BeginInput()
    {
        if (IsEditing)
        {
            Input.Focus();
            return;
        }
        InputRow.Visibility = Visibility.Visible;
        EmptyView.Visibility = Visibility.Collapsed;
        Input.Text = "";
        TextInputChanged?.Invoke(true);
        // Fokus setelah jendela diaktifkan oleh pemiliknya.
        Dispatcher.BeginInvoke(() => { Input.Focus(); Keyboard.Focus(Input); }, DispatcherPriority.Input);
    }

    public void EndInput()
    {
        if (!IsEditing) return;
        InputRow.Visibility = Visibility.Collapsed;
        Input.Text = "";
        TextInputChanged?.Invoke(false);
        Rebuild();
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _list?.Add(Input.Text);
            Input.Text = "";
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            EndInput();
            e.Handled = true;
        }
    }

    private void OnInputLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Klik di luar (jendela lain) mengakhiri input; teks yang sudah diketik disimpan.
        if (Input.Text.Trim().Length > 0) _list?.Add(Input.Text);
        EndInput();
    }

    private void OnInputTextChanged(object sender, TextChangedEventArgs e) =>
        Placeholder.Visibility = Input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Celah.Brush.{token}"];
}
