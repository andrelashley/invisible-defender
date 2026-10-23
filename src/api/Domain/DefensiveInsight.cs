namespace InvisibleDefender.Domain;

public record InsightMeasurement(string EventId, string? ReceiverId, Point BallPosition,
    Point? ReceiverPosition, string[] BlockerIds, double? DefenderDistanceMetres, Point? DefenderPosition, double CorridorMetres);
public record SignificanceFactor(string Reason, int Points);
public record DefensiveInsight(string Id, string MatchId, string ScenarioId, string InsightType,
    double StartTimestamp, double EndTimestamp, string? PrimaryDefenderId, string? AttackingPlayerId,
    string? AffectedReceiverId, string[] SupportingEventIds, InsightMeasurement ActualMeasurements,
    InsightMeasurement? PreviousMeasurements, string Title, string Explanation, int SignificanceScore,
    SignificanceFactor[] SignificanceFactors, MatchEvent Observation, MatchEvent TriggerEvent, MatchEvent[] SupportingObservations)
{
    public bool IsKeyMoment => SignificanceScore >= 35 && InsightType != "PassingLaneReopening";
}

// Stateless prefix analysis: no later events are consulted, including when grouping an episode.
public sealed class DefensiveInsightEngine(PassingLaneAnalyzer lanes)
{
    public const double GroupWindowSeconds = 4;
    public DefensiveInsight[] Analyze(IEnumerable<MatchEvent> source)
    {
        var result = new List<DefensiveInsight>();
        var history = new List<MatchEvent>();
        MatchEvent? previous = null;
        var boundary = "";
        foreach (var current in source.OrderBy(f => f.Timestamp).ThenBy(f => f.Id, StringComparer.Ordinal).DistinctBy(f => f.Id))
        {
            if (previous is null || current.MatchId != previous.MatchId || current.Scenario != previous.Scenario || current.EventType == "ScenarioStart")
            { history.Clear(); previous = null; boundary = current.Id; }
            var now = lanes.Analyze(current);
            if (previous is not null && current.Timestamp - previous.Timestamp <= 1 &&
                !previous.BallInFlight && !current.BallInFlight && previous.PlayerId == current.PlayerId && previous.TeamId == current.TeamId)
            {
                var before = lanes.Analyze(previous);
                foreach (var receiver in PassingLaneAnalyzer.Receivers(current).Where(r => PassingLaneAnalyzer.Receivers(previous).Any(p => p.PlayerId == r.PlayerId)))
                {
                    var oldBlocks = before.Where(c => c.ReceiverId == receiver.PlayerId).ToArray();
                    var newBlocks = now.Where(c => c.ReceiverId == receiver.PlayerId).ToArray();
                    if ((oldBlocks.Length == 0) == (newBlocks.Length == 0)) continue;
                    var denial = newBlocks.Length > 0;
                    // Require actual defender movement; a moving carrier/receiver alone cannot identify a responsible defender.
                    var blocker = (denial ? newBlocks : oldBlocks).OrderBy(c => c.DistanceMetres).ThenBy(c => c.DefenderId, StringComparer.Ordinal)
                        .FirstOrDefault(c => Moved(previous, current, c.DefenderId));
                    if (blocker is null) continue;
                    Add(denial ? "PassingLaneDenial" : "PassingLaneReopening", blocker.DefenderId, current.PlayerId, receiver.PlayerId,
                        current, previous, [previous, current],
                        denial ? $"Defender {blocker.DefenderId} closes a passing lane" : $"Passing lane reopens as {blocker.DefenderId} moves",
                        denial ? "An available candidate lane becomes geometrically covered alongside defender movement. This does not establish intent or a prevented pass."
                            : "A covered candidate lane becomes available alongside defender movement. This is not classified as a successful defensive contribution.");
                }
            }
            if (current.EventType == "PassCompleted")
            {
                var attempt = history.LastOrDefault(f => f.EventType == "PassAttempt" && f.TeamId == current.TeamId && f.TargetPlayerId == current.PlayerId && current.Timestamp - f.Timestamp <= 2);
                var pressure = attempt is null ? null : history.LastOrDefault(f => f.EventType == "Pressure" && f.TeamId == current.TeamId && f.PlayerId == attempt.PlayerId && attempt.Timestamp - f.Timestamp <= 4);
                var positioning = pressure is null ? null : history.LastOrDefault(f => !f.BallInFlight && f.PlayerId == attempt!.PlayerId && f.TeamId == current.TeamId && f.Timestamp >= pressure.Timestamp && f.Timestamp <= attempt!.Timestamp && lanes.Analyze(f).Length > 0);
                if (attempt?.BallFrom is not null && attempt.BallTo is not null && pressure is not null && positioning is not null)
                {
                    var progression = (attempt.BallTo.X - attempt.BallFrom.X) * Direction(current.TeamId);
                    if (progression <= 2)
                    {
                        var block = lanes.Analyze(positioning).OrderBy(c => c.DefenderId, StringComparer.Ordinal).First();
                        Add("AttackingRedirection", block.DefenderId, attempt.PlayerId, block.ReceiverId, current, positioning,
                            [pressure, positioning, attempt, current], progression < -2 ? "Attack recycles backward after pressure" : "Attack moves laterally after pressure",
                            "A completed backward/lateral pass follows a scripted pressure event and nearby measured coverage. The sequence is observed; pressure causing the pass is not established.");
                    }
                }
            }
            if (current.EventType is "Interception" or "BallRecovery" && previous is not null && previous.TeamId != current.TeamId)
            {
                var positioning = history.LastOrDefault(f => current.Timestamp - f.Timestamp <= 4 && lanes.Analyze(f).Any(c => c.DefenderId == current.PlayerId));
                Add("DefensiveRecovery", current.PlayerId, previous.PlayerId, positioning is null ? previous.TargetPlayerId : lanes.Analyze(positioning).First(c => c.DefenderId == current.PlayerId).ReceiverId, current, positioning,
                    positioning is null ? [previous, current] : [positioning, previous, current],
                    $"{current.PlayerId} regains possession",
                    positioning is null ? "An observed recovery transfers possession. No preceding lane-coverage link is established."
                        : "An observed recovery transfers possession after this defender occupied a passing lane. Temporal association does not establish that positioning caused the recovery.");
            }
            foreach (var i in Enumerable.Range(0, result.Count))
            {
                var insight = result[i];
                if (insight.MatchId != current.MatchId || insight.ScenarioId != boundary || insight.InsightType != "PassingLaneDenial" ||
                    current.Timestamp - insight.EndTimestamp > GroupWindowSeconds || current.PlayerId != insight.AttackingPlayerId ||
                    !now.Any(c => c.ReceiverId == insight.AffectedReceiverId && c.DefenderId == insight.PrimaryDefenderId)) continue;
                result[i] = insight with { EndTimestamp = current.Timestamp,
                    SupportingEventIds = insight.SupportingEventIds.Append(current.Id).Distinct().ToArray(),
                    SupportingObservations = insight.SupportingObservations.Append(current).DistinctBy(f => f.Id).ToArray() };
            }
            history.Add(current); previous = current;
        }
        return result.OrderByDescending(i => i.SignificanceScore).ThenBy(i => i.StartTimestamp).ThenBy(i => i.Id, StringComparer.Ordinal).ToArray();

        void Add(string type, string? defender, string? attacker, string? receiver, MatchEvent observation, MatchEvent? before,
            MatchEvent[] evidence, string title, string explanation)
        {
            var factors = new List<SignificanceFactor> { new("Distinct grouped sequence", 5), new("Referenced synthetic observations", 5) };
            factors.Add(new(type switch { "PassingLaneDenial" => "Available-to-covered transition", "PassingLaneReopening" => "Reopening context (not defensive success)", "DefensiveRecovery" => "Observed possession change", _ => "Observed redirection with pressure and geometry" },
                type switch { "PassingLaneDenial" => 30, "PassingLaneReopening" => 0, "DefensiveRecovery" => 45, _ => 25 }));
            var geometry = type == "AttackingRedirection" ? before! : observation;
            var target = geometry.PlayerPositions.FirstOrDefault(p => p.PlayerId == receiver);
            if (type == "PassingLaneDenial" && target is not null)
            {
                if ((target.Position.X - geometry.BallPosition.X) * Direction(geometry.TeamId) >= 5) factors.Add(new("Forward option of at least 5 m", 10));
                if ((geometry.TeamId == "A" ? 105 - target.Position.X : target.Position.X) <= 25) factors.Add(new("Receiver within 25 m of goal line", 10));
            }
            if (before is not null) factors.Add(new(type is "PassingLaneDenial" or "PassingLaneReopening" ? "Before/after positional evidence" : "Linked preceding positional evidence", 5));
            var ids = evidence.Select(f => f.Id).Distinct().ToArray();
            var start = evidence.Min(f => f.Timestamp);
            var existing = result.FindLastIndex(i => i.ScenarioId == boundary && i.MatchId == observation.MatchId && i.InsightType == type &&
                i.PrimaryDefenderId == defender && i.AttackingPlayerId == attacker && i.AffectedReceiverId == receiver &&
                observation.Timestamp - i.EndTimestamp <= GroupWindowSeconds);
            if (existing >= 0)
            {
                var old = result[existing];
                result[existing] = old with { EndTimestamp = observation.Timestamp, SupportingEventIds = old.SupportingEventIds.Concat(ids).Distinct().ToArray(), SupportingObservations = old.SupportingObservations.Concat(evidence).DistinctBy(f => f.Id).ToArray() };
                return; // Repeated transitions retain evidence, never add score or action counts.
            }
            var frozen = type is "AttackingRedirection" or "DefensiveRecovery" ? before ?? observation : observation;
            result.Add(new($"{boundary}:{type}:{defender}:{attacker}:{receiver}:{observation.Id}", observation.MatchId, boundary, type,
                start, observation.Timestamp, defender, attacker, receiver, ids, Measure(frozen, receiver, defender),
                before is null ? null : Measure(before, receiver, defender), title, explanation, factors.Sum(f => f.Points), factors.ToArray(), frozen, observation, evidence.DistinctBy(f => f.Id).ToArray()));
        }
    }
    private InsightMeasurement Measure(MatchEvent frame, string? receiver, string? defender)
    {
        var blocks = lanes.Analyze(frame).Where(c => c.ReceiverId == receiver).ToArray();
        var target = frame.PlayerPositions.FirstOrDefault(p => p.PlayerId == receiver)?.Position;
        var position = frame.PlayerPositions.FirstOrDefault(p => p.PlayerId == defender)?.Position;
        double? distance = null;
        if (!frame.BallInFlight && target is not null && position is not null)
        {
            var dx = target.X - frame.BallPosition.X; var dy = target.Y - frame.BallPosition.Y;
            var length = dx*dx + dy*dy;
            var t = length == 0 ? 0 : ((position.X-frame.BallPosition.X)*dx + (position.Y-frame.BallPosition.Y)*dy)/length;
            if (t > 0 && t < 1) distance = Math.Sqrt(Math.Pow(position.X-frame.BallPosition.X-t*dx, 2) + Math.Pow(position.Y-frame.BallPosition.Y-t*dy, 2));
        }
        return new(frame.Id, receiver, frame.BallPosition, target,
            blocks.Select(c => c.DefenderId).ToArray(), distance, position, PassingLaneAnalyzer.CorridorMetres);
    }
    private static int Direction(string team) => team == "A" ? 1 : -1;
    private static bool Moved(MatchEvent a, MatchEvent b, string id) =>
        a.PlayerPositions.FirstOrDefault(p => p.PlayerId == id)?.Position is Point p &&
        b.PlayerPositions.FirstOrDefault(p => p.PlayerId == id)?.Position is Point q &&
        Math.Abs(p.X-q.X) + Math.Abs(p.Y-q.Y) > 0.000001;
}
