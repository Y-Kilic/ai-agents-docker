# Architecture

This document outlines the high-level architecture for the WorldSeed template.

- **Orchestrator.API** - ASP.NET Core backend to manage agent lifecycles.
- **Orchestrator.UI** - Blazor Web App providing dashboard and task interface.
- **Agent.Runtime** - Console app running inside Docker container executing agent logic.
- **Shared** - Common DTOs and utilities shared between projects.
