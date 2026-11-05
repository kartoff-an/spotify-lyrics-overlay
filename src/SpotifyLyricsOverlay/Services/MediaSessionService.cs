using Windows.Media.Control;
using SpotifyLyricsOverlay.Models;

namespace SpotifyLyricsOverlay.Services;

public class MediaSessionService
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;

    public async Task InitializeAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
    }

    public async Task<Track?> GetCurrentTrackAsync()
    {
        if (_manager is null)
            return null;

        var session = _manager.GetCurrentSession();

        if (session is null)
            return null;

        var mediaProperties = await session.TryGetMediaPropertiesAsync();
        var playbackInfo = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();

        if (mediaProperties is null || playbackInfo is null)
            return null;

        bool isPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        TimeSpan position = timeline.Position;

        if (isPlaying)
        {
            TimeSpan elapsed = DateTimeOffset.UtcNow - timeline.LastUpdatedTime;

            if (elapsed > TimeSpan.Zero && elapsed < TimeSpan.FromSeconds(10))
            {
                position += elapsed;
            }
        }

        TimeSpan duration = timeline.EndTime - timeline.StartTime;

        if (duration < TimeSpan.Zero)
            duration = TimeSpan.Zero;

        if (position < TimeSpan.Zero)
            position = TimeSpan.Zero;

        if (duration > TimeSpan.Zero && position > duration)
            position = duration;

        return new Track
        {
            Title = mediaProperties.Title ?? string.Empty,
            Artist = mediaProperties.Artist ?? string.Empty,
            Album = mediaProperties.AlbumTitle ?? string.Empty,
            SourceApp = session.SourceAppUserModelId,
            Duration = duration,
            Position = position,
            IsPlaying = isPlaying
        };
    }
}