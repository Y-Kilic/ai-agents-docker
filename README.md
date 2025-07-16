# Autonomous Agent Network Orchestrator (C# / Blazor / Docker)

## 🧠 Overview

This project is a **modular AI agent framework** built in **C#/.NET**, designed to orchestrate, isolate, and operate autonomous agents in **sandboxed Docker containers**.

Each agent operates independently with its own reasoning loop, memory, and tools. A central orchestrator manages their lifecycle and coordination.

The frontend is implemented in **Blazor** to provide a live dashboard, task interface, and system visibility.

---

## Getting Started

This repository now includes a .NET solution with starter projects. To build the entire solution run:

```bash
dotnet build WorldSeed.sln
```

Use `scripts/devtools.ps1` for simple dev workflows.

Both the backend (Orchestrator.API) and the Blazor UI run on the same local machine. Calls between them do not require any authentication.

### Running without Docker

If Docker is not available on your system you can still run the agents locally.
Set the environment variable `USE_LOCAL_AGENT=1` before starting the orchestrator
and it will launch agent processes directly using `dotnet run` instead of
spinning up containers.

### OpenAI Configuration

Set the environment variable `OPENAI_API_KEY` before starting the orchestrator
if you want the agents to use the real OpenAI API. When this variable is not
present the runtime falls back to a mock provider that simply echoes prompts.

### Agent Loop Count

The number of iterations an agent performs can be specified by setting the
`LOOP_COUNT` environment variable or by passing a `loops` value when starting an
agent through the API. If neither is supplied, the agent defaults to **5** loops.
The agent will run until either it receives the `DONE` signal from the LLM or
the configured loop count is reached. Specify `0` or any negative value to run
indefinitely until the LLM returns `DONE`. On each iteration the agent sends its
full history to the LLM provider so actions are planned with complete context.

### Expected LLM Response Format

The planner parses only the first line of the LLM response and expects the
format `<tool> <input>` or the single word `DONE`. If the returned tool name does
not match one of the registered tools the agent falls back to the `chat` tool.
Ensure your language model is prompted to follow this format precisely. When
using other models or custom prompts, verify that the first word corresponds to
a valid tool name such as `chat`, `echo`, `list`, `compare`, or `web`.
When calling the `web` tool, place the URL in quotes, for example:
`web "https://example.com"`. Unquoted URLs like `web https://example.com` are
also accepted, but quoting ensures the parser handles spaces correctly.

When an unknown tool is encountered the agent reuses the `chat` tool to handle
the response. Such fallbacks do not count toward the configured loop limit, but
after three consecutive unknown responses the agent stops to avoid an infinite
loop.

### Keeping Containers Alive

Agents normally exit after the loop completes. The runtime now waits
indefinitely so logs continue streaming unless `KEEP_ALIVE=0` is provided. The
orchestrator explicitly sets `KEEP_ALIVE=1` when launching agents so they remain
running until stopped.

### Agent Connectivity

Agents run inside Docker containers which by default use Docker's bridge
network. This means they can reach the internet to download packages or
fetch remote data. If your host has a restrictive firewall, ensure the
containers are allowed outbound access or run the orchestrator with a
custom network mode, for example `--network host`.

### Terminal Access

Agents now have direct terminal access. When prompted, the LLM should respond
with the exact shell command to run (or `DONE`). The runtime executes the command
and returns a JSON object with `exit_code`, truncated `stdout`, `stderr`, and any
detected file output. Example:

```
echo hi > out.txt
```

Produces:

```
{"exit_code":0,"stdout":"","stderr":"","side_effect":"wrote 3 bytes to out.txt"}
```

## ⚙️ Architecture

