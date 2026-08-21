using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using FantasyFootballRag.Retrieval;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Spectre.Console;

namespace FantasyFootballRag.Commands.Interactive;

/// <summary>
/// Interactive RAG chat loop with grounded answers and citations.
/// Session Q&A is written to logs/session-{timestamp}.log for eval traceability.
/// </summary>
public class ChatCommand
{
    private readonly IConfiguration _config;
    private readonly ILogger<ChatCommand> _logger;

    public ChatCommand(IConfiguration config, ILoggerFactory loggerFactory)
    {
        _config = config;
        _logger = loggerFactory.CreateLogger<ChatCommand>();
    }

    public async Task ExecuteAsync()
    {
        string foundryEndpoint = _config["Foundry:Endpoint"]
            ?? throw new InvalidOperationException("Set Foundry:Endpoint in appsettings.json.");
        string chatDeployment = _config["Foundry:ChatDeployment"]
            ?? throw new InvalidOperationException("Set Foundry:ChatDeployment in appsettings.json.");
        string embeddingDeployment = _config["Foundry:EmbeddingDeployment"]
            ?? throw new InvalidOperationException("Set Foundry:EmbeddingDeployment in appsettings.json.");
        string searchEndpoint = _config["Search:Endpoint"]
            ?? throw new InvalidOperationException("Set Search:Endpoint in appsettings.json.");
        string indexName = _config["Search:IndexName"]
            ?? throw new InvalidOperationException("Set Search:IndexName in appsettings.json.");

        var credential = new DefaultAzureCredential();
        var openAiClient = new AzureOpenAIClient(new Uri(foundryEndpoint), credential);
        var chatClient = openAiClient.GetChatClient(chatDeployment);
        var embeddingClient = openAiClient.GetEmbeddingClient(embeddingDeployment);
        var indexClient = new SearchIndexClient(new Uri(searchEndpoint), credential);
        var searchClient = indexClient.GetSearchClient(indexName);
        var retriever = new HybridRetriever(searchClient, embeddingClient, _logger);

        var history = new ChatHistory();
        history.AddSystemMessage(ContextFormatter.SystemPrompt);

        // Session log — written regardless of interactive vs. batch mode, useful for eval sweeps
        string logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDir);
        string logPath = Path.Combine(logDir, $"session-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        await using var sessionLog = new StreamWriter(logPath, append: false) { AutoFlush = true };
        await sessionLog.WriteLineAsync($"# FF RAG Session — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        await sessionLog.WriteLineAsync($"# Model: {chatDeployment}  Index: {indexName}");
        await sessionLog.WriteLineAsync();

        _logger.LogInformation("Session log: {LogPath}", logPath);

        AnsiConsole.MarkupLine("[green]Fantasy Football RAG — Chat[/]  (type [grey]exit[/] to quit)");
        AnsiConsole.MarkupLine($"[grey]Session log:[/] {logPath}");
        AnsiConsole.WriteLine();

        while (true)
        {
            // AnsiConsole.Ask throws on redirected stdin — fall back to plain read for batch eval runs
            string? question;
            if (Console.IsInputRedirected)
            {
                AnsiConsole.Markup("[blue]You:[/] ");
                question = Console.ReadLine();
                if (question != null) AnsiConsole.WriteLine(question);
            }
            else
            {
                question = AnsiConsole.Ask<string>("[blue]You:[/]");
            }

            if (string.IsNullOrWhiteSpace(question) || question.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            await sessionLog.WriteLineAsync($"Q: {question}");

            // Retrieve fresh chunks for this question
            List<ChunkResult> chunks;
            try
            {
                chunks = await retriever.RetrieveAsync(question, mode: "hybrid", topK: 3);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Retrieval failed for question: {Question}", question);
                AnsiConsole.MarkupLine($"[red]✗ Retrieval error (HTTP {ex.Status}):[/] {ex.Message}");
                await sessionLog.WriteLineAsync($"A: [RETRIEVAL ERROR: {ex.Status} — {ex.Message}]");
                continue;
            }

            string userMessage = ContextFormatter.FormatUserMessage(question, chunks);

            // Build message list for this turn
            var messages = new List<OpenAI.Chat.ChatMessage>
            {
                new OpenAI.Chat.SystemChatMessage(ContextFormatter.SystemPrompt)
            };
            foreach (var msg in history)
            {
                if (msg.Role == AuthorRole.User)
                    messages.Add(new OpenAI.Chat.UserChatMessage(msg.Content));
                else if (msg.Role == AuthorRole.Assistant && !string.IsNullOrEmpty(msg.Content))
                    messages.Add(new OpenAI.Chat.AssistantChatMessage(msg.Content));
            }
            messages.Add(new OpenAI.Chat.UserChatMessage(userMessage));

            string response;
            try
            {
                var completion = await chatClient.CompleteChatAsync(messages);
                response = completion.Value.Content[0].Text;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Chat completion failed");
                AnsiConsole.MarkupLine($"[red]✗ Model error (HTTP {ex.Status}):[/] {ex.Message}");
                await sessionLog.WriteLineAsync($"A: [MODEL ERROR: {ex.Status} — {ex.Message}]");
                continue;
            }

            history.AddUserMessage(userMessage);
            history.AddAssistantMessage(response);

            AnsiConsole.MarkupLine($"[green]Assistant:[/] {response}");
            AnsiConsole.WriteLine();

            await sessionLog.WriteLineAsync($"A: {response}");
            await sessionLog.WriteLineAsync();
        }

        await sessionLog.WriteLineAsync($"# Session ended {DateTime.Now:HH:mm:ss} — {history.Count / 2} turns");
        AnsiConsole.MarkupLine($"[grey]Session saved:[/] {logPath}");
    }
}
