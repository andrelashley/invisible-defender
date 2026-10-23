namespace InvisibleDefender.Domain;

public record Point(double X, double Y);
public record PlayerPosition(string PlayerId, string TeamId, Point Position);
public record MatchEvent(string Id, string MatchId, double Timestamp, string EventType,
    string TeamId, string PlayerId, string Outcome, Point BallPosition, PlayerPosition[] PlayerPositions,
    string Scenario = "", string? TargetPlayerId = null, bool BallInFlight = false,
    Point? BallFrom = null, Point? BallTo = null)
{
    public string? BallCarrierId => BallInFlight ? null : PlayerId;
}
public record Contribution(string Id, string Type, string EventId, string DefenderId,
    string ReceiverId, Point Start, Point End, Point DefenderPosition, double DistanceMetres,
    double CorridorMetres, string Explanation);

// Pure geometric analysis: blockage of an available passing segment, not causal attribution.
public sealed class PassingLaneAnalyzer
{
    public const double CorridorMetres = 2;
    public static PlayerPosition[] Receivers(MatchEvent frame) =>
        !frame.BallInFlight && frame.PlayerPositions.Any(p => p.PlayerId == frame.PlayerId && p.TeamId == frame.TeamId)
        ? frame.PlayerPositions.Where(p => p.TeamId == frame.TeamId && p.PlayerId != frame.PlayerId)
            .Where(p => { var dx = p.Position.X - frame.BallPosition.X; var dy = p.Position.Y - frame.BallPosition.Y;
                return dx * dx + dy * dy >= 25 && dx * dx + dy * dy <= 1600; }).ToArray()
        : [];
    public Contribution[] Analyze(MatchEvent frame)
    {
        var carrier = frame.PlayerPositions.SingleOrDefault(p => p.PlayerId == frame.PlayerId);
        if (frame.BallInFlight || carrier is null || carrier.TeamId != frame.TeamId) return [];
        var results = new List<Contribution>();
        foreach (var receiver in Receivers(frame))
        {
            var start = frame.BallPosition;
            var end = receiver.Position;
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var lengthSquared = dx * dx + dy * dy;
            if (lengthSquared < 25 || lengthSquared > 1600) continue;
            foreach (var defender in frame.PlayerPositions.Where(p => p.TeamId != frame.TeamId))
            {
                var t = ((defender.Position.X - start.X) * dx + (defender.Position.Y - start.Y) * dy) / lengthSquared;
                if (t <= 0 || t >= 1) continue;
                var distance = Math.Sqrt(Math.Pow(defender.Position.X - start.X - t * dx, 2) + Math.Pow(defender.Position.Y - start.Y - t * dy, 2));
                if (distance > CorridorMetres) continue;
                results.Add(new($"{frame.Id}:{defender.PlayerId}:{receiver.PlayerId}", "PassingLaneDenial", frame.Id,
                    defender.PlayerId, receiver.PlayerId, start, end, defender.Position, distance, CorridorMetres,
                    FormattableString.Invariant($"{defender.PlayerId} occupies the lane to {receiver.PlayerId}: {distance:F2} m from the passing segment (threshold {CorridorMetres:F1} m). Geometric blockage; no attempted pass or causal effect is inferred.")));
            }
        }
        return results.ToArray();
    }
}