```text
+-------------------+           +--------------------------+
|   Blazor Frontend | ◀───────▶ | ASP.NET Core Orchestrator|
+-------------------+           +-----------+--------------+
                                              |
                                              | Docker.DotNet API
                                              v
                           +------------------------------+
                           | Docker Runtime (Isolated Agents)
                           |  +--------------------------+ |
                           |  |   Agent Container N      | |
                           |  |   └── Goal Executor      | |
                           |  |   └── Memory Vectorizer  | |
                           |  |   └── Toolchain Runner   | |
                           |  +--------------------------+ |
                           +------------------------------+


---

🧩 Components

1. Orchestrator.API

ASP.NET Core backend

Responsibilities:

Accept tasks from UI

Launch/terminate agent containers via Docker.DotNet

Expose agent status and logs over REST

Assign goals, environment variables, or task definitions



2. Orchestrator.UI (Blazor Server)

Live dashboard UI for:

Agent creation and management

Task submission

Real-time log and memory inspection

The UI now includes a **Logs** page that polls the API for messages from each
agent and displays them in real time. A new **Memory** page shows the latest
memory entries reported by each agent.



3. Agent.Runtime

C# console app compiled into a Docker image

Executes the agent reasoning loop

Communicates with orchestrator via REST

Supports goal decomposition, vector memory, and tool invocation



---

📦 Directory Layout

/src
  /Orchestrator.API        ← ASP.NET Core backend logic
  /Orchestrator.UI         ← Blazor Server frontend
  /Agent.Runtime           ← Dockerized agent logic
  /Shared                  ← Shared models, DTOs, protocols

/docker
  /agent.Dockerfile        ← Docker image definition for agents

/scripts
  /devtools.ps1            ← CLI helper for starting/stopping agents

/docs
  /architecture.md         ← Extended architecture & flow


---

🔐 Isolation Model

Each agent runs in its own Docker container, with:

Memory/CPU limits (configurable)

Separate mounted volumes (/agent/memory)

Optional network isolation


Communication via:

REST back to orchestrator


---

🚀 Deployment & Execution

🔧 Prerequisites

.NET 8 SDK

Docker Desktop

Optional: Redis or message queue for shared memory


🛠️ Build All

dotnet build ./src

🐳 Build Agent Image

docker build -f ./docker/agent.Dockerfile -t worldseed-agent:latest .

Building the solution automatically removes the previous `worldseed-agent:latest` image and rebuilds it so everything stays up-to-date.

The build also compiles the Agent.Runtime project in Release mode so the Docker
context contains the latest binaries. The agent image now installs **Chromium**
and **ChromeDriver** so the Selenium-based `web` tool can fetch websites during
execution.

▶️ Run Orchestrator Locally

cd src/Orchestrator.API
dotnet run

Once running, start an agent with:

```bash
curl -X POST "http://localhost:5000/api/agent/start" \
     -H "Content-Type: application/json" \
     -d '{"goal":"echo hello","loops":5}'
```

```bash
# list running agents
curl http://localhost:5000/api/agent/list

# stop an agent
curl -X POST http://localhost:5000/api/agent/<id>/stop
```

🌐 Access Dashboard

Visit: http://localhost:5000 (Blazor UI)


---

🔄 Agent Lifecycle

1. User submits goal via UI


2. Orchestrator generates unique agent ID and assigns metadata


3. Orchestrator spins up a Docker container:

```bash
docker run -d --rm \
  --name agent-{id} \
  worldseed-agent:latest --goal="..."
```


4. Agent begins loop: Thought → Tool → Action → Memory


5. Agent saves logs thar can be obtained by the REST API


6. Orchestrator tears down or persists agent state




---

📡 Communication Protocols

Component	Protocol	Description

UI ↔ Orchestrator	REST	Live UI sync, task submission
Orchestrator ↔ Agent	REST	Commands, status, goal injection
Agent ↔ Tools	Local call	CLI, file system, internal logic



---

🧠 Agent Capabilities

Each agent supports:

Goal decomposition using LLM APIs

Memory vectorization (e.g., using embeddings)

Execution of tools (e.g., web fetch, calculator, file ops)

Dynamic tool loading (via plugin system or REST)



---

🧪 Extending the System

To add a new tool:

1. Define it in Agent.Runtime/Tools/


2. Register in ToolRegistry.cs


3. Allow goal planner to discover and call it



To add a new agent profile:

Extend AgentConfig DTO

Mount custom volumes or ENV vars in orchestrator



---

📍 Status

Feature	Status

Multi-agent spawning	✅ Working
Docker orchestration	✅ Working
Blazor dashboard	✅ Basic UI
LLM integration (OpenAI)	✅
Mock LLM provider       ✅
When OPENAI_API_KEY is not set, the runtime uses a mock provider that echoes prompts.
Repository pattern & unit of work       ✅
Basic message hub           ✅
Agent memory / embeddings       ✅ Basic loop
Tool execution framework	✅
Container sandboxing    ✅
Inter-agent comms	🚧 Planned



---

🤖 AI Handoff Metadata

Language: C#

Runtime: .NET 8

Containerized: Yes (agents)

Orchestrator: Docker.DotNet + ASP.NET Core

Frontend: Blazor Server

Communication: REST



---

🧭 Next Steps (For Agent Developers)

Add custom agent behavior in /Agent.Runtime

Build visual status activity graph in Blazor UI

---

🛡️ Contact / Control Handoff

This project is ready for autonomous modification by intelligent agents with C#/.NET and container orchestration capabilities.

AI_CONTROL_PROTOCOL = [
  { ROLE: "ORCHESTRATOR", COMMAND: "START_AGENT", PARAMS: goal },
  { ROLE: "AGENT", COMMAND: "THINK_ACT_LOOP", PARAMS: goal_context },
  { ROLE: "TOOL", COMMAND: "EXECUTE", PARAMS: task },
]
