namespace AgentEval.Services;

public sealed record CustomerTurnRequest(
    string ScenarioName,
    string Objective,
    IReadOnlyList<(string Role, string Content)> Transcript,
    int ExchangeNumber,
    int MaxExchanges);

public sealed record CustomerTurnResult(string Message, bool IsComplete, string? CompletionReason);

public interface ICustomerTurnGenerator
{
    Task<CustomerTurnResult> GenerateNextTurnAsync(CustomerTurnRequest request, CancellationToken cancellationToken);
}
