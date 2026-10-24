using System.Text.Json;
using InvisibleDefender.Domain;
using InvisibleDefender.Narration;
using Microsoft.Extensions.Options;

public static class AnalystNarrationChecks
{
    public static async Task Run(Action<bool, string> check, DefensiveInsight[] insights)
    {
        var insight = insights.First(i => i.IsKeyMoment);
        var evidence = EvidenceBuilder.Build(insight);
        string Natural(string? explanation = null, string[]? facts = null, string? title = null) => JsonSerializer.Serialize(new {
            title = title ?? "Reading the defensive screen",
            explanation = explanation ?? $"{evidence.Defender} shifts into the candidate passing route as the option becomes covered. This positional screen highlights defensive work away from the ball while leaving the attacking decision open to interpretation.",
            evidenceFactIds = facts ?? ["moment"],
            limitations = new[] { "Positioning alone does not establish why the attacker chose another option." }
        }, EvidenceBuilder.Json);
        var raw = Natural();
        var parsed = NarrativeValidator.Parse(raw, evidence, "analyst");
        check(parsed is not null && parsed.EvidenceEventIds.SequenceEqual(evidence.SupportingEventIds),
            "Analyst: concise natural commentary needs no numbers, event IDs or observation/derivation phrases; C# resolves events");
        check(NarrativeValidator.Parse(raw, evidence, "casual") is null,
            "Analyst: new four-field schema does not change Casual requirements");
        var numeric = Natural($"The coverage threshold is 98765 metres for this passing option. This defensive screen helps explain positioning away from the ball without establishing the reason for the attacking decision.");
        check(NarrativeValidator.Parse(numeric, evidence, "analyst") is null, "Analyst: optional fabricated numerical claim rejected");
        var accurate = Natural($"The coverage threshold is 2 metres for this passing option. This defensive screen helps explain positioning away from the ball without establishing the reason for the attacking decision.", ["moment", "actual:coverage"]);
        check(NarrativeValidator.Parse(accurate, evidence, "analyst") is not null, "Analyst: optional precisely grounded number accepted");
        foreach (var (text, label) in new[] {
            ($"Defender B99 shifts into the candidate passing route as the option becomes covered. This positional screen highlights defensive work away from the ball while leaving the attacking decision open to interpretation.", "invented identity"),
            ($"{evidence.Defender} scored a goal after covering the passing route. This action highlights defensive work away from the ball and provides a way to examine the attacking options.", "fabricated goal event"),
            ($"{evidence.Defender} made a tackle after covering the passing route. This action highlights defensive work away from the ball and provides a way to examine the attacking options.", "fabricated tackle"),
            ($"{evidence.Defender} caused the attacker to pass backward instead of progressing. This positional screen highlights defensive work away from the ball while leaving other attacking options open for review.", "unsupported causal claim"),
            ($"{evidence.Defender} prevented a goal by covering the passing route. This positional screen highlights defensive work away from the ball and offers definitive proof of defensive impact.", "proven goal prevention"),
            ($"The coverage threshold is two metres for this passing option. This defensive screen helps explain positioning away from the ball without establishing the reason for the attacking decision.", "spelled-out measurement")
        }) check(NarrativeValidator.Parse(Natural(text), evidence, "analyst") is null, "Analyst: reject " + label);
        check(NarrativeValidator.Parse(Natural(facts: ["invented"]), evidence, "analyst") is null, "Analyst: invalid fact reference rejected");
        check(NarrativeValidator.Parse(Natural(facts: ["moment", "moment"]), evidence, "analyst") is null, "Analyst: duplicate fact reference rejected");
        var concise = JsonSerializer.Deserialize<Narrative>(raw, EvidenceBuilder.Json)! with { EvidenceEventIds = evidence.SupportingEventIds };
        foreach (var limitation in new[] { "The sequence does not reveal whether defensive alignment influenced the attacking player's choice.",
            "The sequence does not indicate whether coverage directly influenced the attacking player's decision.",
            "This sequence does not provide information about player A2's intended passing choice." }) {
            check(NarrativeValidator.Parse(JsonSerializer.Serialize(concise with { Limitations = [limitation] }, EvidenceBuilder.Json), evidence, "analyst") is not null,
                "Analyst: explicit uncertainty about causation or intent is not a positive assertion");
            check(NarrativeValidator.Parse(JsonSerializer.Serialize(concise with { Limitations = [limitation + " The defender caused the pass."] }, EvidenceBuilder.Json), evidence, "analyst") is null,
                "Analyst: negative limitation cannot conceal a separate causal assertion");
        }
        var recovery = EvidenceBuilder.Build(insights.First(i => i.InsightType == "DefensiveRecovery" && i.IsKeyMoment));
        var cited = recovery.Facts;
        check(AnalystClaimValidator.Check($"{recovery.BallCarrier} intercepted the pass.", recovery, cited) == "event_actor" &&
            AnalystClaimValidator.Check($"Interception by {recovery.BallCarrier}.", recovery, cited) == "event_actor",
            "Analyst: supplied player cannot be substituted as the recovering actor");
        check(AnalystClaimValidator.Check($"{recovery.Defender} intercepted the pass.", recovery, cited) is null,
            "Analyst: correctly attributed interception accepted");
        check(AnalystClaimValidator.Check("This spatial positioning likely contributed to the defensive recovery that followed.", recovery, cited) == "unsupported_causation",
            "Analyst: hedged contribution to recovery is still an unsupported causal claim");
        foreach (var claim in new[] { "Positional coverage leading to interception", "This created a physical barrier within the passing corridor.",
            "Defender B5 applies pressure directly on carrier A2.", "Following pressure by defender B5 on carrier A2.",
            "The alignment facilitated the interception.", "This positioning increases opportunities to regain possession." })
            check(AnalystClaimValidator.Check(claim, recovery, cited) is not null, "Analyst: reject unsupported physical, pressure-agent or causal interpretation: " + claim);
        check(AnalystClaimValidator.Check($"Defender {recovery.BallCarrier} covers the option.", recovery, cited) == "player_role",
            "Analyst: known carrier cannot be presented as defender");
        check(AnalystClaimValidator.Check($"A pass to {recovery.BallCarrier}.", recovery, cited) == "pass_target",
            "Analyst: known player cannot be substituted as the actual pass target");
        foreach (var source in new[] { "generated", "fallback", "mock" }) {
            var options = new NarrationOptions { Provider = source == "mock" ? "Mock" : "Foundry" };
            using var service = new ExplanationService(source == "mock" ? new MockNarrator() : new FixedNarrator(source == "generated" ? raw : "{}"), Options.Create(options));
            var result = await service.Explain(insight, "analyst");
            check(result.Source == source && result.VerifiedFacts is { } verified &&
                verified.Actual == evidence.Actual && verified.Previous == evidence.Previous && verified.SignificanceScore == evidence.RankingScore &&
                verified.SupportingEventIds.SequenceEqual(evidence.SupportingEventIds) &&
                JsonSerializer.Serialize(verified.Facts) == JsonSerializer.Serialize(evidence.Facts),
                "Analyst: authoritative measurements, units, scores, event IDs and facts survive " + source + " independently of prose");
            if (source == "generated") {
                var cached = await service.Explain(insight, "analyst");
                check(cached.Source == "cached" && cached.VerifiedFacts == result.VerifiedFacts,
                    "Analyst: cached commentary retains server factual presentation");
            }
        }
    }
    private sealed class FixedNarrator(string raw) : IInsightNarrator
    {
        public Task<string> Generate(Evidence evidence, string audience, CancellationToken ct) => Task.FromResult(raw);
    }
}
