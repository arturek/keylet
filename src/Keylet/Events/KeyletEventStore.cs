using System.Collections.Concurrent;
using System.Diagnostics;
using Keylet.Configuration;
using Microsoft.Extensions.Options;

namespace Keylet.Events;

public interface IKeyletEventStore
{
    KeyletEvent Record(string type, string summary, IReadOnlyDictionary<string, string?>? properties = null);

    IReadOnlyList<KeyletEvent> GetAll();
}

internal sealed partial class KeyletEventStore(
    IOptions<KeyletOptions> options,
    ILogger<KeyletEventStore> logger) : IKeyletEventStore
{
    public const string ActivitySourceName = "Keylet.Events";
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private readonly ConcurrentQueue<KeyletEvent> events = new();
    private long sequence;

    public KeyletEvent Record(
        string type,
        string summary,
        IReadOnlyDictionary<string, string?>? properties = null)
    {
        var safeProperties = properties ?? new Dictionary<string, string?>();
        var entry = new KeyletEvent(
            Interlocked.Increment(ref sequence),
            DateTimeOffset.UtcNow,
            type,
            summary,
            safeProperties);

        events.Enqueue(entry);
        while (events.Count > options.Value.EventCapacity)
        {
            events.TryDequeue(out _);
        }

        using var activity = ActivitySource.StartActivity(type, ActivityKind.Internal);
        activity?.SetTag("keylet.event.sequence", entry.Sequence);
        activity?.SetTag("keylet.event.type", type);
        foreach (var property in safeProperties)
        {
            activity?.SetTag($"keylet.{property.Key}", property.Value);
        }

        LogEvent(logger, entry.Sequence, type, summary, safeProperties);
        return entry;
    }

    public IReadOnlyList<KeyletEvent> GetAll() => events.Reverse().ToArray();

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Keylet event {Sequence}: {EventType} - {Summary}; properties={Properties}")]
    private static partial void LogEvent(
        ILogger logger,
        long sequence,
        string eventType,
        string summary,
        IReadOnlyDictionary<string, string?> properties);
}
