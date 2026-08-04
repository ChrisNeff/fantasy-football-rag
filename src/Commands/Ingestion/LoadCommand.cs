using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace FantasyFootballRag.Commands.Ingestion;

/// <summary>
/// Command that loads and parses the fantasy football corpus, printing file and record counts.
/// </summary>
public class LoadCommand
{
    private readonly IConfiguration _config;

    public LoadCommand(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Executes the load command: scans the corpus directory and displays statistics.
    /// </summary>
    /// <param name="corpusPath">Root path to the corpus directory (defaults to "data/sample-corpus").</param>
    public void Execute(string? corpusPath)
    {
        string root = corpusPath ?? "data/sample-corpus";

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

    /// <summary>
    /// Scans the corpus directory for article and stat files.
    /// </summary>
    /// <param name="root">Root corpus directory path.</param>
    /// <returns>Tuple of article paths and stat file paths.</returns>
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
