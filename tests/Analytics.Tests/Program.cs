using InvisibleDefender.Domain;
using System.Text.Json;

// Dependency-free executable test suite: throws on failure and exits non-zero.
var analyzer = new PassingLaneAnalyzer();
var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"FAIL: {name}");
    passed++;
    Console.WriteLine($"PASS: {name}");
}
MatchEvent Frame(Point defender, Point? receiver = null) => new("e1", "m1", 0, "PositionSnapshot", "A", "A1", "synthetic", new(10, 10),
    [new("A1", "A", new(10, 10)), new("A2", "A", receiver ?? new(30, 10)), new("B1", "B", defender)]);
foreach (var (x, y, expected) in new[] { (20d, 10d, true), (20d, 12d, true), (20d, 12.001, false), (9d, 10d, false), (31d, 10d, false), (10d, 10d, false), (30d, 10d, false) })
    Check((analyzer.Analyze(Frame(new(x, y))).Length == 1) == expected, $"Finite segment and corridor ({x}, {y})");
foreach (var x in new[] { 10d, 14.99, 50.01 })
    Check(analyzer.Analyze(Frame(new(12, 10), new(x, 10))).Length == 0, $"Reject degenerate/short/long lane {x}");
foreach (var x in new[] { 15d, 50d })
    Check(analyzer.Analyze(Frame(new(12, 10), new(x, 10))).Length == 1, $"Include length boundary {x}");
