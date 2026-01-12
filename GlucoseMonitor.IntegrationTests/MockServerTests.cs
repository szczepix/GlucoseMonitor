using System.Net.Http.Json;
using FluentAssertions;
using GlucoseMonitor.Core.Models;
using GlucoseMonitor.MockServer;
using GlucoseMonitor.MockServer.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GlucoseMonitor.IntegrationTests;

/// <summary>
/// Integration tests that verify the GlucoseMonitor app works correctly
/// with the Mock Nightscout Server across all scenarios.
/// </summary>
public class MockServerTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly HttpClient _client;

    public MockServerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    #region Helper Methods

    /// <summary>
    /// Sets the mock server to a specific scenario.
    /// </summary>
    private Task SetScenarioAsync(string scenario) =>
        _client.PostAsync($"/mock/scenario/{scenario}", null);

    /// <summary>
    /// Sets a specific glucose value on the mock server.
    /// </summary>
    private Task SetGlucoseValueAsync(double value) =>
        _client.PostAsync($"/mock/value/{value}", null);

    /// <summary>
    /// Sets the trend direction on the mock server.
    /// </summary>
    private Task SetDirectionAsync(string direction) =>
        _client.PostAsync($"/mock/direction/{direction}", null);

    /// <summary>
    /// Fetches glucose entries from the mock server.
    /// </summary>
    private async Task<List<SgvEntry>> GetEntriesAsync(int count = 1)
    {
        var response = await _client.GetAsync($"/api/v1/entries/sgv.json?count={count}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<SgvEntry>>() ?? [];
    }

    /// <summary>
    /// Fetches the first glucose entry from the mock server.
    /// </summary>
    private async Task<SgvEntry> GetFirstEntryAsync()
    {
        var entries = await GetEntriesAsync(1);
        return entries.First();
    }

    /// <summary>
    /// Fetches the mock server status.
    /// </summary>
    private async Task<MockStatusResponse> GetMockStatusAsync()
    {
        var response = await _client.GetAsync("/mock/status");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MockStatusResponse>()
            ?? throw new InvalidOperationException("Failed to deserialize mock status");
    }

    #endregion

    #region API Endpoint Tests

    [Fact]
    [Trait("Category", "API")]
    public async Task MockServer_StatusEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/status");
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "API")]
    public async Task MockServer_EntriesEndpoint_ReturnsValidData()
    {
        var entries = await GetEntriesAsync(5);

        entries.Should().HaveCount(5);
        entries.Should().AllSatisfy(e =>
        {
            int.Parse(e.Sgv!).Should().BeGreaterThan(0);
            e.Direction.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    [Trait("Category", "API")]
    public async Task MockServer_PebbleEndpoint_ReturnsValidData()
    {
        var response = await _client.GetAsync("/pebble?count=1&units=mg");
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    #endregion

    #region Scenario Tests

    [Theory]
    [Trait("Category", "Scenarios")]
    [InlineData("normal", 70, 140)]
    [InlineData("high", 170, 250)]
    [InlineData("low", 50, 80)]
    [InlineData("urgent_high", 240, 320)]
    [InlineData("urgent_low", 35, 60)]
    [InlineData("stable", 95, 105)]
    public async Task Scenario_ReturnsGlucoseInExpectedRange(string scenario, int minValue, int maxValue)
    {
        await SetScenarioAsync(scenario);

        var entry = await GetFirstEntryAsync();

        int.Parse(entry.Sgv!).Should().BeInRange(minValue, maxValue,
            $"Scenario '{scenario}' should return glucose between {minValue}-{maxValue}");
    }

    [Theory]
    [Trait("Category", "Scenarios")]
    [InlineData("rising", "SingleUp")]
    [InlineData("falling", "SingleDown")]
    [InlineData("urgent_high", "DoubleUp")]
    [InlineData("urgent_low", "DoubleDown")]
    [InlineData("stable", "Flat")]
    public async Task Scenario_ReturnsCorrectDirection(string scenario, string expectedDirection)
    {
        await SetScenarioAsync(scenario);

        var entry = await GetFirstEntryAsync();

        entry.Direction.Should().Be(expectedDirection);
    }

    #endregion

    #region Glucose Reading Model Tests

    [Fact]
    [Trait("Category", "Model")]
    public async Task GlucoseReading_HasAllRequiredFields()
    {
        await SetScenarioAsync("normal");

        var entry = await GetFirstEntryAsync();

        int.Parse(entry.Sgv!).Should().BeGreaterThan(0);
        entry.Direction.Should().NotBeNullOrEmpty();
        entry.Type.Should().Be("sgv");
        entry.Device.Should().Be("MockNightscout");
    }

    [Fact]
    [Trait("Category", "Model")]
    public async Task GlucoseReading_HistoryReturnsMultipleEntries()
    {
        await SetScenarioAsync("normal");

        var entries = await GetEntriesAsync(20);

        entries.Should().HaveCount(20);
    }

    #endregion

    #region Alarm Threshold Tests

    [Theory]
    [Trait("Category", "Alarms")]
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
        await SetGlucoseValueAsync(glucoseValue);

        var entry = await GetFirstEntryAsync();
        var alarmCategory = GlucoseThresholds.GetAlarmCategory(int.Parse(entry.Sgv!));

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

    #endregion

    #region Color Coding Tests

    [Theory]
    [Trait("Category", "Colors")]
    [InlineData(50, 255, 0, 0)]     // Low (<70) - Red
    [InlineData(65, 255, 0, 0)]     // Low (<70) - Red
    [InlineData(75, 255, 165, 0)]   // Low-normal (70-80) - Orange
    [InlineData(100, 0, 255, 0)]    // Normal (80-180) - Lime
    [InlineData(190, 255, 255, 0)]  // High-normal (180-250) - Yellow
    [InlineData(260, 255, 0, 0)]    // High (>250) - Red
    public void GlucoseReading_ReturnsCorrectColor(double glucoseValue, int expectedR, int expectedG, int expectedB)
    {
        var reading = CreateGlucoseReading(glucoseValue);

        var color = reading.GetGlucoseColor();

        color.R.Should().Be((byte)expectedR);
        color.G.Should().Be((byte)expectedG);
        color.B.Should().Be((byte)expectedB);
    }

    private static GlucoseReading CreateGlucoseReading(double value, string direction = "Flat") =>
        new()
        {
            Value = value,
            Direction = direction,
            Units = "mg/dL",
            Timestamp = DateTime.Now
        };

    #endregion

    #region Direction Arrow Tests

    [Theory]
    [Trait("Category", "Arrows")]
    [InlineData("Flat", "→")]
    [InlineData("FortyFiveUp", "↗")]
    [InlineData("FortyFiveDown", "↘")]
    [InlineData("SingleUp", "↑")]
    [InlineData("SingleDown", "↓")]
    [InlineData("DoubleUp", "⇈")]
    [InlineData("DoubleDown", "⇊")]
    public void GlucoseReading_ReturnsCorrectArrow(string direction, string expectedArrow)
    {
        var reading = CreateGlucoseReading(100, direction);

        var arrow = reading.GetDirectionArrow();

        arrow.Should().Be(expectedArrow);
    }

    #endregion

    #region Manual Control Tests

    [Theory]
    [Trait("Category", "Control")]
    [InlineData(42)]
    [InlineData(100)]
    [InlineData(250)]
    [InlineData(350)]
    public async Task ManualValue_SetsExactGlucose(double expectedValue)
    {
        await SetGlucoseValueAsync(expectedValue);

        var entry = await GetFirstEntryAsync();

        int.Parse(entry.Sgv!).Should().BeCloseTo((int)expectedValue, 5,
            "Manual value should set glucose to approximately the specified value");
    }

    [Theory]
    [Trait("Category", "Control")]
    [InlineData("Flat")]
    [InlineData("FortyFiveUp")]
    [InlineData("FortyFiveDown")]
    [InlineData("SingleUp")]
    [InlineData("SingleDown")]
    [InlineData("DoubleUp")]
    [InlineData("DoubleDown")]
    public async Task DirectionControl_SetsCorrectDirection(string direction)
    {
        await SetDirectionAsync(direction);

        var entry = await GetFirstEntryAsync();

        entry.Direction.Should().Be(direction);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    [Trait("Category", "Errors")]
    public async Task InvalidScenario_ReturnsBadRequest()
    {
        var response = await _client.PostAsync("/mock/scenario/invalid_scenario", null);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Errors")]
    public async Task InvalidDirection_ReturnsBadRequest()
    {
        var response = await _client.PostAsync("/mock/direction/InvalidDirection", null);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    #endregion

    #region Mock Status Tests

    [Fact]
    [Trait("Category", "Status")]
    public async Task MockStatus_ReturnsCurrentState()
    {
        await SetScenarioAsync("high");

        var status = await GetMockStatusAsync();

        status.Should().NotBeNull();
        status.Scenario.Should().Be("high");
        status.CurrentGlucose.Should().BeGreaterThan(0);
    }

    #endregion
}
