using System.Diagnostics;
using System.Text.Json;
using Azure.Identity;
using InvisibleDefender.Domain;
using InvisibleDefender.Narration;
using Microsoft.Extensions.Options;

// Deliberate live inference only. No ExplanationService/cache or Azure configuration mutations.
public static class LiveNarrationProbe
{
    public static DefensiveInsight[] Plan(IEnumerable<DefensiveInsight> insights, int rounds)
    {
        if (rounds is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(rounds));
        var ranked = insights.Where(i => i.IsKeyMoment).Take(5).ToArray();
        if (ranked.Length != 5) throw new InvalidOperationException("Expected all five ranked moments.");
        return Enumerable.Range(0, rounds).SelectMany(_ => ranked).ToArray();
    }

    public static async Task<bool> Run(DefensiveInsight[] insights)
    {
        var rounds = int.Parse(Environment.GetEnvironmentVariable("NARRATION_LIVE_ROUNDS") ?? "3");
        var delaySeconds = int.Parse(Environment.GetEnvironmentVariable("NARRATION_LIVE_DELAY_SECONDS") ?? "15");
        if (delaySeconds is < 0 or > 60) throw new ArgumentOutOfRangeException(nameof(delaySeconds));
        var plan = Plan(insights, rounds);
        var options = new NarrationOptions {
            Endpoint = Environment.GetEnvironmentVariable("Narration__Endpoint") ?? "",
            Deployment = Environment.GetEnvironmentVariable("Narration__Deployment") ?? ""
        };
        using var http = new HttpClient();
        var narrator = new FoundryNarrator(http, new DefaultAzureCredential(), Options.Create(options));
        var outcomes = new List<(string Insight, string Rule, string Field)>();
        Console.WriteLine($"LIVE probe prompt={FoundryNarrator.NarratorPromptVersion} tokens={options.MaxOutputTokens} timeout={options.TimeoutSeconds}s audience=analyst rounds={rounds} cache=bypassed identity=local-default-credential");
        for (var index = 0; index < plan.Length; index++) {
            if (index > 0) await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            var insight = plan[index];
            var evidence = EvidenceBuilder.Build(insight);
            var rule = "validated";
            var field = "response";
            string? raw = null;
            var timer = Stopwatch.StartNew();
            try {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
                raw = await narrator.Generate(evidence, "analyst", timeout.Token);
                NarrativeValidator.Parse(raw, evidence, "analyst", (r, f) => { rule = r; field = f; });
            } catch (OperationCanceledException) { rule = "timeout"; }
            catch (HttpRequestException ex) { rule = "http_failure"; field = ex.StatusCode?.ToString() ?? "transport"; }
            catch (Exception ex) when (ex is not OutOfMemoryException) { rule = "generation_exception"; field = ex.GetType().Name; }
            outcomes.Add((insight.Id, rule, field));
            Console.WriteLine("LIVE_RESULT " + JsonSerializer.Serialize(new {
                round = index / 5 + 1, rank = index % 5 + 1, insightId = insight.Id,
                fingerprint = EvidenceBuilder.Fingerprint(evidence), rule, field, elapsedMs = timer.ElapsedMilliseconds,
                // Synthetic prose is opt-in, never credentials, tokens or HTTP envelopes.
                prose = Environment.GetEnvironmentVariable("NARRATION_LIVE_PROSE") == "1" ? raw : null
            }));
        }
        foreach (var group in outcomes.GroupBy(o => new { o.Insight, o.Rule, o.Field }))
            Console.WriteLine("LIVE_SUMMARY " + JsonSerializer.Serialize(new { group.Key.Insight, group.Key.Rule, group.Key.Field, count = group.Count() }));
        Console.WriteLine($"LIVE_TOTAL validated={outcomes.Count(o => o.Rule == "validated")} rejected={outcomes.Count(o => o.Rule != "validated")} total={outcomes.Count}");
        return outcomes.All(o => o.Rule == "validated");
    }
}
