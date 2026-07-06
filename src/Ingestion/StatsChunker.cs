namespace FantasyFootballRag.Ingestion;

// Player stats chunking (see docs/chunking-decisions.md): one atomic, self-contained
// record per player-week row, no overlap.
public class StatsChunker
{
    public IEnumerable<Chunk> ChunkCsv(string filePath, string relativePath)
    {
        string[] lines = File.ReadAllLines(filePath);
        if (lines.Length < 2)
        {
            yield break;
        }

        string[] headers = lines[0].Split(',');

        for (int rowIndex = 0; rowIndex < lines.Length - 1; rowIndex++)
        {
            string[] values = lines[rowIndex + 1].Split(',');
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length && i < values.Length; i++)
            {
                row[headers[i].Trim()] = values[i].Trim();
            }

            string player = row.GetValueOrDefault("player", "");
            string team = row.GetValueOrDefault("team", "");
            int week = int.Parse(row.GetValueOrDefault("week", "0"));
            string targets = row.GetValueOrDefault("targets", "0");
            string receptions = row.GetValueOrDefault("receptions", "0");
            string recYards = row.GetValueOrDefault("rec_yards", "0");
            string recTd = row.GetValueOrDefault("rec_td", "0");
            string pprPoints = row.GetValueOrDefault("ppr_points", "0");

            string content =
                $"{player} ({team}), Week {week}: {targets} targets, {receptions} receptions, " +
                $"{recYards} rec yards, {recTd} rec TD, {pprPoints} PPR points.";

            yield return new Chunk(
                Id: Chunk.BuildDocId("stats", relativePath, $"row{rowIndex}"),
                Content: content,
                SourceType: "stat",
                SourceFile: relativePath,
                Player: player,
                Team: team,
                Week: week,
                Section: $"{player} — Week {week}");
        }
    }
}
