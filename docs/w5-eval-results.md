# W5 Evaluation — 20-Question Test Set & Scoring

**Date:** 2026-08-14
**Corpus:** Real 2024 NFL regular season (nflverse), top-40 WR/TE by season PPR points, 626 stat
chunks + 18 article sections (644 total) indexed into `ff-corpus`. See
`data/corpus/stats/2024-wr-te-season.csv` and `data/corpus/articles/*.md`.

Per `docs/goals.md` Week 5 plan: 20 hand-picked, self-verifiable questions across 4 styles.
Ground truth below is derived directly from the corpus files (authored/transformed by us, so
it's exactly checkable). For each question: run `just query "<question>" hybrid` to check
**retrieval recall** (is the right chunk in Top-3?), then `just chat` to check the **generated
answer** (faithfulness + completeness).

**Success targets (docs/goals.md):** 15/20 correct answers, ≥80% Top-3 retrieval recall.

---

## Factual stat lookups (5)

| # | Question | Ground truth | Source chunk | Recall (Top-3)? | Answer correct? | Notes |
|---|---|---|---|---|---|---|
| 1 | How many receiving yards did Ja'Marr Chase have in Week 5, 2024? | 193 yards | stats row: Chase Wk5 | ✅ rank 1 | | |
| 2 | How many targets did Brock Bowers see in Week 1, 2024? | 8 targets | stats row: Bowers Wk1 | ✅ rank 1 | | |
| 3 | How many receptions did Amon-Ra St. Brown have in Week 10, 2024? | 6 receptions | stats row: St. Brown Wk10 | ✅ rank 1 | | |
| 4 | What was Terry McLaurin's PPR point total in Week 12, 2024? | 21.2 PPR | stats row: McLaurin Wk12 | ✅ rank 1 | | |
| 5 | How many receiving touchdowns did CeeDee Lamb have in Week 3, 2024? | 0 TD | stats row: Lamb Wk3 | ✅ rank 1 | | |

## Comparisons (5)

| # | Question | Ground truth | Source chunk(s) | Recall (Top-3)? | Answer correct? | Notes |
|---|---|---|---|---|---|---|
| 6 | Who had more receiving yards in Week 1, 2024 — Ja'Marr Chase or Justin Jefferson? | Chase (62 vs 59) | stats rows: both Wk1 | ❌ Jefferson absent — all 3 slots are Chase (article + Wk1 + Wk4) | | |
| 7 | Who scored more PPR points over the 2024 season, Brian Thomas Jr. or Malik Nabers? | Thomas (284.0 vs 273.6) | stats rows: both, all weeks | ❌ Thomas absent — all 3 slots are Nabers weeks | | |
| 8 | Who had more receptions in the 2024 season, Amon-Ra St. Brown or Brock Bowers? | St. Brown (115 vs 112) | stats rows: both, all weeks | ❌ Bowers absent — all 3 slots are St. Brown weeks | | |
| 9 | Who had a higher PPR total in 2024, Puka Nacua or Tyreek Hill? | Hill (218.2 vs 206.6) | stats rows: both, all weeks | ❌ Hill absent — all 3 slots are Nacua weeks | | |
| 10 | Whose team had more receiving touchdowns in 2024 — Terry McLaurin or CeeDee Lamb? | McLaurin (13 vs 6) | stats rows: both, all weeks | ❌ McLaurin absent — all 3 slots are Lamb weeks | | |

## News / context (5)

| # | Question | Ground truth | Source chunk | Recall (Top-3)? | Answer correct? | Notes |
|---|---|---|---|---|---|---|
| 11 | Why did Puka Nacua only play 11 games in 2024? | Knee injury | articles/2024-injury-report-wr.md sec1 | ✅ rank 1 | | |
| 12 | What happened to Dak Prescott in 2024 and how did it affect CeeDee Lamb? | Hamstring injury ended Prescott's season; Lamb's efficiency dropped w/o him | articles/2024-nfc-east-passing-attacks.md sec1 | ✅ rank 1 | | |
| 13 | Who set the NFL rookie tight end receptions record in 2024? | Brock Bowers (112 catches) | articles/2024-rookie-te-brock-bowers.md sec1 | ✅ rank 1 | | |
| 14 | Why was Tyreek Hill's 2024 season considered a down year despite playing all 17 games? | Tua Tagovailoa missed games w/ concussion, hurting the passing game around Hill | articles/2024-injury-report-wr.md sec2 | ✅ rank 1 | | |
| 15 | Which rookie quarterback's breakout season boosted Terry McLaurin's production in 2024? | Jayden Daniels | articles/2024-nfc-east-passing-attacks.md sec2 | ✅ rank 1 | | |

## Summarization (5)

| # | Question | Ground truth (key points) | Source chunk(s) | Recall (Top-3)? | Answer correct? | Notes |
|---|---|---|---|---|---|---|
| 16 | Summarize why Ja'Marr Chase won the receiving triple crown in 2024. | Led NFL in rec/yards/TD; slow start then historic 2nd half from Wk5; Bengals D forced shootouts, Higgins injuries concentrated targets | articles/2024-afc-north-wr-report.md (3 sections) | ⚠️ only sec0 (triple-crown) in top 3; sec1 (slow start/2nd half) & sec2 (Bengals D context) crowded out by 2 stat rows | | |
| 17 | Summarize why Brian Thomas Jr. and Malik Nabers were fantasy-relevant rookies in 2024 despite shaky QB play. | Both led on raw volume/target share despite unstable QB situations (Lawrence injuries; Jones benched/released) | articles/2024-rookie-wr-class.md (3 sections) | ❌ Thomas section (sec0) absent; got Nabers (sec1) + own conclusion (sec2) + a wrong-article chunk (nfc-east's QB-dependency section, semantically similar but not about these two rookies) | | |
| 18 | Summarize Detroit's passing offense in 2024 and Amon-Ra St. Brown's role in it. | St. Brown 3rd among WRs in PPR via consistency, not one big week; 12 TD tied for league lead; scheme insulated passing game from O-line injuries | articles/2024-nfc-north-passing-attack.md (3 sections) | ⚠️ only sec1 (St. Brown) in top 3; sec2 (O-line injuries) crowded out by 2 stat rows | | |
| 19 | Summarize what happened with Puka Nacua and Tyreek Hill in 2024 and why their fantasy production suffered. | Nacua: own knee injury capped games (11). Hill: stayed healthy but Tua's absences hurt the offense around him | articles/2024-injury-report-wr.md (3 sections) | ❌ Nacua's own article section (sec1) absent — got Hill (sec2, correct) + wrong-article chunk (rookie-wr-class conclusion) + a Nacua stat row (not the injury narrative) | | |
| 20 | Summarize Brock Bowers' 2024 rookie season and why the Raiders relied on him so heavily. | Rookie TE receptions record (112); thin WR corps behind him meant heavy target share across multiple QBs; usage likely sticky going forward | articles/2024-rookie-te-brock-bowers.md (3 sections) | ⚠️ only sec2 (Raiders reliance) in top 3; sec1 (the actual 112-catch record recap) crowded out by 2 stat rows | | |

---

## Scoring Summary

| Metric | Before tuning | After season-rollup fix | Target | Pass? |
|---|---|---|---|---|
| Retrieval recall (Top-3) | 13/20 (65%) | 14/20 (70%) | ≥80% (16/20) | ❌ still short |
| Answer accuracy | 12/20 (60%) | **15/20 (75%)** | ≥75% (15/20) | ✅ met |

**By category:**

| Style | Recall | Accuracy |
|---|---|---|
| Factual lookups (1–5) | 5/5 | 5/5 |
| Comparisons (6–10) | 0/5 | 0/5 |
| News/context (11–15) | 5/5 | 5/5 |
| Summarization (16–20) | 3/5 | 2/5 |

**Root cause — not a chunking problem.** Single-fact lookups and news/context questions hit
100% on both metrics: the right chunk lands at rank 1 every time, and the CRISP-LO answer is
correct and cited. The failures cluster entirely in questions that need *more than one chunk's
worth of information*:

- **Comparisons (6–10):** each needs a chunk for *both* named players, but `topK=3` under hybrid
  search consistently returns 3 chunks for whichever player scores higher on keyword/vector
  match — the second player is completely absent, 0/5 times. The chat command's CRISP-LO
  prompt handled this well from a *faithfulness* standpoint — it never hallucinated a winner,
  it correctly said "I don't have that information in my dataset" every time — but that's still
  0/5 on answer accuracy since it never actually answers the question.
- **Summarization (16–20):** each source article is split into 3 structural chunks (per
  `docs/chunking-decisions.md`), but `topK=3` often gets crowded out by 1–2 stat rows that
  keyword-match a player's name, leaving only 1 of the 3 relevant sections in context. Two of
  five (Q17, Q19) lost an entire sub-topic this way — Q17's answer never mentions Brian Thomas
  Jr. at all, and Q19 refused to answer the whole question because Nacua's context was missing,
  even though Hill's was available.

**Good news:** zero hallucinations across all 20 questions, including the 8 that "failed" —
the CRISP-LO prompt's grounding discipline (cite every claim to a chunk ID, or say "Unknown")
is working exactly as `docs/goals.md` intended. This is a retrieval-coverage problem, not a
prompt-faithfulness problem.

## Tuning Log

**Fix applied (2026-08-14): season-rollup chunks.** `StatsChunker.ChunkSeasonRollups` (new)
computes one pre-aggregated chunk per player — season totals for targets/receptions/yards/TD/PPR
— alongside the existing 626 per-week rows. Adds 40 chunks (`SourceType: "stat-season"`),
684 total after re-ingest. `ChatCommand.cs` also gained a non-interactive stdin fallback so the
20-question sweep can be piped in for repeatable scoring (`Console.IsInputRedirected` check —
`AnsiConsole.Ask` throws on redirected input, which blocked batch eval runs entirely).

**Result:** answer accuracy went 12/20 → 15/20, clearing the 75% target. Recall went
13/20 → 14/20 — real but partial:

- **Q7** (season-total comparison) flipped fully — both players' rollup chunks landed in Top-3,
  answer is now correct with exact numbers.
- **Q20** (Bowers summary) gained the season-recap numbers it was missing (112 catches etc.),
  making an already-adequate answer complete.
- **Q17** answer improved (now names Brian Thomas Jr. specifically) even though its *recall*
  is still a miss — the LLM generalized from a chunk that named both teams rather than getting
  Thomas's dedicated section. Answer-level win, not a retrieval-level one.
- **Q8, Q9, Q10** (other season-total comparisons) are still 0/3 in Top-3 for one side. The
  rollup chunk existing doesn't guarantee it *outranks* the losing player's own weekly-row
  noise in hybrid search — Nacua's 3 weekly rows still crowd out both Hill's rollup and Hill's
  rows entirely in Q9, for example.
- **Q19** (Nacua/Hill summary) is unchanged — still a full refusal, since Nacua's *article*
  section (not a stat chunk) never surfaces, and this fix only added stat-side chunks.

**What's left, if pursued further:** the remaining gap is structural — a single hybrid query
can't reliably retrieve balanced context for *two* named entities at once. The clean fix is
query decomposition (detect comparison questions, issue one retrieval call per named entity,
merge results) rather than further Top-K or chunking changes. Not implemented this session —
diminishing returns for a Week 5 exercise vs. real architecture work; flagged as a candidate
for Week 6 polish or a future session.
