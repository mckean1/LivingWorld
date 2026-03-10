using System.Text.Json;
using System.Text.Json.Serialization;
using LivingWorld.Core;

namespace LivingWorld.Presentation;

public sealed class HistoryJsonlWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly JsonSerializerOptions _jsonOptions;

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
            AutoFlush = true
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public void Write(WorldEvent worldEvent)
    {
        string json = JsonSerializer.Serialize(worldEvent, _jsonOptions);
        _writer.WriteLine(json);
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
