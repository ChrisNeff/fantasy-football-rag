# Use Cases

Kept intentionally lightweight — one actor (you, via the console), no team handoff, so this
skips formal preconditions/postconditions/alternate-flow ceremony in favor of plain descriptions.
See the vault's SDD research if that tradeoff ever needs revisiting for a later phase.

## UC1 — Ingest the Corpus

**Command:** `just ingest [path]`

Scan a corpus directory (stats CSVs + article markdown), chunk each source per
`docs/chunking-decisions.md`, embed the chunks, and upload them to the Azure AI Search index.
Idempotent — re-running against the same corpus updates existing documents rather than
duplicating them (`MergeOrUpload`, deterministic chunk IDs).

- **Input:** `data/sample-corpus/` by default, or a path to the full season corpus (`data/corpus/`, gitignored)
- **Output:** a populated, queryable Azure AI Search index
- **Status:** done (Wk2)

## UC2 — Ask a Fantasy Football Question

**Command:** *(not yet built — Wk3–4)*

Given a natural-language question, hybrid-retrieve the Top-K most relevant chunks from the
index, pass them to `gpt-5-mini` via a grounded prompt (CRISP-LO template — see
`docs/goals.md`), and return an answer that cites its source chunks or says "Unknown" rather
than guessing.

- **Input:** a question typed at the console (e.g. "How did Justin Jefferson perform down the stretch in 2025?")
- **Output:** a grounded answer + the chunk IDs it drew from
- **Status:** retrieval not started (Wk3), generation not started (Wk4)

## UC3 — Evaluate Answer Quality

**Command:** *(not yet built — Wk5)*

Run a fixed 20-question test set (mix of factual lookup, comparison, news/context, and
summarization questions) through UC2, then score retrieval recall (is the right chunk in the
Top-3?), answer faithfulness (no hallucination), and completeness. Feeds back into chunking or
prompt tuning if the success criteria in `docs/goals.md` aren't met.

- **Input:** the 20-question validation set (hand-authored, self-verifiable)
- **Output:** a scored report against the three success criteria
- **Status:** not started (Wk5)

