using AgentEval.Models;

namespace AgentEval.Services;

public sealed record LocalAgentResponse(string Message, bool IsFinal, string Outcome);
public sealed record RoamingAgentRequest(
    string ScenarioName,
    string Objective,
    IReadOnlyList<ConversationTurn> Transcript);

public interface IRoamingAgentSimulator
{
    Task<LocalAgentResponse> RespondAsync(RoamingAgentRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Local policy-flow emulator for development. It never invents rates, eligibility, or knowledge-tool results.
/// It is not the Rogers production agent or a substitute for testing that endpoint.
/// </summary>
public sealed class LocalRoamingAgentSimulator : IRoamingAgentSimulator
{
    private const string TripOpener = "I'd be glad to help you find the right roaming option for your trip.";
    private const string KnowledgeUnavailable = "I don't have that information right now. I can connect you with a Rogers representative through live chat if you'd like.";

    public Task<LocalAgentResponse> RespondAsync(
        RoamingAgentRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var latestCustomerMessage = request.Transcript.LastOrDefault(turn => turn.Role == "Customer")?.Content ?? string.Empty;
        if (IsPastChargeDispute(request.Objective, latestCustomerMessage))
        {
            return Task.FromResult(new LocalAgentResponse(
                "This local roaming assistant cannot handle disputes about past charges. Please contact Rogers support for billing help.",
                true,
                "out_of_scope"));
        }

        if (!IsTripScenario(request))
        {
            return Task.FromResult(new LocalAgentResponse(KnowledgeUnavailable, true, "knowledge_tools_unavailable"));
        }

        var customerTurnCount = request.Transcript.Count(turn => turn.Role == "Customer");
        var hasCruise = request.Objective.Contains("type: cruise", StringComparison.OrdinalIgnoreCase)
            || request.Objective.Contains("cruise itinerary", StringComparison.OrdinalIgnoreCase)
            || request.Objective.Contains("taking a cruise", StringComparison.OrdinalIgnoreCase)
            || request.Objective.Contains("cruise with", StringComparison.OrdinalIgnoreCase);
        if (hasCruise)
        {
            var response = customerTurnCount switch
            {
                1 => $"{TripOpener} Can you walk me through your cruise itinerary, starting from where you board? Include the stops you'll make and any days at sea.",
                2 => "Will you be visiting any other countries?",
                3 => "Which cruise line will you be sailing with?",
                4 => "And which ship will you be on?",
                _ => KnowledgeUnavailable
            };
            return Task.FromResult(new LocalAgentResponse(
                response,
                customerTurnCount >= 5,
                customerTurnCount >= 5 ? "cruise_eligibility_tool_unavailable" : "collecting_trip_details"));
        }

        var landResponse = customerTurnCount switch
        {
            1 => $"{TripOpener} Will you be visiting any other countries?",
            2 => "Will any part of your trip be on a cruise?",
            3 => BuildLandConfirmation(request.Transcript),
            _ => KnowledgeUnavailable
        };

        return Task.FromResult(new LocalAgentResponse(
            landResponse,
            customerTurnCount >= 4,
            customerTurnCount >= 4 ? "rates_tool_unavailable" : "collecting_trip_details"));
    }

    private static bool IsTripScenario(RoamingAgentRequest request)
        => request.Objective.Contains("destination", StringComparison.OrdinalIgnoreCase)
            || request.Objective.Contains("trip", StringComparison.OrdinalIgnoreCase)
            || request.Objective.Contains("trip", StringComparison.OrdinalIgnoreCase)
            || request.Objective.Contains("itinerary", StringComparison.OrdinalIgnoreCase);

    private static bool IsPastChargeDispute(string objective, string message) =>
        (objective.Contains("dispute", StringComparison.OrdinalIgnoreCase)
            || objective.Contains("past charge", StringComparison.OrdinalIgnoreCase))
        && (message.Contains("charge", StringComparison.OrdinalIgnoreCase)
            || message.Contains("bill", StringComparison.OrdinalIgnoreCase)
            || objective.Contains("dispute", StringComparison.OrdinalIgnoreCase));

    private static string BuildLandConfirmation(IReadOnlyList<ConversationTurn> transcript)
    {
        var customerDetails = string.Join(" ", transcript
            .Where(turn => turn.Role == "Customer")
            .Select(turn => turn.Content));
        return $"Before I look at your options, here is the trip information you shared: {customerDetails} Does that look correct and complete?";
    }
}
