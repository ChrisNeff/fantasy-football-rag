# fantasy-football-rag

A RAG system over a season of **NFL / fantasy football** data — player game logs plus
news/analysis articles. Ask natural-language questions ("How did Justin Jefferson perform
down the stretch in 2025?") and get accurate, grounded answers from the corpus.

See [`docs/goals.md`](docs/goals.md) for the full goal, success criteria, milestones, and
evaluation approach, and [`docs/use-cases.md`](docs/use-cases.md) for what the system needs
to do.

## Stack

- **Platform:** Microsoft Foundry — keyless auth via `DefaultAzureCredential` (no API keys)
- **Models:** `gpt-5-mini` (chat) + `text-embedding-3-small` (embeddings), deployed `GlobalStandard`
- **SDK:** `Azure.AI.OpenAI` 2.x against the Foundry endpoint
- **Search:** Azure AI Search (hybrid: keyword + vector) — raw SDK for full hybrid-search control
- **Chunking:** stats = one record per player-week; articles = structural on `##`/`###` headers
- **Console UI:** [Spectre.Console](https://github.com/spectreconsole/spectre.console)

> **Phase 3 (agents)** targets **Microsoft Agent Framework** (`Microsoft.Agents.AI`), Semantic
> Kernel's successor — not the legacy SK `Kernel`.

## Architecture

```
Stats (CSV) + Articles (Markdown)
        │
        ▼  [Ingestion]  chunk → embed → index        (W2)
  Azure AI Search
        │
   Query ──► hybrid retrieve Top-K ──► grounded prompt ──► gpt-5-mini ──► Answer   (W3–W4)
```

Full architecture (decisions + rationale, code structure, config, auth, data-folder split) is
in [`docs/architecture.md`](docs/architecture.md).

## Project Structure

```
src/                 # C# console app (Azure.AI.OpenAI + Foundry)
scripts/
  provision-foundry.sh   # stand up the Foundry resource + model deployments
  teardown-azure.sh      # delete the resource group when done
data/
  sample-corpus/     # tiny committed sample (articles/ + stats/) so `load` runs out of the box
  corpus/            # full season snapshot — NOT committed (see .gitignore)
docs/
  goals.md           # why this project exists, success criteria, milestones, evaluation
  use-cases.md       # what the system needs to do
  architecture.md    # decisions + rationale, code structure, config, auth
  chunking-decisions.md
  data-sources.md
  w2-ingestion-plan.md
```

## Getting Started

> Prerequisites: .NET 8 SDK, Azure CLI (`az login`), a subscription that can create a Foundry resource.
> Optional: [`just`](https://github.com/casey/just) (`brew install just`) for the shortcuts below.

### With `just` (shortcut)

```bash
just provision         # stand up Foundry + model deployments
just provision-search  # stand up Azure AI Search (Free tier, roles-only auth)
just model             # keyless model call (W1 done-bar #2)
just load              # parse the sample corpus (W1 done-bar #3)
just ingest            # chunk + embed + index the sample corpus (W2)
```

Run `just` with no args to list all recipes. Or do it the long way:

```bash
# 1. Provision Foundry (resource + gpt-5-mini + text-embedding-3-small, GlobalStandard)
./scripts/provision-foundry.sh

# 2. Provision Azure AI Search (Free tier, roles-only auth)
./scripts/provision-search.sh

# 3. Confirm src/appsettings.json points at your resource endpoints + deployment/index names
#    (endpoint + deployment/index names are NOT secrets — safe to commit, since auth is keyless)

# 4. Verify the keyless model call works (W1 done-bar #2)
dotnet run --project src -- model

# 5. Load + parse the corpus (W1 done-bar #3)
dotnet run --project src -- load

# 6. Chunk + embed + index the corpus into Azure AI Search (W2)
dotnet run --project src -- ingest
```

Config lives in **`src/appsettings.json`** (committed — endpoints + deployment/index names only).
Environment variables (`Foundry__Endpoint`, `Foundry__ChatDeployment`, `Search__Endpoint`,
`Search__IndexName`) override it for CI/containers. There are **no API keys** —
`DefaultAzureCredential` uses your `az login` session locally and managed identity in Azure.

## Debugging (Zed)

`.zed/debug.json` defines three launch configs — **Debug: model**, **Debug: load**, and
**Debug: ingest**. Set
breakpoints in `src/Program.cs`, open the debug panel, pick a config, and run. Each builds
Debug first via `just build-debug`.

Requires the **`netcoredbg`** adapter. Zed offers to install it on first debug run; if it
doesn't, grab it from [Samsung/netcoredbg releases](https://github.com/Samsung/netcoredbg/releases)
and put it on your `PATH`.

## Roadmap

- [x] **W1** Environment + scaffold — Foundry provisioned, keyless model call, corpus parses
- [x] **W2** Ingestion — summarize stats + header-chunk articles, embed, index to Azure AI Search
- [x] **W3** Hybrid retrieval — Top-3 relevant chunks from console
- [x] **W4** Chat interface — grounded answers end-to-end
- [x] **W5** Evaluation — validation-question eval, accuracy 60% → 75% via targeted pre-aggregation (see Decisions)
- [ ] **W6** Polish — architecture.md + README updated; diagram, blog post, and making the repo public are still open

## Notes

- The Foundry resource is named `ff-foundry-cneff` / project `ff-rag`. A single Foundry
  resource can host multiple deployments/projects; rename or split later if needed.