var vertical = analyzer.Analyze(Frame(new(12, 20), new(10, 30))).Single();
Check(vertical.DistanceMetres == 2 && vertical.EventId == "e1" && vertical.DefenderId == "B1" && vertical.ReceiverId == "A2" && vertical.DefenderPosition == new Point(12, 20), "Vertical lane and exact evidence");
var diagonal = analyzer.Analyze(Frame(new(21, 19), new(30, 30))).Single();
Check(Math.Abs(diagonal.DistanceMetres - Math.Sqrt(2)) < 1e-10, "Diagonal distance");
var frame = Frame(new(20, 10));
Check(analyzer.Analyze(frame with { PlayerPositions = frame.PlayerPositions.Select(p => p with { TeamId = "A" }).ToArray() }).Length == 0, "Teammates are not defenders");
Check(analyzer.Analyze(frame with { PlayerId = "missing" }).Length == 0, "Missing possession owner");
Check(analyzer.Analyze(frame with { TeamId = "B" }).Length == 0, "Possession team mismatch");
var multiple = analyzer.Analyze(frame with { PlayerPositions = [.. frame.PlayerPositions, new("B2", "B", new(22, 11))] });
Check(multiple.Length == 2 && multiple.Select(c => c.Id).Distinct().Count() == 2, "Multiple blockers have distinct evidence");
var generator = new SyntheticMatchGenerator();
var counts = new List<int>();
var valid = true;
var repeatable = true;
var linked = true;
for (var i = 0; i < SyntheticMatchGenerator.FrameCount; i++)
{
    var current = generator.Generate(i);
    repeatable &= JsonSerializer.Serialize(current) == JsonSerializer.Serialize(generator.Generate(i));
    valid &= current.PlayerPositions.All(p => p.Position.X >= 0 && p.Position.X <= 105 && p.Position.Y >= 0 && p.Position.Y <= 68);
    valid &= current.BallInFlight
        ? current.BallCarrierId is null && current.BallFrom is not null && current.BallTo is not null
        : current.PlayerPositions.Any(p => p.PlayerId == current.BallCarrierId && p.TeamId == current.TeamId && p.Position == current.BallPosition);
    valid &= current.Timestamp == i * .5;
    var results = analyzer.Analyze(current);
    linked &= results.All(c => c.EventId == current.Id && current.PlayerPositions.Any(p => p.PlayerId == c.DefenderId && p.Position == c.DefenderPosition));
    counts.Add(results.Length);
}
Check(valid, "All 120 frames have valid positions, ball owner, timestamps");
Check(repeatable, "Replay repeats all 120 frames exactly");
Check(linked, "Every generated detection links to observed evidence");
Check(counts.Any(count => count > 0) && counts.Any(count => count == 0), "Scenario exercises both open and blocked lanes");
var counterfactual = new CounterfactualAnalyzer(analyzer);
var originalJson = JsonSerializer.Serialize(frame);
var opened = counterfactual.Analyze(frame, "B1", new(20, 20));
Check(opened.Before.Single().BlockerIds.SequenceEqual(new[] { "B1" }) && opened.After.Single().BlockerIds.Length == 0, "Moving defender opens lane");
Check(opened.EventId == frame.Id && opened.ActualPosition == new Point(20, 10) && opened.HypotheticalPosition == new Point(20, 20), "Counterfactual links actual and hypothetical evidence");
Check(JsonSerializer.Serialize(frame) == originalJson, "Counterfactual leaves source observation unchanged");
var closed = counterfactual.Analyze(Frame(new(20, 20)), "B1", new(20, 12));
Check(closed.Before.Single().BlockerIds.Length == 0 && closed.After.Single().BlockerIds.Length == 1, "Hypothetical corridor boundary closes lane");
var unchanged = counterfactual.Analyze(frame, "B1", new(20, 10));
Check(JsonSerializer.Serialize(unchanged.Before) == JsonSerializer.Serialize(unchanged.After), "Actual position restores baseline coverage");
var shared = frame with { PlayerPositions = [.. frame.PlayerPositions, new("B2", "B", new(22, 11))] };
var stillCovered = counterfactual.Analyze(shared, "B1", new(20, 20));
Check(stillCovered.Before.Length == 1 && stillCovered.Before[0].BlockerIds.Length == 2 && stillCovered.After[0].BlockerIds.SequenceEqual(new[] { "B2" }), "Lane remains covered by another defender and is counted once");
foreach (var (id, point) in new[] { ("A1", new Point(20, 20)), ("missing", new Point(20, 20)), ("B1", new Point(-1, 20)), ("B1", new Point(106, 20)), ("B1", new Point(20, 69)), ("B1", new Point(double.NaN, 20)), ("B1", new Point(20, double.PositiveInfinity)) })
{
    var rejected = false;
    try { counterfactual.Analyze(frame, id, point); } catch (ArgumentException) { rejected = true; }
    Check(rejected, $"Reject invalid hypothetical input {id} {point}");
}
Check(counterfactual.Analyze(frame, "B1", new(0, 68)).After.Length == 1, "Pitch boundary is permitted");
Check(counterfactual.Analyze(Frame(new(20, 10), new(51, 10)), "B1", new(20, 20)).After.Length == 0, "Counterfactual shares candidate distance rule");
Check(JsonSerializer.Serialize(counterfactual.Analyze(frame, "B1", new(20, 20))) == JsonSerializer.Serialize(opened), "Counterfactual is repeatable");
// Differential checks across every live frame: the scenario must use the same analyzer
// and candidate lanes, while retaining all observed positions for later reset/replay.
var allSourcesUnchanged = true;
var allCoverageMatches = true;
for (var i = 0; i < SyntheticMatchGenerator.FrameCount; i++)
{
    var observation = generator.Generate(i);
    var source = JsonSerializer.Serialize(observation);
    foreach (var defender in observation.PlayerPositions.Where(p => p.TeamId != observation.TeamId))
    {
        var target = new Point(50, 34);
        var result = counterfactual.Analyze(observation, defender.PlayerId, target);
        var expectedFrame = observation with { PlayerPositions = observation.PlayerPositions.Select(p =>
            p.PlayerId == defender.PlayerId ? p with { Position = target } : p).ToArray() };
        var expected = analyzer.Analyze(expectedFrame);
        allCoverageMatches &= result.Before.Select(l => l.ReceiverId).SequenceEqual(result.After.Select(l => l.ReceiverId));
        allCoverageMatches &= result.After.All(l => l.BlockerIds.SequenceEqual(
            expected.Where(c => c.ReceiverId == l.ReceiverId).Select(c => c.DefenderId)));
        allSourcesUnchanged &= JsonSerializer.Serialize(observation) == source;
    }
}
Check(allCoverageMatches, "All frames and defenders reuse deterministic candidate lanes and coverage");
Check(allSourcesUnchanged, "Repeated scenarios preserve every source frame and other player positions");
var switching = counterfactual.Analyze(shared, "B2", new(0, 0));
Check(switching.After.Single().BlockerIds.SequenceEqual(new[] { "B1" }), "Switching defender starts from actual positions");
var endpoints = new[] { new Point(10, 10), new Point(30, 10) };
Check(endpoints.All(p => counterfactual.Analyze(frame, "B1", p).After.Single().BlockerIds.Length == 0),
    "Hypothetical defenders at segment endpoints do not cover the lane");
