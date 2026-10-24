using InvisibleDefender.Narration;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using InvisibleDefender.Domain;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddSingleton<PassingLaneAnalyzer>();
builder.Services.AddSingleton<DefensiveInsightEngine>();
builder.Services.AddSingleton<CounterfactualAnalyzer>();
builder.Services.AddSingleton<SyntheticMatchGenerator>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<PlaybackSessions>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PlaybackSessions>());
builder.Services.AddScoped<MatchPlayback>(sp => sp.GetRequiredService<PlaybackSessions>().Get(
    (string)sp.GetRequiredService<IHttpContextAccessor>().HttpContext!.Items["playbackSession"]!));
builder.Services.Configure<NarrationOptions>(builder.Configuration.GetSection("Narration"));
builder.Services.AddSingleton<TokenCredential>(sp => {
    var o = sp.GetRequiredService<IOptions<NarrationOptions>>().Value;
    return o.ManagedIdentity
        ? new ManagedIdentityCredential(string.IsNullOrWhiteSpace(o.ManagedIdentityClientId) ? ManagedIdentityId.SystemAssigned : ManagedIdentityId.FromUserAssignedClientId(o.ManagedIdentityClientId))
        : new DefaultAzureCredential();
});
builder.Services.AddHttpClient<FoundryNarrator>(client => client.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddSingleton<IInsightNarrator>(sp => sp.GetRequiredService<IOptions<NarrationOptions>>().Value.Provider switch {
    "Foundry" => sp.GetRequiredService<FoundryNarrator>(), "Mock" => new MockNarrator(), _ => new DisabledNarrator() });
builder.Services.AddSingleton<NarrationDiagnostics>();
builder.Services.AddSingleton<ExplanationService>();
builder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode = 429;
    options.AddPolicy("explanations", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions {
            PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            context.Request.Path == "/api/explanations"
            ? RateLimitPartition.GetFixedWindowLimiter("public-inference", _ => new FixedWindowRateLimiterOptions {
                PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
            : RateLimitPartition.GetNoLimiter("playback")),
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
        context.Request.Path == "/api/explanations"
        ? RateLimitPartition.GetConcurrencyLimiter("inference", _ => new ConcurrencyLimiterOptions { PermitLimit = 8, QueueLimit = 0 })
        : RateLimitPartition.GetNoLimiter("playback")));
});
var app = builder.Build();
// Fail closed across browser origins, including WebSocket upgrades. No cross-origin CORS policy.
app.Use(async (context, next) => {
    var origin = context.Request.Headers.Origin.ToString();
    var expected = builder.Configuration["PublicOrigin"] ?? $"{context.Request.Scheme}://{context.Request.Host}";
    var developmentOrigin = app.Environment.IsDevelopment() && origin is "http://127.0.0.1:5173" or "http://localhost:5173";
    if (origin.Length > 0 && !developmentOrigin && !string.Equals(origin.TrimEnd('/'), expected.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) {
        context.Response.StatusCode = 403; return;
    }
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    try { await next(); }
    catch (BadHttpRequestException) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { error = "Invalid request." }); }
    catch (Exception) when (!app.Environment.IsDevelopment() && !context.Response.HasStarted) {
        context.Response.Clear(); context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { error = "Request could not be completed." });
    }
});
app.Use(async (context, next) => {
    if (context.Request.Path.StartsWithSegments("/api/match") || context.Request.Path.StartsWithSegments("/hubs/match") || context.Request.Path == "/api/explanations") {
        var sessions = context.RequestServices.GetRequiredService<PlaybackSessions>();
        var id = context.Request.Cookies["id-playback"];
        if (id is null || !sessions.Contains(id)) {
            id = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            if (!sessions.TryAdd(id)) { context.Response.StatusCode = 503; return; }
            context.Response.Cookies.Append("id-playback", id, new CookieOptions {
                HttpOnly = true, Secure = !app.Environment.IsDevelopment(), SameSite = SameSiteMode.Strict, Path = "/", MaxAge = TimeSpan.FromHours(1) });
        }
        context.Items["playbackSession"] = id;
    }
    await next();
});
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapPost("/api/explanations", async (ExplanationRequest request, MatchPlayback match,
    SyntheticMatchGenerator generator, DefensiveInsightEngine engine, ExplanationService explanations, CancellationToken cancellationToken) => {
    if (request.Audience is not ("casual" or "analyst") || request.Scope is not ("live" or "replay") ||
        string.IsNullOrWhiteSpace(request.MatchId) || string.IsNullOrWhiteSpace(request.InsightId))
        return Results.BadRequest(new { error = "Known match, insight, audience and scope required." });
    var known = request.Scope == "live" ? match.Snapshot().Insights! : engine.Analyze(
        Enumerable.Range(0, SyntheticMatchGenerator.FrameCount).Select(generator.Generate));
    var insight = known.FirstOrDefault(i => i.MatchId == request.MatchId && i.Id == request.InsightId);
    if (insight is null) return Results.NotFound(new { error = "Insight is not available in this match scope." });
    return Results.Ok(await explanations.Explain(insight, request.Audience, cancellationToken));
}).RequireRateLimiting("explanations");
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
app.MapFallback((HttpContext context) => {
    if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/hubs") || Path.HasExtension(context.Request.Path))
        return Results.NotFound();
    var index = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html");
    return File.Exists(index) ? Results.File(index, "text/html") : Results.NotFound();
});
app.Run();

