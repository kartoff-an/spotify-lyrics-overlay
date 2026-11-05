using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SpotifyLyricsOverlay.Models;

namespace SpotifyLyricsOverlay.Services;

public class LyricsService
{
    private const string BaseUrl = "https://lrclib.net/api/get";
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly Regex TimestampRegex = new(
        @"\[(?<minutes>\d{1,3}):(?<seconds>\d{2}(?:\.\d+)?)\]",
        RegexOptions.Compiled
    );

    private class LyricsResponse
    {
        [JsonPropertyName("syncedLyrics")]
        public string? SyncedLyrics { get; set; }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SpotifyLyricsOverlay/1.0");
        return client;
    }

    private static List<LyricLine> ParseLrc(string lrc)
    {
        var lyrics = new List<LyricLine>();

        foreach (string rawLine in lrc.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            MatchCollection matches = TimestampRegex.Matches(line);
            if (matches.Count == 0)
                continue;

            string text = TimestampRegex.Replace(line, "").Trim();

            foreach (Match match in matches)
            {
                if (!double.TryParse(
                        match.Groups["minutes"].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out double minutes
                    ))
                {
                    continue;
                }

                if (!double.TryParse(
                        match.Groups["seconds"].Value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double seconds
                    ))
                {
                    continue;
                }

                if (seconds >= 60)
                    continue;

                TimeSpan timestamp = TimeSpan.FromSeconds((minutes * 60) + seconds);
                lyrics.Add(new LyricLine 
                { 
                    Timestamp = timestamp,
                    Text = text }
                );
            }
        }

        return lyrics.OrderBy(line => line.Timestamp).ToList();
    }

    public async Task<List<LyricLine>?> GetSyncedLyricsAsync(Track track, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(track.Title) || string.IsNullOrWhiteSpace(track.Artist) || track.Duration <= TimeSpan.Zero)
        {
            return null;
        }

        int duration = (int)Math.Round(track.Duration.TotalSeconds);
        string url =
            $"{BaseUrl}?" +
            $"track_name={Uri.EscapeDataString(track.Title)}&" +
            $"artist_name={Uri.EscapeDataString(track.Artist)}&" +
            $"album_name={Uri.EscapeDataString(track.Album ?? string.Empty)}&" +
            $"duration={duration}";

        using var response = await HttpClient.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LyricsResponse>(cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(result?.SyncedLyrics))
            return null;

        return ParseLrc(result.SyncedLyrics);
    }
}