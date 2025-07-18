# Architecture

This document outlines the high-level architecture for the WorldSeed template.

- **Orchestrator.API** - ASP.NET Core backend to manage agent lifecycles.
- **Orchestrator.UI** - Blazor Web App providing dashboard and task interface.
- **Agent.Runtime** - Console app running inside Docker container executing agent logic.
- **Shared** - Common DTOs and utilities shared between projects.

## Sandbox Setup

Agents normally run in isolated Docker containers with the following safeguards.
When Docker is unavailable the orchestrator can fall back to launching the agent
process locally.

Container safeguards include:

- **Resource Limits** – CPU and memory quotas applied per container.
- **Volumes** – a dedicated `/agent` volume is mounted for state.
- **Network** – can be disabled per agent for offline execution.
- **Seccomp Profile** – `docker/profiles/seccomp-agent.json` restricts system calls.
- **AppArmor** – profile `worldseed-agent` confines filesystem and network access.

The orchestrator passes these options when launching each container.
This logic is implemented in `AgentOrchestrator` via Docker.DotNet's `HostConfig` settings.

Both the orchestrator backend and the Blazor UI are meant to run on the same local machine. Because of this local setup, requests from the UI to the API do not require authentication.

## Plugin System

WorldSeed now supports loading additional tools at runtime from a `plugins` directory. The agent runtime scans this folder for `.dll` files and registers any `ITool` implementations they contain. This enables extending agent capabilities without modifying the core codebase.

When the runtime is built, any referenced plugin projects are automatically copied into this `plugins` folder so agents can start using them immediately.

The first example plugin is **Codex.Plugin** which registers a `codex` tool. The tool now supports basic repository interactions:

* `codex status` – shows the current `git status` output
* `codex branch` – displays the active branch name
* `codex ls [path]` – lists files within a directory
* `codex cat <file>` – dumps the contents of a file
* `codex diff [args]` – shows repository diffs
* `codex patch <file>` – applies a patch file
* `codex generate <instruction> [--files <paths>]` – uses the LLM to produce a patch, optionally including file context
* `codex autopatch <instruction> [--files <paths>] [--commit <msg>]` – generates a patch, applies it, and optionally commits with a message
* `codex annotate <file>` – summarizes a code file using the LLM
* `codex build [project]` – runs `dotnet build` for the specified project or solution
* `codex run [project]` – builds and executes a project using `dotnet run`
* `codex tools` – lists all loaded tools

These commands are a starting point for richer repository-aware features that will enable automated code modifications and analysis.

The Docker image now copies the plugin directory so these tools are immediately
available when running agents inside containers.

The Blazor interface now includes a **Codex** page so users can run plugin commands directly without creating an agent or overseer. The page shows the current status and recent Codex logs for visibility and provides a button to clear the logs.

The agent planner now keeps a set of previously executed actions to avoid repeating the same sub-goal. Its prompt explicitly instructs the language model to output only valid C# 12 code targeting .NET 8 to prevent language drift.

To reduce hallucinations and infinite loops, the planner now uses a **pass/fail rubric** and a short **self‑critique** after each draft. If the rubric marks a step as failed, the planner retries up to **three** times before giving up. A built‑in `dotnet` tool compiles the project with warnings treated as errors and runs simple checks so the rubric receives ground‑truth PASS/FAIL results.

### Next Cycle

* Provide an interactive preview before applying patches.
* Improve error handling when commits fail or patches do not apply.
* Add an undo command to revert the last Codex patch.
* Persist critiques in a store and escalate when retry budget is exhausted.
* Automate building and testing after each patch for ground-truth results.
