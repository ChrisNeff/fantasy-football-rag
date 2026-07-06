namespace FantasyFootballRag.Ingestion;

// News/analysis chunking (see docs/chunking-decisions.md): structural split on
// ##/### headers so each chunk is one author-defined topic, target ~800 chars
// (500-1500 range), with ~15% overlap for sections that exceed the range.
public class ArticleChunker
{
    private const int TargetChunkChars = 800;
    private const int MaxChunkChars = 1500;
    private const double OverlapRatio = 0.15;

    public IEnumerable<Chunk> ChunkMarkdown(string filePath, string relativePath)
    {
        string[] lines = File.ReadAllLines(filePath);

        var sections = new List<(string Heading, string Body)>();
        string? currentHeading = null;
        var currentBody = new List<string>();

        foreach (string line in lines)
        {
            if (line.StartsWith("## ") || line.StartsWith("### "))
            {
                if (currentHeading is not null)
                {
                    sections.Add((currentHeading, string.Join('\n', currentBody).Trim()));
                }

                currentHeading = line.TrimStart('#', ' ');
                currentBody = new List<string>();
            }
            else if (currentHeading is not null)
            {
                // Content before the first header (e.g. an H1 title) is skipped — boilerplate, not a topic.
                currentBody.Add(line);
            }
        }

        if (currentHeading is not null)
        {
            sections.Add((currentHeading, string.Join('\n', currentBody).Trim()));
        }

        int sectionIndex = 0;
        foreach ((string heading, string body) in sections)
        {
            if (body.Length <= MaxChunkChars)
            {
                // Kept as one chunk even if under the 500-char floor — never merge across
                // an author-defined header boundary just to hit a target size.
                yield return new Chunk(
                    Id: Chunk.BuildDocId("articles", relativePath, $"sec{sectionIndex}"),
                    Content: body,
                    SourceType: "article",
                    SourceFile: relativePath,
                    Player: null,
                    Team: null,
                    Week: null,
                    Section: heading);
            }
            else
            {
                int subIndex = 0;
                foreach (string window in SlideWindow(body))
                {
                    yield return new Chunk(
                        Id: Chunk.BuildDocId("articles", relativePath, $"sec{sectionIndex}_sub{subIndex}"),
                        Content: window,
                        SourceType: "article",
                        SourceFile: relativePath,
                        Player: null,
                        Team: null,
                        Week: null,
                        Section: heading);
                    subIndex++;
                }
            }

            sectionIndex++;
        }
    }

    private IEnumerable<string> SlideWindow(string text)
    {
        int overlap = (int)(TargetChunkChars * OverlapRatio);
        int advance = TargetChunkChars - overlap;

        for (int start = 0; start < text.Length; start += advance)
        {
            int length = Math.Min(TargetChunkChars, text.Length - start);
            yield return text.Substring(start, length).Trim();

            if (start + length >= text.Length)
            {
                yield break;
            }
        }
    }
}
