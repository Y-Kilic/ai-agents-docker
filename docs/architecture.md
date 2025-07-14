# Architecture

This document outlines the high-level architecture for the WorldSeed template.

- **Orchestrator.API** - ASP.NET Core backend to manage agent lifecycles.
- **Orchestrator.UI** - Blazor Web App providing dashboard and task interface.
- **Agent.Runtime** - Console app running inside Docker container executing agent logic.
- **Shared** - Common DTOs and utilities shared between projects.

## Sandbox Setup

Agents run in isolated Docker containers with the following safeguards:

- **Resource Limits** – CPU and memory quotas applied per container.
- **Volumes** – a dedicated `/agent` volume is mounted for state.
- **Network** – can be disabled per agent for offline execution.
- **Seccomp Profile** – `docker/profiles/seccomp-agent.json` restricts system calls.
- **AppArmor** – profile `worldseed-agent` confines filesystem and network access.

The orchestrator passes these options when launching each container.
