using System.Collections.Concurrent;
using System.Diagnostics;

namespace Keylet.TestClient.Events;

public interface IClientEventStore
{
    ClientEvent Record(string type, string summary, IReadOnlyDictionary<string, string?>? properties = null);

    IReadOnlyList<ClientEvent> GetAll();
}

internal sealed partial class ClientEventStore(ILogger<ClientEventStore> logger) : IClientEventStore
{
    public const string ActivitySourceName = "Keylet.TestClient.Events";
    private const int Capacity = 200;
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private readonly ConcurrentQueue<ClientEvent> events = new();
    private long sequence;

    public ClientEvent Record(
        string type,
        string summary,
        IReadOnlyDictionary<string, string?>? properties = null)
    {
        var safeProperties = properties ?? new Dictionary<string, string?>();
        var entry = new ClientEvent(
            Interlocked.Increment(ref sequence),
            DateTimeOffset.UtcNow,
            type,
            summary,
            safeProperties);

        events.Enqueue(entry);
        while (events.Count > Capacity)
        {
            events.TryDequeue(out _);
        }

        using var activity = ActivitySource.StartActivity(type, ActivityKind.Internal);
        activity?.SetTag("keylet.client.event.sequence", entry.Sequence);
        activity?.SetTag("keylet.client.event.type", type);
        foreach (var property in safeProperties)
        {
            activity?.SetTag($"keylet.client.{property.Key}", property.Value);
        }

        LogEvent(logger, entry.Sequence, type, summary, safeProperties);
        return entry;
    }

    public IReadOnlyList<ClientEvent> GetAll() => events.Reverse().ToArray();

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Keylet test client event {Sequence}: {EventType} - {Summary}; properties={Properties}")]
    private static partial void LogEvent(
        ILogger logger,
        long sequence,
        string eventType,
        string summary,
        IReadOnlyDictionary<string, string?> properties);
}

