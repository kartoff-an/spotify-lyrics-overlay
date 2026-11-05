namespace SpotifyLyricsOverlay.Models;

public class Track
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string SourceApp {get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public TimeSpan Position { get; set; }
    public bool IsPlaying { get; set; }
}