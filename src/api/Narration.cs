using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.Core;
using InvisibleDefender.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
namespace InvisibleDefender.Narration;
public sealed class NarrationOptions
{
    public string Provider { get; set; } = "Disabled";
    public string Endpoint { get; set; } = "";
    public string Deployment { get; set; } = "";
    public bool ManagedIdentity { get; set; }
    public string? ManagedIdentityClientId { get; set; }
    public int MaxOutputTokens { get; set; } = 650;
    public int TimeoutSeconds { get; set; } = 15;
    public int CacheExpirationSeconds { get; set; } = 3600;
    public int CacheSize { get; set; } = 256;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record ExplanationRequest(string MatchId, string InsightId, string Audience, string Scope = "live");
public record Narrative(string Title, string Explanation, string[] EvidenceEventIds, string[] Limitations, string[]? EvidenceFactIds = null);
public record ExplanationResult(Narrative Narrative, string Source, string Audience, string EvidenceFingerprint);
public record LaneCoverage(int? PreviousBlockerCount, int ActualBlockerCount);
public record Evidence(string MatchId, string InsightId, string Category, double Timestamp, string? Defender,
    string? BallCarrier, string? Receiver, InsightMeasurement Actual, InsightMeasurement? Previous,
    int RankingScore, SignificanceFactor[] RankingFactors, LaneCoverage Coverage, string[] SupportingEventIds, object[] Sequence)
{
    public SemanticFact[] Facts => SemanticEvidence.Build(this);
}
public static class EvidenceBuilder
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static Evidence Build(DefensiveInsight i) => new(i.MatchId, i.Id, i.InsightType, i.TriggerEvent.Timestamp,
        i.PrimaryDefenderId, i.AttackingPlayerId, i.AffectedReceiverId, i.ActualMeasurements, i.PreviousMeasurements,
        i.SignificanceScore, i.SignificanceFactors, new(i.PreviousMeasurements?.BlockerIds.Length, i.ActualMeasurements.BlockerIds.Length), i.SupportingEventIds,
        i.SupportingObservations.OrderBy(e => e.Timestamp).ThenBy(e => e.Id, StringComparer.Ordinal).Select(e => (object)new {
            e.Id, e.Timestamp, e.EventType, e.PlayerId, e.TeamId, e.TargetPlayerId, e.Outcome, e.BallInFlight }).ToArray());
    public static string Fingerprint(Evidence e) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(e, Json)));
}
public record SemanticMeasurement(string Subject, string Relationship, double Value, string Unit);
public record SemanticFact(string FactId, string FactType, string Statement, string[] SupportingEventIds,
    SemanticMeasurement[] Measurements, string[] PermittedInterpretations, string[] ProhibitedInterpretations);
