# fantasy-football-rag

A RAG system over a season of **NFL / fantasy football** data — player game logs plus
news/analysis articles. Ask natural-language questions ("How did Justin Jefferson perform
down the stretch in 2025?") and get accurate, grounded answers from the corpus.

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

## Project Structure

```
src/                 # C# console app (Azure.AI.OpenAI + Foundry)
scripts/
  provision-foundry.sh   # stand up the Foundry resource + model deployments
  teardown-azure.sh      # delete the resource group when done
data/
  sample-corpus/     # tiny committed sample (articles/ + stats/) so `load` runs out of the box
  corpus/            # full season snapshot — NOT committed (see .gitignore)
docs/                # design + chunking decisions
```

## Getting Started

> Prerequisites: .NET 8 SDK, Azure CLI (`az login`), a subscription that can create a Foundry resource.
> Optional: [`just`](https://github.com/casey/just) (`brew install just`) for the shortcuts below.

### With `just` (shortcut)

```bash
just provision   # stand up Foundry + model deployments
just model       # keyless model call (W1 done-bar #2)
just load        # parse the sample corpus (W1 done-bar #3)
```

Run `just` with no args to list all recipes. Or do it the long way:

```bash
# 1. Provision Foundry (resource + gpt-5-mini + text-embedding-3-small, GlobalStandard)
./scripts/provision-foundry.sh

# 2. Confirm src/appsettings.json points at your resource endpoint + deployment
#    (endpoint + deployment name are NOT secrets — safe to commit, since auth is keyless)

# 3. Verify the keyless model call works (W1 done-bar #2)
dotnet run --project src -- model

# 4. Load + parse the corpus (W1 done-bar #3)
dotnet run --project src -- load
```

Config lives in **`src/appsettings.json`** (committed — endpoint + deployment only). Environment
variables (`Foundry__Endpoint`, `Foundry__ChatDeployment`) override it for CI/containers. There are
**no API keys** — `DefaultAzureCredential` uses your `az login` session locally and managed identity
in Azure.

## Debugging (Zed)

`.zed/debug.json` defines two launch configs — **Debug: model** and **Debug: load**. Set
breakpoints in `src/Program.cs`, open the debug panel, pick a config, and run. Each builds
Debug first via `just build-debug`.

Requires the **`netcoredbg`** adapter. Zed offers to install it on first debug run; if it
doesn't, grab it from [Samsung/netcoredbg releases](https://github.com/Samsung/netcoredbg/releases)
and put it on your `PATH`.

## Roadmap

- [x] **W1** Environment + scaffold — Foundry provisioned, keyless model call, corpus parses
- [ ] **W2** Ingestion — summarize stats + header-chunk articles, embed, index to Azure AI Search
- [ ] **W3** Hybrid retrieval — Top-3 relevant chunks from console
- [ ] **W4** Chat interface — grounded answers end-to-end
- [ ] **W5** Evaluation — 20-question test set, recall + accuracy
- [ ] **W6** Polish — diagram, blog post, public repo

## Notes

- The Foundry resource is named `ff-foundry-cneff` / project `ff-rag`. A single Foundry
  resource can host multiple deployments/projects; rename or split later if needed.
