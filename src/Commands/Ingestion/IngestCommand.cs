using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using FantasyFootballRag.Ingestion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FantasyFootballRag.Commands.Ingestion;

/// <summary>
/// Command that chunks the corpus, embeds the chunks, and indexes them into Azure AI Search.
/// </summary>
public class IngestCommand
{
    private readonly IConfiguration _config;
    private readonly ILogger<IngestCommand> _logger;

    public IngestCommand(IConfiguration config, ILoggerFactory loggerFactory)
    {
        _config = config;
        _logger = loggerFactory.CreateLogger<IngestCommand>();
    }

    /// <summary>
    /// Executes the ingest command: chunks, embeds, and uploads corpus data to Azure AI Search.
    /// </summary>
    /// <param name="corpusPath">Root path to the corpus directory (defaults to "data/sample-corpus").</param>
    public async Task ExecuteAsync(string? corpusPath)
    {
        string root = corpusPath ?? "data/sample-corpus";

        if (!Directory.Exists(root))
        {
            AnsiConsole.MarkupLine($"[red]✗ Corpus directory not found:[/] {root}");
            AnsiConsole.MarkupLine($"[yellow]Hint:[/] Run from the repo root, or pass the path explicitly: [grey]just ingest data/corpus[/]");
            return;
        }

        string foundryEndpoint = _config["Foundry:Endpoint"]
            ?? throw new InvalidOperationException("Set Foundry:Endpoint in appsettings.json.");
        string embeddingDeployment = _config["Foundry:EmbeddingDeployment"]
            ?? throw new InvalidOperationException("Set Foundry:EmbeddingDeployment in appsettings.json.");
        string searchEndpoint = _config["Search:Endpoint"]
            ?? throw new InvalidOperationException("Set Search:Endpoint in appsettings.json.");
        string indexName = _config["Search:IndexName"]
            ?? throw new InvalidOperationException("Set Search:IndexName in appsettings.json.");

        AnsiConsole.MarkupLine($"[grey]Search endpoint :[/] {searchEndpoint}");
        AnsiConsole.MarkupLine($"[grey]Index           :[/] {indexName}");

        var credential = new DefaultAzureCredential();
        var openAiClient = new AzureOpenAIClient(new Uri(foundryEndpoint), credential);
        var embeddingClient = openAiClient.GetEmbeddingClient(embeddingDeployment);
        var indexClient = new SearchIndexClient(new Uri(searchEndpoint), credential);
        var searchClient = indexClient.GetSearchClient(indexName);

        // ── Chunk ──────────────────────────────────────────────────────────────
        (string[] articles, string[] statFiles) = ScanCorpus(root);

        var articleChunker = new ArticleChunker();
        var statsChunker = new StatsChunker();
        var chunks = new List<Chunk>();

        foreach (string articlePath in articles)
            chunks.AddRange(articleChunker.ChunkMarkdown(articlePath, $"articles/{Path.GetFileName(articlePath)}"));
        foreach (string statPath in statFiles)
        {
            string relativePath = $"stats/{Path.GetFileName(statPath)}";
            chunks.AddRange(statsChunker.ChunkCsv(statPath, relativePath));
            chunks.AddRange(statsChunker.ChunkSeasonRollups(statPath, relativePath));
        }

        // ── Ensure index ───────────────────────────────────────────────────────
        var searchIndexer = new SearchIndexer();
        try
        {
            await searchIndexer.EnsureIndexAsync(indexClient, indexName);
        }
        catch (RequestFailedException ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Index setup failed (HTTP {ex.Status}):[/] {ex.Message}");
            return;
        }

        // ── Embed + upload (with progress) ────────────────────────────────────
        int uploaded = 0;
        try
        {
            await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn(),
                })
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Embedding + indexing chunks[/]", maxValue: chunks.Count);
                    uploaded = await searchIndexer.EmbedAndUploadAsync(
                        searchClient, embeddingClient, chunks,
                        onBatchComplete: count => task.Increment(count));
                });
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Embed/upload failed after {Uploaded} chunks", uploaded);
            AnsiConsole.MarkupLine($"[red]✗ Ingest failed (HTTP {ex.Status}):[/] {ex.Message}");
            AnsiConsole.MarkupLine($"[yellow]Note:[/] {uploaded} chunks may have been indexed before the failure. Re-running ingest is safe — upsert-by-id is idempotent.");
            return;
        }

        // ── Summary ────────────────────────────────────────────────────────────
        int articleChunks = chunks.Count(c => c.SourceType == "article");
        int statChunks = chunks.Count(c => c.SourceType == "stat");
        int statSeasonChunks = chunks.Count(c => c.SourceType == "stat-season");

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Source");
        table.AddColumn(new TableColumn("Files").RightAligned());
        table.AddColumn(new TableColumn("Chunks").RightAligned());
        table.AddRow("Articles (.md)", articles.Length.ToString(), articleChunks.ToString());
        table.AddRow("Player stats — weekly (.csv)", statFiles.Length.ToString(), statChunks.ToString());
        table.AddRow("Player stats — season rollup (.csv)", statFiles.Length.ToString(), statSeasonChunks.ToString());
        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine($"[green]✓ Embedded + indexed {uploaded} chunks into '{indexName}'.[/]");
    }

    private (string[] Articles, string[] Stats) ScanCorpus(string root)
    {
        string articlesDir = Path.Combine(root, "articles");
        string statsDir = Path.Combine(root, "stats");

        string[] articles = Directory.Exists(articlesDir)
            ? Directory.GetFiles(articlesDir, "*.md") : Array.Empty<string>();
        string[] statFiles = Directory.Exists(statsDir)
            ? Directory.GetFiles(statsDir, "*.csv") : Array.Empty<string>();

        return (articles, statFiles);
    }
}
