using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using FantasyFootballRag.Retrieval;
using Microsoft.Extensions.Configuration;
using OpenAI.Embeddings;
using Spectre.Console;

namespace FantasyFootballRag.Commands.Interactive;

/// <summary>
/// Command that executes a hybrid search query against the indexed corpus.
/// </summary>
public class QueryCommand
{
    private readonly IConfiguration _config;

    public QueryCommand(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Executes a search query using hybrid retrieval (keyword + vector + RRF).
    /// </summary>
    /// <param name="question">The user's question.</param>
    /// <param name="mode">Search mode: "keyword", "vector", or "hybrid" (default).</param>
    /// <param name="filter">Optional OData filter (e.g., "Week eq 14").</param>
    public async Task ExecuteAsync(string question, string mode, string? filter)
    {
        string foundryEndpoint = _config["Foundry:Endpoint"]
            ?? throw new InvalidOperationException("Set Foundry:Endpoint in appsettings.json.");
        string embeddingDeployment = _config["Foundry:EmbeddingDeployment"]
            ?? throw new InvalidOperationException("Set Foundry:EmbeddingDeployment in appsettings.json.");
        string searchEndpoint = _config["Search:Endpoint"]
            ?? throw new InvalidOperationException("Set Search:Endpoint in appsettings.json.");
        string indexName = _config["Search:IndexName"]
            ?? throw new InvalidOperationException("Set Search:IndexName in appsettings.json.");

        // Same keyless credential as `model` — no API keys for Foundry or Azure AI Search.
        var credential = new DefaultAzureCredential();
        var openAiClient = new AzureOpenAIClient(new Uri(foundryEndpoint), credential);
        EmbeddingClient embeddingClient = openAiClient.GetEmbeddingClient(embeddingDeployment);

        var indexClient = new SearchIndexClient(new Uri(searchEndpoint), credential);
        SearchClient searchClient = indexClient.GetSearchClient(indexName);

        // Use the new HybridRetriever instead of the inline method
        var retriever = new HybridRetriever(searchClient, embeddingClient);
        var chunks = await retriever.RetrieveAsync(question, mode, topK: 3, filter);

        AnsiConsole.MarkupLine($"[grey]Total results:[/] {chunks.Count}");

        if (chunks.Count < 1)
        {
            AnsiConsole.MarkupLine("  [red]No player information results found[/]");
        }
        else
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("[grey]Player[/]");
            table.AddColumn("[grey]Team[/]");
            table.AddColumn("[grey]Rank[/]");
            table.AddColumn("[grey]Content[/]");

            var rank = 0;
            foreach (var chunk in chunks)
            {
                table.AddRow(
                    chunk.Player ?? string.Empty,
                    chunk.Team ?? string.Empty,
                    (++rank).ToString(),
                    chunk.Content ?? string.Empty
                );
            }

            AnsiConsole.Write(table);
        }
    }
}
