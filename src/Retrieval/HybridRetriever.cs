using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using FantasyFootballRag.Ingestion;
using OpenAI.Embeddings;

namespace FantasyFootballRag.Retrieval;

public class HybridRetriever
{
    private readonly SearchClient _searchClient;
    private readonly EmbeddingClient _embeddingClient;

    public HybridRetriever(SearchClient searchClient, EmbeddingClient embeddingClient)
    {
        _searchClient = searchClient;
        _embeddingClient = embeddingClient;
    }

    // Hybrid search (keyword + vector + RRF)
    public async Task<List<ChunkResult>> RetrieveAsync(
        string question,
        string mode = "hybrid",
        int topK = 3,
        string? filter = null)
    {
        var embedding = await _embeddingClient.GenerateEmbeddingAsync(question);
        var queryVector = embedding.Value.ToFloats();

        var options = new SearchOptions { Size = topK, Filter = filter, IncludeTotalCount = true };
        options.Select.Add("Id");
        options.Select.Add("Content");
        options.Select.Add("SourceType");
        options.Select.Add("Player");
        options.Select.Add("Week");
        options.Select.Add("Team");
        options.Select.Add("Section");

        string? searchText = null;

        if (mode is "vector" or "hybrid")
        {
            options.VectorSearch = new()
            {
                Queries = { new VectorizedQuery(queryVector)
                 {
                     KNearestNeighborsCount = 10,
                     Fields = { "ContentVector" },
                 } }
            };
        }
        if (mode is "keyword" or "hybrid")
        {
            searchText = question;
        }

        var searchResults = await _searchClient.SearchAsync<ChunkDocument>(searchText, options);

        var results = new List<ChunkResult>();
        await foreach (var result in searchResults.Value.GetResultsAsync())
        {
            results.Add(new ChunkResult(
                result.Document.Content,
                result.Document.Player,
                result.Document.Team,
                result.Document.Week,
                result.Document.SourceType
            ));
        }

        return results;
    }
}
