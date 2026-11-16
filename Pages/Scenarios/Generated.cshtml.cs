using AgentEval.Models;
using AgentEval.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgentEval.Pages.Scenarios;

public class GeneratedModel(ConversationRunStore runStore, ConversationRunQueue runQueue) : PageModel
{
    public ConversationRun? Run { get; private set; }

    public IActionResult OnGet(Guid runId)
    {
        Run = runStore.Get(runId);
        return Run is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostRunAllAsync(Guid runId)
    {
        var run = runStore.Get(runId);
        if (run is null) return NotFound();
        if (run.Conversations.Any(item => item.Status is "Queued" or "Running"))
        {
            return RedirectToPage(new { runId });
        }
        foreach (var conversation in run.Conversations)
        {
            runStore.SetConversationStatus(runId, conversation.Number, "Queued");
        }
        runStore.SetRunStatus(runId, "Queued");
        await runQueue.QueueRunAsync(runId, cancellationToken: HttpContext.RequestAborted);
        return RedirectToPage(new { runId });
    }

    public async Task<IActionResult> OnPostRunConversationAsync(Guid runId, int number)
    {
        var run = runStore.Get(runId);
        if (run is null || run.Conversations.All(item => item.Number != number)) return NotFound();
        var conversation = run.Conversations.Single(item => item.Number == number);
        if (conversation.Status is "Queued" or "Running") return RedirectToPage(new { runId });
        runStore.SetConversationStatus(runId, number, "Queued");
        runStore.SetRunStatus(runId, "Queued");
        await runQueue.QueueRunAsync(runId, number, HttpContext.RequestAborted);
        return RedirectToPage(new { runId });
    }
}
