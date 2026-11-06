using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SpotifyLyricsOverlay.Models;
using SpotifyLyricsOverlay.Services;

namespace SpotifyLyricsOverlay;

public partial class MainWindow : Window
{
    private const int WM_NCHITTEST = 0x0084;

    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;

    private const int RESIZE_BORDER = 8;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

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

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        HwndSource source = HwndSource.FromHwnd(handle);
        source.AddHook(WindowProc);
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_NCHITTEST)
            return IntPtr.Zero;

        if (!GetWindowRect(hwnd, out RECT rect))
            return IntPtr.Zero;

        long position = lParam.ToInt64();

        int x = unchecked((short)(position & 0xFFFF));
        int y = unchecked((short)((position >> 16) & 0xFFFF));

        bool left = x >= rect.Left &&
                x < rect.Left + RESIZE_BORDER;

        bool right = x <= rect.Right &&
                     x > rect.Right - RESIZE_BORDER;

        bool top = y >= rect.Top &&
                   y < rect.Top + RESIZE_BORDER;

        bool bottom = y <= rect.Bottom &&
                      y > rect.Bottom - RESIZE_BORDER;

        int hitTest = 0;

        if (top && left)
            hitTest = HTTOPLEFT;
        else if (top && right)
            hitTest = HTTOPRIGHT;
        else if (bottom && left)
            hitTest = HTBOTTOMLEFT;
        else if (bottom && right)
            hitTest = HTBOTTOMRIGHT;
        else if (left)
            hitTest = HTLEFT;
        else if (right)
            hitTest = HTRIGHT;
        else if (top)
            hitTest = HTTOP;
        else if (bottom)
            hitTest = HTBOTTOM;

        if (hitTest != 0)
        {
            handled = true;
            return new IntPtr(hitTest);
        }

        return IntPtr.Zero;
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

        AnimateLyric(PreviousLyric, 5, 250);
        AnimateLyric(CurrentLyric, 14, 350);
        AnimateLyric(NextLyric, 5, 250);
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

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        BeginAnimation(
            Window.OpacityProperty,
            new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            }
        );
    }

    private void AnimateLyric(TextBlock textBlock, double distance, int duration)
    {
        textBlock.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(duration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            }
        );

        if (textBlock.RenderTransform is TranslateTransform transform)
        {
            transform.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation
                {
                    From = distance,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(duration),
                    EasingFunction = new CubicEase {  EasingMode = EasingMode.EaseOut }
                }
            );
        }
    }
}