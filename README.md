# Semantic Kernel Agents & Workflows (.NET 10)

A compact starter that shows how to:
- Define Semantic Kernel agents with embedded prompts and tool plugins (KernelFunctions).
- Orchestrate typed workflows that chain functions/tools and LLM calls.
- Expose clean entrypoints via dedicated services (agent vs workflow).

## What’s in the code

- `Program.cs`: .NET host composition + logging, registers SK (`AddSk`), agents/services (`AddAgents`), then separately invokes an agent service (`IIncidentTriageAgentService`) and a workflow service (`IWorkflowSampleService`).
- `Infrastructure/Sk/SkConfiguration.cs`: builds the `Kernel`, configures OpenAI chat completion, registers plugins:
  - `IncidentTriageTools` for the triage agent.
  - `WorkflowTools` for the workflow (includes delegation to the SummarizationAgent).
- `Framework/Agents`:
  - `AgentBase<TVariables>`: handles LLM call, builds `ChatHistory`, injects system prompt from embedded resource, enables FunctionCalling for authorized KernelFunctions, maps typed input `TVariables` into `KernelArguments`.
  - `IAgent<TVariables>`: only `AskAsync(AgentRequest<TVariables>)`.
  - `AgentRequest<TVariables>` / `AgentResponse`: typed request/response contract.
- `Framework/Prompting`: `PromptBuilder` reads embedded YAML, deserializes to `Prompt`, renders via Handlebars + `KernelArguments`. `EmbeddedResourceManager` reads embedded resources.
- `Framework/Workflow`: mini DSL (`WorkflowBuilder`) + `WorkflowRunner` + `KernelFunctionStep`. `WorkflowContext.State` passes values between steps.
- Sample agents:
  - `IncidentTriageAgent` (prompt `IncidentTriagePrompt.yaml`, tools `IncidentTriageTools.cs`, typed input `IncidentTriageInput`).
  - `SummarizationAgent` (prompt `SummarizationPrompt.yaml`, typed input `SummarizationInput`).
- Sample services:
  - `IncidentTriageAgentService`: entrypoint for the triage agent (`AgentRequest<IncidentTriageInput>` → response).
  - `WorkflowSampleService`: executes a typed workflow `IncidentTriageWorkflow` (defined in `Samples/Workflow/IncidentTriageWorkflow.cs`).

## Quick start

1) Prereqs
- .NET 10 SDK.
- OpenAI key: replace `open_api_key` in `Infrastructure/Sk/SkConfiguration.cs` (ideally via config/secret).

2) Restore & run
```bash
dotnet restore
dotnet run --project PocSKConsole/PocSKConsole.csproj
```

## Agent structure

### 1) Typed input/output
- Create an input record, e.g., `IncidentTriageInput`.
- Agent inherits `AgentBase<TVariables>` with `TVariables` = input record.
- Call: `AskAsync(new AgentRequest<TVariables>("user message", variables), ct);`

### 2) Embedded prompt
- YAML under `Samples/MyAgents/<AgentName>/Prompts/<PromptName>.yaml`.
- Declared as EmbeddedResource in the csproj.
- Prompt can reference input properties via Handlebars (e.g., `{{Incident}}`).

### 3) Tools / KernelFunctions
- Class under `Samples/MyAgents/<AgentName>/Tools/...`.
- Each method annotated `[KernelFunction("name")]`.
- Registered in SK (`SkConfiguration`) via `pluginCollection.AddFromType<YourTools>(pluginName: nameof(YourTools))`.
- Agent whitelists functions via `AuthorizedKernelFunctions => GetKernelFunctionsForPlugin<YourTools>()`.

### 4) Concrete agent
- File e.g., `IncidentTriageAgent.cs`:
  - `PromptFileName` → YAML filename (without extension).
  - `AuthorizedKernelFunctions` → allowed tools.
  - Inherits `AgentBase<IncidentTriageInput>`.

### 5) Service entrypoint
- Create a dedicated service, e.g., `IncidentTriageAgentService`, that takes the agent via DI and exposes a clear method (`CallIncidentTriageAgent`).
- This service is the “external” entrypoint (API/CLI/worker).

## Workflow structure

### Typed workflow (existing: `IncidentTriageWorkflow`)
- Defined in its own class under `Samples/Workflow/`.
- Builds a `WorkflowDefinition` via `WorkflowBuilder`:
  - `ThenCallFunction` to invoke a `KernelFunction` (local or LLM-backed).
  - Can store results in `WorkflowContext.State`.
- `WorkflowRunner` executes sequentially and returns `WorkflowResult`.

### Tools for workflows
- `WorkflowTools` exposes operational KernelFunctions (fetch/classify/assign) and delegates to `SummarizationAgent` for LLM summarization.
- Registered in SK (`SkConfiguration`).

### Service entrypoint
- `WorkflowSampleService` takes the typed workflow + `WorkflowRunner` and exposes `RunStaticWorkflow`.
- This is the “external” entrypoint to orchestrate the workflow.

## Add a new agent (step-by-step)

1) Create folder `Samples/MyAgents/<NewAgent>/`.
2) Create input record `Models/<NewInput>.cs`.
3) Create prompt YAML in `Prompts/<PromptName>.yaml`; add as EmbeddedResource in csproj.
4) Create tools in `Tools/<NewTools>.cs` with `[KernelFunction]`.
5) Create the agent `NewAgent.cs` inheriting `AgentBase<NewInput>`, set `PromptFileName`, whitelist tools.
6) Register in DI (`Samples/DI.cs`) and in SK (`SkConfiguration.cs` via `AddFromType<NewTools>`).
7) Create a dedicated service to expose the agent (`<NewAgent>Service`) if needed.

## Add a new workflow

1) Create a class `Samples/Workflow/<NewWorkflow>.cs` that builds the `WorkflowDefinition` via `WorkflowBuilder`.
2) Add required tools (KernelFunctions) and register them in `SkConfiguration`.
3) Create a service entrypoint (`<NewWorkflow>Service`) that takes the typed workflow and `WorkflowRunner`.
4) Register the workflow and service in DI (`Samples/DI.cs`).

## Tips / gotchas

- Secrets: don’t hardcode the OpenAI key; use config/Secret Manager.
- Prompts: always embed agent prompts and declare them in the csproj.
- Typing: use `AgentRequest<TVariables>` to force required inputs; the agent turns that into `KernelArguments` and system prompt automatically.
- Tools: keep tools per agent/workflow separated and registered as distinct plugins.
- Services: keep agent and workflow entrypoints separated to keep responsibilities clear.

## What the demo does

- **Agent**: `IncidentTriageAgentService` calls the triage agent with an `IncidentTriageInput` and gets a triage summary (LLM + allowed tools).
- **Workflow**: `WorkflowSampleService` runs `IncidentTriageWorkflow`:
  - `fetch_incident` (mock) → `classify_incident` (mock) → `summarize_incident` (LLM via SummarizationAgent) → `assign_incident` (mock), chaining state through steps.
