using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using FantasyFootballRag.Retrieval;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Spectre.Console;

namespace FantasyFootballRag.Commands.Interactive;

/// <summary>
/// Interactive RAG chat loop with grounded answers and citations.
/// </summary>
public class ChatCommand
{
    private readonly IConfiguration _config;

    public ChatCommand(IConfiguration config)
    {
        _config = config;
    }

    public async Task ExecuteAsync()
    {
        // 1. Build the kernel with gpt-5-mini via Foundry (keyless)
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

        // Build kernel and get chat client directly from AzureOpenAIClient
        var openAiClient = new AzureOpenAIClient(new Uri(foundryEndpoint), credential);
        var chatClient = openAiClient.GetChatClient(chatDeployment);
        
        // Initialize chat history with the system prompt
        var history = new ChatHistory();
        history.AddSystemMessage(ContextFormatter.SystemPrompt);

        // 3. Set up embedding + search clients (same as query command)
        var embeddingClient = openAiClient.GetEmbeddingClient(embeddingDeployment);
        var indexClient = new SearchIndexClient(new Uri(searchEndpoint), credential);
        var searchClient = indexClient.GetSearchClient(indexName);

        var retriever = new HybridRetriever(searchClient, embeddingClient);

        AnsiConsole.MarkupLine("[green]Fantasy Football RAG — Chat[/]  (type [grey]exit[/] to quit)");
        AnsiConsole.WriteLine();

        while (true)
        {
            string? question = AnsiConsole.Ask<string>("[blue]You:[/]");
            if (string.IsNullOrWhiteSpace(question) || question.Trim().ToLower() == "exit") break;

            // 4. Retrieve fresh chunks for this question (hybrid search)
            var chunks = await retriever.RetrieveAsync(question, mode: "hybrid", topK: 3);

            // 5. Format and add the user turn (question + context)
            string userMessage = ContextFormatter.FormatUserMessage(question, chunks);
            
            // 6. Get the grounded answer using OpenAI ChatClient directly
            var messages = new List<OpenAI.Chat.ChatMessage>
            {
                new OpenAI.Chat.SystemChatMessage(ContextFormatter.SystemPrompt)
            };
            
            // Add conversation history
            foreach (var msg in history)
            {
                if (msg.Role == AuthorRole.User)
                    messages.Add(new OpenAI.Chat.UserChatMessage(msg.Content));
                else if (msg.Role == AuthorRole.Assistant && !string.IsNullOrEmpty(msg.Content))
                    messages.Add(new OpenAI.Chat.AssistantChatMessage(msg.Content));
            }
            
            // Add current user message
            messages.Add(new OpenAI.Chat.UserChatMessage(userMessage));
            
            var completion = await chatClient.CompleteChatAsync(messages);
            string response = completion.Value.Content[0].Text;
            
            // Update history
            history.AddUserMessage(userMessage);
            history.AddAssistantMessage(response);

            AnsiConsole.MarkupLine($"[green]Assistant:[/] {response}");
            AnsiConsole.WriteLine();
        }
    }
}
