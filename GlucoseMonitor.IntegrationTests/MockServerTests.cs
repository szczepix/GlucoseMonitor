using System.Net.Http.Json;
using FluentAssertions;
using GlucoseMonitor.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GlucoseMonitor.IntegrationTests;

/// <summary>
/// Integration tests that verify the GlucoseMonitor app works correctly
/// with the Mock Nightscout Server across all scenarios.
/// </summary>
public class MockServerTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public MockServerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // =========================================
    // API ENDPOINT TESTS
    // =========================================

    [Fact]
    public async Task MockServer_StatusEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/status");
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task MockServer_EntriesEndpoint_ReturnsValidData()
    {
        var response = await _client.GetAsync("/api/v1/entries/sgv.json?count=5");
        var entries = await response.Content.ReadFromJsonAsync<List<SgvEntry>>();

        response.IsSuccessStatusCode.Should().BeTrue();
        entries.Should().NotBeNull();
        entries.Should().HaveCount(5);
        entries!.All(e => e.Sgv > 0).Should().BeTrue();
        entries.All(e => !string.IsNullOrEmpty(e.Direction)).Should().BeTrue();
    }

    [Fact]
    public async Task MockServer_PebbleEndpoint_ReturnsValidData()
    {
        var response = await _client.GetAsync("/pebble?count=1&units=mg");
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    // =========================================
    // SCENARIO TESTS - Test all glucose ranges
    // =========================================

    [Theory]
    [InlineData("normal", 70, 140)]
    [InlineData("high", 170, 250)]
    [InlineData("low", 50, 80)]
    [InlineData("urgent_high", 240, 320)]
    [InlineData("urgent_low", 35, 60)]
    [InlineData("stable", 95, 105)]
    public async Task Scenario_ReturnsGlucoseInExpectedRange(string scenario, int minValue, int maxValue)
    {
        // Set scenario
        await _client.PostAsync($"/mock/scenario/{scenario}", null);

        // Fetch glucose via API
        var response = await _client.GetAsync("/api/v1/entries/sgv.json?count=1");
        var entries = await response.Content.ReadFromJsonAsync<List<SgvEntry>>();

        entries.Should().NotBeEmpty();
        var glucose = entries!.First().Sgv;
        glucose.Should().BeInRange(minValue, maxValue,
            $"Scenario '{scenario}' should return glucose between {minValue}-{maxValue}");
    }

    [Theory]
    [InlineData("rising", "SingleUp")]
    [InlineData("falling", "SingleDown")]
    [InlineData("urgent_high", "DoubleUp")]
    [InlineData("urgent_low", "DoubleDown")]
    [InlineData("stable", "Flat")]
    public async Task Scenario_ReturnsCorrectDirection(string scenario, string expectedDirection)
    {
        await _client.PostAsync($"/mock/scenario/{scenario}", null);

        var response = await _client.GetAsync("/api/v1/entries/sgv.json?count=1");
        var entries = await response.Content.ReadFromJsonAsync<List<SgvEntry>>();

        entries.Should().NotBeEmpty();
        entries!.First().Direction.Should().Be(expectedDirection);
    }

    // =========================================
    // GLUCOSE READING MODEL TESTS
    // =========================================

    [Fact]
    public async Task GlucoseReading_HasAllRequiredFields()
    {
        await _client.PostAsync("/mock/scenario/normal", null);

        var response = await _client.GetAsync("/api/v1/entries/sgv.json?count=1");
        var entries = await response.Content.ReadFromJsonAsync<List<SgvEntry>>();

        var entry = entries.Should().ContainSingle().Subject;
        entry.Sgv.Should().BeGreaterThan(0);
        entry.Direction.Should().NotBeNullOrEmpty();
        entry.Type.Should().Be("sgv");
        entry.Device.Should().Be("MockNightscout");
    }

    [Fact]
    public async Task GlucoseReading_HistoryReturnsMultipleEntries()
    {
        await _client.PostAsync("/mock/scenario/normal", null);

        var response = await _client.GetAsync("/api/v1/entries/sgv.json?count=20");
        var entries = await response.Content.ReadFromJsonAsync<List<SgvEntry>>();

        entries.Should().HaveCount(20);
    }

    // =========================================
    // ALARM THRESHOLD TESTS
    // =========================================

    [Theory]
    [InlineData(53, "UrgentLow")]
    [InlineData(54, "UrgentLow")]
    [InlineData(69, "Low")]
    [InlineData(70, "Low")]
    [InlineData(100, null)]          // Normal - no alarm
    [InlineData(180, "High")]
    [InlineData(181, "High")]
    [InlineData(250, "UrgentHigh")]
    [InlineData(251, "UrgentHigh")]
    public async Task AlarmThreshold_TriggersCorrectly(double glucoseValue, string? expectedCategory)
    {
        await _client.PostAsync($"/mock/value/{glucoseValue}", null);

        var response = await _client.GetAsync("/api/v1/entries/sgv.json?count=1");
        var entries = await response.Content.ReadFromJsonAsync<List<SgvEntry>>();

        var glucose = entries!.First().Sgv;
        var alarmCategory = GetAlarmCategory(glucose);

        if (expectedCategory != null)
        {
            alarmCategory.Should().Be(expectedCategory,
                $"Glucose {glucoseValue} should trigger {expectedCategory} alarm");
        }
        else
        {
            alarmCategory.Should().BeNull($"Glucose {glucoseValue} should not trigger an alarm");
        }
    }

    private static string? GetAlarmCategory(double value)
    {
        const double urgentHigh = 250;
        const double high = 180;
        const double low = 70;
        const double urgentLow = 54;

        if (value >= urgentHigh) return "UrgentHigh";
        if (value >= high) return "High";
        if (value <= urgentLow) return "UrgentLow";
        if (value <= low) return "Low";
        return null;
    }

    // =========================================
    // COLOR CODING TESTS (using GlucoseReading model)
    // =========================================

    [Theory]
    [InlineData(50, 255, 0, 0)]     // Low (<70) - Red
    [InlineData(65, 255, 0, 0)]     // Low (<70) - Red
    [InlineData(75, 255, 165, 0)]   // Low-normal (70-80) - Orange
    [InlineData(100, 0, 255, 0)]    // Normal (80-180) - Lime
    [InlineData(190, 255, 255, 0)]  // High-normal (180-250) - Yellow
    [InlineData(260, 255, 0, 0)]    // High (>250) - Red
    public void GlucoseReading_ReturnsCorrectColor(double glucoseValue, int expectedR, int expectedG, int expectedB)
    {
        // Create a GlucoseReading model directly to test color logic
        var reading = new GlucoseReading
        {
            Value = glucoseValue,
            Direction = "Flat",
            Units = "mg/dL",
            Timestamp = DateTime.Now
        };

        var color = reading.GetGlucoseColor();

        color.R.Should().Be((byte)expectedR);
        color.G.Should().Be((byte)expectedG);
        color.B.Should().Be((byte)expectedB);
    }

    // =========================================
    // DIRECTION ARROW TESTS
    // =========================================

    [Theory]
    [InlineData("Flat", "→")]
    [InlineData("FortyFiveUp", "↗")]
    [InlineData("FortyFiveDown", "↘")]
    [InlineData("SingleUp", "↑")]
    [InlineData("SingleDown", "↓")]
    [InlineData("DoubleUp", "⇈")]
    [InlineData("DoubleDown", "⇊")]
    public void GlucoseReading_ReturnsCorrectArrow(string direction, string expectedArrow)
    {
        var reading = new GlucoseReading
        {
            Value = 100,
            Direction = direction,
            Units = "mg/dL",
            Timestamp = DateTime.Now
        };

        var arrow = reading.GetDirectionArrow();
        arrow.Should().Be(expectedArrow);
    }

    // =========================================
    // MANUAL VALUE CONTROL TESTS
    // =========================================

    [Theory]
    [InlineData(42)]
    [InlineData(100)]
    [InlineData(250)]
    [InlineData(350)]
    public async Task ManualValue_SetsExactGlucose(double expectedValue)
    {
        await _client.PostAsync($"/mock/value/{expectedValue}", null);

        var response = await _client.GetAsync("/api/v1/entries/sgv.json?count=1");
        var entries = await response.Content.ReadFromJsonAsync<List<SgvEntry>>();

        entries!.First().Sgv.Should().BeCloseTo((int)expectedValue, 5,
            "Manual value should set glucose to approximately the specified value");
    }

    // =========================================
    // DIRECTION CONTROL TESTS
    // =========================================

    [Theory]
    [InlineData("Flat")]
    [InlineData("FortyFiveUp")]
    [InlineData("FortyFiveDown")]
    [InlineData("SingleUp")]
    [InlineData("SingleDown")]
    [InlineData("DoubleUp")]
    [InlineData("DoubleDown")]
    public async Task DirectionControl_SetsCorrectDirection(string direction)
    {
        await _client.PostAsync($"/mock/direction/{direction}", null);

        var response = await _client.GetAsync("/api/v1/entries/sgv.json?count=1");
        var entries = await response.Content.ReadFromJsonAsync<List<SgvEntry>>();

        entries!.First().Direction.Should().Be(direction);
    }

    // =========================================
    // ERROR HANDLING TESTS
    // =========================================

    [Fact]
    public async Task InvalidScenario_ReturnsBadRequest()
    {
        var response = await _client.PostAsync("/mock/scenario/invalid_scenario", null);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvalidDirection_ReturnsBadRequest()
    {
        var response = await _client.PostAsync("/mock/direction/InvalidDirection", null);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    // =========================================
    // MOCK STATUS TESTS
    // =========================================

    [Fact]
    public async Task MockStatus_ReturnsCurrentState()
    {
        await _client.PostAsync("/mock/scenario/high", null);

        var response = await _client.GetAsync("/mock/status");
        var status = await response.Content.ReadFromJsonAsync<MockStatus>();

        status.Should().NotBeNull();
        status!.Scenario.Should().Be("high");
        status.CurrentGlucose.Should().BeGreaterThan(0);
    }
}

// DTOs for deserializing mock server responses
public class SgvEntry
{
    public string? _id { get; set; }
    public int Sgv { get; set; }
    public long Date { get; set; }
    public string? DateString { get; set; }
    public int Trend { get; set; }
    public string? Direction { get; set; }
    public string? Device { get; set; }
    public string? Type { get; set; }
    public int UtcOffset { get; set; }
    public string? SysTime { get; set; }
    public long Mills { get; set; }
    public double? BgDelta { get; set; }
}

public class MockStatus
{
    public string? Scenario { get; set; }
    public double CurrentGlucose { get; set; }
    public string? Direction { get; set; }
    public double Delta { get; set; }
    public string? ServerTime { get; set; }
}
