using AgentEval.Models;
using AgentEval.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgentEval.Pages.Scenarios;

public class IndexModel(ConversationRunStore runStore) : PageModel
{
    public IReadOnlyList<ScenarioDefinition> Scenarios { get; private set; } = [];
    public string? Notice { get; private set; }

    public void OnGet(string? notice)
    {
        Scenarios = runStore.GetScenarios("rogers-roaming");
        Notice = notice;
    }

    public IActionResult OnPostDelete(Guid scenarioId)
    {
        runStore.DeleteScenario(scenarioId);
        return RedirectToPage(new { notice = "Scenario deleted" });
    }

    public IActionResult OnPostView(Guid scenarioId)
    {
        var scenario = runStore.GetScenario(scenarioId);
        if (scenario is null) return NotFound();

        var run = runStore.CreateRun(scenario);
        return RedirectToPage("/Scenarios/Generated", new { runId = run.Id });
    }
}
