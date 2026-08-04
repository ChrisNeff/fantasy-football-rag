# W4 Chat Interface — Smoke Test Results

**Date:** 2026-08-04  
**Context:** Initial testing of the grounded RAG chat loop with the 8-chunk sample corpus  
**Command:** `just chat` (or `dotnet run --project src -- chat`)

---

## Test 1: Factual Stat in Corpus

**Question:** "How did Jefferson perform in Week 14?"

**Expected Behavior:**
- Cited answer with SRC-id references (e.g., "per SRC-1")
- Uses only information from the retrieved chunks
- No hallucination or external knowledge

**Actual Result:**
```
[Paste the assistant's response here]
```

**Pass/Fail:** [ ] Pass  [ ] Fail

**Notes:**
- 

---

## Test 2: Out-of-Corpus Query

**Question:** "What were Patrick Mahomes' stats in Week 3?"

**Expected Behavior:**
- Response: "I don't have that information in my dataset."
- Does NOT attempt to answer from external knowledge
- Does NOT fabricate statistics

**Actual Result:**
```
[Paste the assistant's response here]
```

**Pass/Fail:** [ ] Pass  [ ] Fail

**Notes:**
-

---

## Test 3: Multi-Turn Follow-Up

**First Question:** "How did Jefferson perform in Week 14?"  
**Follow-Up Question:** "What about the following week?"

**Expected Behavior:**
- Uses conversation history to understand "following week" means Week 15
- Retrieves fresh chunks for Week 15
- Maintains coherent multi-turn context

**Actual Result:**
```
First response:
[Paste first response here]

Follow-up response:
[Paste follow-up response here]
```

**Pass/Fail:** [ ] Pass  [ ] Fail

**Notes:**
-

---

## Overall Assessment

**System Status:** [ ] Ready for W5 Eval  [ ] Needs Tuning

**Observations:**
- 

**Known Limitations (8-chunk corpus):**
- Limited coverage — only Week 14 articles + minimal player stats
- Cannot test comprehensive retrieval quality until full corpus ingested
- Do NOT tune chunking/retrieval based on these results (per W4 plan)

**Next Steps:**
- [ ] Ingest full nflverse corpus (data/corpus/)
- [ ] Begin W5 20-question eval set