var replay = Enumerable.Range(0, SyntheticMatchGenerator.FrameCount).Select(generator.Generate).ToArray();
Check(replay.Select(f => f.Scenario).Distinct().Count() == 5, "Five distinct scripted sequences");
Check(replay.Select(f => f.BallCarrierId).Where(id => id != null).Distinct().Count() >= 7, "Possession changes among multiple players");
Check(replay.Where(f => f.EventType is "Interception" or "Save").All(f => f.TeamId == "B"), "Interception and save transfer possession to B");
Check(replay.Any(f => f.EventType == "PassAttempt") && replay.Any(f => f.EventType == "PassCompleted") && replay.Any(f => f.EventType == "ShotAttempt"), "Attempted and completed passes plus shot flight");
Check(replay.Where(f => f.BallInFlight).All(f => analyzer.Analyze(f).Length == 0), "In-flight balls do not create possession-based insights");
var allContributions = replay.SelectMany(analyzer.Analyze).ToArray();
Check(allContributions.Count(c => c.DefenderId is "B1" or "B2") < allContributions.Length / 2d,
    "Most contributions come from defenders beyond B1 and B2");
var contributors = allContributions.Select(c => c.DefenderId).Distinct().ToArray();
Check(new[] { "B1", "B2", "B3", "B4", "B5" }.All(contributors.Contains), "Five defenders contribute measured evidence");
Check(replay.GroupBy(f => f.Scenario).All(g => g.SelectMany(analyzer.Analyze).Any()), "Every scenario supports geometric evidence");
Check(replay.Where(f => f.BallInFlight).All(f => {
    var a = f.BallFrom!; var b = f.BallTo!;
    var cross = (f.BallPosition.X-a.X)*(b.Y-a.Y)-(f.BallPosition.Y-a.Y)*(b.X-a.X);
    return Math.Abs(cross) < 1e-8 && f.BallPosition.X >= Math.Min(a.X,b.X) && f.BallPosition.X <= Math.Max(a.X,b.X);
}), "Ball flight follows declared travel segment");
Check(replay.Where(f => f.EventType == "PassCompleted").Any(f => f.PlayerId == "A1" && f.Scenario.Contains("Backward")), "Backward recycling pass is observed");
var engine = new DefensiveInsightEngine(analyzer);
var openFrame = Frame(new(20, 16)) with { Id = "open", Scenario = "test" };
var coveredFrame = Frame(new(20, 10)) with { Id = "covered", Timestamp = .5, Scenario = "test" };
var reopenedFrame = openFrame with { Id = "reopened", Timestamp = 1 };
var transitions = engine.Analyze([openFrame, coveredFrame, reopenedFrame]);
Check(transitions.Any(i => i.InsightType == "PassingLaneDenial" && i.PrimaryDefenderId == "B1" && i.AffectedReceiverId == "A2"), "Available-to-covered transition identifies players");
Check(transitions.Any(i => i.InsightType == "PassingLaneReopening" && i.Explanation.Contains("not classified")), "Reopening is not defensive success");
Check(transitions.All(i => i.SupportingEventIds.Contains(i.Observation.Id) && i.PreviousMeasurements != null && i.SupportingEventIds.Contains(i.PreviousMeasurements.EventId)), "Exact before and after evidence references");
Check(engine.Analyze([]).Length == 0 && engine.Analyze([openFrame]).Length == 0, "Empty and insignificant matches produce no insights");
Check(engine.Analyze([coveredFrame, coveredFrame with { Id = "stable", Timestamp = 1 }]).Length == 0, "Static coverage is not a new action");
var reclose = coveredFrame with { Id = "reclose", Timestamp = 1.5 };
Check(engine.Analyze([openFrame, coveredFrame, reopenedFrame, reclose]).Count(i => i.InsightType == "PassingLaneDenial") == 1, "Nearby repeated transitions are grouped");
Check(engine.Analyze([openFrame, coveredFrame, reopenedFrame, reclose]).Single(i => i.InsightType == "PassingLaneDenial").SupportingEventIds.Contains("reclose"), "Grouped transition retains observations");
Check(engine.Analyze([openFrame, coveredFrame, openFrame with { Id = "reset", Timestamp = 2, EventType = "ScenarioStart" }, coveredFrame with { Id = "other", Timestamp = 2.5 }]).Count(i => i.InsightType == "PassingLaneDenial") == 2, "Explicit scenario resets separate identical lanes");
Check(engine.Analyze([openFrame, coveredFrame with { Scenario = "other" }]).Length == 0, "Scenario boundaries cannot create transitions");
var recovery = coveredFrame with { Id = "recover", Timestamp = 1, EventType = "Interception", PlayerId = "B1", TeamId = "B", BallPosition = new(20,10) };
Check(engine.Analyze([openFrame, coveredFrame, recovery]).Any(i => i.InsightType == "DefensiveRecovery" && i.SupportingEventIds.Contains("covered")), "Recovery links preceding defensive geometry");
Check(!engine.Analyze([recovery]).Any(), "Recovery requires preceding opposing possession");
var pressure = coveredFrame with { EventType = "Pressure", PlayerId = "A2", BallPosition = new(30,10) };
var attempt = pressure with { Id = "attempt", Timestamp = 1, EventType = "PassAttempt", BallInFlight = true, TargetPlayerId = "A1", BallFrom = new(30,10), BallTo = new(10,10) };
var completion = coveredFrame with { Id = "completion", Timestamp = 1.5, EventType = "PassCompleted", PlayerId = "A1" };
var redirect = engine.Analyze([openFrame, pressure, attempt, completion]).Single(i => i.InsightType == "AttackingRedirection");
Check(redirect.SupportingEventIds.Contains("attempt") && redirect.Explanation.Contains("not established"), "Redirection requires observed pass and rejects causal claim");
Check(!engine.Analyze([openFrame, coveredFrame, attempt, completion]).Any(i => i.InsightType == "AttackingRedirection"), "No unsupported pressure redirection");
Check(!engine.Analyze([openFrame, pressure with { PlayerPositions = openFrame.PlayerPositions }, attempt, completion]).Any(i => i.InsightType == "AttackingRedirection"), "Pressure label without geometric evidence is insufficient");
var detected = engine.Analyze(replay);
Check(detected.Any(i => i.InsightType == "DefensiveRecovery") && detected.Any(i => i.InsightType == "AttackingRedirection"), "Synthetic sequences support recovery and redirection");
Check(JsonSerializer.Serialize(detected) == JsonSerializer.Serialize(engine.Analyze(replay)), "Deterministic insight IDs ordering measurements and scores");
Check(detected.All(i => i.SignificanceScore == i.SignificanceFactors.Sum(f => f.Points)), "Every ranking point is explained");
Check(detected.All(i => i.SupportingEventIds.All(id => replay.Any(f => f.Id == id && f.Timestamp <= i.EndTimestamp))), "All insights reference real observed events");
Check(Enumerable.Range(1, replay.Length).All(n => engine.Analyze(replay.Take(n)).All(i => i.EndTimestamp <= replay[n-1].Timestamp && i.SupportingEventIds.All(id => replay.Take(n).Any(f => f.Id == id)))), "Every live prefix excludes future insights and evidence");
var sustained = coveredFrame with { Id = "sustained", Timestamp = 1 };
var stableInsight = engine.Analyze([openFrame, coveredFrame, sustained]).Single();
Check(stableInsight.SupportingEventIds.Contains("sustained") && stableInsight.SignificanceScore == engine.Analyze([openFrame, coveredFrame]).Single().SignificanceScore, "Sustained coverage retains evidence without inflating score");
Check(stableInsight.SupportingObservations.Select(f => f.Id).SequenceEqual(stableInsight.SupportingEventIds), "Grouped full positional evidence matches every event reference");
Check(engine.Analyze([openFrame, coveredFrame with { Timestamp = 2 }]).Length == 0, "Sparse observations do not imply a continuous transition");
Check(engine.Analyze([openFrame, coveredFrame with { PlayerId = "A2" }]).Length == 0, "Carrier changes do not imply a same-lane transition");
Check(engine.Analyze([openFrame, coveredFrame with { PlayerPositions = openFrame.PlayerPositions }]).Length == 0, "Stationary defenders do not receive transition attribution");
Check(engine.Analyze([openFrame, coveredFrame, reopenedFrame, reclose with { Timestamp = 8 }]).Count(i => i.InsightType == "PassingLaneDenial") == 1, "Separated sparse frame does not invent a transition");
Check(detected.Where(i => i.InsightType is "AttackingRedirection" or "DefensiveRecovery").All(i => i.Observation.Timestamp <= i.TriggerEvent.Timestamp && i.Observation.PlayerPositions.Any(p => p.PlayerId == i.PrimaryDefenderId && p.TeamId != i.Observation.TeamId)), "Outcome insights select preceding defensive geometry for interaction");
Check(transitions.Single(i => i.InsightType == "PassingLaneDenial").PreviousMeasurements!.DefenderDistanceMetres == 6, "Available-lane distance is retained before closure");
Console.WriteLine($"{passed} analytical tests passed.");

