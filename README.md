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

```bash
# 1. Provision Foundry (resource + gpt-5-mini + text-embedding-3-small, GlobalStandard)
./scripts/provision-foundry.sh
# copy the endpoint it prints, then:
export AZURE_OPENAI_ENDPOINT="https://ff-foundry-cneff.cognitiveservices.azure.com/"
export CHAT_DEPLOYMENT="gpt-5-mini"

# 2. Verify the keyless model call works (W1 done-bar #2)
dotnet run --project src -- model

# 3. Load + parse the corpus (W1 done-bar #3)
dotnet run --project src -- load
```

There are **no API keys** — `DefaultAzureCredential` uses your `az login` session locally and
managed identity in Azure.

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
