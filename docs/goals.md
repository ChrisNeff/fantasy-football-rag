# Goals & Success Criteria

## Goal

Build a RAG system over a season of NFL/fantasy football data — player game logs plus
news/analysis articles. Ask natural-language questions ("How did Justin Jefferson perform down
the stretch in 2025?" or "What was the injury situation around the Niners backfield?") and get
accurate, grounded answers from the corpus.

**Why fantasy football as the corpus:** it's a domain worth the motivation (traction), and it's
data-rich — weekly time-series stats, joins, and aggregations. That makes it a vehicle not just
for RAG (Phase 1) but for real Databricks/PySpark data engineering (Phase 2) and an agentic
lineup/trade assistant (Phase 3, targeting Microsoft Agent Framework). The domain is also
well-known enough to hand-verify answers without external fact-checking.

## Success Criteria

The MVP is done when **all three** are met:

| Criterion | Target |
|---|---|
| **Answer accuracy** | Correctly answers 15/20 hand-picked, self-verifiable validation questions |
| **Retrieval recall** | Relevant chunk appears in Top-3 results for ≥80% of test queries |
| **Coverage** | Test set spans both corpus types (stats + news/analysis) and a range of question styles (factual lookup, comparison, summarization) |

## Weekly Milestones

| Wk | Target Date | Focus | Done When | Status |
|---|---|---|---|---|
| 1 | Jun 28 | Environment + scaffold | Foundry provisioned, keyless model call, corpus parses | ✅ Done |
| 2 | ~Jul 3 | Ingestion + embedding | Stats summarized + articles header-chunked, embedded, indexed to Azure AI Search | ✅ Done |
| 3 | ~Jul 17 | Hybrid retrieval | Query returns Top-3 relevant chunks from console; hybrid (keyword + vector) search working | ⏳ Next |
| 4 | ~Jul 31 | SK chat interface | SK pipeline wired end-to-end; grounded answer from retrieved chunks; conversational console loop | Not started |
| 5 | ~Aug 14 | Evaluation + iteration | 20-question test set scored; recall/accuracy measured; chunking or embedding adjusted if below target | ✅ Done — see `docs/w5-eval-results.md` (recall 70%, accuracy 75%; comparison-question retrieval flagged as open gap) |
| 6 | ~Aug 21 | Polish + docs | Architecture diagram, blog post draft, README, repo public | Not started |

Dates are spaced ~1–2 weeks apart for one build session/week. Re-verify against `README.md`'s
Roadmap checklist, which is the quick at-a-glance status — this table is the detail behind it.

## Evaluation Approach (Week 5)

**Test set:** 20 questions, hand-picked and self-verifiable from FF domain knowledge — mix of:
1. Factual stat lookups ("How many receiving yards did X have in Week 10?")
2. Comparisons ("Who scored more PPR points, X or Y, over the last 4 weeks?")
3. News/context ("What was the reported injury status for X?")
4. Summarization ("Summarize the RB committee situation for team Z")

**Metrics:**
- **Retrieval recall** — is the correct chunk in the Top-3? (manual check)
- **Answer faithfulness** — does the answer match the corpus, without hallucination?
- **Answer completeness** — does it cover the key points?

**Trigger:** run after Wk4. If recall <80% → fix chunking. If recall is fine but answers are
poor → tune the prompt or increase Top-K.

## Answer-Generation Prompt (Week 4)

The default prompt template for Wk4's grounded-answer step is **CRISP-LO** (Context / Rules /
Inputs / Steps / Proof / Limits / Output) — enforces citing every claim to a source chunk ID or
labeling it "Unknown," which is the grounding discipline the success criteria above depend on.
Template lives in the vault at `Prompts/prompt-crisp-lo.md` if it needs to be pulled in here
verbatim when Wk4 starts.

## Deliverables

| Artifact | Status |
|---|---|
| GitHub repo | In progress (private until Wk6) |
| Architecture diagram | Not started |
| Blog post draft | Not started |

> The Marvel side-quest that used to live here has been split out into its own standalone
> project (idea-stage, decoupled from this repo's timeline) now that the Marvel Developer API
> makes it a real corpus rather than a quick detour. Tracked in the vault, not in this repo.
