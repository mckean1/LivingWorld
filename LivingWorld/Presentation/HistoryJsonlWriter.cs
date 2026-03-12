using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using LivingWorld.Core;

namespace LivingWorld.Presentation;

public sealed class HistoryJsonlWriter : IDisposable
{
    private const int FlushIntervalEvents = 64;
    private readonly StreamWriter _writer;
    private readonly JsonSerializerOptions _jsonOptions;
    private int _pendingFlushEvents;
    private long _totalWriteTicks;

    public int WrittenEventCount { get; private set; }
    public TimeSpan TotalWriteTime => TimeSpan.FromTicks(_totalWriteTicks);

    public HistoryJsonlWriter(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(
            new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = false
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public void Write(WorldEvent worldEvent)
    {
        long startedAt = Stopwatch.GetTimestamp();
        string json = JsonSerializer.Serialize(worldEvent, _jsonOptions);
        _writer.WriteLine(json);
        _totalWriteTicks += Stopwatch.GetElapsedTime(startedAt).Ticks;
        WrittenEventCount++;
        _pendingFlushEvents++;
        if (_pendingFlushEvents >= FlushIntervalEvents || worldEvent.Severity >= WorldEventSeverity.Major)
        {
            Flush();
        }
    }

    public void Dispose()
    {
        Flush();
        _writer.Dispose();
    }

    private void Flush()
    {
        _writer.Flush();
        _pendingFlushEvents = 0;
    }
}
