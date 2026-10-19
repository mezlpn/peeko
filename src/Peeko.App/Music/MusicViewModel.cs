using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Peeko.App.Music;

/// <summary>Data tampilan untuk pil musik (collapsed) dan modul Musik (expanded).</summary>
public sealed partial class MusicViewModel : ObservableObject
{
    public MusicViewModel(Func<Task> playPause, Func<Task> next, Func<Task> previous, Func<int, Task> switchSession)
    {
        PlayPauseCommand = new AsyncRelayCommand(playPause, () => CanPlayPause);
        NextCommand = new AsyncRelayCommand(next, () => CanNext);
        PreviousCommand = new AsyncRelayCommand(previous, () => CanPrevious);
        PreviousSessionCommand = new AsyncRelayCommand(() => switchSession(-1));
        NextSessionCommand = new AsyncRelayCommand(() => switchSession(1));
    }

    public IAsyncRelayCommand PlayPauseCommand { get; }
    public IAsyncRelayCommand NextCommand { get; }
    public IAsyncRelayCommand PreviousCommand { get; }

    /// <summary>Pindah sumber suara (mis. Brave ↔ Chrome) saat ada lebih dari satu sesi.</summary>
    public IAsyncRelayCommand PreviousSessionCommand { get; }
    public IAsyncRelayCommand NextSessionCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMultipleSessions), nameof(SessionLabel))]
    private int _sessionCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SessionLabel))]
    private int _sessionIndex;

    public bool HasMultipleSessions => SessionCount > 1;

    public string SessionLabel => $"{SessionIndex + 1}/{SessionCount}";

    [ObservableProperty] private bool _hasSession;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _artist = "";
    [ObservableProperty] private string _sourceName = "";
    [ObservableProperty] private Brush _sourceBrush = Brushes.Gray;
    [ObservableProperty] private ImageSource? _cover;
    [ObservableProperty] private Brush _accentBrush = Brushes.White;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _canSeek;
    [ObservableProperty] private bool _hasTimeline;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _positionText = "0:00";
    [ObservableProperty] private string _durationText = "0:00";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    private bool _canPlayPause;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private bool _canNext;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousCommand))]
    private bool _canPrevious;

    public bool HasCover => Cover is not null;

    partial void OnCoverChanged(ImageSource? value) => OnPropertyChanged(nameof(HasCover));

    public static string FormatTime(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
}
