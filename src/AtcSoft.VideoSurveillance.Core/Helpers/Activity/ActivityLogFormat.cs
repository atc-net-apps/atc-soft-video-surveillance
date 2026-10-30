namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// The lines of an activity index file (JSON Lines). The first line is the header,
/// <c>{"v":1,"start":"2026-10-19T10:15:00.000Z"}</c>; every further line is one analysed second,
/// <c>{"t":12,"a":3.4,"n":1,"ids":[4],"z":["Gate"]}</c> (offset, level, tracks, track ids, the watch
/// zones a track was in; the ids and zones are left out when there are none). A reader skips what it
/// does not know, so the zones, added later, did not need a new version.
/// </summary>
internal static class ActivityLogFormat
{
    public const int Version = 1;

    public static string Header(DateTimeOffset start)
        => Write(w =>
        {
            w.WriteNumber("v", Version);
            w.WriteString("start", start.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
        });

    public static string Line(ActivitySecond second)
        => Write(w =>
        {
            w.WriteNumber("t", second.Offset);
            w.WriteNumber("a", Math.Round(second.Level, 1));
            w.WriteNumber("n", second.Tracks);
            if (second.TrackIds.Count > 0)
            {
                w.WriteStartArray("ids");
                foreach (var id in second.TrackIds)
                {
                    w.WriteNumberValue(id);
                }

                w.WriteEndArray();
            }

            if (second.Zones.Count > 0)
            {
                w.WriteStartArray("z");
                foreach (var zone in second.Zones)
                {
                    w.WriteStringValue(zone);
                }

                w.WriteEndArray();
            }
        });

    public static bool TryReadHeader(
        string line,
        out DateTimeOffset start)
    {
        start = default;
        return TryParse(line, out var root) &&
               root.TryGetProperty("v", out var version) &&
               version.ValueKind == JsonValueKind.Number &&
               root.TryGetProperty("start", out var value) &&
               value.ValueKind == JsonValueKind.String &&
               DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out start);
    }

    // A line per second, thousands per hour: read with the forward-only reader, not a document per
    // line, so a timeline over days stays quick to build.
    public static bool TryReadSecond(
        string line,
        [NotNullWhen(true)] out ActivitySecond? second)
    {
        second = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var bytes = System.Buffers.ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(line.Length));
        try
        {
            var length = Encoding.UTF8.GetBytes(line, bytes);
            return TryReadSecond(bytes.AsSpan(0, length), out second);
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    private static bool TryReadSecond(
        ReadOnlySpan<byte> json,
        [NotNullWhen(true)] out ActivitySecond? second)
    {
        second = null;
        int? offset = null;
        double level = 0;
        var tracks = 0;
        List<int>? ids = null;
        List<string>? zones = null;

        try
        {
            var reader = new Utf8JsonReader(json);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return false;
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals("t"u8))
                {
                    offset = reader.Read() && reader.TryGetInt32(out var t) ? t : null;
                }
                else if (reader.ValueTextEquals("a"u8))
                {
                    level = reader.Read() && reader.TryGetDouble(out var a) ? a : 0;
                }
                else if (reader.ValueTextEquals("n"u8))
                {
                    tracks = reader.Read() && reader.TryGetInt32(out var n) ? n : 0;
                }
                else if (reader.ValueTextEquals("ids"u8) && reader.Read() && reader.TokenType == JsonTokenType.StartArray)
                {
                    ids = [];
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TryGetInt32(out var id))
                        {
                            ids.Add(id);
                        }
                    }
                }
                else if (reader.ValueTextEquals("z"u8) && reader.Read() && reader.TokenType == JsonTokenType.StartArray)
                {
                    zones = [];
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType == JsonTokenType.String && reader.GetString() is { Length: > 0 } zone)
                        {
                            zones.Add(zone);
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }
                }
                else
                {
                    reader.Read();
                    reader.Skip();
                }
            }

            // A line cut off before its end is not a second.
            if (reader.TokenType != JsonTokenType.EndObject || offset is not >= 0)
            {
                return false;
            }
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            // A number where none was expected, in a line someone edited.
            return false;
        }

        second = new ActivitySecond(offset.Value, level, tracks, ids ?? []) { Zones = zones ?? [] };
        return true;
    }

    // A line cut off by a crash or still being written is not JSON; it is skipped.
    private static bool TryParse(
        string line,
        out JsonElement root)
    {
        root = default;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            root = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Write(Action<Utf8JsonWriter> body)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}