# Data Sources

Candidates for populating `data/corpus/` (the real, gitignored corpus — see
`docs/chunking-decisions.md` for how each is chunked). Nothing here is wired into the ingestion
pipeline yet; this just records what to fetch from and why.

## Player stats

**[nflverse](https://github.com/nflverse/nflverse-data)** — recommended.
- Free, open-source, no API key. Weekly player stats, play-by-play, rosters, and schedules
  published as CSV/parquet files on GitHub releases.
- De facto standard in the fantasy/NFL analytics community; actively maintained.
- Maps almost exactly onto the existing "one record per player-week" chunking decision.
- Not a live REST API — periodically-refreshed static files. Fine for a weekly-cadence corpus,
  and simpler to ingest than a paginated JSON API.
- Wrappers available if preferred over raw file URLs: `nfl_data_py` (Python), `nflreadr` (R).

## News / analysis (RSS)

**[ESPN NFL RSS](https://www.espn.com/espn/rss/nfl/news)** — recommended.
- Free, no key, standard RSS/XML. General NFL news.

Fallbacks for more fantasy-specific analysis:
- Reddit `r/fantasyfootball` RSS: `https://www.reddit.com/r/fantasyfootball/.rss`
- Google News RSS search query (configurable, e.g. `news.google.com/rss/search?q=...`)
