using System.Text.Json;
using System.Text.RegularExpressions;

namespace InvisibleDefender.Narration;

// Conservative checks for explicit claim classes; not a general semantic entailment model.
public static class AnalystClaimValidator
{
    public static string? Check(string text, Evidence evidence, SemanticFact[] citedFacts)
    {
        var events = evidence.Sequence.Select(item => JsonSerializer.SerializeToElement(item, EvidenceBuilder.Json)).ToArray();
        string? Value(JsonElement item, string property) => item.GetProperty(property).GetString();
        var identities = new[] { evidence.Defender, evidence.BallCarrier, evidence.Receiver }
            .Concat(events.SelectMany(item => new[] { Value(item, "playerId"), Value(item, "targetPlayerId") }))
            .Where(id => id is not null).ToHashSet(StringComparer.Ordinal);
        foreach (Match player in Regex.Matches(text, @"\b[A-Z]\d+\b"))
            if (!identities.Contains(player.Value)) return "unknown_player";
        foreach (Match player in Regex.Matches(text, @"\b(?:defender|carrier|receiver)\s+([A-Z]\d+)\b", RegexOptions.IgnoreCase)) {
            var role = player.Value.Split(' ')[0].ToLowerInvariant();
            var id = player.Groups[1].Value;
            if (role == "defender" && id != evidence.Defender || role == "carrier" && id != evidence.BallCarrier ||
                role == "receiver" && id != evidence.Receiver && !events.Any(item => Value(item, "targetPlayerId") == id))
                return "player_role";
        }
        // Pressure events identify the pressured carrier, not the pressuring defender.
        if (Regex.IsMatch(text, @"\b(?:defender\s+)?[A-Z]\d+\s+(?:appl(?:y|ies|ied)\s+pressure|pressur(?:es|ed|ing)|presses)\b|\bpressure\s+(?:by|from)\s+(?:defender\s+)?[A-Z]\d+\b", RegexOptions.IgnoreCase))
            return "pressure_actor_unrecorded";
        if (Regex.IsMatch(text, @"\b(?:physical barrier|physically block\w*|physical obstruction|readiness|ready to|prevent(?:s|ing)?|increas\w* (?:the )?(?:chance\w*|likelihood|opportunit\w*))\b", RegexOptions.IgnoreCase))
            return "unsupported_physical_or_predictive_claim";
        // All action claims must at least have that action in the cited observations.
        var citedEvents = citedFacts.SelectMany(f => f.SupportingEventIds).ToHashSet();
        var supported = events.Where(item => citedEvents.Contains(Value(item, "id")!)).ToArray();
        var actions = new[] {
            (Pattern: @"\bintercept\w*\b", Types: new[] { "Interception" }),
            (Pattern: @"\b(?:regain\w*|recover\w*)\b", Types: new[] { "Interception", "BallRecovery" }),
            (Pattern: @"\btackl\w*\b", Types: new[] { "Tackle" }),
            (Pattern: @"\b(?:shot|shots|shoot\w*|shooting)\b", Types: new[] { "ShotAttempt" }),
            (Pattern: @"\bsav(?:e|es|ed|ing)\b", Types: new[] { "Save" }),
            (Pattern: @"\b(?:goal|goals|scor\w*|foul\w*|clearance\w*|header\w*|dribbl\w*|crossed|crosses)\b", Types: Array.Empty<string>()),
            (Pattern: @"\b(?:pass(?:ed|es|ing)?|reception)\b", Types: new[] { "PassAttempt", "PassCompleted", "Carry", "Pressure" })
        };
        foreach (var action in actions)
            if (Regex.IsMatch(text, action.Pattern, RegexOptions.IgnoreCase) &&
                !supported.Any(item => action.Types.Contains(Value(item, "eventType")))) return "unsupported_event";
        foreach (Match claim in Regex.Matches(text,
            @"\b([A-Z]\d+)\s+(?:has\s+|had\s+)?(intercept\w*|recover\w*|regain\w*|carri(?:ed|es)|attempted a pass|completed a pass)\b", RegexOptions.IgnoreCase)) {
            var id = claim.Groups[1].Value;
            var verb = claim.Groups[2].Value.ToLowerInvariant();
            var types = verb.StartsWith("intercept") ? new[] { "Interception" } :
                verb.StartsWith("recover") || verb.StartsWith("regain") ? new[] { "Interception", "BallRecovery" } :
                verb.StartsWith("carri") ? new[] { "Carry" } : verb.StartsWith("attempted") ? new[] { "PassAttempt" } : new[] { "PassCompleted" };
            if (!supported.Any(item => Value(item, "playerId") == id && types.Contains(Value(item, "eventType")))) return "event_actor";
        }
        foreach (Match target in Regex.Matches(text, @"\bpass(?:ed)?\s+to\s+(?:player\s+|teammate\s+)?([A-Z]\d+)\b", RegexOptions.IgnoreCase))
            if (!supported.Any(item => Value(item, "eventType") == "PassAttempt" && Value(item, "targetPlayerId") == target.Groups[1].Value)) return "pass_target";
        foreach (Match actor in Regex.Matches(text, @"\b(interception|recovery)\s+by\s+([A-Z]\d+)\b", RegexOptions.IgnoreCase))
            if (!supported.Any(item => Value(item, "playerId") == actor.Groups[2].Value &&
                (Value(item, "eventType") == "Interception" || actor.Groups[1].Value.Equals("recovery", StringComparison.OrdinalIgnoreCase) && Value(item, "eventType") == "BallRecovery"))) return "event_actor";
        if (Regex.IsMatch(text, @"\b(?:zero|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|twenty|thirty|forty|fifty|hundred|thousand|million)\s+(?:metres?|meters?|feet|yards?|seconds?|percent)\b", RegexOptions.IgnoreCase))
            return "spelled_measurement";
        var causalAssertions = Regex.Replace(text,
            @"\b(?:does not|do not|cannot|can not)\s+(?:establish|prove|confirm|demonstrate)\s+(?:direct\s+)?(?:influence|causation|causality|a causal link)\b", "", RegexOptions.IgnoreCase);
        if (Regex.IsMatch(causalAssertions, @"\b(?:(?:led to|leads? to|leading to|resulted in)\s+(?:(?:a|the)\s+)?(?:backward|sideways|pass|goal|recovery|interception)|contribut(?:e|es|ed|ing)\s+to|facilitat\w*|enabl\w*|trigger\w*|prompted|compelled|made \w+ pass|stopped the pass|goals? prevented|proven impact|guarantee\w*)\b", RegexOptions.IgnoreCase))
            return "unsupported_causation";
        return null;
    }
}
