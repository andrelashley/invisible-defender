namespace InvisibleDefender.Domain;

public record LaneAvailability(string ReceiverId, Point Start, Point End, string[] BlockerIds);
public record CounterfactualResult(string EventId, string DefenderId, Point ActualPosition,
    Point HypotheticalPosition, LaneAvailability[] Before, LaneAvailability[] After);

// Stateless analysis of a copied observation; never writes to match playback.
public sealed class CounterfactualAnalyzer(PassingLaneAnalyzer analyzer)
{
    public CounterfactualResult Analyze(MatchEvent frame, string defenderId, Point position)
    {
        if (!double.IsFinite(position.X) || !double.IsFinite(position.Y) ||
            position.X < 0 || position.X > 105 || position.Y < 0 || position.Y > 68)
            throw new ArgumentException("Hypothetical position must be within the pitch.");
        var defender = frame.PlayerPositions.SingleOrDefault(p => p.PlayerId == defenderId);
        if (defender is null || defender.TeamId == frame.TeamId)
            throw new ArgumentException("Select a defending player.");
        var hypothetical = frame with { PlayerPositions = frame.PlayerPositions.Select(p =>
            p.PlayerId == defenderId ? p with { Position = position } : p).ToArray() };
        return new(frame.Id, defenderId, defender.Position, position, Lanes(frame), Lanes(hypothetical));
    }
    private LaneAvailability[] Lanes(MatchEvent frame)
    {
        var blockers = analyzer.Analyze(frame);
        return PassingLaneAnalyzer.Receivers(frame).Select(p => new LaneAvailability(p.PlayerId,
            frame.BallPosition, p.Position, blockers.Where(c => c.ReceiverId == p.PlayerId)
                .Select(c => c.DefenderId).ToArray())).ToArray();
    }
}
