using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SpotifyLyricsOverlay.Models;
using SpotifyLyricsOverlay.Services;

namespace SpotifyLyricsOverlay;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly MediaSessionService _mediaSessionService = new();
    private readonly LyricsService _lyricsService = new();

    private List<LyricLine> _lyrics = [];

    private string _currentTrackKey = string.Empty;

    private bool _isRefreshing;
    private bool _isFetchingLyrics;

    private DateTime _lastMediaRefresh = DateTime.MinValue;

    private int _currentLyricIndex = -1;

    public MainWindow()
    {
        InitializeComponent();

        _timer = new DispatcherTimer{Interval = TimeSpan.FromMilliseconds(100)};
        _timer.Tick += UpdateLyrics;
        _timer.Start();

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            TrackInfo.Text = "Connecting to media session...";

            await _mediaSessionService.InitializeAsync();
            await RefreshMediaSessionAsync();
        }
        catch (Exception ex)
        {
            TrackInfo.Text = "Media session unavailable";
            Debug.WriteLine($"Media session error: {ex}");
        }
    }

    private async void UpdateLyrics(object? sender, EventArgs e)
    {
        if (_isRefreshing)
            return;

        if (DateTime.UtcNow - _lastMediaRefresh >= TimeSpan.FromSeconds(1))
        {
            await RefreshMediaSessionAsync();
        }
    }

    private async Task RefreshMediaSessionAsync()
    {
        if (_isRefreshing)
            return;

        _isRefreshing = true;

        try
        {
            Track? track = await _mediaSessionService.GetCurrentTrackAsync();

            _lastMediaRefresh = DateTime.UtcNow;

            if (track is null)
            {
                TrackInfo.Text = "No active media session";
                PreviousLyric.Text = string.Empty;
                CurrentLyric.Text = "Waiting for playback...";
                NextLyric.Text = string.Empty;
                return;
            }

            TrackInfo.Text = $"{track.Title} | {track.Artist}";
            string trackKey = $"{track.Title}|{track.Artist}|{track.Album}";
            if (trackKey != _currentTrackKey)
            {
                _currentTrackKey = trackKey;
                _lyrics.Clear();
                _currentLyricIndex = -1;

                PreviousLyric.Text = string.Empty;
                CurrentLyric.Text = "Loading lyrics...";
                NextLyric.Text = string.Empty;

                await FetchLyricsAsync(track);
            }

            UpdateLyricDisplay(track.Position);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Media refresh error: {ex}");
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task FetchLyricsAsync(Track track)
    {
        if (_isFetchingLyrics)
            return;

        _isFetchingLyrics = true;

        string requestedTrackKey = $"{track.Title}|{track.Artist}|{track.Album}";

        try
        {
            List<LyricLine>? result = await _lyricsService.GetSyncedLyricsAsync(track);

            if (_currentTrackKey != requestedTrackKey)
                return;

            if (result is null || result.Count == 0)
            {
                CurrentLyric.Text = "Synchronized lyrics unavailable";
                return;
            }

            _lyrics = result;

            Debug.WriteLine($"Loaded {_lyrics.Count} lyric lines.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lyrics fetch error: {ex}");
            if (_currentTrackKey == requestedTrackKey)
                CurrentLyric.Text = "Unable to load lyrics";
        }
        finally
        {
            _isFetchingLyrics = false;
        }
    }

    private void UpdateLyricDisplay(TimeSpan position)
    {
        if (_lyrics.Count == 0)
            return;

        int activeIndex = -1;
        for (int i = 0; i < _lyrics.Count; i++)
        {
            if (_lyrics[i].Timestamp <= position)
                activeIndex = i;
            else
                break;
        }

        if (activeIndex == _currentLyricIndex)
            return;

        _currentLyricIndex = activeIndex;

        PreviousLyric.Text = activeIndex > 0 ? _lyrics[activeIndex - 1].Text : string.Empty;
        CurrentLyric.Text = activeIndex >= 0 ? _lyrics[activeIndex].Text : "Waiting for lyrics...";
        NextLyric.Text = activeIndex < _lyrics.Count - 1 ? _lyrics[activeIndex + 1].Text : string.Empty;
    }

    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? parent = e.OriginalSource as DependencyObject;
        while (parent is not null)
        {
            if (parent is Button)
                return;
            parent = VisualTreeHelper.GetParent(parent);
        }
        
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        Close();
    }
}