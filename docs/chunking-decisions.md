# Chunking Decisions

Two corpus types, two chunking strategies.

## Player stats (structured CSV/JSON)
- **One text record per player-week.** Each row (player × week) becomes one atomic,
  retrievable record. This honors the natural grain of the data and keeps each fact
  independently retrievable.
- **No overlap** — stat records are self-contained; there is nothing to "cut mid-thought."

## News / analysis (markdown)
- **Structural chunking** — split on `##`/`###` headers so each chunk is a coherent,
  author-defined topic.
- **10–20% overlap** on prose sections to avoid cutting mid-thought.
- **Target ~800 chars**, 500–1500 range.

## Embedding
- Start with `text-embedding-3-small`. Upgrade to `3-large` only if recall is
  insufficient — **fix chunking before swapping embedding models.**

## Retrieval
- **Hybrid** (keyword + vector): keyword handles exact player/team names; vector handles
  semantic intent. Top-K starts at 3, expand to 5 if answers miss context.