public record CounterfactualRequest(MatchEvent Frame, string DefenderId, Point Position);
public sealed class MatchHub : Hub
{
    public override async Task OnConnectedAsync() {
        var id = (string)Context.GetHttpContext()!.Items["playbackSession"]!;
        await Groups.AddToGroupAsync(Context.ConnectionId, id);
        await base.OnConnectedAsync();
    }
}
public record MatchState(long Revision, string Status, int FrameIndex, int FrameCount, MatchEvent Event, Contribution[] Contributions, DefensiveInsight[]? Insights = null, MatchState[]? Events = null);
public sealed class MatchPlayback(SyntheticMatchGenerator generator, PassingLaneAnalyzer analyzer, DefensiveInsightEngine insights, IHubContext<MatchHub> hub, string sessionId)
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
            await hub.Clients.Group(sessionId).SendAsync("MatchUpdated", Snapshot());
            return true;
        }
        finally { gate.Release(); }
    }
    public async Task Advance(CancellationToken stoppingToken)
    {
        await gate.WaitAsync(stoppingToken);
        try {
            lock (sync) {
                if (status != "running") return;
                if (index < SyntheticMatchGenerator.FrameCount - 1) index++;
                if (index == SyntheticMatchGenerator.FrameCount - 1) status = "completed";
                revision++;
            }
            await hub.Clients.Group(sessionId).SendAsync("MatchUpdated", Snapshot(), stoppingToken);
        }
        finally { gate.Release(); }
    }
}

// Bounded, ephemeral visitor state. Evidence and inference cache remain deterministic and shared.
public sealed class PlaybackSessions(SyntheticMatchGenerator generator, PassingLaneAnalyzer analyzer,
    DefensiveInsightEngine insights, IHubContext<MatchHub> hub) : BackgroundService
{
    private readonly object sync = new();
    private readonly Dictionary<string, (MatchPlayback Playback, DateTime Touched)> sessions = new();
    public bool Contains(string id) {
        lock (sync) {
            if (!sessions.TryGetValue(id, out var entry)) return false;
            sessions[id] = (entry.Playback, DateTime.UtcNow); return true;
        }
    }
    public bool TryAdd(string id) {
        lock (sync) {
            if (sessions.Count >= 128) return false;
            sessions.Add(id, (new MatchPlayback(generator, analyzer, insights, hub, id), DateTime.UtcNow));
            return true;
        }
    }
    public MatchPlayback Get(string id) {
        lock (sync) {
            var entry = sessions[id]; sessions[id] = (entry.Playback, DateTime.UtcNow); return entry.Playback;
        }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        try {
            while (await timer.WaitForNextTickAsync(stoppingToken)) {
                MatchPlayback[] active;
                lock (sync) {
                    foreach (var id in sessions.Where(s => s.Value.Touched < DateTime.UtcNow.AddHours(-1)).Select(s => s.Key).ToArray()) sessions.Remove(id);
                    active = sessions.Values.Select(s => s.Playback).ToArray();
                }
                foreach (var playback in active) await playback.Advance(stoppingToken);
            }
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
