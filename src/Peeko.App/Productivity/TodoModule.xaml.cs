using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Peeko.App.Animation;
using Peeko.Core.Productivity;

namespace Peeko.App.Productivity;

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
            Style = (Style)FindResource("Peeko.Text.Body"),
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
            Style = (Style)FindResource("Peeko.Button.Icon"),
            Tag = FindResource("Peeko.Icon.Close"),
            Width = 24,
            Height = 24,
            Opacity = 0,
            ToolTip = "Hapus tugas",
        };
        delete.Click += (_, _) => _list?.Remove(item.Id);
        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (item.Due is { } due && !item.Done)
        {
            bool late = _list?.IsOverdue(item) == true;
            right.Children.Add(new TextBlock
            {
                Text = (item.Repeat != TaskRepeat.None ? "↻ " : "") + DueParser.Describe(due, item.DueHasTime, DateTime.Now),
                Style = (Style)FindResource("Peeko.Text.Caption"),
                FontWeight = FontWeights.SemiBold,
                Foreground = late ? B("Danger") : due.Date == DateTime.Today ? B("Rest") : B("TextSecondary"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = item.Repeat switch
                {
                    TaskRepeat.Daily => "Berulang tiap hari",
                    TaskRepeat.Weekdays => "Berulang tiap hari kerja",
                    TaskRepeat.Weekly => $"Berulang tiap {due:dddd}",
                    TaskRepeat.Monthly => $"Berulang tiap tanggal {due.Day}",
                    _ => late ? "Sudah lewat" : null,
                },
            });
        }
        right.Children.Add(delete);
        Grid.SetColumn(right, 2);
        content.Children.Add(right);
        if (!item.Done) row.ContextMenu = BuildMenu(item);

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

    /// <summary>Klik kanan tugas: tunda, hapus tenggat, hapus.</summary>
    private ContextMenu BuildMenu(TodoItem item)
    {
        var menu = new ContextMenu { Style = (Style)FindResource("Peeko.TrayMenu") };
        void Add(string header, Action action)
        {
            var mi = new MenuItem { Header = header, Style = (Style)FindResource("Peeko.TrayItem") };
            mi.Click += (_, _) => action();
            menu.Items.Add(mi);
        }
        Add("Tunda 10 menit", () => _list?.Snooze(item.Id, TimeSpan.FromMinutes(10)));
        Add("Tunda 1 jam", () => _list?.Snooze(item.Id, TimeSpan.FromHours(1)));
        Add("Besok pagi", () => _list?.SnoozeToMorning(item.Id));
        if (item.Due is not null) Add("Hapus tenggat", () => _list?.ClearDue(item.Id));
        menu.Items.Add(new Separator { Style = (Style)FindResource("Peeko.TraySeparator") });
        Add("Hapus tugas", () => _list?.Remove(item.Id));
        // Panel jangan menutup selama menu terbuka.
        menu.Opened += (_, _) => MenuOpenChanged?.Invoke(true);
        menu.Closed += (_, _) => MenuOpenChanged?.Invoke(false);
        return menu;
    }

    /// <summary>Menu klik kanan dibuka/ditutup: pemilik menahan panel tetap terbuka.</summary>
    public event Action<bool>? MenuOpenChanged;

    private static Path Check() => new()
    {
        Data = (Geometry)Application.Current.Resources["Peeko.Icon.Check"],
        Stroke = (Brush)Application.Current.Resources["Peeko.Brush.AccentInk"],
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

    private void OnInputTextChanged(object sender, TextChangedEventArgs e)
    {
        Placeholder.Visibility = Input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var parsed = Input.Text.Trim().Length == 0 ? null : DueParser.Parse(Input.Text, DateTime.Now);
        if (parsed?.Due is { } due)
        {
            DuePreviewText.Text = (parsed.Repeat != TaskRepeat.None ? "↻ " : "") + DueParser.Describe(due, parsed.HasTime, DateTime.Now);
            DuePreview.Visibility = Visibility.Visible;
            Input.Padding = new Thickness(0, 0, DuePreview.ActualWidth > 0 ? DuePreview.ActualWidth + 8 : 90, 0);
        }
        else
        {
            DuePreview.Visibility = Visibility.Collapsed;
            Input.Padding = new Thickness(0);
        }
    }

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Peeko.Brush.{token}"];
}
