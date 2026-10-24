using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Azure.Core;
using InvisibleDefender.Domain;
using InvisibleDefender.Narration;
using Microsoft.Extensions.Options;

public static class NarrationChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        var insights = new DefensiveInsightEngine(new()).Analyze(Enumerable.Range(0, 120).Select(new SyntheticMatchGenerator().Generate));
        var insight = insights.First(i => i.InsightType == "PassingLaneDenial");
        if (Environment.GetEnvironmentVariable("NARRATION_LIVE_CHECK") == "1") {
            var liveOptions = new NarrationOptions { Endpoint = Environment.GetEnvironmentVariable("Narration__Endpoint") ?? "", Deployment = Environment.GetEnvironmentVariable("Narration__Deployment") ?? "" };
            var live = new FoundryNarrator(new HttpClient(), new Azure.Identity.DefaultAzureCredential(), Options.Create(liveOptions));
            var allLiveValid = true;
            foreach (var audience in new[] { "casual", "analyst" }) {
                var evidence = EvidenceBuilder.Build(insights.First(i => i.IsKeyMoment));
                var raw = await live.Generate(evidence, audience, default);
                Console.WriteLine("LIVE " + audience + " " + raw);
                var valid = NarrativeValidator.Parse(raw, evidence, audience, (rule, field) => Console.WriteLine("REJECT " + audience + " " + rule + " " + field)) is not null;
                Console.WriteLine("LIVE VALID " + audience + " " + valid);
                allLiveValid &= valid;
            }
            check(allLiveValid, "Live Foundry validation for both audiences");
        }
        using (var captured = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "LiveNarrationRegression.json")))) {
            var liveEvidence = EvidenceBuilder.Build(insights.First(i => i.IsKeyMoment));
            foreach (var audience in new[] { "casual", "analyst" })
                check(NarrativeValidator.Parse(captured.RootElement.GetProperty(audience).GetRawText(), liveEvidence, audience) is not null,
                    "Narration: captured real Foundry response validates for " + audience);
        }
        var original = JsonSerializer.Serialize(insight);
        var e = EvidenceBuilder.Build(insight);
        check(e.Actual == insight.ActualMeasurements && e.Previous == insight.PreviousMeasurements && e.Sequence.Length == insight.SupportingObservations.Length && e.SupportingEventIds.SequenceEqual(insight.SupportingEventIds), "Narration: exact measurements and observed sequence payload");
        string Valid(Evidence evidence, string audience) {
            var explanation = audience == "casual"
                ? $"Watch {evidence.Defender} in this defensive moment: the changing passing options offer something to follow beyond the ball itself. The recorded sequence gives viewers a useful glimpse of defensive positioning, although it cannot tell us the reason behind an attacking decision."
                : $"The recorded sequence places {evidence.Defender} at the centre of this defensive moment, with the candidate passing option providing useful context for reviewing the play. The derived analysis uses a coverage threshold of {evidence.Actual.CorridorMetres.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} metres to describe that option. This geometric assessment helps an analyst follow the available routes alongside the recorded actions and distinguish positional coverage from an actual challenge for possession. It offers a focused way to examine defensive work beyond the final action on the ball.";
            return JsonSerializer.Serialize(new Narrative("Reading the defensive moment", explanation, evidence.SupportingEventIds,
                ["The sequence does not establish causation."], evidence.Facts.Select(f => f.FactId).ToArray()), EvidenceBuilder.Json);
        }
        check(NarrativeValidator.Parse(Valid(e, "casual"), e) is not null && NarrativeValidator.Parse(Valid(e, "analyst"), e, "analyst") is not null, "Narration: both audience structures validate");
        foreach (var item in insights) {
            var evidence = EvidenceBuilder.Build(item);
            foreach (var audience in new[] { "casual", "analyst" })
                check(NarrativeValidator.Parse(Valid(evidence, audience), evidence, audience) is not null,
                    "Narration: audience limits and grounding for " + item.InsightType + " " + audience);
        }
        void Reject(string explanation, string label, Evidence? evidence = null, string audience = "analyst") {
            var source = evidence ?? e;
            var bad = FallbackNarrator.Build(source, audience) with { Explanation = explanation };
            check(NarrativeValidator.Parse(JsonSerializer.Serialize(bad, EvidenceBuilder.Json), source, audience) is null, "Narration: " + label);
        }
        var approved = FallbackNarrator.Build(e, "analyst").Explanation;
        Reject(approved.Replace("to the candidate passing segment", "to the ball carrier"), "lane distance is not carrier distance");
        Reject(approved.Replace("to the candidate passing segment", "to the receiver"), "lane distance is not receiver distance");
        Reject(approved.Replace("geometric coverage threshold", "physical corridor width"), "threshold is not obstruction width");
        Reject(approved.Replace("metres", "feet"), "reject wrong units even for a supplied value");
        Reject(approved.Replace("geometric coverage threshold", "observed physical contact distance"), "derived is not observed contact");
        foreach (var claim in new[] { "The defender caused the backward pass.", "The pressure forced the attacker to retreat.",
            "The defender prevented a goal.", "The defender influenced the pass decision.", "The defender intended to stop the pass." })
            Reject(approved + " " + claim, "reject unsupported assertion: " + claim);
        var zero = e with { Actual = e.Actual with { DefenderDistanceMetres = 0 } };
        check(NarrativeValidator.Parse(Valid(zero, "analyst"), zero, "analyst") is not null, "Narration: zero segment distance remains geometric");
        Reject(FallbackNarrator.Build(zero, "analyst").Explanation.Replace("to the candidate passing segment", "to the receiver in physical contact"), "zero is not player overlap", zero);
        var casual = FallbackNarrator.Build(e, "casual").Explanation;
        Reject(casual + " " + casual, "reject casual verbosity", audience: "casual");
        Reject(casual + " " + e.SupportingEventIds[0], "reject casual event ID in main explanation", audience: "casual");
        Reject(approved.Replace("metres", "metres away from the attacker"), "reject reused accurate number with wrong semantics");
        Reject(approved.Replace("metres", "metres (98765)"), "reject inaccurate analyst measurement");
        Reject(approved + " Evidence: other-match:999.", "reject foreign event reference");
        check(FoundryNarrator.NarratorPromptVersion == "v3.1" && ExplanationService.CacheKey(e, "casual", new()) !=
            ExplanationService.CacheKey(e, "casual", new(), "v2"), "Narration: v1 entries unreachable with current cache key");
        var invalidId = FallbackNarrator.Build(e, "casual") with { EvidenceEventIds = ["nonexistent"] };
        check(NarrativeValidator.Parse(JsonSerializer.Serialize(invalidId, EvidenceBuilder.Json), e) is null, "Narration: reject nonexistent evidence IDs");
        foreach (var malformed in new[] { "not json", "{}", "null", "{\"title\":32}", "{\"extra\":true}", "[]" })
            check(NarrativeValidator.Parse(malformed, e) is null, "Narration: reject malformed structure " + malformed);
        var proseId = FallbackNarrator.Build(e, "casual") with { Explanation = $"Defender {e.Defender} covered a passing lane in event {e.MatchId}:999." };
        check(NarrativeValidator.Parse(JsonSerializer.Serialize(proseId, EvidenceBuilder.Json), e) is null, "Narration: reject nonexistent evidence ID in prose");
        var irrelevant = FallbackNarrator.Build(e, "casual") with { Explanation = $"{e.Defender} is mentioned here in an unrelated description about cooking and shopping." };
        check(NarrativeValidator.Parse(JsonSerializer.Serialize(irrelevant, EvidenceBuilder.Json), e) is null, "Narration: reject irrelevant explanation");
        var unsupported = FallbackNarrator.Build(e, "analyst") with { Explanation = $"Defender {e.Defender} was 98765 metres away from the lane and stopped the pass." };
        check(NarrativeValidator.Parse(JsonSerializer.Serialize(unsupported, EvidenceBuilder.Json), e) is null, "Narration: reject unsupported numeric claim");
        var validOriginal = JsonSerializer.Deserialize<Narrative>(Valid(e, "analyst"), EvidenceBuilder.Json)!;
        check(validOriginal.Explanation != FallbackNarrator.Build(e, "analyst").Explanation, "Narration: original non-catalogue prose accepted");
        foreach (var disclaimer in new[] { "The sequence cannot establish what caused the attacking decision.", "The defender did not necessarily force a pass." }) {
            var qualified = validOriginal with { Limitations = [disclaimer] };
            check(NarrativeValidator.Parse(JsonSerializer.Serialize(qualified, EvidenceBuilder.Json), e, "analyst") is not null, "Narration: explicit negative causal disclaimer is legitimate");
            var assertion = qualified with { Limitations = [disclaimer + " The defender caused the backward pass."] };
            string? rule = null, field = null;
            check(NarrativeValidator.Parse(JsonSerializer.Serialize(assertion, EvidenceBuilder.Json), e, "analyst", (r, f) => { rule = r; field = f; }) is null && rule == "unsupported_language" && field == "limitations[0]", "Narration: disclaimer cannot hide a causal assertion; exact diagnostic field");
        }
        check(FoundryNarrator.Instructions.Contains("35 to 60") && FoundryNarrator.Instructions.Contains("65 to 110"), "Narration: audience ranges are unambiguous");
        var foreignFact = validOriginal with { EvidenceFactIds = ["invented"] };
        check(NarrativeValidator.Parse(JsonSerializer.Serialize(foreignFact, EvidenceBuilder.Json), e, "analyst") is null, "Narration: reject foreign fact");
        var distanceFact = e.Facts.First(f => f.FactId == "actual:lane-distance");
        check(distanceFact.FactType == "derived" && distanceFact.Statement.Contains("passing segment") && distanceFact.Measurements.Single().Unit == "metres", "Narration: typed segment evidence");
        foreach (var replacement in new[] { "98765 metres", "2 feet", "2 metres away from the attacker" }) {
            var altered = validOriginal with { Explanation = validOriginal.Explanation.Replace(e.Actual.CorridorMetres.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " metres", replacement) };
            check(NarrativeValidator.Parse(JsonSerializer.Serialize(altered, EvidenceBuilder.Json), e, "analyst") is null, "Narration: original prose rejects invented or misattributed measurement");
        }
        var distance = distanceFact.Measurements.Single().Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var withDistance = validOriginal with { Explanation = validOriginal.Explanation.Replace("The derived analysis uses", $"The derived distance from {e.Defender} to the candidate passing segment is {distance} metres, and analysis uses") };
        check(NarrativeValidator.Parse(JsonSerializer.Serialize(withDistance, EvidenceBuilder.Json), e, "analyst") is not null, "Narration: original segment measurement validates");
        foreach (var wrong in new[] { "receiver", "ball carrier" }) {
            var badDistance = withDistance with { Explanation = withDistance.Explanation.Replace("candidate passing segment", wrong) };
            check(NarrativeValidator.Parse(JsonSerializer.Serialize(badDistance, EvidenceBuilder.Json), e, "analyst") is null, "Narration: original distance cannot become player separation");
        }
        foreach (var causal in new[] { "caused", "forced", "prevented", "intended" }) {
            var badCause = validOriginal with { Title = $"Defender {causal} the outcome" };
            check(NarrativeValidator.Parse(JsonSerializer.Serialize(badCause, EvidenceBuilder.Json), e, "analyst") is null, "Narration: reject causal assertions in original output title");
        }
        var o = new NarrationOptions { Provider = "Foundry", Deployment = "test-deployment", CacheExpirationSeconds = 1, TimeoutSeconds = 1 };
        var fake = new TestNarrator((e, a, ct) => Task.FromResult(Valid(e, a)));
        using var service = new ExplanationService(fake, Options.Create(o));
        var first = await service.Explain(insight, "casual");
        var refresh = await service.Explain(insight, "casual");
        var otherSession = await service.Explain(insight, "casual");
        check(first.Source == "generated" && refresh.Source == "cached" && otherSession.Source == "cached" && fake.Calls == 1, "Narration: backend cache shared across refresh and sessions");
        var analyst = await service.Explain(insight, "analyst");
        check(fake.Calls == 2 && analyst.Narrative.Explanation != first.Narrative.Explanation, "Narration: distinct audience calls and wording with identical evidence");
        check(analyst.EvidenceFingerprint == first.EvidenceFingerprint, "Narration: audience does not change analytical evidence");
        var changed = insight with { ActualMeasurements = insight.ActualMeasurements with { CorridorMetres = 3 } };
        await service.Explain(changed, "casual");
        check(fake.Calls == 3, "Narration: changed evidence invalidates cache");
        check(ExplanationService.CacheKey(e, "casual", o) != ExplanationService.CacheKey(e, "casual", o, "v1"), "Narration: prompt version invalidates cache");
        check(ExplanationService.CacheKey(e, "casual", o) != ExplanationService.CacheKey(e, "casual", new NarrationOptions { Deployment = "different" }), "Narration: deployment changes cache key");
        await Task.Delay(1150);
        check(fake.Calls == 3, "Narration: expiry alone does not trigger inference");
        check((await service.Explain(insight, "casual")).Source == "generated" && fake.Calls == 4, "Narration: explicit request after expiration generates again");
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var concurrent = new TestNarrator((_, _, _) => release.Task);
        using var dedup = new ExplanationService(concurrent, Options.Create(o));
        var requests = Enumerable.Range(0, 20).Select(_ => dedup.Explain(insight, "casual")).ToArray();
        check(concurrent.Calls == 1 && requests.All(t => !t.IsCompleted), "Narration: concurrent identical requests share one inference");
        release.SetResult(Valid(e, "casual"));
        await Task.WhenAll(requests);
        check(concurrent.Calls == 1, "Narration: completed concurrent inference remains single call");
        foreach (var raw in new[] { "{}", JsonSerializer.Serialize(invalidId, EvidenceBuilder.Json) }) {
            var invalid = new TestNarrator((_, _, _) => Task.FromResult(raw));
            using var fallback = new ExplanationService(invalid, Options.Create(o));
            check((await fallback.Explain(insight, "casual")).Source == "fallback", "Narration: invalid output becomes fallback");
            await fallback.Explain(insight, "casual");
            check(invalid.Calls == 2, "Narration: invalid output is never cached as success");
        }
        var slow = new TestNarrator((_, _, ct) => Task.Delay(Timeout.Infinite, ct).ContinueWith<string>(_ => throw new OperationCanceledException(), TaskScheduler.Default));
        using var timed = new ExplanationService(slow, Options.Create(o));
        check((await timed.Explain(insight, "analyst")).Source == "fallback", "Narration: bounded timeout returns fallback");
        var missing = new FoundryNarrator(new HttpClient(), new TestCredential(), Options.Create(new NarrationOptions()));
        using var unconfigured = new ExplanationService(missing, Options.Create(o));
        check((await unconfigured.Explain(insight, "casual")).Source == "fallback", "Narration: missing configuration returns fallback without credentials");
        using var disabled = new ExplanationService(new DisabledNarrator(), Options.Create(new NarrationOptions()));
        check((await disabled.Explain(insight, "casual")).Source == "fallback", "Narration: default disabled provider remains useful");
        using var mock = new ExplanationService(new MockNarrator(), Options.Create(new NarrationOptions { Provider = "Mock" }));
        check((await mock.Explain(insight, "analyst")).Source == "mock", "Narration: mock has explicit non-live source");
        check(JsonSerializer.Serialize(insight) == original, "Narration: no analytical measurements modified");
        // Exercise the actual HTTP adapter, including schema and bearer token handling, without Azure.
        var transport = new TestTransport();
        var foundry = new FoundryNarrator(new HttpClient(transport), new TestCredential(), Options.Create(new NarrationOptions { Endpoint = "https://example.invalid/openai/v1/", Deployment = "gpt-41-mini" }));
        transport.Content = Valid(e, "analyst");
        await foundry.Generate(e, "analyst", default);
        check(transport.Uri == "https://example.invalid/openai/v1/chat/completions" && transport.Auth == "Bearer test-token", "Narration: v1 API and Entra bearer authentication");
        using var body = JsonDocument.Parse(transport.Request!);
        check(body.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("strict").GetBoolean() && body.RootElement.GetProperty("model").GetString() == "gpt-41-mini", "Narration: deployment and strict output schema sent");
        using var supplied = JsonDocument.Parse(body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
        check(supplied.RootElement.GetProperty("measurementDefinitions").GetString() == FoundryNarrator.MeasurementDefinitions,
            "Narration: complete measurement semantics sent to Foundry");
        check(!supplied.RootElement.TryGetProperty("approvedNarratives", out _) && supplied.RootElement.GetProperty("semanticFacts").GetArrayLength() > 0,
            "Narration: semantic facts without a narrative catalogue sent to Foundry");
        check(body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!.Contains("analyst"), "Narration: audience sent to Foundry adapter");
    }
    private sealed class TestNarrator(Func<Evidence, string, CancellationToken, Task<string>> run) : IInsightNarrator
    {
        public int Calls;
        public Task<string> Generate(Evidence e, string a, CancellationToken ct) { Interlocked.Increment(ref Calls); return run(e, a, ct); }
    }
    private sealed class TestCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext context, CancellationToken ct) => new("test-token", DateTimeOffset.UtcNow.AddHours(1));
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct) => ValueTask.FromResult(GetToken(context, ct));
    }
    private sealed class TestTransport : HttpMessageHandler
    {
        public string? Uri, Auth, Request, Content;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            Uri = request.RequestUri!.ToString(); Auth = request.Headers.Authorization!.ToString(); Request = await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { choices = new[] { new { finish_reason = "stop", message = new { content = Content } } } }) };
        }
    }
}
