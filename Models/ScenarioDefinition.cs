using System.Text.Json.Serialization;

namespace AgentEval.Models;

public sealed class ScenarioDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string AgentId { get; init; } = "rogers-roaming";
    public string Name { get; init; } = string.Empty;
    public string Objective { get; init; } = string.Empty;
    public string GroundTruth { get; init; } = string.Empty;
    public int ConversationCount { get; init; }
    public int MaxTurnsPerConversation { get; init; } = 12;
    [JsonPropertyName("GroundTruthJson")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyGroundTruthJson { get; set; }
    [JsonPropertyName("CustomerFacts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyCustomerFacts { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
