namespace Keylet.TestClient.Events;

public sealed record ClientEvent(
    long Sequence,
    DateTimeOffset Timestamp,
    string Type,
    string Summary,
    IReadOnlyDictionary<string, string?> Properties);

