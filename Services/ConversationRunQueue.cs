using System.Threading.Channels;
using AgentEval.Models;

namespace AgentEval.Services;

public sealed class ConversationRunQueue : BackgroundService
{
    private readonly Channel<RunRequest> _runRequests = Channel.CreateUnbounded<RunRequest>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });
    private readonly ConversationRunStore _runStore;
    private readonly ICustomerTurnGenerator _customerTurnGenerator;
    private readonly IRoamingAgentSimulator _roamingAgentSimulator;
    private readonly IAnswerEvaluator _answerEvaluator;
    private readonly ILogger<ConversationRunQueue> _logger;

    public ConversationRunQueue(
        ConversationRunStore runStore,
        ICustomerTurnGenerator customerTurnGenerator,
        IRoamingAgentSimulator roamingAgentSimulator,
        IAnswerEvaluator answerEvaluator,
        ILogger<ConversationRunQueue> logger)
    {
        _runStore = runStore;
        _customerTurnGenerator = customerTurnGenerator;
        _roamingAgentSimulator = roamingAgentSimulator;
        _answerEvaluator = answerEvaluator;
        _logger = logger;
    }

    public ValueTask QueueRunAsync(Guid runId, int? conversationNumber = null, CancellationToken cancellationToken = default) =>
        _runRequests.Writer.WriteAsync(new RunRequest(runId, conversationNumber), cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _runRequests.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessRunAsync(request, stoppingToken);
        }
    }

    private async Task ProcessRunAsync(RunRequest request, CancellationToken cancellationToken)
    {
        var runId = request.RunId;
        var run = _runStore.Get(runId);
        if (run is null) return;

        var conversations = run.Conversations
            .Where(item => !request.ConversationNumber.HasValue || item.Number == request.ConversationNumber.Value)
            .Where(item => item.Status == "Queued")
            .ToList();
        if (conversations.Count == 0) return;

        _runStore.SetRunStatus(runId, "Running");
        try
        {
            foreach (var conversation in conversations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _runStore.ResetConversation(runId, conversation.Number);
                _runStore.SetConversationStatus(runId, conversation.Number, "Running");
                var transcript = new List<ConversationTurn>();
                var finalStatus = "TurnLimitReached";
                string? statusMessage = $"Stopped after {run.MaxTurnsPerConversation} back-and-forth turns.";

                for (var exchange = 1; exchange <= run.MaxTurnsPerConversation; exchange++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var customerTurn = await _customerTurnGenerator.GenerateNextTurnAsync(
                        new CustomerTurnRequest(
                            run.ScenarioName,
                            run.Objective,
                            transcript.Select(turn => (turn.Role, turn.Content)).ToList(),
                            exchange,
                            run.MaxTurnsPerConversation),
                        cancellationToken);

                    var customerMessage = customerTurn.Message.Trim();
                    if (customerMessage.Length == 0)
                    {
                        throw new InvalidOperationException("The customer simulator returned an empty message.");
                    }

                    var customerTurnRecord = new ConversationTurn { Role = "Customer", Content = customerMessage };
                    transcript.Add(customerTurnRecord);
                    _runStore.AddTurn(runId, conversation.Number, customerTurnRecord.Role, customerTurnRecord.Content);

                    var agentReply = await _roamingAgentSimulator.RespondAsync(
                        new RoamingAgentRequest(run.ScenarioName, run.Objective, transcript),
                        cancellationToken);
                    if (string.IsNullOrWhiteSpace(agentReply.Message))
                        throw new InvalidOperationException("The agent returned an empty response.");
                    var agentTurnRecord = new ConversationTurn { Role = "Roaming agent", Content = agentReply.Message };
                    transcript.Add(agentTurnRecord);
                    _runStore.AddTurn(runId, conversation.Number, agentTurnRecord.Role, agentTurnRecord.Content);

                    if (agentReply.IsFinal || customerTurn.IsComplete)
                    {
                        finalStatus = agentReply.Outcome == "out_of_scope" ? "OutOfScope" : "Completed";
                        statusMessage = null;
                        break;
                    }
                }

                var finalResponse = transcript.Last(turn => turn.Role == "Roaming agent").Content;
                var evaluation = await _answerEvaluator.EvaluateAsync(run.GroundTruth, finalResponse, cancellationToken);
                _runStore.SetEvaluation(runId, conversation.Number, evaluation);
                _runStore.SetConversationStatus(runId, conversation.Number, finalStatus, statusMessage);
            }

            UpdateAggregateRunStatus(runId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            foreach (var conversation in GetActiveConversations(runId, conversations))
            {
                _runStore.SetConversationStatus(runId, conversation.Number, "Failed", "The application stopped before this conversation completed.");
            }
            UpdateAggregateRunStatus(runId, "The application stopped before this run completed.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Conversation run {RunId} failed", runId);
            foreach (var conversation in GetActiveConversations(runId, conversations))
            {
                _runStore.SetConversationStatus(runId, conversation.Number, "Failed", exception.Message);
            }
            UpdateAggregateRunStatus(runId, exception.Message);
        }
    }

    private void UpdateAggregateRunStatus(Guid runId, string? errorMessage = null)
    {
        var finalRun = _runStore.Get(runId);
        if (finalRun is null) return;
        var sessions = finalRun.Conversations;
        if (sessions.Any(item => item.Status is "Queued" or "Running"))
        {
            _runStore.SetRunStatus(runId, "Queued", errorMessage);
        }
        else if (sessions.Any(item => item.Status == "Failed"))
        {
            _runStore.SetRunStatus(runId, "Failed", errorMessage);
        }
        else if (sessions.Any(item => item.Status == "Not started"))
        {
            _runStore.SetRunStatus(runId, "Partially completed", errorMessage);
        }
        else
        {
            _runStore.SetRunStatus(runId, "Completed", errorMessage);
        }
    }

    private IReadOnlyList<GeneratedConversation> GetActiveConversations(Guid runId, IReadOnlyList<GeneratedConversation> selected)
    {
        var selectedNumbers = selected.Select(item => item.Number).ToHashSet();
        return (_runStore.Get(runId)?.Conversations ?? [])
            .Where(item => selectedNumbers.Contains(item.Number) && (item.Status is "Queued" or "Running"))
            .ToList();
    }

    private sealed record RunRequest(Guid RunId, int? ConversationNumber);
}
