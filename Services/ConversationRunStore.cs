using System.Collections.Concurrent;
using System.Text.Json;
using AgentEval.Models;
using Microsoft.AspNetCore.Hosting;

namespace AgentEval.Services;

public sealed class ConversationRunStore
{
    private readonly ConcurrentDictionary<Guid, ConversationRun> _runs = new();
    private readonly ConcurrentDictionary<Guid, ScenarioDefinition> _scenarios = new();
    private readonly string _scenarioFilePath;
    private readonly object _scenarioLock = new();

    public ConversationRunStore(IWebHostEnvironment environment)
    {
        var dataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);
        _scenarioFilePath = Path.Combine(dataDirectory, "scenarios.json");

        if (File.Exists(_scenarioFilePath))
        {
            try
            {
                var savedScenarios = JsonSerializer.Deserialize<List<ScenarioDefinition>>(File.ReadAllText(_scenarioFilePath)) ?? [];
                var migrated = false;
                foreach (var scenario in savedScenarios)
                {
                    var storedScenario = scenario;
                    if (!string.IsNullOrWhiteSpace(storedScenario.LegacyGroundTruthJson)
                        || !string.IsNullOrWhiteSpace(storedScenario.LegacyCustomerFacts))
                    {
                        storedScenario = MigrateLegacyScenario(storedScenario);
                        migrated = true;
                    }
                    _scenarios[storedScenario.Id] = storedScenario;
                }
                if (migrated) PersistScenarios();
            }
            catch (JsonException)
            {
                // A damaged local data file should not prevent the app from starting.
            }
        }
        else
        {
            foreach (var scenario in RoamingScenarioSeedCatalog.Create()) _scenarios[scenario.Id] = scenario;
            PersistScenarios();
        }
    }

    public IReadOnlyList<ScenarioDefinition> GetScenarios(string agentId) => _scenarios.Values
        .Where(scenario => scenario.AgentId == agentId)
        .OrderByDescending(scenario => scenario.UpdatedAt)
        .ToList();

    public ScenarioDefinition? GetScenario(Guid id) => _scenarios.GetValueOrDefault(id);

    public ScenarioDefinition SaveScenario(Guid? id, string agentId, string name, string objective, string groundTruth, int conversationCount, int maxTurnsPerConversation)
    {
        var existing = id.HasValue ? GetScenario(id.Value) : null;
        var scenario = new ScenarioDefinition
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            AgentId = agentId,
            Name = name,
            Objective = objective,
            GroundTruth = groundTruth,
            ConversationCount = conversationCount,
            MaxTurnsPerConversation = maxTurnsPerConversation,
            CreatedAt = existing?.CreatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _scenarios[scenario.Id] = scenario;
        PersistScenarios();
        return scenario;
    }

    public bool DeleteScenario(Guid id)
    {
        var removed = _scenarios.TryRemove(id, out _);
        if (removed) PersistScenarios();
        return removed;
    }

    public ConversationRun CreateRun(ScenarioDefinition scenario)
    {
        var run = new ConversationRun
        {
            AgentId = scenario.AgentId,
            ScenarioName = scenario.Name,
            Objective = scenario.Objective,
            GroundTruth = scenario.GroundTruth,
            MaxTurnsPerConversation = scenario.MaxTurnsPerConversation,
            Conversations = Enumerable.Range(1, scenario.ConversationCount)
                .Select(number => new GeneratedConversation
                {
                    Number = number,
                    Title = $"Agent Eval Conversation {number switch { 1 => "One", 2 => "Two", 3 => "Three", _ => number.ToString() }}"
                })
                .ToList()
        };

        _runs[run.Id] = run;
        return run;
    }

    public ConversationRun? Get(Guid id)
    {
        if (!_runs.TryGetValue(id, out var run)) return null;
        lock (run)
        {
            return new ConversationRun
            {
                Id = run.Id,
                AgentId = run.AgentId,
                AgentName = run.AgentName,
                ScenarioName = run.ScenarioName,
                Objective = run.Objective,
                GroundTruth = run.GroundTruth,
                MaxTurnsPerConversation = run.MaxTurnsPerConversation,
                Status = run.Status,
                ErrorMessage = run.ErrorMessage,
                CreatedAt = run.CreatedAt,
                CompletedAt = run.CompletedAt,
                Conversations = run.Conversations.Select(conversation => new GeneratedConversation
                {
                    Number = conversation.Number,
                    Title = conversation.Title,
                    Status = conversation.Status,
                    ErrorMessage = conversation.ErrorMessage,
                    Evaluation = conversation.Evaluation,
                    Turns = conversation.Turns.Select(turn => new ConversationTurn
                    {
                        Role = turn.Role,
                        Content = turn.Content,
                        IsPending = turn.IsPending
                    }).ToList()
                }).ToList()
            };
        }
    }

    public void SetRunStatus(Guid runId, string status, string? errorMessage = null)
    {
        if (!_runs.TryGetValue(runId, out var run)) return;
        lock (run)
        {
            run.Status = status;
            run.ErrorMessage = errorMessage;
            run.CompletedAt = status is "Completed" or "Failed" ? DateTimeOffset.UtcNow : null;
        }
    }

    public void ResetConversation(Guid runId, int number)
    {
        if (!_runs.TryGetValue(runId, out var run)) return;
        lock (run)
        {
            var conversation = run.Conversations.FirstOrDefault(item => item.Number == number);
            if (conversation is null || conversation.Status == "Running") return;
            conversation.Turns.Clear();
            conversation.ErrorMessage = null;
            conversation.Evaluation = null;
            conversation.Status = "Not started";
            run.ErrorMessage = null;
        }
    }

    public void SetConversationStatus(Guid runId, int number, string status, string? errorMessage = null)
    {
        if (!_runs.TryGetValue(runId, out var run)) return;
        lock (run)
        {
            var conversation = run.Conversations.FirstOrDefault(item => item.Number == number);
            if (conversation is null) return;
            if (status == "Queued") conversation.Evaluation = null;
            conversation.Status = status;
            conversation.ErrorMessage = errorMessage;
        }
    }

    public void SetEvaluation(Guid runId, int number, AnswerEvaluation evaluation)
    {
        if (!_runs.TryGetValue(runId, out var run)) return;
        lock (run)
        {
            var conversation = run.Conversations.FirstOrDefault(item => item.Number == number);
            if (conversation is not null) conversation.Evaluation = evaluation;
        }
    }

    public void AddTurn(Guid runId, int number, string role, string content)
    {
        if (!_runs.TryGetValue(runId, out var run)) return;
        lock (run)
        {
            var conversation = run.Conversations.FirstOrDefault(item => item.Number == number);
            conversation?.Turns.Add(new ConversationTurn { Role = role, Content = content });
        }
    }

    private void PersistScenarios()
    {
        lock (_scenarioLock)
        {
            var temporaryPath = _scenarioFilePath + ".tmp";
            var json = JsonSerializer.Serialize(_scenarios.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _scenarioFilePath, overwrite: true);
        }
    }

    private static ScenarioDefinition MigrateLegacyScenario(ScenarioDefinition scenario)
    {
        var objective = scenario.Objective.Trim();
        var facts = scenario.LegacyCustomerFacts;
        JsonDocument? document = null;

        if (!string.IsNullOrWhiteSpace(scenario.LegacyGroundTruthJson))
        {
            document = JsonDocument.Parse(scenario.LegacyGroundTruthJson);
            var root = document.RootElement;
            if (root.TryGetProperty("customer", out var customer)
                && customer.TryGetProperty("facts", out var customerFacts)
                && customerFacts.ValueKind != JsonValueKind.Null)
            {
                facts = ToReadableFacts(customerFacts);
            }

            if (string.IsNullOrWhiteSpace(scenario.GroundTruth)
                && root.TryGetProperty("verifiedExpectedRecommendation", out var answer)
                && answer.ValueKind == JsonValueKind.String)
            {
                scenario = CopyScenario(scenario, groundTruth: answer.GetString());
            }
        }

        if (!string.IsNullOrWhiteSpace(facts)
            && !objective.Contains(facts.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            objective = $"{objective}{Environment.NewLine}{Environment.NewLine}Customer scenario details for simulation:{Environment.NewLine}{facts.Trim()}";
        }

        if (string.IsNullOrWhiteSpace(scenario.GroundTruth)
            && scenario.Name.Contains("India direct trip", StringComparison.OrdinalIgnoreCase))
        {
            scenario = CopyScenario(scenario, groundTruth: "Asia Travel Plan, 14 days.");
        }

        document?.Dispose();
        return CopyScenario(scenario, objective: objective, legacyGroundTruthJson: null, legacyCustomerFacts: null);
    }

    private static ScenarioDefinition CopyScenario(
        ScenarioDefinition scenario,
        string? objective = null,
        string? groundTruth = null,
        string? legacyGroundTruthJson = null,
        string? legacyCustomerFacts = null) => new()
    {
        Id = scenario.Id,
        AgentId = scenario.AgentId,
        Name = scenario.Name,
        Objective = objective ?? scenario.Objective,
        GroundTruth = groundTruth ?? scenario.GroundTruth,
        ConversationCount = scenario.ConversationCount,
        MaxTurnsPerConversation = scenario.MaxTurnsPerConversation,
        LegacyGroundTruthJson = legacyGroundTruthJson,
        LegacyCustomerFacts = legacyCustomerFacts,
        CreatedAt = scenario.CreatedAt,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static string ToReadableFacts(JsonElement element)
    {
        var lines = new List<string>();
        AppendFacts(lines, element, "Scenario detail");
        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendFacts(List<string> lines, JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject()) AppendFacts(lines, property.Value, $"{path} · {property.Name}");
                break;
            case JsonValueKind.Array:
                var index = 1;
                foreach (var item in element.EnumerateArray()) AppendFacts(lines, item, $"{path} #{index++}");
                break;
            case JsonValueKind.String:
                lines.Add($"{path}: {element.GetString()}");
                break;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                lines.Add($"{path}: {element}");
                break;
        }
    }
}
