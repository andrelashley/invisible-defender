using System.Text.Json;
using InvisibleDefender.Domain;
using InvisibleDefender.Narration;

public static class ReplayNarrationProbe
{
    // Revalidate exact captured synthetic responses after tightening a rule, without new inference.
    public static async Task<bool> Run(DefensiveInsight[] insights, string path)
    {
        var accepted = 0;
        var total = 0;
        foreach (var line in await File.ReadAllLinesAsync(path)) {
            if (!line.StartsWith("LIVE_RESULT ", StringComparison.Ordinal)) continue;
            using var row = JsonDocument.Parse(line[12..]);
            var data = row.RootElement;
            var raw = data.GetProperty("prose").GetString();
            if (raw is null) throw new InvalidOperationException("Replay requires NARRATION_LIVE_PROSE=1 capture.");
            var evidence = EvidenceBuilder.Build(insights.Single(i => i.Id == data.GetProperty("insightId").GetString()));
            var rule = "validated";
            var field = "response";
            var narrative = NarrativeValidator.Parse(raw, evidence, "analyst", (r, f) => { rule = r; field = f; });
            if (narrative is not null) accepted++;
            total++;
            Console.WriteLine("REPLAY_RESULT " + JsonSerializer.Serialize(new {
                round = data.GetProperty("round").GetInt32(), rank = data.GetProperty("rank").GetInt32(),
                insightId = evidence.InsightId, fingerprint = EvidenceBuilder.Fingerprint(evidence), rule, field
            }));
        }
        Console.WriteLine($"REPLAY_TOTAL version={FoundryNarrator.NarratorPromptVersion} accepted={accepted} rejected={total - accepted} total={total}");
        return total == 15 && accepted == total;
    }
}
