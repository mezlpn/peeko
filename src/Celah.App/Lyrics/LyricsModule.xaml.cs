using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Celah.App.Animation;
using Celah.Core.Lyrics;

namespace Celah.App.Lyrics;

/// <summary>
/// Jendela lirik tersinkron. Semua baris memakai font 18 Semibold; baris non-aktif hanya
/// di-scale 0.86 supaya ukuran font tidak pernah berubah (Motion.dc.html → pergantian baris lirik).
/// </summary>
public partial class LyricsModule : UserControl
{
    private const double SlotHeight = 26;
    private const int ActiveSlot = 2; // slot ketiga (0-based)
    private const double DimScale = 0.86;

    private static readonly Color Primary = (Color)ColorConverter.ConvertFromString("#F4F4F1");
    private static readonly Color Tertiary = (Color)ColorConverter.ConvertFromString("#7A7A83");

    private readonly List<TextBlock> _lines = [];
    private int _active = -1;
    private bool _visible;

    public LyricsModule()
    {
        InitializeComponent();
        LinesCanvas.SizeChanged += (_, _) => UpdateLineWidths();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is INotifyPropertyChanged old) old.PropertyChanged -= OnViewModelChanged;
            if (e.NewValue is INotifyPropertyChanged vm) vm.PropertyChanged += OnViewModelChanged;
            UpdateEmptyText();
        };
    }

    /// <summary>Baris lirik diklik (indeks), untuk lompat ke timestamp-nya.</summary>
    public event Action<int>? LineClicked;

    private LyricsViewModel? ViewModel => DataContext as LyricsViewModel;

    /// <summary>Animasi (skeleton memuat) hanya berjalan saat modul terlihat.</summary>
    public void SetVisible(bool visible)
    {
        _visible = visible;
        UpdateSkeleton();
    }

    public void SetLines(SyncedLyrics? lyrics)
    {
        LinesPanel.Children.Clear();
        _lines.Clear();
        _active = -1;
        LinesShift.BeginAnimation(TranslateTransform.YProperty, null);
        LinesShift.Y = ActiveSlot * SlotHeight;
        if (lyrics is null) return;

        for (int i = 0; i < lyrics.Lines.Count; i++)
        {
            int index = i;
            var line = new TextBlock
            {
                Text = lyrics.Lines[i].Text,
                Height = SlotHeight,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                FontFamily = (FontFamily)FindResource("Celah.Font"),
                Foreground = new SolidColorBrush(Tertiary),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, 1, 0, 0),
                RenderTransformOrigin = new Point(0, 0.5),
                RenderTransform = new ScaleTransform(DimScale, DimScale),
                Opacity = 0,
                Cursor = Cursors.Hand,
            };
            line.MouseLeftButtonUp += (_, e) =>
            {
                LineClicked?.Invoke(index);
                e.Handled = true;
            };
            _lines.Add(line);
            LinesPanel.Children.Add(line);
        }
        UpdateLineWidths();
        ApplyStyles(-1, animate: false);
    }

    /// <summary>
    /// Pindah ke baris aktif: daftar bergeser satu slot (320 ms ease-standard), baris baru
    /// membesar 0.86 → 1 dan menjadi terang, tetangga redup, baris tepi opacity 0.45.
    /// </summary>
    public void SetActive(int index, bool animate)
    {
        if (_lines.Count == 0) return;
        _active = index;
        animate &= !Motion.Reduced && _visible;

        // Sebelum baris pertama: baris pertama menunggu di slot ketiga, belum menyala.
        int anchor = Math.Max(index, 0);
        double y = ActiveSlot * SlotHeight - OffsetOf(anchor);
        if (animate)
            LinesShift.Animate(TranslateTransform.YProperty, Motion.To(y, Motion.Slow, Motion.Standard));
        else
        {
            LinesShift.BeginAnimation(TranslateTransform.YProperty, null);
            LinesShift.Y = y;
        }

        ActiveMarker.Opacity = index >= 0 ? 1 : 0;
        ApplyStyles(index, animate);
    }

    /// <summary>
    /// Posisi atas baris di dalam daftar. Diambil dari layout sungguhan, bukan indeks × 26: di skala
    /// 125% layout rounding membuat tiap baris 25,6 DIP dan selisihnya menumpuk (baris ke-33 meleset 13 DIP).
    /// </summary>
    private double OffsetOf(int index)
    {
        var line = _lines[index];
        if (!line.IsArrangeValid || LinesPanel.ActualHeight <= 0)
            LinesPanel.UpdateLayout();
        return line.TranslatePoint(new Point(0, 0), LinesPanel).Y;
    }

    private void ApplyStyles(int index, bool animate)
    {
        int anchor = Math.Max(index, 0);
        for (int i = 0; i < _lines.Count; i++)
        {
            int distance = Math.Abs(i - anchor);
            bool active = index >= 0 && i == index;

            double scale = active ? 1 : DimScale;
            double opacity = active ? 1 : distance switch { <= 1 => 1, 2 => 0.45, _ => 0 };
            var color = active ? Primary : Tertiary;

            var line = _lines[i];
            var scaleT = (ScaleTransform)line.RenderTransform;
            var brush = (SolidColorBrush)line.Foreground;

            // Hanya baris di sekitar jendela yang dianimasikan; sisanya langsung di-set.
            if (animate && distance <= 3)
            {
                double delay = active ? 40 : 0;
                scaleT.Animate(ScaleTransform.ScaleXProperty, Motion.To(scale, Motion.Slow, Motion.Standard, delay));
                scaleT.Animate(ScaleTransform.ScaleYProperty, Motion.To(scale, Motion.Slow, Motion.Standard, delay));
                line.Animate(OpacityProperty, Motion.To(opacity, Motion.Slow, Motion.Standard, delay));
                var c = new ColorAnimation(color, Motion.Slow) { EasingFunction = Motion.Standard, BeginTime = Motion.Ms(delay) };
                brush.BeginAnimation(SolidColorBrush.ColorProperty, c, HandoffBehavior.SnapshotAndReplace);
            }
            else
            {
                scaleT.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scaleT.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                line.BeginAnimation(OpacityProperty, null);
                brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                scaleT.ScaleX = scaleT.ScaleY = scale;
                line.Opacity = opacity;
                brush.Color = color;
            }
        }
    }

    private void UpdateLineWidths()
    {
        // Lebar penuh untuk baris aktif; saat di-scale 0.86 teks otomatis memendek.
        double width = Math.Max(0, LinesCanvas.ActualWidth);
        foreach (var line in _lines) line.Width = width;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LyricsViewModel.State))
        {
            UpdateEmptyText();
            UpdateSkeleton();
        }
    }

    private void UpdateEmptyText()
    {
        (EmptyTitle.Text, EmptyBody.Text) = ViewModel?.State switch
        {
            LyricsViewState.NotFound => ("Lirik tidak ditemukan", "Belum ada lirik untuk lagu ini di lrclib.net."),
            LyricsViewState.Failed => ("Lirik belum bisa dimuat", "Periksa koneksi internet, lalu coba lagi."),
            LyricsViewState.Instrumental => ("Instrumental", "Lagu ini tidak punya lirik."),
            _ => ("Belum ada yang diputar", "Putar lagu dan liriknya akan muncul di sini."),
        };
    }

    /// <summary>Kerangka baris berdenyut (opacity 0.45 ↔ 1, 1,2 dtk, stagger 150 ms).</summary>
    private void UpdateSkeleton()
    {
        bool run = _visible && ViewModel?.State == LyricsViewState.Loading && !Motion.Reduced;
        int i = 0;
        foreach (var child in LoadingView.Children.OfType<Border>())
        {
            if (run)
            {
                var pulse = new DoubleAnimation(1, 0.45, TimeSpan.FromMilliseconds(600))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = Motion.Sine,
                    BeginTime = TimeSpan.FromMilliseconds(150 * i++),
                };
                Timeline.SetDesiredFrameRate(pulse, 30);
                child.BeginAnimation(OpacityProperty, pulse);
            }
            else
            {
                child.BeginAnimation(OpacityProperty, null);
            }
        }
    }
}
