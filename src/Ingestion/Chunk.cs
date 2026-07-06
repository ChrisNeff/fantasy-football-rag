using System.Text.RegularExpressions;

namespace FantasyFootballRag.Ingestion;

// A source-agnostic retrievable unit, decoupled from the Azure Search document
// schema (see ChunkDocument) so chunking logic doesn't depend on the SDK.
public record Chunk(
    string Id,
    string Content,
    string SourceType,
    string SourceFile,
    string? Player,
    string? Team,
    int? Week,
    string? Section)
{
    // Azure Search keys must be URL-safe (letters/digits/_/-/= only). Deriving the id
    // from the file + a stable label makes re-running `ingest` idempotent: the same
    // input always produces the same id, so MergeOrUpload overwrites instead of duplicating.
    public static string BuildDocId(string sourceType, string relativePath, string label)
    {
        string baseName = Path.GetFileNameWithoutExtension(relativePath);
        string sanitized = Regex.Replace(baseName, "[^A-Za-z0-9_-]", "_");
        return $"{sourceType}_{sanitized}_{label}";
    }
}
