# Autonomous Agent Network Orchestrator (C# / Blazor / Docker)

## 🧠 Overview

This project is a **modular AI agent framework** built in **C#/.NET**, designed to orchestrate, isolate, and operate autonomous agents in **sandboxed Docker containers**.

Each agent operates independently with its own reasoning loop, memory, and tools. A central orchestrator manages their lifecycle and coordination.

The frontend is implemented in **Blazor Server** to provide a live dashboard, task interface, and system visibility.

---

## Getting Started

This repository now includes a .NET solution with starter projects. To build the entire solution run:

```bash
dotnet build WorldSeed.sln
```

Use `scripts/devtools.ps1` for simple dev workflows.

### Running without Docker

If Docker is not available on your system you can still run the agents locally.
Set the environment variable `USE_LOCAL_AGENT=1` before starting the orchestrator
and it will launch agent processes directly using `dotnet run` instead of
spinning up containers.

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

Expose agent status and logs over REST/SignalR

Assign goals, environment variables, or task definitions



2. Orchestrator.UI (Blazor Server)

Live dashboard UI for:

Agent creation and management

Task submission

Real-time log and memory inspection



3. Agent.Runtime

C# console app compiled into a Docker image

Executes the agent reasoning loop

Communicates with orchestrator via gRPC/REST

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

Seccomp & AppArmor profiles enforced


Communication via:

gRPC or REST back to orchestrator

Message queue (optional extension)



---

🚀 Deployment & Execution

🔧 Prerequisites

.NET 8 SDK

Docker Desktop

Optional: Redis or message queue for shared memory


🛠️ Build All

dotnet build ./src

🐳 Build Agent Image

docker build -f ./docker/agent.Dockerfile -t myagent:latest .

▶️ Run Orchestrator Locally

cd src/Orchestrator.API
dotnet run

Once running, start an agent with:

```bash
curl -X POST "http://localhost:5000/api/agent/start" \
     -H "Content-Type: application/json" \
     -d '{"goal":"echo hello"}'
```

```bash
# list running agents
curl http://localhost:5000/api/agent/list

# stop an agent
curl -X POST http://localhost:5000/api/agent/<id>/stop
```

🌐 Access Dashboard

Visit: http://localhost:5000 (Blazor Server UI)


---

🔄 Agent Lifecycle

1. User submits goal via UI


2. Orchestrator generates unique agent ID and assigns metadata


3. Orchestrator spins up a Docker container:

```bash
docker run -d --rm \
  --name agent-{id} \
  --security-opt seccomp=./docker/profiles/seccomp-agent.json \
  --security-opt apparmor=worldseed-agent \
  myagent:latest --goal="..."
```


4. Agent begins loop: Thought → Tool → Action → Memory


5. Agent reports status/results via gRPC/REST


6. Orchestrator tears down or persists agent state




---

📡 Communication Protocols

Component	Protocol	Description

UI ↔ Orchestrator	SignalR/REST	Live UI sync, task submission
Orchestrator ↔ Agent	REST/gRPC	Commands, status, goal injection
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
JWT authentication          ✅
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

Communication: REST + SignalR (agents optional gRPC)



---

🧭 Next Steps (For Agent Developers)

Add custom agent behavior in /Agent.Runtime

Extend the orchestrator controller to support agent types

Secure container sandboxing (AppArmor, seccomp, etc.)

Add logs and telemetry collection per agent

Build visual memory graph in Blazor UI

Add unit tests for core libraries

Use a strong JWT signing key via `appsettings.json`



---

🛡️ Contact / Control Handoff

This project is ready for autonomous modification by intelligent agents with C#/.NET and container orchestration capabilities.

AI_CONTROL_PROTOCOL = [
  { ROLE: "ORCHESTRATOR", COMMAND: "START_AGENT", PARAMS: goal },
  { ROLE: "AGENT", COMMAND: "THINK_ACT_LOOP", PARAMS: goal_context },
  { ROLE: "TOOL", COMMAND: "EXECUTE", PARAMS: task },
]

Let me know if you want this turned into a real repo skeleton with code next.

