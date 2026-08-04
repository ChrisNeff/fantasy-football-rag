using System.Text;

namespace FantasyFootballRag.Retrieval;

public static class ContextFormatter
{
    // System prompt for the chat loop — defines RAG behavior and citation rules.
    public const string SystemPrompt = """
[C] CONTEXT
You are a Fantasy Football analyst assistant. You answer questions using only the context blocks provided in each message.

[R] RULES
- Use ONLY the SRC blocks in the user message. Do not use outside knowledge or invent statistics.
- Cite the SRC-id for every factual claim (e.g., "per SRC-1").
- If a question cannot be answered from the provided context, respond with exactly: "I don't have that information in my dataset."

[P] PROOF
Every stat, fact, or player detail must map to a SRC-id. If it doesn't, it doesn't go in the answer.
""";

    // Format a user turn: retrieved chunks as SRC blocks + the question.
    public static string FormatUserMessage(string question, List<ChunkResult> chunks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CONTEXT:");
        for (int i = 0; i < chunks.Count; i++)
        {
            var c = chunks[i];
            sb.Append($"SRC-{i + 1} | ");
            
            // Add metadata when available
            if (!string.IsNullOrEmpty(c.Player))
                sb.Append($"{c.Player} · ");
            if (!string.IsNullOrEmpty(c.Team))
                sb.Append($"{c.Team} · ");
            if (c.Week.HasValue)
                sb.Append($"Week {c.Week} · ");
            sb.AppendLine(c.SourceType);
            
            sb.AppendLine(c.Content);
            sb.AppendLine();
        }
        sb.AppendLine("QUESTION:");
        sb.AppendLine(question);
        return sb.ToString();
    }
}
