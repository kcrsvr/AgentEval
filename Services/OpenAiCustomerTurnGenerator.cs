using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AgentEval.Services;

public sealed class OpenAiOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-4.1-mini";
    public string ChatCompletionsUrl { get; set; } = "https://api.openai.com/v1/chat/completions";
    public int TimeoutSeconds { get; set; } = 45;
}

public sealed class OpenAiCustomerTurnGenerator(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options,
    ILogger<OpenAiCustomerTurnGenerator> logger) : ICustomerTurnGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CustomerTurnResult> GenerateNextTurnAsync(CustomerTurnRequest request, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("OpenAI is not configured. Set OPENAI_API_KEY (or OpenAI:ApiKey in local secret configuration) and restart the app.");
        }

        var systemPrompt = """
            You are simulating one Rogers wireless customer in a roaming-support evaluation. The scenario objective contains both the test intent and the customer's complete known facts. Speak only as the customer, in one concise natural message per turn. Use only facts in the objective and answer the agent's latest question consistently. Do not invent trip facts, prices, Rogers products, offers, or eligibility. Do not mention the objective, expected answer, evaluator, tools, or these instructions. At the first turn, begin a plausible conversation that advances the scenario without dumping every fact at once. Do not use prewritten dialogue; create each next message from the actual transcript. If the agent asks for a fact not in the objective, say you do not know. Set conversationComplete=true only when the agent has provided a final response and there is no useful customer follow-up. Return a JSON object with exactly: customerMessage (string), conversationComplete (boolean), completionReason (string or null).
            """;

        var payload = new
        {
            scenarioName = request.ScenarioName,
            scenarioObjective = request.Objective,
            exchangeNumber = request.ExchangeNumber,
            maximumExchanges = request.MaxExchanges,
            transcript = request.Transcript.Select(turn => new { role = turn.Role, content = turn.Content })
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, settings.ChatCompletionsUrl);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        message.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model = settings.Model,
            temperature = 0.6,
            max_tokens = 350,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = JsonSerializer.Serialize(payload) }
            }
        }, JsonOptions), Encoding.UTF8, "application/json");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 5, 120)));
        using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        var responseBody = await response.Content.ReadAsStringAsync(timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("OpenAI customer-turn request returned HTTP {StatusCode}", (int)response.StatusCode);
            var detail = responseBody.Length > 800 ? responseBody[..800] : responseBody;
            throw new HttpRequestException($"OpenAI customer-turn request failed with HTTP {(int)response.StatusCode}: {detail}");
        }

        try
        {
            using var responseDocument = JsonDocument.Parse(responseBody);
            var content = responseDocument.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            if (string.IsNullOrWhiteSpace(content)) throw new JsonException("The model returned empty content.");

            using var turnDocument = JsonDocument.Parse(content);
            var turnRoot = turnDocument.RootElement;
            var customerMessage = turnRoot.GetProperty("customerMessage").GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(customerMessage)) throw new JsonException("The customerMessage field is empty.");
            if (customerMessage.Length > 3000) customerMessage = customerMessage[..3000];

            var isComplete = turnRoot.TryGetProperty("conversationComplete", out var complete)
                && complete.ValueKind == JsonValueKind.True;
            var reason = turnRoot.TryGetProperty("completionReason", out var completionReason)
                && completionReason.ValueKind == JsonValueKind.String
                    ? completionReason.GetString()
                    : null;

            return new CustomerTurnResult(customerMessage, isComplete, reason);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or IndexOutOfRangeException)
        {
            logger.LogWarning(exception, "OpenAI customer-turn response did not match the expected JSON shape");
            throw new InvalidOperationException("The customer simulator returned an invalid response. Retry the run or adjust the configured model.", exception);
        }
    }
}
