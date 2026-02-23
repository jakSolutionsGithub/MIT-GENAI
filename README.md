# SK Agent Workflow Starter — POC

A **.NET 10** proof-of-concept that demonstrates an opinionated, reusable framework for
building type-safe, tool-enabled, streaming AI agents on top of
[Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel).

The repository is split into two clearly separated layers:

| Layer | Purpose |
|---|---|
| **Framework** | Generic, domain-agnostic infrastructure — agents, workflows, state, memory, guidelines, prompting |
| **Samples** | Concrete implementations that exercise every framework capability |

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Quick Start](#quick-start)
4. [Framework Concepts](#framework-concepts)
   - [Agent Abstraction](#agent-abstraction)
   - [Workflow Engine](#workflow-engine)
   - [State Management](#state-management)
   - [Semantic Memory (RAG)](#semantic-memory-rag)
   - [Baseline Conflict Detection](#baseline-conflict-detection)
   - [Guidelines Library](#guidelines-library)
   - [Prompt Templating](#prompt-templating)
5. [Sample Applications](#sample-applications)
   - [Mobility Form Agent (primary)](#mobility-form-agent-primary)
   - [Incident Triage Agent](#incident-triage-agent)
   - [Summarization Agent](#summarization-agent)
   - [Incident Triage Workflow](#incident-triage-workflow)
6. [Project Structure](#project-structure)
7. [Prerequisites](#prerequisites)
8. [Configuration](#configuration)
9. [Running the Application](#running-the-application)
10. [Docker](#docker)
11. [Extending the Framework](#extending-the-framework)
    - [Adding a New Agent](#adding-a-new-agent)
    - [Adding a New Workflow](#adding-a-new-workflow)
    - [Adding State to an Agent](#adding-state-to-an-existing-agent)
    - [Adding Semantic Memory](#adding-semantic-memory)
12. [Tips & Gotchas](#tips--gotchas)
13. [Key Dependencies](#key-dependencies)

---

## Overview

Rather than building a single monolithic agent, the framework provides composable building
blocks — a typed agent base class, a lightweight workflow runner, a generic state store,
vector-search memory, a guideline retrieval tool, and a Handlebars prompt pipeline — all wired
together through standard .NET dependency injection.

The **Mobility Form Agent** is the primary sample. It is a conversational AI assistant that
helps event managers assess and reduce the CO₂ footprint of their event's transportation. It
demonstrates every framework capability simultaneously:

- Streaming token-by-token responses to the terminal
- Persistent multi-turn conversation history
- Automatic tool invocation (Semantic Kernel function calling)
- Structured state capture via a typed patch/apply pattern
- Real-time CO₂ estimation for participants, staff, and freight
- Semantic vector search over historical event records
- Guideline retrieval from a curated mobility best-practice library
- Baseline conflict detection and interactive resolution



---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  Program.cs — Microsoft.Extensions.Hosting entry point          │
│    AddSk()     — builds and registers the Semantic Kernel       │
│    AddAgents() — registers all framework services and agents    │
└────────────────────────────┬────────────────────────────────────┘
                             │
             ┌───────────────▼───────────────┐
             │   Framework (generic layer)   │
             │                               │
             │  AgentBase<TVariables>        │
             │  WorkflowRunner               │
             │  AgentStateTools<T, TPatch>   │
             │  AgentMemoryTools<TRecord>    │
             │  BaselineConflictTools<T>     │
             │  GuidelinesTools              │
             │  PromptBuilder (Handlebars)   │
             └───────────────┬───────────────┘
                             │
             ┌───────────────▼───────────────┐
             │   Samples (domain layer)      │
             │                               │
             │  MobilityFormAgentClean       │
             │  IncidentTriageAgent          │
             │  SummarizationAgent           │
             │  IncidentTriageWorkflow       │
             │  Co2Calculator                │
             │  MobilityGuidelinesLibrary    │
             │  MobilityHistoryIngestion /   │
             │    Search (vector store RAG)  │
             └───────────────────────────────┘
```

All Kernel plugins are resolved from the DI container and registered by name using the
convention `typeof(T).Name` with generic suffixes stripped (e.g. `AgentStateTools`). This
ensures `AgentBase.GetKernelFunctionsForPlugin<T>()` can always locate the matching plugin at
runtime.

---

## Quick Start

**Prerequisites:** .NET 10 SDK and an OpenAI API key.

```bash
# 1 — Clone and restore
dotnet restore

# 2 — Set your API key via User Secrets (recommended)
cd SkAgentWorkFlowStarter.Console
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."

# 3 — Run
dotnet run --project SkAgentWorkFlowStarter.Console/SkAgentWorkFlowStarter.Console.csproj
```

On startup the application loads `BaselineForm.json` as the reference event document and
launches the interactive Mobility Form Agent chat loop. Type any natural-language message to
begin.

> **Secrets:** Never hardcode the OpenAI key. Use .NET User Secrets locally and the
> `OPENAI_API_KEY` environment variable in CI / containers (see [Configuration](#configuration)).

---

## Framework Concepts

### Agent Abstraction

**Files:** `Framework/Agents/Abstractions/`

```csharp
public interface IAgent<TVariables>
{
    Task<AgentResponse>        AskAsync(AgentRequest<TVariables> request, CancellationToken ct);
    IAsyncEnumerable<string>   StreamAsync(AgentRequest<TVariables> request, CancellationToken ct);
    void                       ResetHistory();
}
```

`AgentBase<TVariables>` provides a complete implementation. Derived agents only need to specify:

| Member | Purpose |
|---|---|
| `PromptFileName` | Name of the embedded YAML prompt file (without extension) |
| `AuthorizedKernelFunctions` | The set of Kernel functions this agent may auto-invoke |
| `UsePersistentHistory` | Whether conversation history is retained across turns (default: `false`) |
| `MaxAutoInvokeAttempts` | Maximum tool-call iterations per turn (default: `10`) |

Variables defined in `TVariables` are automatically mapped to `KernelArguments` via reflection
and injected into the Handlebars prompt template at render time.

`AgentBase` exposes `GetKernelFunctionsForPlugin<T>()`, which resolves exactly the
`[KernelFunction]`-decorated methods from a registered plugin class — giving each agent
fine-grained control over which tools the LLM may call.

#### Agent structure at a glance

| Concern | Where it lives |
|---|---|
| Typed input | A `record` passed as `TVariables` — maps automatically to `KernelArguments` |
| System prompt | Embedded YAML rendered via Handlebars (e.g. `{{Incident}}`) |
| Tools | A class with `[KernelFunction]` methods, whitelisted in `AuthorizedKernelFunctions` |
| Entrypoint | A dedicated service (e.g. `IncidentTriageAgentService`) injected by DI |

---

### Workflow Engine

**Files:** `Framework/Workflow/`

A lightweight sequential pipeline for chaining deterministic Kernel function calls — no LLM in
the loop unless a step explicitly delegates to an agent.

```csharp
WorkflowBuilder
    .Create("Incident triage workflow")
    .ThenCallFunction("WorkflowTools", "fetch_incident",     args => args["incidentId"] = 5012, ...)
    .ThenCallFunction("WorkflowTools", "classify_incident",  ctx => BuildArgs(ctx), ...)
    .ThenCallFunction("WorkflowTools", "summarize_incident", ctx => BuildArgs(ctx), ...)
    .ThenCallFunction("WorkflowTools", "assign_incident",    ctx => BuildArgs(ctx), ...)
    .Build();
```

`WorkflowContext` carries the `Kernel` instance and a mutable `State` dictionary. Each step
may store its output under a named key (e.g. `"incident"`, `"classification"`) and subsequent
steps read previous results from `context.State`. `WorkflowRunner` executes steps in order and
returns a `WorkflowResult` with per-step outputs and the final state snapshot.

---

### State Management

**Files:** `Framework/State/AgentStateTools.cs`

A generic, thread-safe state store with a patch/apply pattern. The LLM calls
`update_state(patch)` to write structured facts and `get_state()` to read the current state.
Derived fields (e.g. CO₂ totals) are recomputed by the application after each patch — the LLM
is explicitly instructed never to write them.

```csharp
public interface IAgentStateStore<TState, TPatch>
{
    TState GetState();
    void   ApplyPatch(TPatch patch);
    void   Reset();
}
```

`AgentStateTools<TState, TPatch>` wraps this store and exposes `update_state` and `get_state`
as `[KernelFunction]` tools the LLM can call.

---

### Semantic Memory (RAG)

**Files:** `Framework/Memory/`

`AgentMemoryTools<TRecord>` provides four Kernel functions for retrieval-augmented generation:

| Function | Description |
|---|---|
| `search_history` | Semantic search over historical records — validates assumptions and finds proven levers |
| `search_similar_records` | Finds similar records to suggest a value for a missing parameter; supports scalar field pinning via JSON path |
| `get_record_by_id` | Fetches a single record by its stable ID |
| `ingest_history` | Saves a completed case outcome to the vector store |

Records must implement `IAgentMemoryRecord`. Concrete implementations provide the
`IAgentMemorySearch<TRecord>` and `IAgentMemoryIngestion<TRecord>` interfaces.

The Mobility sample uses an `InMemoryVectorStore` (Semantic Kernel) backed by OpenAI
`text-embedding-3-small` (1536 dimensions). Historical event data is ingested from Excel
worksheets using ClosedXML.

---

### Baseline Conflict Detection

**Files:** `Framework/State/BaselineConflictTools.cs`, `IBaselineStore.cs`

Handles the pattern where an agent holds authoritative reference data (a pre-filled form, a
configuration document) and must detect when user input contradicts it.

| Function | Description |
|---|---|
| `get_baseline` | Returns the full baseline document |
| `get_baseline_field` | Returns the effective value of a single field (session override takes priority) |
| `flag_baseline_conflict` | Signals a detected mismatch; halts the conversation until resolved |
| `resolve_baseline_conflict` | Applies the user-confirmed value as a session override |
| `check_pending_conflict` | Returns any unresolved conflict; agent must wait before proceeding |

Session overrides are stored in memory alongside the baseline. All future `get_baseline_field`
calls for an overridden field return the confirmed value, not the original.

---

### Guidelines Library

**Files:** `Framework/Guidelines/GuidelinesTools.cs`

Retrieves domain-specific best-practice entries from any `IGuidelinesLibrary` implementation.
Entries carry stable IDs (e.g. `CORP-CHOICE-06`) that the LLM is instructed to cite in its
responses and record in the state patch `assumptions` array.

| Function | Description |
|---|---|
| `get_guidelines` | Primary entry point — ranked token-scored search |
| `get_guidelines_by_type` | Returns all guidelines for a specific context type |
| `get_guideline_by_id` | Fetches a single entry by its stable ID |
| `list_guideline_categories` | Lists all available Category :: Topic pairs |

---

### Prompt Templating

**Files:** `Framework/Prompting/`

Prompts are authored as YAML files and compiled as embedded resources at build time:

```yaml
name: MobilityFormPromptClean
template: |
  You are an event mobility sustainability assistant...
  Baseline: {{{BaselineJson}}}
  Current state: {{{CurrentStateJson}}}
  User message: {{UserMessage}}
```

`PromptBuilder` reads the YAML via `EmbeddedResourceManager`, deserializes it with YamlDotNet,
and renders it with the Semantic Kernel Handlebars template engine against the current
`KernelArguments`. The rendered string becomes the system prompt for the turn.

> **Rule of thumb:** Always declare prompt YAML files as `EmbeddedResource` in the `.csproj`.
> The agent will fail at startup — not at runtime — if the resource is missing.

---

## Sample Applications

### Mobility Form Agent (primary)

**Files:** `Samples/MyAgents/MobilityFormAgentClean/`, `Samples/MobilityAssistant/`,
`Samples/Services/MobilityChatServiceClean.cs`

An interactive console chatbot for event mobility sustainability assessment.

**Capabilities:**

- **Structured data capture** — participant segments (origin, mode, count, distance), staff
  segments, freight items; all stored in `MobilityFormState` via the typed patch pattern.
- **CO₂ estimation** — `Co2Calculator` covers 15+ transport modes (walk, bike, metro, train,
  car, carpool, electric car, coach, taxi, plane, ferry, …) and three freight modes (van,
  truck, plane, ship). CO₂ is recomputed automatically after every state patch.
- **Guidelines** — `MobilityGuidelinesLibrary` contains 40+ curated best-practice entries
  across Corporate Events, Public/Trade Shows, and Incentive/Teambuilding categories.
- **Historical RAG** — Excel files under `Samples/MobilityAssistant/Historical/Data/` are
  ingested into an in-memory vector store. The LLM searches these to validate estimates or
  suggest values for unknown parameters.
- **Baseline conflict detection** — `BaselineForm.json` is loaded at session start. Any user
  input that contradicts the baseline triggers an explicit conflict resolution flow.
- **Streaming** — responses are streamed token-by-token to `stdout`.
- **Persistent history** — multi-turn conversation history is maintained within the session.

**Interactive commands:**

| Input | Action |
|---|---|
| Any natural language | Sent to the Mobility Form Agent |
| `state` | Prints the current structured state as JSON |
| `baseline` | Prints the loaded baseline reference document |
| `done` | Ends the session and prints a final state summary |



## Project Structure

```
sk-agent-workflow-starter-poc/
├── SkAgentWorkFlowStarter.Console.sln
└── SkAgentWorkFlowStarter.Console/
    ├── Program.cs                          # Host bootstrap
    ├── appsettings.json                    # Non-secret configuration
    ├── Dockerfile
    │
    ├── Infrastructure/
    │   └── Sk/
    │       └── SkConfiguration.cs          # Kernel and plugin registration
    │
    ├── Framework/                          #  Generic agent infrastructure 
    │   ├── Agents/
    │   │   ├── Abstractions/
    │   │   │   ├── IAgent.cs
    │   │   │   └── AgentBase.cs
    │   │   └── Models/
    │   │       ├── AgentRequest.cs
    │   │       ├── AgentResponse.cs
    │   │       └── Prompt.cs
    │   ├── Guidelines/
    │   │   └── GuidelinesTools.cs
    │   ├── Memory/
    │   │   ├── AgentMemoryTools.cs
    │   │   ├── IAgentMemoryRecord.cs
    │   │   └── IAgentMemorySearch.cs
    │   ├── Prompting/
    │   │   ├── IPromptBuilder.cs
    │   │   └── EmbeddedResourceManager.cs
    │   ├── State/
    │   │   ├── AgentStateTools.cs
    │   │   ├── BaselineConflictTools.cs
    │   │   └── IBaselineStore.cs
    │   └── Workflow/
    │       ├── IWorkflowStep.cs
    │       ├── WorkflowBuilder.cs
    │       ├── WorkflowRunner.cs
    │       ├── Models/
    │       │   ├── WorkflowContext.cs
    │       │   ├── WorkflowDefinition.cs
    │       │   └── WorkflowResult.cs
    │       └── Steps/
    │           └── KernelFunctionStep.cs
    │
    └── Samples/                            #  Domain implementations 
        ├── DI.cs                           # AddAgents() extension method
        ├── MobilityAssistant/
        │   ├── BaselineForm.json
        │   ├── Guidelines/
        │   │   └── MobilityGuidelinesLibrary.cs
        │   ├── Historical/
        │   │   ├── Data/                   # Excel files ingested at runtime
        │   │   ├── Models/
        │   │   │   └── MobilityHistoryRecord.cs
        │   │   └── Rules/
        │   │       ├── MobilityHistoryIngestionService.cs
        │   │       └── MobilityHistorySearchService.cs
        │   ├── Models/
        │   │   ├── Co2.cs
        │   │   ├── Enums.cs
        │   │   └── Provenance.cs
        │   ├── Rules/
        │   │   └── Co2Calculator.cs
        │   └── State/
        │       └── MobilityBaselineStore.cs
        ├── MyAgents/
        │   ├── IncidentTriageAgent/
        │   │   ├── IncidentTriageAgent.cs
        │   │   ├── Models/
        │   │   ├── Prompts/
        │   │   │   └── IncidentTriagePrompt.yaml    
        │   │   └── Tools/
        │   │       └── IncidentTriageTools.cs
        │   ├── MobilityFormAgentClean/
        │   │   ├── MobilityFormAgentClean.cs
        │   │   ├── Models/
        │   │   │   └── MobilityFormInput.cs
        │   │   ├── Prompts/
        │   │   │   └── MobilityFormPromptClean.yaml  # Embedded resource
        │   │   └── Tools/
        │   │       ├── Co2Tools.cs
        │   │       ├── MobilityConflictTools.cs
        │   │       ├── MobilityFormPatchModels.cs
        │   │       └── MobilityLightTools.cs
        │   └── SummarizationAgent/
        │       ├── SummarizationAgent.cs
        │       ├── Models/
        │       └── Prompts/
        │           └── SummarizationPrompt.yaml     
        ├── Services/
        │   ├── IncidentTriageAgentService.cs
        │   ├── MobilityChatServiceClean.cs
        │   ├── MobilityFormStateStore.cs
        │   └── WorkflowSampleService.cs
        └── Workflow/
            ├── IncidentTriageWorkflow.cs
            └── Tools/
                └── WorkflowTools.cs
```

---

## Prerequisites

| Requirement | Version |
|---|---|
| .NET SDK | 10.0 or later |
| OpenAI API key | — |
| Docker (optional) | Any recent version |

---

## Configuration

### `appsettings.json`

```json
{
  "OpenAI": {
    "Model": "gpt-5",
    "BaseUrl": "https://api.openai.com/v1",
    "ServiceId": "openAi",
    "EmbeddingModel": "text-embedding-3-small"
  },
  "MobilityAssistant": {
    "DefaultSupplierRoundTrips": 4
  },
  "MobilityHistory": {
    "ExcelFolder": "Samples/MobilityAssistant/Historical/Data"
  }
}
```

### API Key — User Secrets (local development)

```bash
cd SkAgentWorkFlowStarter.Console
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
```

### API Key — Environment Variable (CI / containers)

```bash
export OPENAI_API_KEY=sk-...
```

> The `OpenAI:BaseUrl` setting allows the application to target any OpenAI-compatible endpoint
> (Azure OpenAI, a local proxy, etc.). Update `Model` accordingly.

---

## Running the Application

```bash
dotnet restore
dotnet run --project SkAgentWorkFlowStarter.Console/SkAgentWorkFlowStarter.Console.csproj
```

On startup the application:

1. Loads `BaselineForm.json` as the reference event document.
2. Ingests historical Excel data into the in-memory vector store.
3. Launches the interactive Mobility Form Agent chat loop.

---

## Docker

A multi-stage `Dockerfile` is included.

```bash
# Build from repository root
docker build -f SkAgentWorkFlowStarter.Console/Dockerfile -t sk-agent-poc .

# Run with API key injected at runtime
docker run -e OPENAI_API_KEY=sk-... sk-agent-poc
```

---

## Extending the Framework

### Adding a New Agent

Follow these steps to add an agent from scratch:

1. **Create the folder** `Samples/MyAgents/<NewAgent>/`.
2. **Define a typed input record** `Models/<NewInput>.cs`.
3. **Author a YAML prompt** `Prompts/<PromptName>.yaml`; declare it as `EmbeddedResource` in
   the `.csproj`.
4. **Create a tools class** `Tools/<NewTools>.cs` with `[KernelFunction]`-decorated methods.
5. **Implement the agent** — derive from `AgentBase<NewInput>`, set `PromptFileName`, and
   whitelist tools in `AuthorizedKernelFunctions`.
6. **Register in DI** (`Samples/DI.cs`) and in Semantic Kernel (`SkConfiguration.cs` via
   `Register<NewTools>(kb, sp)`).
7. **Create a service entrypoint** `<NewAgent>Service` that takes the agent via DI and exposes
   a clear public method.

```csharp
public class MyAgent(Kernel kernel, IPromptBuilder promptBuilder)
    : AgentBase<MyVariables>(kernel, promptBuilder)
{
    protected override string PromptFileName => "MyPrompt";
    protected override IEnumerable<KernelFunction> AuthorizedKernelFunctions =>
        GetKernelFunctionsForPlugin<MyTools>();
}
```

---

### Adding a New Workflow

1. **Create a class** `Samples/Workflow/<NewWorkflow>.cs` and build a `WorkflowDefinition` via
   `WorkflowBuilder`.
2. **Add tool classes** with the required `[KernelFunction]` steps and register them in
   `SkConfiguration.cs`.
3. **Create a service entrypoint** `<NewWorkflow>Service` that takes the typed workflow and
   `WorkflowRunner`.
4. **Register** workflow and service in `DI.cs`.

---

### Adding State to an Existing Agent

1. Define `TState : IAgentState` and a corresponding `TPatch`.
2. Implement `IAgentStateStore<TState, TPatch>` — handle merge logic and derived-field
   recomputation inside `ApplyPatch`.
3. Register the store in DI and add `AgentStateTools<TState, TPatch>` to the agent's
   `AuthorizedKernelFunctions`.

---

### Adding Semantic Memory

1. Define a record implementing `IAgentMemoryRecord` with `[VectorStoreKey]`,
   `[VectorStoreData]`, and `[VectorStoreVector]` attributes.
2. Implement `IAgentMemorySearch<TRecord>` and `IAgentMemoryIngestion<TRecord>`.
3. Register both interfaces in DI and add `AgentMemoryTools<TRecord>` to the agent's tool set.

---

## Tips & Gotchas

- **Secrets** — never hardcode the OpenAI key. Use .NET User Secrets locally and the
  `OPENAI_API_KEY` environment variable in all other environments.
- **Embedded prompts** — always declare YAML prompt files as `EmbeddedResource` in the
  `.csproj`. Missing resources surface at agent startup, not at call time, making them easy to
  catch in CI.
- **Typed inputs** — use `AgentRequest<TVariables>` to force required inputs at the call site.
  `AgentBase` maps the record's properties into `KernelArguments` and the Handlebars system
  prompt automatically.
- **Tool isolation** — keep tool classes scoped to a single agent or workflow. Avoid sharing
  plugin classes across agents; instead duplicate the few functions needed, or extract shared
  logic into a plain service and call it from both tool classes.
- **Service entrypoints** — keep agent and workflow services separate. Each should expose one
  clear public method and be the single external entry point for its capability.
- **Function allowlisting** — `AuthorizedKernelFunctions` is the only gate between the LLM and
  registered Kernel functions. Returning an empty enumerable from a plugin class effectively
  disables all its tools for that agent.
- **Plugin naming** — the `typeof(T).Name` convention (with generic suffixes stripped) is
  assumed by `GetKernelFunctionsForPlugin<T>()`. Register all plugins under this same name in
  `SkConfiguration.cs` to avoid silent lookup failures.

---

## Key Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.Extensions.Hosting` | 10.0.0 | DI, configuration, host lifecycle |
| `Microsoft.SemanticKernel` | 1.71.0 | Core SK — chat completion, plugins, function calling |
| `Microsoft.SemanticKernel.Connectors.AzureOpenAI` | 1.71.0 | OpenAI / Azure OpenAI connector |
| `Microsoft.SemanticKernel.Connectors.InMemory` | 1.71.0-preview | In-memory vector store |
| `Microsoft.SemanticKernel.PromptTemplates.Handlebars` | 1.68.0 | Handlebars prompt rendering |
| `Microsoft.Extensions.VectorData.Abstractions` | 9.7.0 | Vector store attribute model |
| `ClosedXML` | 0.104.2 | Excel ingestion for historical RAG data |
| `YamlDotNet` | 16.3.0 | YAML prompt file deserialization |

**Target framework:** `net10.0` · **Nullable reference types:** enabled · **Implicit usings:** enabled
