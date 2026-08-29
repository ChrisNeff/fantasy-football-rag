using System.Text.RegularExpressions;

namespace FantasyFootballRag.Ingestion;

// Player stats chunking (see docs/chunking-decisions.md): one atomic, self-contained
// record per player-week row, no overlap.
public class StatsChunker
{
    // W5 eval finding: comparison questions ("who scored more, X or Y") need one chunk per
    // player, but topK=3 hybrid search only ever surfaces weekly rows for whichever player
    // matches best — the other player is completely absent. A season rollup gives comparisons
    // a single dense chunk per player instead of needing all ~17 weekly rows in context.
    public IEnumerable<Chunk> ChunkSeasonRollups(string filePath, string relativePath)
    {
        string[] lines = File.ReadAllLines(filePath);
        if (lines.Length < 2)
        {
            yield break;
        }

        string[] headers = lines[0].Split(',');
        var byPlayer = new Dictionary<string, (string Team, int Targets, int Receptions, int RecYards, int RecTd, double Ppr, int Games)>();

        for (int rowIndex = 1; rowIndex < lines.Length; rowIndex++)
        {
            string[] values = lines[rowIndex].Split(',');
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length && i < values.Length; i++)
            {
                row[headers[i].Trim()] = values[i].Trim();
            }

            string player = row.GetValueOrDefault("player", "");
            if (player.Length == 0)
            {
                continue;
            }

            string team = row.GetValueOrDefault("team", "");
            int targets = int.Parse(row.GetValueOrDefault("targets", "0"));
            int receptions = int.Parse(row.GetValueOrDefault("receptions", "0"));
            int recYards = int.Parse(row.GetValueOrDefault("rec_yards", "0"));
            int recTd = int.Parse(row.GetValueOrDefault("rec_td", "0"));
            double ppr = double.Parse(row.GetValueOrDefault("ppr_points", "0"));

            (string Team, int Targets, int Receptions, int RecYards, int RecTd, double Ppr, int Games) totals =
                byPlayer.TryGetValue(player, out var existing) ? existing : (team, 0, 0, 0, 0, 0.0, 0);
            byPlayer[player] = (
                team,
                totals.Targets + targets,
                totals.Receptions + receptions,
                totals.RecYards + recYards,
                totals.RecTd + recTd,
                totals.Ppr + ppr,
                totals.Games + 1);
        }

        foreach ((string player, var t) in byPlayer)
        {
            string content =
                $"{player} ({t.Team}) — season totals: {t.Targets} targets, {t.Receptions} receptions, " +
                $"{t.RecYards} rec yards, {t.RecTd} rec TD, {t.Ppr:0.0} PPR points across {t.Games} games.";

            string playerSlug = Regex.Replace(player, "[^A-Za-z0-9_-]", "_");

            yield return new Chunk(
                Id: Chunk.BuildDocId("stat-season", relativePath, $"season_{playerSlug}"),
                Content: content,
                SourceType: "stat-season",
                SourceFile: relativePath,
                Player: player,
                Team: t.Team,
                Week: null,
                Section: $"{player} — Season Totals");
        }
    }

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
