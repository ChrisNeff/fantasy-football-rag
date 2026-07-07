# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A RAG system over a season of NFL/fantasy football data (player game logs + news/analysis
articles), answering natural-language questions with grounded answers from the corpus. Currently
at **W2 (ingestion) done** of a 6-week build — see the Roadmap in README.md for what's done vs.
planned. Retrieval and chat are not yet built.

## Commands

Use `just` (task runner) where possible; run `just` with no args to list all recipes.

```bash
just build            # dotnet build
just build-debug      # Debug config build (used by Zed launch configs)
just model            # keyless Foundry chat call — proves auth works
just load [path]      # parse corpus (default: data/sample-corpus), prints file/record counts
just ingest [path]    # chunk + embed + index the corpus into Azure AI Search
just provision        # stand up Foundry resource + model deployments (./scripts/provision-foundry.sh)
just provision-search # stand up Azure AI Search, Free tier (./scripts/provision-search.sh)
just teardown         # delete all Azure resources for this project (./scripts/teardown-azure.sh)
```

Without `just`, the equivalent raw commands are `dotnet build` and
`dotnet run --project src -- <model|load|ingest> [path]`.

There is no test suite yet.

## Architecture & Debugging

Kept out of this file to keep its context footprint small:
- `docs/architecture.md` — decisions + rationale, code structure, config, auth, data-folder split
- `docs/goals.md` — why this project exists, success criteria, milestones
- `docs/use-cases.md` — what the system needs to do
- `README.md` — stack overview, setup, Zed debug configs

Read whichever's relevant to the task; otherwise the gotchas below cover what changes behavior
day to day.

## Gotchas

- **Auth is keyless everywhere** — `DefaultAzureCredential` only. Never introduce API
  keys/secrets for Foundry or Azure AI Search.
- Read `docs/chunking-decisions.md` before touching anything under `src/Ingestion/`.
- Never commit `data/corpus/` (full season snapshot) — only `data/sample-corpus/` is tracked.
