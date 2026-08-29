using Azure;
using FantasyFootballRag.Commands;
using FantasyFootballRag.Commands.Ingestion;
using FantasyFootballRag.Commands.Interactive;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Spectre.Console;

// ── Config ────────────────────────────────────────────────────────────────────
IConfiguration config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

// ── Logger factory ────────────────────────────────────────────────────────────
// Verbose flag: `dotnet run -- --verbose <command>` or any arg position containing "--verbose"
bool verbose = args.Any(a => a.Equals("--verbose", StringComparison.OrdinalIgnoreCase));
string[] effectiveArgs = args.Where(a => !a.Equals("--verbose", StringComparison.OrdinalIgnoreCase)).ToArray();

using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Warning)
        .AddSimpleConsole(opts =>
        {
            opts.SingleLine = true;
            opts.TimestampFormat = "HH:mm:ss ";
        });
});

// ── Dispatch ──────────────────────────────────────────────────────────────────
string command = effectiveArgs.FirstOrDefault() ?? "help";

try
{
    switch (command.ToLowerInvariant())
    {
        case "model":
            await new ModelCheckCommand(config, loggerFactory).ExecuteAsync();
            break;

        case "load":
            new LoadCommand(config).Execute(effectiveArgs.Skip(1).FirstOrDefault() ?? "data/sample-corpus");
            break;

        case "ingest":
            await new IngestCommand(config, loggerFactory).ExecuteAsync(
                effectiveArgs.Skip(1).FirstOrDefault() ?? "data/sample-corpus");
            break;

        case "query":
            string question = effectiveArgs.Skip(1).FirstOrDefault()
                ?? throw new ArgumentException("Provide a question: just query \"<question>\" [mode] [filter]");
            await new QueryCommand(config, loggerFactory).ExecuteAsync(
                question,
                mode: effectiveArgs.Skip(2).FirstOrDefault() ?? "hybrid",
                filter: effectiveArgs.Skip(3).FirstOrDefault());
            break;

        case "chat":
            await new ChatCommand(config, loggerFactory).ExecuteAsync();
            break;

        default:
            PrintUsage();
            break;
    }
}
catch (ArgumentException ex)
{
    AnsiConsole.MarkupLine($"[red]✗ Bad arguments:[/] {ex.Message}");
    Environment.Exit(1);
}
catch (InvalidOperationException ex)
{
    // Config validation failures — these are always actionable for the user
    AnsiConsole.MarkupLine($"[red]✗ Configuration error:[/] {ex.Message}");
    if (verbose) AnsiConsole.WriteException(ex);
    Environment.Exit(1);
}
catch (RequestFailedException ex)
{
    AnsiConsole.MarkupLine($"[red]✗ Azure request failed[/] (HTTP {ex.Status}): {ex.Message}");
    AnsiConsole.MarkupLine(ex.Status switch
    {
        401 or 403 => "[yellow]Hint:[/] Run [grey]az login[/] and verify your RBAC role on the resource.",
        404        => "[yellow]Hint:[/] Check endpoint URLs and index name in appsettings.json.",
        429        => "[yellow]Hint:[/] Rate limit hit — wait a moment and retry.",
        _          => "[yellow]Hint:[/] Run with [grey]--verbose[/] to see the full exception."
    });
    if (verbose) AnsiConsole.WriteException(ex);
    Environment.Exit(1);
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]✗ Unexpected error:[/] {ex.Message}");
    AnsiConsole.MarkupLine("[yellow]Hint:[/] Run with [grey]--verbose[/] to see the full stack trace.");
    if (verbose) AnsiConsole.WriteException(ex);
    Environment.Exit(1);
}

// ── Help ──────────────────────────────────────────────────────────────────────
static void PrintUsage()
{
    AnsiConsole.MarkupLine("[yellow]Usage:[/] dotnet run --project src -- [grey][[--verbose]][/] <command>");
    AnsiConsole.MarkupLine("  [green]model[/]                        verify Foundry model call (keyless auth smoke test)");
    AnsiConsole.MarkupLine("  [green]load [path][/]                  parse corpus, print counts (no Azure calls)");
    AnsiConsole.MarkupLine("  [green]ingest [path][/]                chunk + embed + index corpus into Azure AI Search");
    AnsiConsole.MarkupLine("  [green]query <q> [mode] [filter][/]    retrieve chunks (mode: keyword|vector|hybrid)");
    AnsiConsole.MarkupLine("  [green]chat[/]                         start grounded RAG chat loop");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("  [grey]--verbose[/]  show full exception stack traces and debug logs");
}
