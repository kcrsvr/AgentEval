# Agent Eval

An ASP.NET Core Razor Pages prototype for managing roaming-agent scenarios and running bounded multi-turn conversation simulations.

## Current workflow

- The home page lists available agents; currently it includes the Roaming Support Agent.
- Each agent has a scenario library with create, edit, save, and delete actions.
- Scenarios store a name, a comprehensive plain-text objective, expected ground-truth recommendation/answer, conversation count (2–5 independent sessions), and max back-and-forth turns per conversation (2–20). The objective includes the simulated customer's role, itinerary, known details, and intent; there is no separate customer-facts field. The expected answer is evaluation-only and is never sent to either simulator. A turn is one customer message plus one agent response.
- The first launch seeds a starter scenario catalog covering trip recommendations, stopovers, cruise travel, U.S. plan questions, current cruise pricing, troubleshooting, international calling, and past-charge dispute routing.
- View opens an unstarted conversation review. Use Run all or a per-conversation Run button to start processing explicitly. OpenAI generates one customer message at a time from the comprehensive scenario objective, then the local roaming-agent policy emulator responds; the next model call receives that actual reply and the transcript. The expected ground-truth answer is withheld from both simulators.
- Each session stops when the simulated customer marks the objective complete, the local agent reaches a terminal policy response, or the configured maximum back-and-forth turns is reached.
- After a session stops, a separate OpenAI evaluation compares only its last agent response with the scenario ground truth. It returns a 0–100 semantic correctness/completeness score and an explanation; 80 or above is Success. The run is Completed when every session has finished and been evaluated, with an average score and Success only when every session succeeds. A turn-limit or out-of-scope response is evaluated too. An API or evaluation error is reported as a run failure, not a match failure. Scores are model judgments, not verified facts.
- Scenarios are stored in `App_Data/scenarios.json`; create, edit, and delete operations update that file. The file is copied to build/publish output when present, and the application can initialize the starter catalog if it is missing.
- Conversation runs are held in memory and disappear when the app restarts; regenerate them from a saved scenario.
- Existing scenarios that stored the old JSON ground-truth blob or separate customer-facts text are migrated at startup: customer facts are appended to the scenario objective and the expected answer stays in a separate plain-text Ground Truth field. The India 7-day scenario uses “Asia Travel Plan, 14 days.”

The OpenAI customer-simulator and answer-evaluator integrations use the Chat Completions JSON response format and the same configured endpoint and model. Ground truth and the last agent response are sent to OpenAI for evaluation, but ground truth is never sent to the customer simulator or agent emulator. Configure `OpenAI:ApiKey` with .NET User Secrets for local development or use the `OPENAI_API_KEY` server environment variable. `OpenAI:Model`, `OpenAI:ChatCompletionsUrl`, and `OpenAI:TimeoutSeconds` are configurable in app settings. Never put API keys in source control, scenario JSON, browser fields, or committed appsettings files.

The roaming-agent side currently uses a minimal local policy-flow emulator for the trip-question/confirmation and past-charge out-of-scope paths. It is not a full implementation of every rule in the supplied specification, is not the Rogers production agent, and does not call the Stargaze gateway. Since rates and knowledge tools are not connected, it refuses to invent prices, eligibility, or knowledge answers. Treat transcripts as orchestration development output, not a Rogers-agent evaluation result. A production gateway client still needs an approved, complete request/response contract and server-side authentication configuration.

Optional environment settings: `OPENAI_MODEL` and `OPENAI_CHAT_COMPLETIONS_URL` override the appsettings defaults. Runs are processed sequentially to avoid a burst of simultaneous API requests; a run is bounded by the configured sessions × maximum exchanges.

## Requirements

- .NET SDK 11.0 or later

## Run and debug in VS Code

Open the **Run and Debug** view, select **Agent Eval: Run and Debug**, and press F5. VS Code builds the solution first, starts the app in Development at `http://localhost:5287`, and opens the site when it is ready. Stop debugging with Shift+F5. The configuration is in `.vscode/launch.json`, with the build task in `.vscode/tasks.json`.

For terminal use, run `dotnet build AgentEval.sln` followed by `dotnet run --project AgentEval.csproj`.
