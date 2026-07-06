# W2 Ingestion Plan — chunk → embed → index

## Context

The repo is currently at "W1 scaffold": `src/Program.cs` has only `model` (proves keyless
Foundry chat works) and `load` (counts corpus files/records, no chunking). W2 is the next
roadmap item in README.md: build the real ingestion pipeline — chunk the sample corpus per
`docs/chunking-decisions.md`, embed the chunks, and index them into Azure AI Search — so a new
`ingest` command produces a searchable index. This unblocks W3 (hybrid retrieval).

Verified via `az resource list`: only the Foundry/Cognitive Services account exists in
`ff-rag-rg` today. No Azure AI Search service exists anywhere in the subscription, so it must be
provisioned. Confirmed with the user: **Free tier** (supports vector fields, $0 cost; the
one-way-door caveat to a paid tier is acceptable since nothing is provisioned yet). Confirmed
stat-row content format: a **templated natural-language sentence** (not raw CSV), since it
embeds/retrieves better for natural-language questions.

`Azure.Search.Documents` 12.0.0 is already in `src/FantasyFootballRag.csproj` (unused until
now) — no new package references needed.

## 1. Provision Azure AI Search (new script, user runs manually — billable, requires the user's Azure session)

**`scripts/provision-search.sh`** (new), mirroring `scripts/provision-foundry.sh`'s structure/idempotency style:
- `RG=ff-rag-rg`, `LOC=eastus2`, `SEARCH_NAME=ff-search-cneff` (globally-unique, bump-if-conflict comment like the Foundry script), `SKU=free`.
- Idempotent create: `az search service show ... || az search service create --sku free --disable-local-auth true ...` (roles-only auth, consistent with the project's "no API keys" pattern).
- Assign both `Search Service Contributor` (schema/index management) and `Search Index Data Contributor` (document read/write) to the signed-in user (`az ad signed-in-user show`), each with the same `role assignment list | grep -q` idempotency guard the Foundry script uses for `Cognitive Services OpenAI User`.
- Print the endpoint `https://ff-search-cneff.search.windows.net` for the user to paste into `appsettings.json`.
- No changes needed to `scripts/teardown-azure.sh` — it deletes the whole `ff-rag-rg` resource group, which will already cover the Search service once created there.

**`justfile`** — add:
```
provision-search:
    ./scripts/provision-search.sh
```

## 2. Config

**`src/appsettings.json`** — add (committed, not secret, same rationale as `Foundry:Endpoint`):
```json
"Search": { "Endpoint": "https://ff-search-cneff.search.windows.net", "IndexName": "ff-corpus" }
```

## 3. Ingestion code — new `src/Ingestion/` folder

**`src/Ingestion/Chunk.cs`** — plain internal record, decoupled from the Search SDK:
```csharp
public record Chunk(string Id, string Content, string SourceType, string SourceFile, string? Player, string? Team, int? Week, string? Section);
```

**`src/Ingestion/StatsChunker.cs`** — `static IEnumerable<Chunk> ChunkCsv(string filePath, string relativePath)`. Splits header + rows (plain `Split(',')` — sample CSV is clean, no library needed). Per row, builds:
- `Content` = templated sentence, e.g. `"Justin Jefferson (MIN), Week 14: 12 targets, 9 receptions, 124 rec yards, 1 TD, 27.4 PPR points."`
- `Player`, `Team`, `Week` as separate fields (for filtering later)
- `Id` = `BuildDocId("stats", relativePath, rowIndex)`

**`src/Ingestion/ArticleChunker.cs`** — `static IEnumerable<Chunk> ChunkMarkdown(string filePath, string relativePath)`. Splits on lines starting with `## ` or `### ` per the chunking doc:
- Content before the first header (e.g. the sample's H1 intro line) is skipped — boilerplate, not a topic.
- Each section's heading → `Section`; body → candidate `Content`.
- Sections under the 500-char floor are kept as their own chunk (topic-coherence over hitting a floor — never merge across an author-defined header boundary).
- Sections over 1500 chars are sub-split into ~800-char windows with ~15% char-based overlap (advance = chunkSize − overlap) until each piece fits 500–1500. (Note: the current sample corpus's 3 sections are all short and never exercise this path — worth eyeballing once a larger article lands in `data/corpus/`.)
- `Id` = `BuildDocId("articles", relativePath, sectionIndex)` (append `_subIndex` when sub-split).

**`BuildDocId` helper** (small static method, e.g. in `Chunk.cs`): Azure Search keys must be URL-safe (letters/digits/`_`/`-`/`=` only, no `.` or `/`). Strip extension, replace path separators/non-alnum with `_`, append the row/section index — e.g. `stats_sample-week14-wr_row0`. This determinism is what makes re-running `ingest` idempotent (`MergeOrUpload` overwrites instead of duplicating).

**`src/Ingestion/ChunkDocument.cs`** — the `FieldBuilder`-attributed POCO actually embedded+uploaded to the index:
```csharp
public class ChunkDocument
{
    [SimpleField(IsKey = true)] public string Id { get; set; } = "";
    [SearchableField] public string Content { get; set; } = "";
    [VectorSearchField(VectorSearchDimensions = 1536, VectorSearchProfileName = "chunk-vector-profile")]
    public ReadOnlyMemory<float> ContentVector { get; set; }
    [SimpleField(IsFilterable = true, IsFacetable = true)] public string SourceType { get; set; } = ""; // "stat" | "article"
    [SimpleField(IsFilterable = true)] public string SourceFile { get; set; } = "";
    [SearchableField(IsFilterable = true, IsFacetable = true)] public string? Player { get; set; }
    [SearchableField(IsFilterable = true, IsFacetable = true)] public string? Team { get; set; }
    [SimpleField(IsFilterable = true, IsSortable = true)] public int? Week { get; set; }
    [SimpleField] public string? Section { get; set; }
}
```
1536 dims = `text-embedding-3-small`'s published embedding size.

**`src/Ingestion/SearchIndexer.cs`** — orchestrator, two responsibilities:
- `EnsureIndexAsync(SearchIndexClient)`: builds `SearchIndex` via `Fields = new FieldBuilder().Build(typeof(ChunkDocument))` + a `VectorSearch` with one `HnswAlgorithmConfiguration` + one `VectorSearchProfile` named `"chunk-vector-profile"`; calls `CreateOrUpdateIndexAsync` (idempotent — safe every run, no vectorizer/semantic config needed since W3 does raw hybrid retrieval via the SDK directly).
- `EmbedAndUploadAsync(SearchClient, EmbeddingClient, IReadOnlyList<Chunk>)`: batches chunk contents through `embeddingClient.GenerateEmbeddingsAsync(IEnumerable<string>)` (batch in groups of ~100–200, well under the 2048-input/300K-token service limits), zips results (`OpenAIEmbeddingCollection` → `.ToFloats()`) into `ChunkDocument`s, uploads via `searchClient.MergeOrUploadDocumentsAsync` in batches of ≤1000.

Verified API shapes (so this compiles first try):
```csharp
using OpenAI.Embeddings;
var client = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
EmbeddingClient embed = client.GetEmbeddingClient(deploymentName);
OpenAIEmbeddingCollection collection = await embed.GenerateEmbeddingsAsync(inputs);

using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
var indexClient = new SearchIndexClient(new Uri(searchEndpoint), new DefaultAzureCredential());
await indexClient.CreateOrUpdateIndexAsync(index);
var searchClient = indexClient.GetSearchClient(indexName);
await searchClient.MergeOrUploadDocumentsAsync(chunkDocuments);
```

## 4. Wiring

**`src/Program.cs`**:
- Factor the existing `Directory.GetFiles(articlesDir, "*.md")` / `Directory.GetFiles(statsDir, "*.csv")` scanning out of `RunCorpusLoad` into a small shared helper so `load` and the new `ingest` don't duplicate directory-walking.
- Add `case "ingest": await RunIngest(args.Skip(1).FirstOrDefault() ?? "data/sample-corpus");`.
- `RunIngest`: read `Search:Endpoint`, `Search:IndexName`, `Foundry:Endpoint`, `Foundry:EmbeddingDeployment` from `config` (same `?? throw new InvalidOperationException(...)` pattern as `RunModelCheck`); construct `SearchIndexClient` + `AzureOpenAIClient` with `DefaultAzureCredential()`; scan → run both chunkers → `EnsureIndexAsync` → `EmbedAndUploadAsync`; print a Spectre `Table` (files scanned / chunks produced by source type / chunks embedded+indexed / index name), matching `RunCorpusLoad`'s existing table style.
- Update the usage/help text (default case) to list `ingest [path]`.

**`.zed/debug.json`** — append a third config `"Debug: ingest"`, identical shape to the existing two, `"args": ["ingest"]`.

**`README.md`** — add the `just provision-search` step next to `just provision` in Getting Started; document `Search:Endpoint`/`Search:IndexName` next to the existing `Foundry:*` config explanation; flip the W2 roadmap checkbox once done.

## Verification

1. `dotnet build` — must compile clean.
2. User runs `just provision-search` (billable/interactive Azure step — I don't run this myself) and confirms the endpoint got pasted into `appsettings.json`.
3. `just ingest` (defaults to `data/sample-corpus`) — should print a table showing 1 article file (3 sections) + 1 stats file (5 rows) = 8 chunks produced, embedded, and indexed.
4. Re-run `just ingest` a second time — document count in the index should stay the same (idempotent merge-or-upload), not double.
5. Spot-check via Azure Portal Search Explorer (roles-only auth via the signed-in user's RBAC) that 8 documents exist with non-null vectors.
