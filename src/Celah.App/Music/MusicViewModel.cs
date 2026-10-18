using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Celah.App.Music;

/// <summary>Data tampilan untuk pil musik (collapsed) dan modul Musik (expanded).</summary>
public sealed partial class MusicViewModel : ObservableObject
{
    public MusicViewModel(Func<Task> playPause, Func<Task> next, Func<Task> previous)
    {
        PlayPauseCommand = new AsyncRelayCommand(playPause, () => CanPlayPause);
        NextCommand = new AsyncRelayCommand(next, () => CanNext);
        PreviousCommand = new AsyncRelayCommand(previous, () => CanPrevious);
    }

    public IAsyncRelayCommand PlayPauseCommand { get; }
    public IAsyncRelayCommand NextCommand { get; }
    public IAsyncRelayCommand PreviousCommand { get; }

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
