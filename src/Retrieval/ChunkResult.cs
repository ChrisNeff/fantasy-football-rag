namespace FantasyFootballRag.Retrieval;

// Result record for retrieval — used by both query (print) and chat (RAG context).
public record ChunkResult(string Content, string? Player, string? Team, int? Week, string SourceType);
