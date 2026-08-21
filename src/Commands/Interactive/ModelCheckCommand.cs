using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Spectre.Console;

namespace FantasyFootballRag.Commands.Interactive;

/// <summary>
/// Command that verifies the Foundry model endpoint with a test query.
/// </summary>
public class ModelCheckCommand
{
    private readonly IConfiguration _config;
    private readonly ILogger<ModelCheckCommand> _logger;

    public ModelCheckCommand(IConfiguration config, ILoggerFactory loggerFactory)
    {
        _config = config;
        _logger = loggerFactory.CreateLogger<ModelCheckCommand>();
    }

    /// <summary>
    /// Executes the model check: sends a test query to the Foundry chat endpoint.
    /// </summary>
    public async Task ExecuteAsync()
    {
        string endpoint = _config["Foundry:Endpoint"]
            ?? throw new InvalidOperationException("Set Foundry:Endpoint in appsettings.json.");
        string deployment = _config["Foundry:ChatDeployment"] ?? "gpt-5-mini";

        AnsiConsole.MarkupLine($"[grey]Endpoint  :[/] {endpoint}");
        AnsiConsole.MarkupLine($"[grey]Deployment:[/] {deployment}");

        _logger.LogDebug("Testing model endpoint {Endpoint} / {Deployment}", endpoint, deployment);

        var client = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
        ChatClient chat = client.GetChatClient(deployment);

        ChatCompletion completion;
        try
        {
            completion = await chat.CompleteChatAsync(
                new UserChatMessage(
                    "In two sentences: is a high-target possession WR or a low-target deep " +
                    "threat generally the safer fantasy WR1 in PPR? Explain the PPR angle."));
        }
        catch (RequestFailedException ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Model check failed (HTTP {ex.Status}):[/] {ex.Message}");
            if (ex.Status == 401 || ex.Status == 403)
                AnsiConsole.MarkupLine("[yellow]Hint:[/] Run [grey]az login[/] and check you have the Cognitive Services OpenAI User role.");
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✓ Model answered:[/]");
        AnsiConsole.WriteLine(completion.Content[0].Text);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            "[grey]Note: this answer is ungrounded — general knowledge, not your corpus. " +
            "Closing that gap is what RAG (W2–W4) is for.[/]");
    }
}
