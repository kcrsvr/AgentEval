using AgentEval.Models;
using AgentEval.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AgentEval.Pages.Scenarios;

public class NewModel(
    ConversationRunStore runStore,
    IOptions<OpenAiOptions> openAiOptions) : PageModel
{
    [BindProperty]
    public Guid? ScenarioId { get; set; }

    [BindProperty]
    public string AgentId { get; set; } = "rogers-roaming";

    [BindProperty]
    public string ScenarioName { get; set; } = "India roaming recommendation";

    [BindProperty]
    public string Objective { get; set; } = "Test whether the agent gathers missing trip details one question at a time, confirms the itinerary before recommending, and uses approved tools without inventing prices. Simulated customer: a consumer postpaid Rogers customer travelling from Canada to Delhi, India for 7 days and returning to Canada; India is the only destination, it is a land trip with no cruise, and the customer wants the best-value roaming option. If asked for an unspecified detail, the customer does not know.";

    [BindProperty]
    public int ConversationCount { get; set; } = 3;

    [BindProperty]
    public int MaxTurnsPerConversation { get; set; } = 12;

    [BindProperty]
    public string GroundTruth { get; set; } = "Asia Travel Plan, 14 days.";

    public bool IsEditing => ScenarioId.HasValue;
    public bool IsOpenAiConfigured => !string.IsNullOrWhiteSpace(openAiOptions.Value.ApiKey);

    public IActionResult OnGet(string? agentId, Guid? id)
    {
        if (!string.IsNullOrWhiteSpace(agentId)) AgentId = agentId;
        if (AgentId != "rogers-roaming") return NotFound();

        if (id.HasValue)
        {
            var scenario = runStore.GetScenario(id.Value);
            if (scenario is null || scenario.AgentId != AgentId) return NotFound();
            LoadScenario(scenario);
        }

        return Page();
    }

    public Task<IActionResult> OnPostSaveAsync() => SaveScenarioAsync(openReview: false);

    public Task<IActionResult> OnPostGenerateAsync() => SaveScenarioAsync(openReview: true);

    private Task<IActionResult> SaveScenarioAsync(bool openReview)
    {
        if (AgentId != "rogers-roaming")
        {
            ModelState.AddModelError(nameof(AgentId), "Choose an available agent.");
        }

        if (ScenarioId.HasValue && runStore.GetScenario(ScenarioId.Value) is null)
        {
            ModelState.AddModelError(nameof(ScenarioId), "This scenario no longer exists.");
        }

        if (string.IsNullOrWhiteSpace(ScenarioName))
        {
            ModelState.AddModelError(nameof(ScenarioName), "Enter a scenario name.");
        }

        if (string.IsNullOrWhiteSpace(Objective))
        {
            ModelState.AddModelError(nameof(Objective), "Enter a scenario objective.");
        }

        if (ConversationCount is < 2 or > 5)
        {
            ModelState.AddModelError(nameof(ConversationCount), "Choose between 2 and 5 conversations. Each conversation contains multiple turns.");
        }

        if (MaxTurnsPerConversation is < 2 or > 20)
        {
            ModelState.AddModelError(nameof(MaxTurnsPerConversation), "Choose between 2 and 20 back-and-forth turns per conversation.");
        }

        if (string.IsNullOrWhiteSpace(GroundTruth))
        {
            ModelState.AddModelError(nameof(GroundTruth), "Enter the expected final recommendation or answer in plain text.");
        }

        if (!ModelState.IsValid) return Task.FromResult<IActionResult>(Page());

        var scenario = runStore.SaveScenario(
            ScenarioId,
            AgentId,
            ScenarioName.Trim(),
            Objective.Trim(),
            GroundTruth.Trim(),
            ConversationCount,
            MaxTurnsPerConversation);

        if (!openReview) return Task.FromResult<IActionResult>(RedirectToPage("/Scenarios/Index", new { notice = "Scenario saved" }));

        var run = runStore.CreateRun(scenario);
        return Task.FromResult<IActionResult>(RedirectToPage("/Scenarios/Generated", new { runId = run.Id }));
    }

    private void LoadScenario(ScenarioDefinition scenario)
    {
        ScenarioId = scenario.Id;
        AgentId = scenario.AgentId;
        ScenarioName = scenario.Name;
        Objective = scenario.Objective;
        ConversationCount = scenario.ConversationCount;
        MaxTurnsPerConversation = scenario.MaxTurnsPerConversation;
        GroundTruth = scenario.GroundTruth;
    }
}
