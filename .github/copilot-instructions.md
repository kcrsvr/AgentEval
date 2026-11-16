# Project guidance

- This is an ASP.NET Core Razor Pages application targeting .NET 11.
- The solution is `AgentEval.sln`; the web project is `AgentEval.csproj`.
- Keep page markup in `Pages/` and register backend services in `Program.cs`.
- Build and verify with `dotnet build AgentEval.sln`.
- The home page lists testable agents; scenario CRUD and conversation review pages live under `Pages/Scenarios/`.
- Store scenario definitions in the backend service and never expose ground-truth data to an agent request.
- Do not display fabricated live agent responses; keep unconfigured integrations clearly marked as previews.
