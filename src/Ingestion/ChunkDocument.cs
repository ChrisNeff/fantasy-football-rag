using Azure.Search.Documents.Indexes;

namespace FantasyFootballRag.Ingestion;

// The Azure AI Search document schema. FieldBuilder.Build(typeof(ChunkDocument)) (see
// SearchIndexer) derives the index schema from these attributes, so this class is the
// single source of truth — the upload model can't drift from the index schema.
public class ChunkDocument
{
    [SimpleField(IsKey = true)]
    public string Id { get; set; } = "";

    [SearchableField]
    public string Content { get; set; } = "";

    // 1536 = text-embedding-3-small's published embedding dimension.
    [VectorSearchField(VectorSearchDimensions = 1536, VectorSearchProfileName = "chunk-vector-profile")]
    public IReadOnlyList<float> ContentVector { get; set; } = Array.Empty<float>();

    [SimpleField(IsFilterable = true, IsFacetable = true)]
    public string SourceType { get; set; } = ""; // "stat" | "article"

    [SimpleField(IsFilterable = true)]
    public string SourceFile { get; set; } = "";

    [SearchableField(IsFilterable = true, IsFacetable = true)]
    public string? Player { get; set; }

    [SearchableField(IsFilterable = true, IsFacetable = true)]
    public string? Team { get; set; }

    [SimpleField(IsFilterable = true, IsSortable = true)]
    public int? Week { get; set; }

    [SimpleField]
    public string? Section { get; set; }
}
