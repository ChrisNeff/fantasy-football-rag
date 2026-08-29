using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using FantasyFootballRag.Retrieval;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FantasyFootballRag.Commands.Interactive;

/// <summary>
/// Command that executes a hybrid search query against the indexed corpus.
/// </summary>
public class QueryCommand
{
    private static readonly HashSet<string> ValidModes =
        new(StringComparer.OrdinalIgnoreCase) { "keyword", "vector", "hybrid" };

    private readonly IConfiguration _config;
    private readonly ILogger<QueryCommand> _logger;

    public QueryCommand(IConfiguration config, ILoggerFactory loggerFactory)
    {
        _config = config;
        _logger = loggerFactory.CreateLogger<QueryCommand>();
    }

    /// <summary>
    /// Executes a search query using hybrid retrieval (keyword + vector + RRF).
    /// </summary>
    /// <param name="question">The user's question.</param>
    /// <param name="mode">Search mode: "keyword", "vector", or "hybrid" (default).</param>
    /// <param name="filter">Optional OData filter (e.g., "Week eq 14").</param>
    public async Task ExecuteAsync(string question, string mode, string? filter)
    {
        if (!ValidModes.Contains(mode))
            throw new ArgumentException($"Invalid mode '{mode}'. Valid options: keyword, vector, hybrid.");

        string foundryEndpoint = _config["Foundry:Endpoint"]
            ?? throw new InvalidOperationException("Set Foundry:Endpoint in appsettings.json.");
        string embeddingDeployment = _config["Foundry:EmbeddingDeployment"]
            ?? throw new InvalidOperationException("Set Foundry:EmbeddingDeployment in appsettings.json.");
        string searchEndpoint = _config["Search:Endpoint"]
            ?? throw new InvalidOperationException("Set Search:Endpoint in appsettings.json.");
        string indexName = _config["Search:IndexName"]
            ?? throw new InvalidOperationException("Set Search:IndexName in appsettings.json.");

        var credential = new DefaultAzureCredential();
        var openAiClient = new AzureOpenAIClient(new Uri(foundryEndpoint), credential);
        var embeddingClient = openAiClient.GetEmbeddingClient(embeddingDeployment);
        var indexClient = new SearchIndexClient(new Uri(searchEndpoint), credential);
        var searchClient = indexClient.GetSearchClient(indexName);

        var retriever = new HybridRetriever(searchClient, embeddingClient, _logger);

        _logger.LogDebug("Query: '{Question}' mode={Mode} filter={Filter}", question, mode, filter);

        List<ChunkResult> chunks;
        try
        {
            chunks = await retriever.RetrieveAsync(question, mode, topK: 3, filter);
        }
        catch (RequestFailedException ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Search failed (HTTP {ex.Status}):[/] {ex.Message}");
            return;
        }

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
