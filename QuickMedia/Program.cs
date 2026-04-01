using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Media.Control;

class Program
{
#pragma warning disable CS8618 
    private static GlobalSystemMediaTransportControlsSessionManager _manager;
#pragma warning restore CS8618 


    static async Task Main()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

        while (true)
        {
            string input = Console.ReadLine().Trim().ToLower();
            if (string.IsNullOrEmpty(input) || input == "exit")
                break;

            try
            {
                if (_manager == null)
                    _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                await ProcessComand(input);
            }
            catch (Exception ex)
            {
                WriteJson(new { error = ex.ToString() });
            }
        }
    }

    private static async Task ProcessComand(string command)
    {
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var session = manager.GetCurrentSession() ?? (manager.GetSessions().Count > 0 ? manager.GetSessions()[0] : null);

        if (session == null)
        {
            WriteJson(new { status = "No active media session" });
            return;
        }

        var props = await session.TryGetMediaPropertiesAsync();
        if (props == null)
        {
            WriteJson(new { error = "meta not available" });
            return;
        }

        var playback = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();
        switch (command)
        {
            case "-all":
                WriteJson(new
                {
                    Title = props.Title,
                    Artist = props.Artist,
                    Album = props.AlbumTitle,
                    AlbumArtist = props.AlbumArtist,
                    Status = playback.PlaybackStatus.ToString(),
                    PlaybackType = playback.PlaybackType.ToString(),
                    Elapsed = timeline.Position.TotalSeconds,
                    Duration = timeline.EndTime.TotalSeconds,
                    ThumbnailBase64 = await GetThumbnail(props)
                });
                break;

            case "-cover":
                string? base64 = await GetThumbnail(props);
                WriteJson(new { ThumbnailBase64 = base64 });
                break;

            case "-name":
                WriteJson(new { Title = props.Title });
                break;

            case "-artist":
                WriteJson(new { Artist = props.Artist });
                break;

            case "-album":
                WriteJson(new
                {
                    AlbumName = props.AlbumTitle,
                    AlbumArtist = props.AlbumArtist
                });
                break;

            case "-timeline":
                WriteJson(new
                {
                    Elapsed = timeline.Position.TotalSeconds,
                    Duration = timeline.EndTime.TotalSeconds
                });
                break;

            case "-skip":
                await session.TrySkipNextAsync();
                WriteJson(new { status = "Skipped" });
                break;

            case "-back":
                await session.TrySkipPreviousAsync();
                WriteJson(new { status = "Previous track" });
                break;

            case "-pause":
                await session.TryTogglePlayPauseAsync();
                WriteJson(new { status = "Toggled pause" });
                break;

            case "-seek":
                {
                    Console.WriteLine("Enter Time");
                    string? timeInput = Console.ReadLine();
                    if (long.TryParse(timeInput, out long seconds))
                    {
                        long ticks = TimeSpan.FromSeconds(seconds).Ticks;
                        await session.TryChangePlaybackPositionAsync(ticks);
                        WriteJson(new { status = "Seeked", position = seconds });
                    }
                    else
                    {
                        WriteJson(new { error = "Not a Correct input" });
                    }
                    break;
                }

            case "-sessions":
                WriteJson(new
                {
                    Count = _manager.GetSessions().Count
                });
                break;

            case "-help":
                WriteJson(new
                {
                    AvailableCommands = new[]
                    {
                        "-all",
                        "-cover",
                        "-name",
                        "-artist",
                        "-album",
                        "-timeline",
                        "-skip",
                        "-back",
                        "-pause",
                        "-sessions",
                        "-seek",
                        "exit"
                    }
                });
                break;

            default:
                WriteJson(new { error = "Unknown command" });
                break;
        }
    }

    private static GlobalSystemMediaTransportControlsSession? GetSession()
    {
        var current = _manager.GetCurrentSession();
        if (current != null)
            return current;

        var sessions = _manager.GetSessions();
        return sessions.Count > 0 ? sessions[0] : null;
    }

    private static async Task<string?> GetThumbnail(GlobalSystemMediaTransportControlsSessionMediaProperties props)
    {
        if (props?.Thumbnail == null) return null;

        try
        {
            using var stream = await props.Thumbnail.OpenReadAsync();
            using var input = stream.AsStreamForRead();
            using var ms = new MemoryStream();

            await input.CopyToAsync(ms);
            return Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return null;
        }
    }

    private static void WriteJson(object data)
    {
        Console.WriteLine(JsonSerializer.Serialize(data));
        Console.Out.Flush();
    }
}
