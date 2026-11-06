# Spotify Lyrics Overlay

A lightweight, always-on-top Windows overlay that displays synchronized lyrics for the media currently playing on your computer.

The application reads playback information through the Windows Global System Media Transport Controls API and retrieves synchronized lyrics from [LRCLIB](https://lrclib.net/). It does not require Spotify developer credentials or a Spotify login.

## Requirements

- Windows 10 version 19041 or later, or a compatible Windows 11 installation.
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) when running a framework-dependent build. The self-contained release includes the runtime.
- An active media session with track metadata. Spotify must be running and playing a track for the normal Spotify use case.
- Internet access to retrieve lyrics from LRCLIB.

## Download and run

Download the latest `SpotifyLyricsOverlay-v*-win-x64.zip` release and extract it to a folder. Run:

```text
SpotifyLyricsOverlay.exe
```


## License

This project is distributed under the [MIT License](LICENSE.txt).

Lyrics are retrieved from LRCLIB and are subject to the availability and terms of that service. Spotify is a trademark of Spotify AB; this project is not affiliated with or endorsed by Spotify.
