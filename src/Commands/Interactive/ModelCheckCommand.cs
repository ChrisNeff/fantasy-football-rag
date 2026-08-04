using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using Spectre.Console;

namespace FantasyFootballRag.Commands.Interactive;

/// <summary>
/// Command that verifies the Foundry model endpoint with a test query.
/// </summary>
public class ModelCheckCommand
{
    private readonly IConfiguration _config;

    public ModelCheckCommand(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Executes the model check: sends a test query to the Foundry chat endpoint.
    /// </summary>
    public async Task ExecuteAsync()
    {
        string endpoint = _config["Foundry:Endpoint"]
            ?? throw new InvalidOperationException("Set Foundry:Endpoint in appsettings.json.");
        string deployment = _config["Foundry:ChatDeployment"] ?? "gpt-5-mini";

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
}
