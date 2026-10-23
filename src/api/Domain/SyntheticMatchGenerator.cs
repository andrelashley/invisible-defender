namespace InvisibleDefender.Domain;

// Curated independent clips, not a full match simulation. No analytical results are scripted.
public sealed class SyntheticMatchGenerator
{
    public const int FrameCount = 120;
    public static readonly string[] Scenarios = [
        "Midfield passing-lane denial", "Backward pass under defensive positioning",
        "Wide attack redirected away from goal", "Defensive recovery stops progression",
        "Combination attack and saved shot"
    ];
    private record Beat(string Owner, string Type, string Outcome, string? Target = null);
    private static readonly Beat[][] Scripts = [
        [new("A1", "ScenarioStart", "Midfield build-up; B1 screens A2"),
         new("A1", "Pressure", "Forward lane covered; A1 retains possession"),
         new("A4", "PassCompleted", "A1 completes a supporting pass to A4"),
         new("A2", "PassCompleted", "A4 finds A2 beyond the midfield screen"),
         new("A2", "Carry", "Attack progresses through a different route")],
        [new("A2", "ScenarioStart", "B3 screens the forward receiver A3"),
         new("A2", "Pressure", "B3 holds the forward lane"),
         new("A1", "PassCompleted", "A2 plays backward to A1"),
         new("A5", "PassCompleted", "A1 recycles possession across midfield"),
         new("A5", "Carry", "Attack retained but farther from goal")],
        [new("A3", "ScenarioStart", "Wide attack; B4 screens the inside pass to A2"),
         new("A3", "Pressure", "Inside lane covered by B4"),
         new("A5", "PassCompleted", "A3 redirects backward along the flank"),
         new("A5", "Carry", "A5 carries away from the penalty area"),
         new("A1", "PassCompleted", "A5 recycles centrally")],
        [new("A2", "ScenarioStart", "A2 advances as B5 recovers toward A3's lane"),
         new("A2", "Pressure", "B5 recovers into the forward passing segment"),
         new("B5", "Interception", "A2's attempted pass to A3 is intercepted by B5"),
         new("B3", "PassCompleted", "B5 completes an outlet to B3"),
         new("B3", "Carry", "Team B carries away from its goal")],
        [new("A1", "ScenarioStart", "B2 screens the direct route to A2"),
         new("A4", "PassCompleted", "A1 combines with A4 around the screen"),
         new("A2", "PassCompleted", "A4 releases A2 toward goal"),
         new("A2", "ShotSetup", "A2 prepares a shot toward the goalkeeper"),
         new("B6", "Save", "B6 gathers the shot; possession changes to Team B")]
    ];
    // Each row specifies all twelve player positions at an observation beat.
    // Intermediate positions are linear movement at 0.5-second intervals.
    private static Point[] Shape(int clip, int beat)
    {
        Point[] p = [new(35,34), new(60,26), new(74,44), new(30,18), new(40,56), new(8,34),
            new(48,8), new(55 + beat*.5,62 + beat*.4), new(67,22), new(75,51), new(83,36), new(98,34)];
        switch (clip)
        {
            case 0:
                p[1] = new(61 + beat,26); p[6] = new(48,30 + beat * .3);
                p[3] = new(30 + beat,18 + beat); break;
            case 1:
                p[0] = new(43 - beat,30); p[1] = new(64,28);
                p[2] = new(84,32); p[8] = new(74,30); p[4] = new(40,49); break;
            case 2:
                p[2] = new(76,58); p[1] = new(84,38); p[9] = new(80,48);
                p[4] = new(58 - beat,58 - beat); p[0] = new(45,36); break;
            case 3:
                p[1] = new(63 + System.Math.Min(beat,1)*2,30);
                p[2] = new(85,34); p[10] = beat == 0 ? new(78,42) : new(75,32);
                p[8] = new(65 - beat,19 + beat); break;
            case 4:
                p[0] = new(57,34); p[1] = new(77 + beat*2,28);
                p[3] = new(65 + beat,16); p[7] = new(67 + beat*.5,31 + beat*.2);
                p[11] = new(98,34); break;
        }
        return p;
    }
    private static readonly int[] Times = [0, 6, 12, 18, 23];
    private static Point Lerp(Point a, Point b, double t) => new(a.X+(b.X-a.X)*t, a.Y+(b.Y-a.Y)*t);
    private static int Slot(string id) => (id[0] == 'A' ? 0 : 6) + int.Parse(id[1..]) - 1;
    public MatchEvent Generate(int index)
    {
        if (index < 0 || index >= FrameCount) throw new ArgumentOutOfRangeException(nameof(index));
        var clip = index / 24; var tick = index % 24;
        var beat = Array.FindLastIndex(Times, t => t <= tick);
        var next = System.Math.Min(beat+1,4);
        var t = next == beat ? 0 : (double)(tick-Times[beat])/(Times[next]-Times[beat]);
        var positions = Shape(clip,beat).Zip(Shape(clip,next), (a,b) => Lerp(a,b,t)).ToArray();
        var action = Scripts[clip][beat]; var destination = Scripts[clip][next];
        var owner = action.Owner;
        var type = tick == Times[beat] ? action.Type : "Carry";
        var outcome = action.Outcome;
        Point? from = null, to = null;
        var flight = false;
        string? target = null;
        var ball = positions[Slot(owner)];
        // Passes and the shot travel over the last second before the receiving beat.
        if (next != beat && destination.Owner != owner && tick >= Times[next]-2)
        {
            flight = true; target = destination.Owner;
            from = Lerp(Shape(clip,beat)[Slot(owner)], Shape(clip,next)[Slot(owner)],
                (double)(Times[next]-3-Times[beat])/(Times[next]-Times[beat])); to = Shape(clip,next)[Slot(destination.Owner)];
            ball = Lerp(from,to,(tick-(Times[next]-3))/3d);
            type = destination.Type == "Save" ? "ShotAttempt" : "PassAttempt";
            outcome = destination.Type == "Interception" ? "Attempt toward A3; ball in flight" : "Ball in flight; outcome pending";
            if (destination.Type == "Interception") target = "A3";
        }
        var players = positions.Select((p,i) => new PlayerPosition($"{(i<6 ? "A" : "B")}{i%6+1}",i<6 ? "A" : "B",p)).ToArray();
        return new($"synthetic-001:{index:D3}","synthetic-001",index*.5,type,owner[..1],owner,outcome,ball,players,
            Scenarios[clip],target,flight,from,to);
    }
}
