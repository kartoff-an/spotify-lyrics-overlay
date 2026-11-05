namespace SpotifyLyricsOverlay.Models;

public class LyricLine
{
    public TimeSpan Timestamp { get; set; }
    public string Text { get; set; } = string.Empty;
}