using Celah.App.Music;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Celah.App.Lyrics;

public enum LyricsViewState
{
    /// <summary>Tidak ada lagu.</summary>
    None,
    Loading,
    Synced,
    Plain,
    Instrumental,
    NotFound,
    Failed,
}

/// <summary>Data tampilan modul Lirik. Header ("sedang diputar") memakai <see cref="Music"/>.</summary>
public sealed partial class LyricsViewModel : ObservableObject
{
    public LyricsViewModel(MusicViewModel music, Func<Task> retry, Action openInBrowser)
    {
        Music = music;
        RetryCommand = new AsyncRelayCommand(retry);
        OpenInBrowserCommand = new RelayCommand(openInBrowser);
    }

    public MusicViewModel Music { get; }

    public IAsyncRelayCommand RetryCommand { get; }

    public IRelayCommand OpenInBrowserCommand { get; }

    [ObservableProperty] private LyricsViewState _state = LyricsViewState.None;
    [ObservableProperty] private string _plainText = "";
}