public static class SemanticEvidence
{
    public static SemanticFact[] Build(Evidence e)
    {
        var facts = new List<SemanticFact>();
        foreach (var item in e.Sequence) {
            var eventData = JsonSerializer.SerializeToElement(item, EvidenceBuilder.Json);
            var id = eventData.GetProperty("id").GetString()!;
            facts.Add(new("event:" + id, "observed",
                $"Recorded synthetic event: {eventData.GetProperty("eventType").GetString()}, player {eventData.GetProperty("playerId").GetString()}, target {eventData.GetProperty("targetPlayerId").GetString() ?? "unavailable"}.",
                [id], [], ["Describe the recorded action and chronological association."],
                ["Event order proves causation.", "A candidate receiver was necessarily the actual pass target."]));
        }
        facts.Add(new("moment", "derived", $"{e.Category}: defender {e.Defender}, carrier {e.BallCarrier}, candidate receiver {e.Receiver}. " +
            (e.Category == "PassingLaneDenial" ? "Defender movement coincided with an available passing option becoming geometrically covered." :
             e.Category == "PassingLaneReopening" ? "Defender movement coincided with a covered passing option reopening; this is not a defensive success." :
             e.Category == "DefensiveRecovery" ? "The recorded sequence establishes possession recovery; earlier coverage does not prove its cause." :
             "A completed backward or sideways pass followed recorded pressure and measured coverage; causation is unproven."),
            e.SupportingEventIds, [], ["Explain changing passing options or observed recovery as defensive context."],
            ["The defender forced a pass.", "The defender prevented a goal.", "Player intentions are known."]));
        Add(e.Actual, "actual");
        if (e.Previous is not null) Add(e.Previous, "previous");
        return facts.ToArray();
        void Add(InsightMeasurement m, string snapshot) {
            if (m.DefenderDistanceMetres is double distance) {
                var rounded = Math.Round(distance, 2, MidpointRounding.AwayFromZero);
                facts.Add(new(snapshot + ":lane-distance", "derived",
                    FormattableString.Invariant($"{snapshot} snapshot: {e.Defender} is {rounded:0.##} metres from the candidate passing segment from carrier {e.BallCarrier} to receiver {m.ReceiverId}."),
                    [m.EventId], [new(e.Defender ?? "defender", "passing segment", rounded, "metres")],
                    ["Distance describes defender alignment with the candidate passing segment.", "Zero means alignment with the segment."],
                    ["Distance to either player.", "Physical contact or player overlap.", "An attempted pass was physically obstructed."]));
            }
            facts.Add(new(snapshot + ":coverage", "derived",
                FormattableString.Invariant($"{snapshot} snapshot: the geometric coverage threshold is {m.CorridorMetres:0.##} metres; candidate receiver {m.ReceiverId}; blockers: {string.Join(", ", m.BlockerIds)}."),
                [m.EventId], [new("coverage threshold", "coverage threshold", m.CorridorMetres, "metres")],
                ["Listed blockers geometrically cover the candidate option."], ["Threshold is physical obstruction width.", "Coverage proves an attempted pass was stopped."]));
        }
    }
}
public interface IInsightNarrator { Task<string> Generate(Evidence evidence, string audience, CancellationToken cancellationToken); }
public sealed class MockNarrator : IInsightNarrator
{
    public Task<string> Generate(Evidence e, string audience, CancellationToken cancellationToken) =>
        Task.FromResult(JsonSerializer.Serialize(FallbackNarrator.Build(e, audience), EvidenceBuilder.Json));
}
// Server-authored prose is retained exclusively for fallback and local mock use.
public static class FallbackNarrator
{
    public static Narrative Build(Evidence e, string audience) => Approved(e, audience)[0];

