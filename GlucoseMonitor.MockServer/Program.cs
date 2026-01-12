using System.Text.Json;
using System.Text.Json.Serialization;
using GlucoseMonitor.MockServer;
using GlucoseMonitor.MockServer.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure to listen on port 5555
builder.WebHost.UseUrls("http://localhost:5555");

builder.Services.AddSingleton<MockGlucoseState>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();
app.UseCors();

// ============================================
// MOCK NIGHTSCOUT API ENDPOINTS
// ============================================

// GET /api/v1/entries/sgv.json - Standard entries endpoint
app.MapGet("/api/v1/entries/sgv.json", (MockGlucoseState state, int? count) =>
{
    var requestedCount = count ?? 10;
    var entries = state.GenerateEntries(requestedCount);
    return Results.Json(entries);
});

// GET /pebble - Legacy pebble endpoint
app.MapGet("/pebble", (MockGlucoseState state, int? count, string? units) =>
{
    var requestedCount = count ?? 1;
    var entries = state.GenerateEntries(requestedCount);

    var pebbleResponse = new PebbleResponse
    {
        Status = new List<StatusInfo>
        {
            new StatusInfo { Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
        },
        Bgs = entries.Select(e => new PebbleBg
        {
            Sgv = e.Sgv.ToString(),
            Trend = e.Trend,
            Direction = e.Direction,
            Datetime = e.Date,
            BgDelta = e.BgDelta?.ToString() ?? "0",
            Battery = "100",
            Iob = "0",
            Cob = "0"
        }).ToList()
    };

    return Results.Json(pebbleResponse);
});

// GET /api/v1/status - Server status
app.MapGet("/api/v1/status", () =>
{
    return Results.Json(new
    {
        status = "ok",
        name = "Mock Nightscout",
        version = "15.0.0-mock",
        apiEnabled = true,
        settings = new
        {
            units = "mg/dl",
            thresholds = new
            {
                bgHigh = GlucoseThresholds.UrgentHigh,
                bgTargetTop = GlucoseThresholds.TargetTop,
                bgTargetBottom = GlucoseThresholds.TargetBottom,
                bgLow = GlucoseThresholds.UrgentLow
            }
        }
    });
});

// ============================================
// CONTROL ENDPOINTS - For test scenarios
// ============================================

// GET /mock/scenarios - List available scenarios
app.MapGet("/mock/scenarios", () =>
{
    return Results.Json(new
    {
        scenarios = new[]
        {
            new { name = "normal", description = "Normal glucose range (90-120 mg/dL)" },
            new { name = "high", description = "High glucose (180-220 mg/dL)" },
            new { name = "low", description = "Low glucose (60-70 mg/dL)" },
            new { name = "urgent_high", description = "Urgent high glucose (250-300 mg/dL)" },
            new { name = "urgent_low", description = "Urgent low glucose (40-54 mg/dL)" },
            new { name = "rising", description = "Rapidly rising glucose" },
            new { name = "falling", description = "Rapidly falling glucose" },
            new { name = "stable", description = "Stable glucose at 100 mg/dL" },
            new { name = "random", description = "Random values within normal range" }
        },
        currentScenario = MockGlucoseState.CurrentScenario,
        currentValue = MockGlucoseState.CurrentGlucose
    });
});

// POST /mock/scenario/{name} - Set active scenario
app.MapPost("/mock/scenario/{name}", (string name, MockGlucoseState state) =>
{
    if (state.SetScenario(name))
    {
        return Results.Ok(new { message = $"Scenario set to: {name}", currentValue = MockGlucoseState.CurrentGlucose });
    }
    return Results.BadRequest(new { error = $"Unknown scenario: {name}" });
});

// POST /mock/value/{value} - Set specific glucose value
app.MapPost("/mock/value/{value:double}", (double value, MockGlucoseState state) =>
{
    state.SetFixedValue(value);
    return Results.Ok(new { message = $"Glucose set to: {value} mg/dL" });
});

// POST /mock/direction/{direction} - Set trend direction
app.MapPost("/mock/direction/{direction}", (string direction, MockGlucoseState state) =>
{
    if (state.SetDirection(direction))
    {
        return Results.Ok(new { message = $"Direction set to: {direction}" });
    }
    return Results.BadRequest(new { error = $"Unknown direction: {direction}. Valid: Flat, FortyFiveUp, FortyFiveDown, SingleUp, SingleDown, DoubleUp, DoubleDown" });
});

// GET /mock/status - Current mock status
app.MapGet("/mock/status", () =>
{
    return Results.Json(new
    {
        scenario = MockGlucoseState.CurrentScenario,
        currentGlucose = MockGlucoseState.CurrentGlucose,
        direction = MockGlucoseState.CurrentDirection,
        delta = MockGlucoseState.CurrentDelta,
        serverTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
    });
});

// ============================================
// WEB UI - Simple control page
// ============================================

app.MapGet("/", () => Results.Content(GetControlPageHtml(), "text/html"));

Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════╗
║           MOCK NIGHTSCOUT SERVER - For Testing               ║
╠══════════════════════════════════════════════════════════════╣
║  Server URL: http://localhost:5555                           ║
║  Control UI: http://localhost:5555/                          ║
╠══════════════════════════════════════════════════════════════╣
║  API Endpoints:                                              ║
║    GET  /api/v1/entries/sgv.json?count=N                     ║
║    GET  /pebble?count=N&units=mg                             ║
║    GET  /api/v1/status                                       ║
╠══════════════════════════════════════════════════════════════╣
║  Control Endpoints:                                          ║
║    GET  /mock/scenarios       - List scenarios               ║
║    POST /mock/scenario/{name} - Set scenario                 ║
║    POST /mock/value/{value}   - Set specific value           ║
║    POST /mock/direction/{dir} - Set trend direction          ║
║    GET  /mock/status          - Current status               ║
╚══════════════════════════════════════════════════════════════╝
");

// HTML Control Page
static string GetControlPageHtml() => """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Mock Nightscout Server - Control Panel</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
            color: #eee;
            min-height: 100vh;
            padding: 20px;
        }
        .container { max-width: 900px; margin: 0 auto; }
        h1 { text-align: center; margin-bottom: 30px; color: #00d9ff; }
        .card {
            background: rgba(255,255,255,0.1);
            border-radius: 15px;
            padding: 20px;
            margin-bottom: 20px;
            backdrop-filter: blur(10px);
        }
        .card h2 { margin-bottom: 15px; color: #00d9ff; font-size: 1.2em; }
        .status {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
            gap: 15px;
            text-align: center;
        }
        .status-item {
            background: rgba(0,0,0,0.3);
            padding: 15px;
            border-radius: 10px;
        }
        .status-value {
            font-size: 2em;
            font-weight: bold;
            color: #00ff88;
        }
        .status-label { font-size: 0.9em; color: #aaa; }
        .scenarios {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
            gap: 10px;
        }
        .scenario-btn {
            padding: 15px;
            border: none;
            border-radius: 10px;
            cursor: pointer;
            font-size: 0.9em;
            font-weight: 600;
            transition: all 0.3s;
        }
        .scenario-btn:hover { transform: scale(1.05); }
        .scenario-btn.active { box-shadow: 0 0 20px rgba(0,217,255,0.5); }
        .normal { background: #00ff88; color: #000; }
        .high { background: #ffaa00; color: #000; }
        .low { background: #ff6600; color: #000; }
        .urgent_high { background: #ff0000; color: #fff; }
        .urgent_low { background: #ff0066; color: #fff; }
        .rising { background: #00aaff; color: #000; }
        .falling { background: #aa00ff; color: #fff; }
        .stable { background: #888; color: #fff; }
        .random { background: #fff; color: #000; }
        .controls { display: flex; gap: 10px; flex-wrap: wrap; align-items: center; }
        input[type="number"], select {
            padding: 10px 15px;
            border: none;
            border-radius: 8px;
            font-size: 1em;
            background: rgba(255,255,255,0.2);
            color: #fff;
            width: 120px;
        }
        button.apply {
            padding: 10px 20px;
            background: #00d9ff;
            border: none;
            border-radius: 8px;
            color: #000;
            font-weight: 600;
            cursor: pointer;
        }
        .api-info { font-family: monospace; font-size: 0.85em; background: #000; padding: 15px; border-radius: 8px; }
        .api-info code { color: #00ff88; }
        .refresh-note { text-align: center; color: #888; font-size: 0.85em; margin-top: 10px; }
    </style>
</head>
<body>
    <div class="container">
        <h1>Mock Nightscout Server</h1>
        <div class="card">
            <h2>Current Status</h2>
            <div class="status">
                <div class="status-item">
                    <div class="status-value" id="glucose">--</div>
                    <div class="status-label">Glucose (mg/dL)</div>
                </div>
                <div class="status-item">
                    <div class="status-value" id="direction">--</div>
                    <div class="status-label">Direction</div>
                </div>
                <div class="status-item">
                    <div class="status-value" id="delta">--</div>
                    <div class="status-label">Delta</div>
                </div>
                <div class="status-item">
                    <div class="status-value" id="scenario">--</div>
                    <div class="status-label">Scenario</div>
                </div>
            </div>
        </div>
        <div class="card">
            <h2>Test Scenarios</h2>
            <div class="scenarios">
                <button class="scenario-btn normal" onclick="setScenario('normal')">Normal<br>(90-120)</button>
                <button class="scenario-btn high" onclick="setScenario('high')">High<br>(180-220)</button>
                <button class="scenario-btn low" onclick="setScenario('low')">Low<br>(60-70)</button>
                <button class="scenario-btn urgent_high" onclick="setScenario('urgent_high')">Urgent High<br>(250-300)</button>
                <button class="scenario-btn urgent_low" onclick="setScenario('urgent_low')">Urgent Low<br>(40-54)</button>
                <button class="scenario-btn rising" onclick="setScenario('rising')">Rising</button>
                <button class="scenario-btn falling" onclick="setScenario('falling')">Falling</button>
                <button class="scenario-btn stable" onclick="setScenario('stable')">Stable<br>(100)</button>
                <button class="scenario-btn random" onclick="setScenario('random')">Random</button>
            </div>
        </div>
        <div class="card">
            <h2>Manual Control</h2>
            <div class="controls">
                <input type="number" id="customValue" placeholder="Value" min="40" max="400" value="100">
                <button class="apply" onclick="setValue()">Set Value</button>
                <select id="customDirection" onchange="setDirection()">
                    <option value="Flat">Flat</option>
                    <option value="FortyFiveUp">45 Up</option>
                    <option value="FortyFiveDown">45 Down</option>
                    <option value="SingleUp">Rising</option>
                    <option value="SingleDown">Falling</option>
                    <option value="DoubleUp">Rapid Rise</option>
                    <option value="DoubleDown">Rapid Fall</option>
                </select>
            </div>
        </div>
        <div class="card">
            <h2>API Endpoints</h2>
            <div class="api-info">
                <p><code>GET /api/v1/entries/sgv.json?count=20</code></p>
                <p><code>GET /pebble?count=1&amp;units=mg</code></p>
                <p><code>GET /api/v1/status</code></p>
                <p style="margin-top:10px;color:#888;">Use: <code>http://localhost:5555</code></p>
            </div>
        </div>
        <p class="refresh-note">Status refreshes every 2 seconds</p>
    </div>
    <script>
        async function updateStatus() {
            try {
                const res = await fetch('/mock/status');
                const data = await res.json();
                document.getElementById('glucose').textContent = Math.round(data.currentGlucose);
                document.getElementById('direction').textContent = data.direction;
                document.getElementById('delta').textContent = (data.delta >= 0 ? '+' : '') + data.delta.toFixed(1);
                document.getElementById('scenario').textContent = data.scenario;
                document.querySelectorAll('.scenario-btn').forEach(btn => btn.classList.remove('active'));
                const activeBtn = document.querySelector('.' + data.scenario);
                if (activeBtn) activeBtn.classList.add('active');
            } catch (e) { console.error(e); }
        }
        async function setScenario(name) {
            await fetch('/mock/scenario/' + name, { method: 'POST' });
            updateStatus();
        }
        async function setValue() {
            const value = document.getElementById('customValue').value;
            await fetch('/mock/value/' + value, { method: 'POST' });
            updateStatus();
        }
        async function setDirection() {
            const dir = document.getElementById('customDirection').value;
            await fetch('/mock/direction/' + dir, { method: 'POST' });
            updateStatus();
        }
        updateStatus();
        setInterval(updateStatus, 2000);
    </script>
</body>
</html>
""";

app.Run();

// ============================================
// MODELS
// ============================================

public class MockGlucoseState
{
    public static string CurrentScenario { get; private set; } = "normal";
    public static double CurrentGlucose { get; private set; } = 100;
    public static string CurrentDirection { get; private set; } = "Flat";
    public static double CurrentDelta { get; private set; } = 0;

    private static readonly Random _random = new();
    private static double? _fixedValue = null;

    private static readonly Dictionary<string, string[]> ValidDirections = new()
    {
        { "Flat", new[] { "Flat" } },
        { "FortyFiveUp", new[] { "FortyFiveUp" } },
        { "FortyFiveDown", new[] { "FortyFiveDown" } },
        { "SingleUp", new[] { "SingleUp" } },
        { "SingleDown", new[] { "SingleDown" } },
        { "DoubleUp", new[] { "DoubleUp" } },
        { "DoubleDown", new[] { "DoubleDown" } }
    };

    public bool SetScenario(string scenario)
    {
        var validScenarios = new[] { "normal", "high", "low", "urgent_high", "urgent_low", "rising", "falling", "stable", "random" };
        if (!validScenarios.Contains(scenario.ToLower()))
            return false;

        CurrentScenario = scenario.ToLower();
        _fixedValue = null;
        UpdateForScenario();
        return true;
    }

    public void SetFixedValue(double value)
    {
        _fixedValue = value;
        CurrentGlucose = value;
        CurrentScenario = "fixed";
    }

    public bool SetDirection(string direction)
    {
        if (!ValidDirections.ContainsKey(direction))
            return false;

        CurrentDirection = direction;
        UpdateDeltaForDirection();
        return true;
    }

    private void UpdateForScenario()
    {
        switch (CurrentScenario)
        {
            case "normal":
                CurrentGlucose = _random.Next(90, 121);
                CurrentDirection = "Flat";
                CurrentDelta = _random.Next(-3, 4);
                break;
            case "high":
                CurrentGlucose = _random.Next(180, 221);
                CurrentDirection = "FortyFiveUp";
                CurrentDelta = _random.Next(5, 15);
                break;
            case "low":
                CurrentGlucose = _random.Next(60, 71);
                CurrentDirection = "FortyFiveDown";
                CurrentDelta = _random.Next(-10, -3);
                break;
            case "urgent_high":
                CurrentGlucose = _random.Next(250, 301);
                CurrentDirection = "DoubleUp";
                CurrentDelta = _random.Next(15, 30);
                break;
            case "urgent_low":
                CurrentGlucose = _random.Next(40, 55);
                CurrentDirection = "DoubleDown";
                CurrentDelta = _random.Next(-25, -10);
                break;
            case "rising":
                CurrentGlucose = _random.Next(100, 150);
                CurrentDirection = "SingleUp";
                CurrentDelta = _random.Next(10, 20);
                break;
            case "falling":
                CurrentGlucose = _random.Next(100, 150);
                CurrentDirection = "SingleDown";
                CurrentDelta = _random.Next(-20, -10);
                break;
            case "stable":
                CurrentGlucose = 100;
                CurrentDirection = "Flat";
                CurrentDelta = 0;
                break;
            case "random":
                CurrentGlucose = _random.Next(70, 181);
                var directions = new[] { "Flat", "FortyFiveUp", "FortyFiveDown", "SingleUp", "SingleDown" };
                CurrentDirection = directions[_random.Next(directions.Length)];
                CurrentDelta = _random.Next(-15, 16);
                break;
        }
    }

    private void UpdateDeltaForDirection()
    {
        CurrentDelta = CurrentDirection switch
        {
            "DoubleUp" => _random.Next(15, 30),
            "SingleUp" => _random.Next(10, 15),
            "FortyFiveUp" => _random.Next(5, 10),
            "Flat" => _random.Next(-3, 4),
            "FortyFiveDown" => _random.Next(-10, -5),
            "SingleDown" => _random.Next(-15, -10),
            "DoubleDown" => _random.Next(-30, -15),
            _ => 0
        };
    }

    public List<SgvEntry> GenerateEntries(int count)
    {
        var entries = new List<SgvEntry>();
        var now = DateTimeOffset.UtcNow;

        // If scenario is not fixed, apply small variations
        if (_fixedValue == null && CurrentScenario != "stable")
        {
            UpdateForScenario();
        }

        for (int i = 0; i < count; i++)
        {
            var entryTime = now.AddMinutes(-5 * i);
            var variation = _fixedValue.HasValue ? 0 : _random.Next(-5, 6);
            var glucose = Math.Max(40, Math.Min(400, CurrentGlucose + variation - (i * CurrentDelta / count)));

            entries.Add(new SgvEntry
            {
                Id = Guid.NewGuid().ToString(),
                Sgv = (int)glucose,
                Date = entryTime.ToUnixTimeMilliseconds(),
                DateString = entryTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                Trend = GetTrendNumber(CurrentDirection),
                Direction = CurrentDirection,
                Device = "MockNightscout",
                Type = "sgv",
                UtcOffset = 0,
                SysTime = entryTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                Mills = entryTime.ToUnixTimeMilliseconds(),
                BgDelta = i == 0 ? CurrentDelta : null
            });
        }

        return entries;
    }

    private static int GetTrendNumber(string direction) => direction switch
    {
        "DoubleUp" => 1,
        "SingleUp" => 2,
        "FortyFiveUp" => 3,
        "Flat" => 4,
        "FortyFiveDown" => 5,
        "SingleDown" => 6,
        "DoubleDown" => 7,
        _ => 4
    };
}

// Expose Program class for integration tests
public partial class Program { }
