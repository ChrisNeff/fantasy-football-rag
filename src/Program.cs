using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using FantasyFootballRag.Ingestion;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Spectre.Console;

// Fantasy Football RAG — Build W1 scaffold + W2 ingestion.
// Three commands:
//   model  -> a real, keyless model call through the Foundry endpoint
//   load   -> the FF corpus loads + parses and prints a count
//   ingest -> chunk + embed + index the corpus into Azure AI Search
// No retrieval/chat yet — that's W3+.

// Config: appsettings.json is primary (safe to commit — endpoint + deployment are not
// secrets now that auth is keyless). Environment variables override (handy for CI/containers).
IConfiguration config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

string command = args.FirstOrDefault() ?? "help";

switch (command)
{
    case "model":
        await RunModelCheck();
        break;
    case "load":
        RunCorpusLoad(args.Skip(1).FirstOrDefault() ?? "data/sample-corpus");
        break;
    case "ingest":
        await RunIngest(args.Skip(1).FirstOrDefault() ?? "data/sample-corpus");
        break;
    default:
        AnsiConsole.MarkupLine("[yellow]Usage:[/] dotnet run --project src -- <command>");
        AnsiConsole.MarkupLine("  [green]model[/]        verify the Foundry model call (keyless auth)");
        AnsiConsole.MarkupLine("  [green]load [path][/]  load + parse the FF corpus, print counts");
        AnsiConsole.MarkupLine("  [green]ingest [path][/] chunk + embed + index the corpus into Azure AI Search");
        break;
}

// --- W1 done-bar #2: a model answer prints, via the Foundry endpoint, no API keys ---
async Task RunModelCheck()
{
    string endpoint = config["Foundry:Endpoint"]
        ?? throw new InvalidOperationException("Set Foundry:Endpoint in appsettings.json.");
    string deployment = config["Foundry:ChatDeployment"] ?? "gpt-5-mini";

    AnsiConsole.MarkupLine($"[grey]Endpoint   :[/] {endpoint}");
    AnsiConsole.MarkupLine($"[grey]Deployment :[/] {deployment}");

    // Keyless: DefaultAzureCredential uses your local `az login` session
    // (managed identity when running in Azure). No secrets in code or config.
    var client = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
    ChatClient chat = client.GetChatClient(deployment);

    ChatCompletion completion = await chat.CompleteChatAsync(
        new UserChatMessage(
            "In two sentences: is a high-target possession WR or a low-target deep " +
            "threat generally the safer fantasy WR1 in PPR? Explain the PPR angle."));

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[green]✓ Model answered:[/]");
    AnsiConsole.WriteLine(completion.Content[0].Text);
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine(
        "[grey]Note: this answer is ungrounded — general knowledge, not your corpus. " +
        "Closing that gap is what RAG (W2–W4) is for.[/]");
}

// Shared by `load` and `ingest`: find the corpus's article/stat files under root.
(string[] Articles, string[] Stats) ScanCorpus(string root)
{
    string articlesDir = Path.Combine(root, "articles");
    string statsDir = Path.Combine(root, "stats");

    string[] articles = Directory.Exists(articlesDir)
        ? Directory.GetFiles(articlesDir, "*.md") : Array.Empty<string>();
    string[] statFiles = Directory.Exists(statsDir)
        ? Directory.GetFiles(statsDir, "*.csv") : Array.Empty<string>();

    return (articles, statFiles);
}