    public static Narrative[] Approved(Evidence e, string audience)
    {
        var moment = e.Category switch {
            "PassingLaneDenial" => $"Defender {e.Defender} moved while a previously open passing option became covered in the positional analysis.",
            "PassingLaneReopening" => $"A previously covered passing option reopened alongside movement by defender {e.Defender} in the positional analysis.",
            "DefensiveRecovery" => $"Defender {e.Defender} regained possession in the recorded sequence of synthetic match events.",
            "AttackingRedirection" => $"A completed backward or sideways pass followed a recorded pressure event, with defender {e.Defender} covering a passing option in the positional analysis.",
            _ => $"The supplied synthetic sequence records defender {e.Defender} and a candidate passing option for analysis."
        };
        var interest = "This moment highlights defensive work beyond the final ball action and helps viewers follow the changing passing options, although the sequence alone cannot establish what caused the attacking decision.";
        string explanation;
        if (audience == "analyst") {
            var distance = e.Actual.DefenderDistanceMetres is double d
                ? FormattableString.Invariant($"The derived shortest perpendicular distance from defender {e.Defender} to the candidate passing segment is {d:0.##} metres")
                : "The defender's perpendicular distance to the candidate passing segment is unavailable";
            explanation = moment + " " + distance + FormattableString.Invariant($", with a geometric coverage threshold of {e.Actual.CorridorMetres:0.##} metres; these describe segment geometry rather than player separation or obstruction width.") +
                " Recorded events and positions are observations, while lane coverage and significance rankings are derived interpretations of those observations." +
                " Temporal ordering does not establish defensive intent, the cause of a passing decision, or a prevented goal; no hypothetical player movement is included in this evidence.";
        } else explanation = moment + " " + interest;
        var narrative = new Narrative("Synthetic defensive evidence", explanation, e.SupportingEventIds,
            ["Synthetic observations; lane coverage and significance rankings are derived, and causation is unproven."], e.Facts.Select(f => f.FactId).ToArray());
        // Legacy fallback variants; never supplied to Foundry.
        return [narrative, narrative with { Title = "Defensive moment explained" }];
    }
}
public static class NarrativeValidator
{
    public static Narrative? Parse(string raw, Evidence e, string audience = "casual", Action<string, string>? rejected = null)
    {
        Narrative? Reject(string rule, string field) { rejected?.Invoke(rule, field); return null; }
        try {
            if (audience is not ("casual" or "analyst")) return Reject("audience", "audience");
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            string[] fields = ["title", "explanation", "evidenceFactIds", "evidenceEventIds", "limitations"];
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != fields.Length ||
                fields.Any(f => !root.TryGetProperty(f, out _))) return Reject("json_shape", "root");
            var n = JsonSerializer.Deserialize<Narrative>(raw, EvidenceBuilder.Json);
            if (n is null) return Reject("deserialization", "root");
            if (string.IsNullOrWhiteSpace(n.Title) || n.Title.Length > 120) return Reject("field_constraints", "title");
            if (string.IsNullOrWhiteSpace(n.Explanation)) return Reject("field_constraints", "explanation");
            if (n.EvidenceFactIds is not { Length: > 0 }) return Reject("field_constraints", "evidenceFactIds");
            if (n.EvidenceEventIds is not { Length: > 0 }) return Reject("field_constraints", "evidenceEventIds");
            if (n.Limitations is null || n.Limitations.Length > 2) return Reject("field_constraints", "limitations");
            for (var i = 0; i < n.Limitations.Length; i++)
                if (string.IsNullOrWhiteSpace(n.Limitations[i]) || n.Limitations[i].Length > 240)
                    return Reject("field_constraints", $"limitations[{i}]");
            var facts = e.Facts.Where(f => n.EvidenceFactIds.Contains(f.FactId)).ToArray();
            if (facts.Length != n.EvidenceFactIds.Length) return Reject("unknown_or_duplicate_fact", "evidenceFactIds");
            if (n.EvidenceEventIds.Distinct().Count() != n.EvidenceEventIds.Length ||
                n.EvidenceEventIds.Any(id => !e.SupportingEventIds.Contains(id)))
                return Reject("unknown_or_duplicate_event", "evidenceEventIds");
            if (facts.SelectMany(f => f.SupportingEventIds).Any(id => !n.EvidenceEventIds.Contains(id)))
                return Reject("missing_fact_supporting_event", "evidenceEventIds");
            var words = Regex.Matches(n.Explanation, @"\S+").Count;
            var sentences = Regex.Split(n.Explanation.Trim(), @"(?<=[.!?])\s+").Length;
            if (audience == "casual" ? words is < 35 or > 65 || sentences is < 2 or > 3 : words is < 65 or > 115 || sentences is < 3 or > 5) return Reject("audience_length", "explanation");
            var text = n.Title + " " + n.Explanation + " " + string.Join(" ", n.Limitations);
            if (audience == "casual" && (e.SupportingEventIds.Any(n.Explanation.Contains) ||
                Regex.IsMatch(n.Explanation, "perpendicular|geometric|projection", RegexOptions.IgnoreCase))) return Reject("casual_style", "explanation");
            // Conservative lexical checks catch known failure classes, not all semantic errors.
            var proseFields = new[] { (Field: "title", Text: n.Title), (Field: "explanation", Text: n.Explanation) }
                .Concat(n.Limitations.Select((l, i) => (Field: $"limitations[{i}]", Text: l))).ToArray();
            foreach (var field in proseFields) {
                // Remove only explicit negative causal predicates, never the remainder of a clause.
                var assertions = Regex.Replace(field.Text,
                    @"\b(?:cannot|can not|does not|do not) establish (?:what caused|that \w+ caused)|\b(?:did not|does not|cannot|can not) (?:necessarily )?(?:force|cause|prevent|influence|intend)\b",
                    "", RegexOptions.IgnoreCase);
                if (Regex.IsMatch(assertions, @"\b(forced?|caused?|prevented|influenced|intended|overlap|contact|obstruction width|feet|yards|percent)\b", RegexOptions.IgnoreCase))
                    return Reject("unsupported_language", field.Field);
            }
            if (!Regex.IsMatch(n.Explanation, "defen|pass|possession|lane|recover", RegexOptions.IgnoreCase)) return Reject("football_relevance", "explanation");
            // Every numerical clause must name the relationship and subject, not just reuse a valid number.
            var withoutIds = text;
            foreach (var id in new[] { e.Defender, e.BallCarrier, e.Receiver }.Where(id => !string.IsNullOrEmpty(id)))
                withoutIds = withoutIds.Replace(id!, "PLAYER", StringComparison.Ordinal);
            var originalClauses = Regex.Split(text, @"(?<=[.!?;])\s+");
            var clauses = Regex.Split(withoutIds, @"(?<=[.!?;])\s+");
            for (var index = 0; index < clauses.Length; index++) {
                var clause = clauses[index];
                var originalClause = originalClauses[index];
                var clauseOffset = originalClauses.Take(index).Sum(c => c.Length + 1);
                var failedField = clauseOffset < n.Title.Length ? "title" :
                    clauseOffset < n.Title.Length + 1 + n.Explanation.Length ? "explanation" : "limitations";
                foreach (Match m in Regex.Matches(clause, @"(?<![\w.])-?\d+(?:\.\d+)?(?![\w.])")) {
                    if (!double.TryParse(m.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)) return Reject("numeric_parse", failedField);
                    var numeric = facts.SelectMany(f => f.Measurements).Where(v => v.Value == value).ToArray();
                    if (!numeric.Any(v => clause.Contains(v.Relationship, StringComparison.OrdinalIgnoreCase) &&
                        clause.Contains(v.Unit, StringComparison.OrdinalIgnoreCase) &&
                        originalClause.Contains(v.Subject, StringComparison.Ordinal))) return Reject("numeric_grounding", failedField);
                    if (Regex.IsMatch(clause, "away from|to the receiver|to the ball carrier|from the attacker|physical|observed.*distance", RegexOptions.IgnoreCase)) return Reject("numeric_relationship", failedField);
                }
            }
            if (Regex.IsMatch(text, @"\b(zero|one|two|three|four|five|six|seven|eight|nine|ten)\s+(metres|meters|feet|yards|seconds|percent)", RegexOptions.IgnoreCase)) return Reject("spelled_measurement", "title/explanation/limitations");
            if (audience == "analyst" && (!Regex.IsMatch(withoutIds, @"\d+(?:\.\d+)?\s+metres") || !Regex.IsMatch(n.Explanation, "observ|record", RegexOptions.IgnoreCase) ||
                !Regex.IsMatch(n.Explanation, "derived|analysis|geometr", RegexOptions.IgnoreCase) || n.Limitations.Length != 1)) return Reject("analyst_requirements", "explanation/limitations");
            return n;
        } catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException) { return Reject("deserialization", "root"); }
    }
}
public sealed class FoundryNarrator(HttpClient http, TokenCredential credential, IOptions<NarrationOptions> options, NarrationDiagnostics? diagnostics = null) : IInsightNarrator
{
    public const string NarratorPromptVersion = "v3.1";
    public const string Instructions = """
        Compose original football commentary from semantic facts only. Do not select or copy a template.
        Return JSON with title, explanation, evidenceFactIds, evidenceEventIds and limitations. Cite every fact used
        and all its supporting events. Respect permitted and prohibited interpretations. Explain why viewers should notice
        this moment. Use fresh, audience-specific prose; never invent actions, intentions or causal conclusions.
        If quoting a measurement, retain its subject, relationship and unit in the same sentence.
        Use only supplied measurements; do not calculate, round further, or spell numbers out.
        OBSERVED: recorded player positions, passing attempts/completions, possession changes and event ordering.
        DERIVED: geometric lane coverage, distances to passing segments and heuristic significance rankings.
        HYPOTHETICAL: counterfactual player movements and recalculated availability; none are supplied here.
        UNSUPPORTED: intentions, definitive causes of passing decisions and goals prevented without a validated model.
        Never elevate derived or hypothetical results into observations. Never claim the defender caused a backward pass,
        forced retreat, prevented a goal or influenced a passing decision. Temporal association is not causation.
        Casual: 2 to 3 short plain English sentences, 35 to 60 words, no event IDs or geometry jargon in the explanation,
        explain why the moment matters, at most one brief uncertainty qualification.
        Analyst: 3 to 5 sentences, 65 to 110 words, relevant precise measurements, explicit observation/inference distinction; exactly one concise uncertainty statement in limitations.
        The audience value casual means Casual; analyst means Analyst. These are mandatory output limits.
        Casual: put exactly one short uncertainty statement in limitations. Do not include measurements or technical
        geometry terms in casual prose. Keep explanation between 35 and 60 words; count before returning.
        Analyst: keep explanation between 65 and 110 words; count before returning.
        For every quoted measurement copy its exact measurement subject, relationship and unit into the same clause.
        In particular use "passing segment", never substitute "passing lane" in a numerical distance claim.
        Avoid repetitive disclaimers. Numerical distance claims must say
        passing segment; threshold claims must say coverage threshold. Zero distance is segment alignment, not player overlap.
        Use supporting event IDs only in evidenceEventIds. Ranking points are not probabilities or confidence scores.
        """;
    public const string MeasurementDefinitions = """
        actual and previous are DERIVED measurement snapshots tied to eventId (an OBSERVED event reference);
        previous is absent when no earlier measurement is available. Null means unavailable, never zero.
        defenderDistanceMetres (defenderDistanceFromLane): metres; shortest perpendicular distance from the defender
        to the candidate passing segment, available for an interior projection. NOT distance from ball carrier or receiver,
        NOT physical contact. Zero means geometrically on the segment, not necessarily at either endpoint.
        corridorMetres (coverageThreshold): metres; geometric threshold classifying a lane as covered,
        NOT measured obstruction width and NOT evidence of intention.
        ballPosition, receiverPosition, defenderPosition: OBSERVED synthetic pitch coordinates in metres (x,y),
        endpoints and defender location at the measurement event, not distances or predicted movements.
        receiverId identifies the candidate receiver, not necessarily the actual pass target.
        blockerIds: DERIVED identities meeting the geometric coverage rule, not observed tackles or contacts.
        coverage.actualBlockerCount/previousBlockerCount: DERIVED count of blockerIds in the corresponding snapshot;
        counts are players, not metres, probabilities or effects. Missing previous count means unavailable.
        timestamp and sequence.timestamp: OBSERVED elapsed match time in seconds, not durations or distances.
        rankingScore and rankingFactors.points: DERIVED heuristic selection points; rankingFactors.reason describes
        the deterministic selection rule, not a causal explanation, calibrated quality or probability.
        matchId, insightId, category and player identities: identifiers/classification, not numeric measurements.
        supportingEventIds and sequence.id: exact OBSERVED synthetic event references. sequence eventType, outcome,
        playerId, teamId, targetPlayerId and ballInFlight describe recorded events, ordered by timestamp then ID;
        ordering establishes sequence, never causation. No counterfactual estimates are supplied.
        """;
    public async Task<string> Generate(Evidence evidence, string audience, CancellationToken cancellationToken)
    {
        var o = options.Value;
        if (!Uri.TryCreate(o.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" ||
            !endpoint.AbsolutePath.TrimEnd('/').EndsWith("/openai/v1", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(o.Deployment))
            throw new InvalidOperationException("Foundry configuration is missing or invalid.");
        var token = await credential.GetTokenAsync(new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]), cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, o.Endpoint.TrimEnd('/') + "/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        var schema = new { type = "object", additionalProperties = false, required = new[] { "title", "explanation", "evidenceFactIds", "evidenceEventIds", "limitations" },
            properties = new { evidenceFactIds = new { type = "array", items = new { type = "string" } }, title = new { type = "string" }, explanation = new { type = "string" }, evidenceEventIds = new { type = "array", items = new { type = "string" } }, limitations = new { type = "array", items = new { type = "string" } } } };
        request.Content = JsonContent.Create(new { model = o.Deployment, max_completion_tokens = Math.Clamp(o.MaxOutputTokens, 128, 2000),
            messages = new[] { new { role = "system", content = Instructions }, new { role = "user", content = JsonSerializer.Serialize(new { audience, measurementDefinitions = MeasurementDefinitions, semanticFacts = evidence.Facts,
                outputRequirements = audience == "casual"
                    ? "Write for a Casual Fan. explanation: exactly 2 sentences, 35 to 60 words total. Describe the movement and changing passing option in plain English without numbers, event IDs or geometry jargon. limitations: exactly one brief sentence. Do not use force, forced, cause, caused, prevented, influenced, intended, overlap or contact, even in disclaimers."
                    : "Write for an Analyst. explanation: exactly 4 sentences, 65 to 110 words total. Describe recorded actions and derived analysis separately. Include one measurement using the exact phrase coverage threshold and its supplied value in metres. Do not say away from, physical, force, forced, cause, caused, prevented, influenced, intended, overlap or contact, even in disclaimers. limitations: exactly one brief sentence.",
                citationRequirements = "Cite the exact fact IDs used. evidenceEventIds must include EVERY supportingEventId of EVERY cited fact, including all events for moment. Do not abbreviate IDs."
            }, EvidenceBuilder.Json) } },
            response_format = new { type = "json_schema", json_schema = new { name = "defensive_explanation", strict = true, schema } } });
        diagnostics?.Write("foundry_call", audience, "http", false);
        using var response = await http.SendAsync(request, cancellationToken);
        diagnostics?.Write(response.IsSuccessStatusCode ? "foundry_http_success" : "foundry_http_failure", audience, "http", false, (int)response.StatusCode);
        response.EnsureSuccessStatusCode();
        try {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var choice = body.RootElement.GetProperty("choices")[0];
        if (choice.GetProperty("finish_reason").GetString() != "stop") throw new InvalidOperationException("Incomplete generation.");
        var content = choice.GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(content)) throw new InvalidOperationException("Missing content.");
        diagnostics?.Write("foundry_response_parsed", audience, "choices[0].message.content", false);
        return content;
        } catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException) {
            diagnostics?.Write("foundry_response_parse", audience, "choices[0].finish_reason/message.content", true);
            throw;
        }
    }
}
public sealed class ExplanationService(IInsightNarrator narrator, IOptions<NarrationOptions> options, NarrationDiagnostics? diagnostics = null) : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = Math.Clamp(options.Value.CacheSize, 1, 10000) });
    private readonly ConcurrentDictionary<string, Lazy<Task<ExplanationResult>>> pending = new();
    public static string CacheKey(Evidence e, string audience, NarrationOptions o, string promptVersion = FoundryNarrator.NarratorPromptVersion) =>
        string.Join('|', e.MatchId, e.InsightId, audience, EvidenceBuilder.Fingerprint(e), o.Provider, o.Endpoint, o.Deployment, promptVersion);
    public async Task<ExplanationResult> Explain(DefensiveInsight insight, string audience, CancellationToken cancellationToken = default)
    {
        var e = EvidenceBuilder.Build(insight);
        var key = CacheKey(e, audience, options.Value);
        if (cache.TryGetValue<ExplanationResult>(key, out var hit)) return hit! with { Source = "cached" };
        var work = pending.GetOrAdd(key, _ => new Lazy<Task<ExplanationResult>>(() => Generate(e, audience, key)));
        var task = work.Value;
        _ = task.ContinueWith(_ => pending.TryRemove(new KeyValuePair<string, Lazy<Task<ExplanationResult>>>(key, work)), TaskScheduler.Default);
        try { return await task.WaitAsync(cancellationToken); }
        finally { if (task.IsCompleted) pending.TryRemove(new KeyValuePair<string, Lazy<Task<ExplanationResult>>>(key, work)); }
    }
    private async Task<ExplanationResult> Generate(Evidence e, string audience, string key)
    {
        if (cache.TryGetValue<ExplanationResult>(key, out var hit)) return hit! with { Source = "cached" };
        var fingerprint = EvidenceBuilder.Fingerprint(e);
        diagnostics?.Write("generation_started", audience, narrator is FoundryNarrator ? "Foundry" : "local_provider", false);
        try {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(options.Value.TimeoutSeconds, 1, 60)));
            var raw = await narrator.Generate(e, audience, timeout.Token).WaitAsync(timeout.Token);
            var validated = options.Value.Provider == "Mock" && narrator is MockNarrator
                ? FallbackNarrator.Build(e, audience) : NarrativeValidator.Parse(raw, e, audience, (rule, field) => diagnostics?.Write(rule, audience, field, true));
            if (validated is not null) {
                diagnostics?.Write("validation_passed", audience, "response", false);
                var source = options.Value.Provider == "Mock" ? "mock" : "generated";
                var result = new ExplanationResult(validated, source, audience, fingerprint);
                if (source == "generated") cache.Set(key, result, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Clamp(options.Value.CacheExpirationSeconds, 1, 86400)) });
                return result;
            }
        } catch (Exception ex) when (ex is not OutOfMemoryException) { diagnostics?.Write(ex is OperationCanceledException ? "timeout" : "generation_exception", audience, ex.GetType().Name, true); /* No exception messages or payloads. */ }
        diagnostics?.Write("fallback_used", audience, "response", true);
        return new(FallbackNarrator.Build(e, audience), "fallback", audience, fingerprint);
    }
    public void Dispose() => cache.Dispose();
}
public sealed class DisabledNarrator : IInsightNarrator
{
    public Task<string> Generate(Evidence evidence, string audience, CancellationToken cancellationToken) => throw new InvalidOperationException("Narration disabled.");
}

// Only fixed stage/rule names, field names and HTTP status codes are logged.
public sealed class NarrationDiagnostics(IHostEnvironment environment, ILogger<NarrationDiagnostics> logger)
{
    public void Write(string rule, string audience, string field, bool fallback, int? status = null)
    {
        if (environment.IsDevelopment())
            logger.LogInformation("Narration rule={Rule} audience={Audience} field={Field} fallback={Fallback} httpStatus={Status}",
                rule, audience, field, fallback, status);
    }
}
