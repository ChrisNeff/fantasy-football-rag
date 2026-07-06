# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A RAG system over a season of NFL/fantasy football data (player game logs + news/analysis
articles), answering natural-language questions with grounded answers from the corpus. Currently
at **W1 (scaffold)** of a 6-week build — see the Roadmap in README.md for what's done vs. planned.
Only two commands exist today (`model`, `load`); ingestion, retrieval, and chat are not yet built.

## Commands

Use `just` (task runner) where possible; run `just` with no args to list all recipes.

```bash
just build         # dotnet build
just build-debug    # Debug config build (used by Zed launch configs)
just model          # keyless Foundry chat call — proves auth works
just load [path]     # parse corpus (default: data/sample-corpus), prints file/record counts
just provision       # stand up Foundry resource + model deployments (./scripts/provision-foundry.sh)
just teardown        # delete all Azure resources for this project (./scripts/teardown-azure.sh)
```

Without `just`, the equivalent raw commands are `dotnet build` and
`dotnet run --project src -- <model|load> [path]`.

There is no test suite yet.

## Architecture

```
Stats (CSV) + Articles (Markdown)
        │
        ▼  [Ingestion]  chunk → embed → index        (W2, not built)
  Azure AI Search
        │
   Query ──► hybrid retrieve Top-K ──► grounded prompt ──► gpt-5-mini ──► Answer   (W3–W4, not built)
```

- **Single-file console app** (`src/Program.cs`) — a top-level-statements `switch` on
  `args[0]` dispatches to command handlers (`RunModelCheck`, `RunCorpusLoad`). New commands
  follow this pattern until it outgrows a single file.
- **Config**: `src/appsettings.json` is the primary source (endpoint + deployment names —
  safe to commit, no secrets, since auth is keyless). Environment variables
  (`Foundry__Endpoint`, `Foundry__ChatDeployment`) override it, read via
  `Microsoft.Extensions.Configuration`.
- **Auth is keyless everywhere**: `DefaultAzureCredential` — local `az login` session in dev,
  managed identity in Azure. Never introduce API keys/secrets for Foundry or Azure AI Search.
- **Chunking strategy** (see `docs/chunking-decisions.md` — read before touching ingestion):
  - Stats: one record per player-week row, no overlap (atomic, self-contained facts).
  - Articles: structural chunking on `##`/`###` headers, 10–20% overlap on prose,
    target ~800 chars (500–1500 range).
  - Embedding starts at `text-embedding-3-small`; fix chunking before considering
    `3-large`.
  - Retrieval will be **hybrid** (keyword + vector) via `Azure.Search.Documents` raw SDK
    (not a higher-level wrapper) for full control — keyword for exact player/team names,
    vector for semantic intent, Top-K starting at 3.
- **Data**: `data/sample-corpus/` (committed, tiny, keeps `load` runnable out of the box) vs.
  `data/corpus/` (full season snapshot, gitignored — never commit this).
- **Phase 3 (agents)**, once reached, targets **Microsoft Agent Framework**
  (`Microsoft.Agents.AI`) — the Semantic Kernel successor, not the legacy SK `Kernel` API.

## Debugging (Zed)

`.zed/debug.json` has two launch configs (**Debug: model**, **Debug: load**) using the
`netcoredbg` adapter; each runs `just build-debug` first. Breakpoints go in `src/Program.cs`.