// --- W1 done-bar #3: corpus files load + parse + print a count ---
void RunCorpusLoad(string root)
{
    if (!Directory.Exists(root))
    {
        AnsiConsole.MarkupLine($"[red]Corpus directory not found:[/] {root}");
        return;
    }

    (string[] articles, string[] statFiles) = ScanCorpus(root);

    // Articles chunk structurally on ## headers (see docs/chunking-decisions.md);
    // each player-week stat row is one atomic record. Just count them here.
    int articleSections = articles.Sum(a => File.ReadLines(a).Count(l => l.StartsWith("## ")));
    int statRows = statFiles.Sum(s => Math.Max(0, File.ReadLines(s).Count() - 1)); // minus header

    var table = new Table().Border(TableBorder.Rounded);
    table.AddColumn("Source");
    table.AddColumn(new TableColumn("Files").RightAligned());
    table.AddColumn(new TableColumn("Records").RightAligned());
    table.AddRow("Articles (.md)", articles.Length.ToString(), $"{articleSections} sections");
    table.AddRow("Player stats (.csv)", statFiles.Length.ToString(), $"{statRows} rows");
    AnsiConsole.Write(table);

    AnsiConsole.MarkupLine(
        $"[green]✓ Parsed {articles.Length + statFiles.Length} corpus files " +
        $"({articleSections + statRows} records).[/]");
}

// --- W2: chunk the corpus, embed the chunks, index them into Azure AI Search ---
async Task RunIngest(string root)
{
    if (!Directory.Exists(root))
    {
        AnsiConsole.MarkupLine($"[red]Corpus directory not found:[/] {root}");
        return;
    }

    string foundryEndpoint = config["Foundry:Endpoint"]
        ?? throw new InvalidOperationException("Set Foundry:Endpoint in appsettings.json.");
    string embeddingDeployment = config["Foundry:EmbeddingDeployment"]
        ?? throw new InvalidOperationException("Set Foundry:EmbeddingDeployment in appsettings.json.");
    string searchEndpoint = config["Search:Endpoint"]
        ?? throw new InvalidOperationException("Set Search:Endpoint in appsettings.json.");
    string indexName = config["Search:IndexName"]
        ?? throw new InvalidOperationException("Set Search:IndexName in appsettings.json.");

    AnsiConsole.MarkupLine($"[grey]Search endpoint :[/] {searchEndpoint}");
    AnsiConsole.MarkupLine($"[grey]Index           :[/] {indexName}");

    // Same keyless credential as `model` — no API keys for Foundry or Azure AI Search.
    var credential = new DefaultAzureCredential();
    var openAiClient = new AzureOpenAIClient(new Uri(foundryEndpoint), credential);
    EmbeddingClient embeddingClient = openAiClient.GetEmbeddingClient(embeddingDeployment);

    var indexClient = new SearchIndexClient(new Uri(searchEndpoint), credential);
    SearchClient searchClient = indexClient.GetSearchClient(indexName);

    (string[] articles, string[] statFiles) = ScanCorpus(root);

    var articleChunker = new ArticleChunker();
    var statsChunker = new StatsChunker();

    var chunks = new List<Chunk>();
    foreach (string articlePath in articles)
    {
        chunks.AddRange(articleChunker.ChunkMarkdown(articlePath, $"articles/{Path.GetFileName(articlePath)}"));
    }
    foreach (string statPath in statFiles)
    {
        chunks.AddRange(statsChunker.ChunkCsv(statPath, $"stats/{Path.GetFileName(statPath)}"));
    }

    var searchIndexer = new SearchIndexer();
    await searchIndexer.EnsureIndexAsync(indexClient, indexName);
    int uploaded = await searchIndexer.EmbedAndUploadAsync(searchClient, embeddingClient, chunks);

    int articleChunks = chunks.Count(c => c.SourceType == "article");
    int statChunks = chunks.Count(c => c.SourceType == "stat");

    var table = new Table().Border(TableBorder.Rounded);
    table.AddColumn("Source");
    table.AddColumn(new TableColumn("Files").RightAligned());
    table.AddColumn(new TableColumn("Chunks").RightAligned());
    table.AddRow("Articles (.md)", articles.Length.ToString(), articleChunks.ToString());
    table.AddRow("Player stats (.csv)", statFiles.Length.ToString(), statChunks.ToString());
    AnsiConsole.Write(table);

    AnsiConsole.MarkupLine($"[green]✓ Embedded + indexed {uploaded} chunks into '{indexName}'.[/]");
}
