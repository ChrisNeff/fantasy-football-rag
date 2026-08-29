using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using OpenAI.Embeddings;

namespace FantasyFootballRag.Ingestion;

public class SearchIndexer
{
    // Well under the service's 2048-input/300K-token-per-call embedding limits and
    // the 1000-document-per-batch upload limit — batching here is future-proofing
    // for the real corpus, not something the tiny sample corpus needs today.
    private const int EmbedBatchSize = 100;
    private const int UploadBatchSize = 1000;

    // Safe to call every `ingest` run — creates the index on first run, no-ops after.
    public async Task EnsureIndexAsync(SearchIndexClient indexClient, string indexName)
    {
        var vectorSearch = new VectorSearch();
        vectorSearch.Algorithms.Add(new HnswAlgorithmConfiguration("chunk-hnsw"));
        vectorSearch.Profiles.Add(new VectorSearchProfile("chunk-vector-profile", "chunk-hnsw"));

        var index = new SearchIndex(indexName)
        {
            Fields = new FieldBuilder().Build(typeof(ChunkDocument)),
            VectorSearch = vectorSearch,
        };

        await indexClient.CreateOrUpdateIndexAsync(index);
    }

    // Embeds chunk content and upserts by id, so re-running `ingest` on the same
    // corpus overwrites existing documents instead of duplicating them.
    // onBatchComplete: called after each embed batch with the batch size, for progress reporting.
    public async Task<int> EmbedAndUploadAsync(
        SearchClient searchClient, EmbeddingClient embeddingClient, IReadOnlyList<Chunk> chunks,
        Action<int>? onBatchComplete = null)
    {
        var documents = new List<ChunkDocument>(chunks.Count);

        for (int offset = 0; offset < chunks.Count; offset += EmbedBatchSize)
        {
            List<Chunk> batch = chunks.Skip(offset).Take(EmbedBatchSize).ToList();
            OpenAIEmbeddingCollection embeddings =
                await embeddingClient.GenerateEmbeddingsAsync(batch.Select(c => c.Content));

            for (int i = 0; i < batch.Count; i++)
            {
                Chunk chunk = batch[i];
                documents.Add(new ChunkDocument
                {
                    Id = chunk.Id,
                    Content = chunk.Content,
                    ContentVector = embeddings[i].ToFloats().ToArray(),
                    SourceType = chunk.SourceType,
                    SourceFile = chunk.SourceFile,
                    Player = chunk.Player,
                    Team = chunk.Team,
                    Week = chunk.Week,
                    Section = chunk.Section,
                });
            }

            onBatchComplete?.Invoke(batch.Count);
        }

        for (int offset = 0; offset < documents.Count; offset += UploadBatchSize)
        {
            List<ChunkDocument> batch = documents.Skip(offset).Take(UploadBatchSize).ToList();
            await searchClient.MergeOrUploadDocumentsAsync(batch);
        }

        return documents.Count;
    }
}
