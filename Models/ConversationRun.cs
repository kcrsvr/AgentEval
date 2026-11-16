namespace AgentEval.Models;

public sealed class ConversationRun
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string AgentId { get; init; } = "rogers-roaming";
    public string AgentName { get; init; } = "Roaming Support Agent";
    public string ScenarioName { get; init; } = string.Empty;
    public string Objective { get; init; } = string.Empty;
    public string GroundTruth { get; init; } = string.Empty;
    public int MaxTurnsPerConversation { get; init; } = 12;
    public string Status { get; set; } = "Not started";
    public string? ErrorMessage { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public List<GeneratedConversation> Conversations { get; init; } = [];
}

public sealed class GeneratedConversation
{
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Status { get; set; } = "Not started";
    public string? ErrorMessage { get; set; }
    public AnswerEvaluation? Evaluation { get; set; }
    public List<ConversationTurn> Turns { get; init; } = [];
}

public sealed record AnswerEvaluation(int Score, string Reason)
{
    public const int SuccessThreshold = 80;
    public bool IsSuccess => Score >= SuccessThreshold;
}

public sealed class ConversationTurn
{
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public bool IsPending { get; init; }
}
