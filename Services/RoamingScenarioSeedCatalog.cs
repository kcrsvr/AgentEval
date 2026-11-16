using System.Text.Json;
using AgentEval.Models;

namespace AgentEval.Services;

internal static class RoamingScenarioSeedCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static IReadOnlyList<ScenarioDefinition> Create() =>
    [
        Scenario(
            "India direct trip · 7 days",
            "Verify the assistant collects missing trip details, confirms the itinerary before recommending, and uses the approved rates and recommendation tools without inventing prices.",
            3,
            [
                "I'm travelling to India for a week. What roaming option would work best?",
                "I'll be in Delhi for seven days. Can you help me choose a roaming option?",
                "I'm going to India for 7 days and want to understand my roaming choices."
            ],
            new
            {
                language = "English",
                travelOrder = new[] { new { type = "land", country = "India", places = new[] { "Delhi" }, days = 7, position = "destination" } },
                cruise = false,
                returnsToCanada = true
            },
            ["No, India is my only destination.", "No cruise; I'm flying.", "Yes, that itinerary is correct and complete."],
            ["Ask only for missing trip details, one question per turn.", "Confirm the complete trip before rate lookup and recommendation.", "Do not assert a price or product fit without the approved rates and recommendation tools."],
            ["https://www.rogers.com/mobility/roaming", "https://www.rogers.com/mobility/roaming/faqs-about-travel-pass"],
            "Asia Travel Plan, 14 days."),

        Scenario(
            "Japan stopover · India destination",
            "Test whether the assistant retains a stopover and final destination as separate ordered land segments, asks only for missing details, and confirms the full itinerary before recommending.",
            4,
            [
                "I'm flying to India with a two-day stop in Japan. What roaming option should I consider?",
                "My route is Toronto to Tokyo for two days, then Delhi for eight days. Can you help with roaming?",
                "I'm stopping in Japan before spending eight days in India. What should I know about roaming?",
                "I'll have a short stay in Japan, then India for eight days. Which roaming option may fit?"
            ],
            new
            {
                language = "English",
                travelOrder = new object[]
                {
                    new { type = "land", country = "Japan", places = new[] { "Tokyo" }, days = 2, position = "stopover" },
                    new { type = "land", country = "India", places = new[] { "Delhi" }, days = 8, position = "destination" }
                },
                cruise = false,
                returnsToCanada = true
            },
            ["No other countries.", "No, there is no cruise.", "Yes, Japan for two days followed by India for eight days is correct."],
            ["Keep the stopover and destination as distinct segments in travel order.", "Do not ask again for destinations or durations already provided.", "Use exact rates from the approved tool for each destination; do not calculate or invent prices."],
            ["https://www.rogers.com/mobility/roaming", "https://www.rogers.com/mobility/roaming/things-to-know-while-abroad"]),

        Scenario(
            "Europe multi-country itinerary",
            "Exercise a multi-country land itinerary: preserve countries and days separately, ask any required trip-flow questions in order, and avoid giving generic rates before itinerary confirmation.",
            5,
            [
                "I'm visiting Rome for four days and Paris for three days. What roaming option should I look at?",
                "I'll spend four days in Italy and three in France. Can you recommend a roaming option?",
                "My trip includes Rome and Paris, four days in Italy and three in France. How should I plan roaming?",
                "I'm going to Italy and France for a week. I'd like to understand roaming options.",
                "I have a seven-day trip split between Italy and France. What details do you need for roaming?"
            ],
            new
            {
                language = "English",
                travelOrder = new object[]
                {
                    new { type = "land", country = "Italy", places = new[] { "Rome" }, days = 4, position = "destination" },
                    new { type = "land", country = "France", places = new[] { "Paris" }, days = 3, position = "destination" }
                },
                cruise = false,
                returnsToCanada = true
            },
            ["Those are the only countries.", "No cruise; I am travelling by air and rail.", "Yes, Italy for four days and France for three days is correct."],
            ["Normalize cities to their countries and preserve the itinerary order.", "Ask for missing trip details one at a time and confirm before recommendation.", "Get approved rates for every destination; never infer or combine rates."],
            ["https://www.rogers.com/mobility/roaming", "https://www.rogers.com/mobility/roaming/faqs-about-travel-pass"]),

        Scenario(
            "Land · cruise · land roaming plan",
            "Test the cruise flow across land before, cruise ports and sea days, and land after. The assistant must collect the itinerary, line and ship, verify eligibility using its knowledge tool, confirm the trip, and only then recommend.",
            5,
            [
                "I'll spend three days in Japan, take a cruise to Australia and New Zealand, then stay two days in New Zealand. What roaming option could work?",
                "My trip is three days in Tokyo, a seven-day cruise from Yokohama with Sydney and Auckland stops, then two days in Auckland. Can you help with roaming?",
                "I'm going to Japan before a cruise and will spend a couple of days in New Zealand after. What should I consider for my phone?",
                "I'm travelling to Japan, cruising to Australia and New Zealand, and staying in Auckland after the ship. Can you help plan roaming?",
                "I have land time in Japan, a cruise with sea days and ports in Australia and New Zealand, then land time in New Zealand."
            ],
            new
            {
                language = "English",
                travelOrder = new object[]
                {
                    new { type = "land", country = "Japan", places = new[] { "Tokyo" }, days = 3, position = "beforeCruise" },
                    new
                    {
                        type = "cruise",
                        cruiseLine = "Princess Cruises",
                        ship = "Diamond Princess",
                        daysTotal = 7,
                        timeline = new object[]
                        {
                            new { dayFrom = 1, dayTo = 1, type = "port", country = "Japan", place = "Yokohama", note = "boarding" },
                            new { dayFrom = 2, dayTo = 3, type = "sea", days = 2 },
                            new { dayFrom = 4, dayTo = 4, type = "port", country = "Australia", place = "Sydney" },
                            new { dayFrom = 5, dayTo = 6, type = "sea", days = 2 },
                            new { dayFrom = 7, dayTo = 7, type = "port", country = "New Zealand", place = "Auckland", note = "return" }
                        }
                    },
                    new { type = "land", country = "New Zealand", places = new[] { "Auckland" }, days = 2, position = "afterCruise" }
                },
                cruise = true,
                returnsToCanada = true,
                cruiseEligibility = "unknown; must be checked with the agent's approved knowledge tool"
            },
            ["No other countries beyond those in the itinerary.", "The cruise line is Princess Cruises.", "The ship is Diamond Princess.", "Yes, that itinerary is correct and complete."],
            ["Keep land-before, cruise timeline, and land-after segments separate and in order.", "Do not assume cruise eligibility; use the approved cruise-eligibility knowledge lookup.", "Do not recommend until the itinerary is confirmed and required rates are returned."],
            ["https://www.rogers.com/mobility/faqs-about-roaming-while-on-a-cruise", "https://www.rogers.com/mobility/roaming/faqs-about-travel-pass"]),

        Scenario(
            "Upcoming U.S. trip · plan inclusion question",
            "Verify that a destination mentioned with a plan-inclusion question takes the trip flow first, avoids claiming account visibility, and directs the customer to official plan details where appropriate.",
            3,
            [
                "I'm going to New York for four days. Is roaming included in my plan?",
                "I have a five-day trip to the U.S. Does my Rogers plan include roaming there?",
                "I'll be in New York next month. Can you tell if my current plan covers roaming?"
            ],
            new
            {
                language = "English",
                travelOrder = new[] { new { type = "land", country = "United States", places = new[] { "New York" }, days = 4, position = "destination" } },
                cruise = false,
                accountType = "consumer postpaid assumed",
                planDetailsUrl = "https://www.rogers.com/consumer/self-serve/wireless/plan-details"
            },
            ["No other countries.", "No cruise, just a city trip.", "Yes, that is the complete itinerary."],
            ["Do not claim access to the customer's account or plan.", "Because the upcoming trip is mentioned, complete the trip flow before answering the plan-inclusion question.", "Use the official plan-details self-serve direction when relevant."],
            ["https://www.rogers.com/support/mobility/canada-us-rogers-infinite-plan-faqs-rogers", "https://www.rogers.com/mobility/roaming/united-states"]),

        Scenario(
            "Already aboard · cruise roaming rates",
            "Test pricing while the customer is currently on a cruise: combine the relevant roaming knowledge and exact rate lookups, answer only the asked question, and do not estimate or fabricate amounts.",
            4,
            [
                "I'm on a cruise ship right now. What roaming charges apply while we're at sea?",
                "I'm already aboard a cruise and want to know the exact cost of using data on the ship.",
                "We're at sea today. What are the roaming rates for phone use on this cruise?",
                "I'm currently travelling by cruise ship. Can you tell me what calls and data cost onboard?"
            ],
            new
            {
                language = "English",
                travelStatus = "currently aboard a cruise ship",
                cruiseLine = "unknown",
                ship = "unknown",
                askedPricing = "cruise roaming rates while at sea",
                expectedTools = new[] { "roaming_support_knowledge", "roaming_rates" },
                expectedPrice = (string?)null
            },
            ["I mean using data and making calls while the ship is at sea.", "I don't know the ship's network name.", "Please give me the exact rates available for cruise roaming."],
            ["Treat this as a current-abroad pricing question, not an upcoming trip recommendation.", "Use both approved roaming knowledge and exact rate tools before answering.", "Do not fabricate or approximate any price; do not call the recommendation tool."],
            ["https://www.rogers.com/mobility/faqs-about-roaming-while-on-a-cruise", "https://www.rogers.com/mobility/roaming/billing"]),

        Scenario(
            "Unable to connect while travelling",
            "Evaluate roaming troubleshooting: acknowledge the connection problem briefly, retrieve the relevant Rogers troubleshooting guidance, summarize only supported steps, and offer escalation only as directed by the support flow.",
            4,
            [
                "I'm in Japan and my phone won't connect to a mobile network. Can you help?",
                "I've landed in Italy but I can't get service on my phone. What should I try?",
                "I'm travelling in Australia and roaming is not working on my phone.",
                "My phone stopped connecting while I'm abroad. I need help troubleshooting roaming."
            ],
            new
            {
                language = "English",
                currentCountry = "Japan",
                issue = "unable to connect to a mobile network while travelling",
                device = "unspecified",
                expectedTool = "roaming_support_knowledge",
                expectedKnowledgeQuery = "roaming connection troubleshooting abroad"
            },
            ["I can make calls over Wi-Fi, but mobile service is unavailable.", "I restarted the phone, but it still cannot connect.", "Please share the next troubleshooting step.", "Yes, connect me with a live agent if the documented steps do not resolve it."],
            ["Call the roaming knowledge tool with a normalized short English query before giving troubleshooting steps.", "Do not guess device-specific steps or claim a fix that the source does not support.", "Escalate only through the defined support flow when appropriate."],
            ["https://www.rogers.com/mobility/roaming/things-to-know-while-abroad", "https://www.rogers.com/support/mobility/roaming"]),

        Scenario(
            "International calling from Canada",
            "Test an international long-distance rate request from Canada: establish the calling origin only if missing, identify the called country, and return exact tool-provided rates without unsupported details.",
            3,
            [
                "How much does it cost to call Australia from Canada?",
                "I'm in Canada and need to call a number in France. What's the rate?",
                "What is the long-distance rate for calling Japan from Canada?"
            ],
            new
            {
                language = "English",
                callingOrigin = "Canada",
                calledCountry = "Australia",
                question = "international long-distance calling price",
                expectedTool = "roaming_rates",
                expectedRate = (string?)null
            },
            ["I'm calling from Canada.", "Australia is the country I want to call.", "I only need the exact rate, please."],
            ["Use the rates tool with the called country and language.", "Do not add unrequested roaming recommendations or estimated amounts.", "If origin or destination is already known, do not ask for it again."],
            ["https://www.rogers.com/support/mobility/how-to-call-and-text-nationally-and-internationally"]),

        Scenario(
            "Past roaming charge dispute · out of scope",
            "Verify routing for a dispute about a past roaming charge: do not investigate or justify the historical bill, do not invent account details, and use the defined out-of-scope behavior.",
            3,
            [
                "I was charged roaming fees last month and want to dispute the bill.",
                "There is a roaming charge on my previous bill that I don't agree with. Can you reverse it?",
                "I want to challenge a roaming fee from my last trip."
            ],
            new
            {
                language = "English",
                intent = "dispute past roaming charge",
                status = "out_of_scope",
                accountActionRequested = true,
                accountActionPermitted = false
            },
            ["The charge is from my trip last month.", "I want someone to review or reverse it.", "I understand you cannot make account changes here."],
            ["Route past roaming charge disputes out of scope.", "Never claim account access or perform account actions.", "Do not provide an unsupported investigation, refund, or billing conclusion."],
            ["https://www.rogers.com/mobility/roaming/billing", "https://www.rogers.com/support/billing-accounts/understanding-charges-on-your-rogers-bill-wireless"])
    ];

    private static ScenarioDefinition Scenario(
        string name,
        string objective,
        int conversationCount,
        string[] initialMessages,
        object scenarioDetails,
        string[] followUps,
        string[] expectedBehavior,
        string[] sourceUrls,
        string? expectedAnswer = null)
    {
        var factsJson = JsonSerializer.Serialize(scenarioDetails, JsonOptions);
        using var factsDocument = JsonDocument.Parse(factsJson);

        return new ScenarioDefinition
        {
            Id = Guid.NewGuid(),
            AgentId = "rogers-roaming",
            Name = name,
            Objective = $"{objective}{Environment.NewLine}{Environment.NewLine}Customer scenario details for the simulator:{Environment.NewLine}{ToReadableFacts(factsDocument.RootElement)}",
            GroundTruth = expectedAnswer ?? "No specific plan or price was supplied as expected ground truth for this starter scenario. Evaluate the stated behavior and require tool-grounded agent answers.",
            ConversationCount = conversationCount,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

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
