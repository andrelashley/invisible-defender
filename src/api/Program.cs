using InvisibleDefender.Domain;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddSingleton<PassingLaneAnalyzer>();
builder.Services.AddSingleton<DefensiveInsightEngine>();
builder.Services.AddSingleton<CounterfactualAnalyzer>();
builder.Services.AddSingleton<SyntheticMatchGenerator>();
builder.Services.AddSingleton<MatchPlayback>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MatchPlayback>());
var app = builder.Build();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/match/moments", (SyntheticMatchGenerator generator, PassingLaneAnalyzer analyzer) =>
    Enumerable.Range(0, SyntheticMatchGenerator.FrameCount)
        .Where(i => new[] { 0, 6, 12, 18, 23 }.Contains(i % 24))
        .Select(i => { var frame = generator.Generate(i);
            return new MatchState(0, "inspection", i, SyntheticMatchGenerator.FrameCount, frame, analyzer.Analyze(frame)); }));
// Explicit replay catalogue; live clients consume timestamp-bounded snapshots instead.
app.MapGet("/api/match/replay", (SyntheticMatchGenerator generator, PassingLaneAnalyzer analyzer, DefensiveInsightEngine insights) => {
    var frames = Enumerable.Range(0, SyntheticMatchGenerator.FrameCount).Select(generator.Generate).ToArray();
    return new { events = frames.Select((f, i) => new MatchState(0, "inspection", i, frames.Length, f, analyzer.Analyze(f))), insights = insights.Analyze(frames) };
});
app.MapGet("/api/match/insights", (MatchPlayback match) => match.Snapshot().Insights);
app.MapGet("/api/match", (MatchPlayback match) => match.Snapshot());
app.MapPost("/api/match/{command}", async (string command, MatchPlayback match) =>
    await match.Control(command) ? Results.Ok(match.Snapshot()) : Results.BadRequest(new { error = "Use start, pause, resume, or replay." }));
app.MapPost("/api/counterfactual", (CounterfactualRequest request, CounterfactualAnalyzer analyzer) =>
{
    if (request.Frame is null || request.Position is null || request.Frame.PlayerPositions is null ||
        request.Frame.BallPosition is null || request.Frame.PlayerPositions.Any(p => p is null || p.Position is null))
        return Results.BadRequest(new { error = "A complete frame and position are required." });
    try { return Results.Ok(analyzer.Analyze(request.Frame, request.DefenderId, request.Position)); }
    catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
});
app.MapHub<MatchHub>("/hubs/match");
app.Run();

public record CounterfactualRequest(MatchEvent Frame, string DefenderId, Point Position);
public sealed class MatchHub : Hub;
public record MatchState(long Revision, string Status, int FrameIndex, int FrameCount, MatchEvent Event, Contribution[] Contributions, DefensiveInsight[]? Insights = null, MatchState[]? Events = null);
public sealed class MatchPlayback(SyntheticMatchGenerator generator, PassingLaneAnalyzer analyzer, DefensiveInsightEngine insights, IHubContext<MatchHub> hub) : BackgroundService
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object sync = new();
    private int index;
    private string status = "ready";
    private long revision;
    public MatchState Snapshot()
    {
        lock (sync)
        {
            var frame = generator.Generate(index);
            var observed = Enumerable.Range(0, index + 1).Select(generator.Generate).ToArray();
            return new(revision, status, index, SyntheticMatchGenerator.FrameCount, frame, analyzer.Analyze(frame), insights.Analyze(observed), observed.Select((f, i) => new MatchState(0, "inspection", i, SyntheticMatchGenerator.FrameCount, f, analyzer.Analyze(f))).ToArray());
        }
    }
    public async Task<bool> Control(string command)
    {
        if (command is not ("start" or "pause" or "resume" or "replay")) return false;
        await gate.WaitAsync();
        try
        {
            lock (sync)
            {
                if (command is "start" or "replay") { index = 0; status = "running"; }
                if (command == "pause" && status == "running") status = "paused";
                if (command == "resume" && status == "paused") status = "running";
                revision++;
            }
            await hub.Clients.All.SendAsync("MatchUpdated", Snapshot());
            return true;
        }
        finally { gate.Release(); }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await gate.WaitAsync(stoppingToken);
                try
                {
                    lock (sync)
                    {
                        if (status != "running") continue;
                        if (index < SyntheticMatchGenerator.FrameCount - 1) index++;
                        if (index == SyntheticMatchGenerator.FrameCount - 1) status = "completed";
                        revision++;
                    }
                    await hub.Clients.All.SendAsync("MatchUpdated", Snapshot(), stoppingToken);
                }
                finally { gate.Release(); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}


