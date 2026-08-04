using FantasyFootballRag.Commands;
using FantasyFootballRag.Commands.Ingestion;
using FantasyFootballRag.Commands.Interactive;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

// Fantasy Football RAG — minimal CLI router.
// Config: appsettings.json is primary (safe to commit — endpoint + deployment are not
// secrets now that auth is keyless). Environment variables override (handy for CI/containers).
IConfiguration config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

string command = args.FirstOrDefault() ?? "help";

switch (command.ToLowerInvariant())
{
    case "model":
        await new ModelCheckCommand(config).ExecuteAsync();
        break;

    case "load":
        new LoadCommand(config).Execute(args.Skip(1).FirstOrDefault() ?? "data/sample-corpus");
        break;

    case "ingest":
        await new IngestCommand(config).ExecuteAsync(args.Skip(1).FirstOrDefault() ?? "data/sample-corpus");
        break;

    case "query":
        await new QueryCommand(config).ExecuteAsync(
            args.Skip(1).FirstOrDefault() ?? throw new InvalidOperationException("Provide a question."),
            mode: args.Skip(2).FirstOrDefault() ?? "hybrid",      // keyword | vector | hybrid
            filter: args.Skip(3).FirstOrDefault());               // e.g. "Week eq 14"
        break;

    case "chat":
        await new ChatCommand(config).ExecuteAsync();
        break;

    default:
        AnsiConsole.MarkupLine("[yellow]Usage:[/] dotnet run --project src -- <command>");
        AnsiConsole.MarkupLine("  [green]model[/]               verify the Foundry model call (keyless auth)");
        AnsiConsole.MarkupLine("  [green]load [path][/]         load + parse the FF corpus, print counts");
        AnsiConsole.MarkupLine("  [green]ingest [path][/]       chunk + embed + index the corpus into Azure AI Search");
        AnsiConsole.MarkupLine("  [green]query <q> [mode][/]    retrieve chunks for question (mode: keyword|vector|hybrid)");
        AnsiConsole.MarkupLine("  [green]chat[/]                start the grounded RAG chat loop");
        break;
}
