using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Peeko.App.Animation;

namespace Peeko.App.Conversion;

public partial class ConvertPanel : UserControl
{
    private ConvertController? _controller;
    private bool _showAll;
    private bool _working;
    private bool _suppressed;

    public ConvertPanel()
    {
        InitializeComponent();
    }

    /// <summary>Disembunyikan sementara selama file baru diseret ke notch (zona drop tampil).</summary>
    public bool Suppressed
    {
        get => _suppressed;
        set
        {
            _suppressed = value;
            Refresh();
        }
    }

    public bool IsActive => _controller is { Stage: not ConvertStage.Idle };

    public void Attach(ConvertController controller)
    {
        _controller = controller;
        controller.Changed += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        if (_controller is not { } c) return;
        var stage = c.Stage;
        Visibility = stage == ConvertStage.Idle || _suppressed ? Visibility.Collapsed : Visibility.Visible;
        if (stage == ConvertStage.Idle) _showAll = false;

        FileText.Text = c.Files.Count > 0 ? c.FilesLabel : "";
        LoadingView.Visibility = Show(stage == ConvertStage.Loading);
        ChooseView.Visibility = Show(stage == ConvertStage.Choose);
        InstallView.Visibility = Show(stage is ConvertStage.NeedsInstall or ConvertStage.Installing);
        WorkView.Visibility = Show(stage == ConvertStage.Converting);
        ResultView.Visibility = Show(stage is ConvertStage.Done or ConvertStage.Failed);

        switch (stage)
        {
            case ConvertStage.Choose:
                BuildChips(c);
                break;
            case ConvertStage.NeedsInstall:
            case ConvertStage.Installing:
                bool installing = c.Installing;
                InstallButtons.Visibility = Show(!installing);
                InstallProgressView.Visibility = Show(installing);
                InstallText.Text = c.InstallProgress >= 1 ? "Memasang…" : $"Mengunduh Filee · {Math.Round(c.InstallProgress * 100)}%";
                InstallFill.Animate(WidthProperty, Motion.To(440 * c.InstallProgress, Motion.Fast, Motion.Standard));
                break;
            case ConvertStage.Converting:
                WorkTitle.Text = $"Mengubah ke {c.Target.ToUpperInvariant()}…";
                break;
            case ConvertStage.Done:
                ResultTitle.Text = "Selesai! Hasilnya masuk rak";
                ResultTitle.Foreground = B("Accent");
                ResultDetail.Text = c.Outputs.Count == 1 ? System.IO.Path.GetFileName(c.Outputs[0]) : $"{c.Outputs.Count} file" + (c.Message is null ? "" : $" · {c.Message}");
                ShowButton.Visibility = Visibility.Visible;
                EnginesButton.Visibility = Visibility.Collapsed;
                RetryButton.Visibility = Visibility.Collapsed;
                break;
            case ConvertStage.Failed:
                ResultTitle.Text = "Belum bisa diubah";
                ResultTitle.Foreground = B("Danger");
                ResultDetail.Text = c.Message ?? "";
                ShowButton.Visibility = Visibility.Collapsed;
                EnginesButton.Visibility = Show(c.CanOpenEngines);
                RetryButton.Visibility = Show(c.Files.Count > 0 && !c.CanOpenEngines && c.All.Count > 0);
                RetryButton.Content = "Pilih format lain";
                break;
        }
        SetWorking(stage == ConvertStage.Converting);
    }

    private void BuildChips(ConvertController c)
    {
        CommonChips.Children.Clear();
        foreach (var target in c.Common) CommonChips.Children.Add(Chip(target, large: true));

        int more = c.All.Count - c.Common.Count;
        if (more > 0)
        {
            var toggle = new Button
            {
                Style = (Style)FindResource("Peeko.Button.Text"),
                Content = _showAll ? "Sembunyikan" : $"Lainnya ({more})",
                Height = 30,
                Margin = new Thickness(0, 0, 8, 8),
            };
            toggle.Click += (_, _) =>
            {
                _showAll = !_showAll;
                BuildChips(c);
            };
            CommonChips.Children.Add(toggle);
        }

        // Mis. PNG → MP4: baru bisa setelah FFmpeg dipasang lewat Filee.
        bool engines = !_showAll && c.NeedsEngine.Count > 0 && c.Filee.AppPath is not null;
        EngineHint.Visibility = Show(engines);
        if (engines) EngineHint.Content = $"+{c.NeedsEngine.Count} format lagi dengan mesin tambahan Filee";

        AllScroll.Visibility = Show(_showAll);
        // Tanpa daftar lengkap, chip berada di tengah area; dengan daftar lengkap, chip naik ke atas.
        ChooseView.RowDefinitions[0].Height = _showAll ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        ChooseView.RowDefinitions[1].Height = _showAll ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        CommonView.Margin = new Thickness(4, _showAll ? 4 : 0, 4, 0);
        AllChips.Children.Clear();
        if (_showAll)
        {
            foreach (var target in c.All.Except(c.Common)) AllChips.Children.Add(Chip(target, large: false));
        }
    }

    private Button Chip(string target, bool large)
    {
        var chip = new Button
        {
            Style = (Style)FindResource("Peeko.Button.PillSecondary"),
            Content = target.ToUpperInvariant(),
            Height = large ? 30 : 26,
            MinWidth = large ? 64 : 0,
            Padding = new Thickness(large ? 14 : 10, 0, large ? 14 : 10, 0),
            Margin = new Thickness(0, 0, large ? 8 : 6, large ? 8 : 6),
            ToolTip = $"Ubah ke {target.ToUpperInvariant()}",
        };
        chip.Click += (_, _) => _controller?.Choose(target);
        return chip;
    }

    /// <summary>Bar berjalan selama Filee bekerja (progres per file tidak dilaporkan Filee).</summary>
    private void SetWorking(bool working)
    {
        if (working == _working) return;
        _working = working;
        if (!working || Motion.Reduced)
        {
            WorkShift.BeginAnimation(TranslateTransform.XProperty, null);
            return;
        }
        var sweep = new DoubleAnimation(-WorkBar.Width, WorkTrack.Width, TimeSpan.FromMilliseconds(1100))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = Motion.Sine,
        };
        Timeline.SetDesiredFrameRate(sweep, 30);
        WorkShift.BeginAnimation(TranslateTransform.XProperty, sweep);
    }

    private void OnClose(object sender, RoutedEventArgs e) => _controller?.Close();

    private void OnInstall(object sender, RoutedEventArgs e) => _ = _controller?.InstallAsync();

    private void OnOpenEngines(object sender, RoutedEventArgs e) => _controller?.OpenEngines();

    private void OnRetry(object sender, RoutedEventArgs e)
    {
        if (_controller is { Files.Count: > 0 } c) c.Begin(c.Files);
    }

    private void OnShowInFolder(object sender, RoutedEventArgs e)
    {
        if (_controller?.Outputs.FirstOrDefault() is not { } file) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Peeko.Brush.{token}"];
}
