using System.Text.Json.Nodes;

public record ClientCommand(
    string Name,
    Func<CancellationToken, Task<JsonNode?>> Action
);
