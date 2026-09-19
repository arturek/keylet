namespace Keylet.Events;

public sealed record KeyletEvent(
    long Sequence,
    DateTimeOffset Timestamp,
    string Type,
    string Summary,
    IReadOnlyDictionary<string, string?> Properties);

