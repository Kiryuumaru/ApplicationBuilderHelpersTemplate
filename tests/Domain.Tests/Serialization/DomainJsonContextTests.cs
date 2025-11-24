using System.Text.Json;
using Domain.Authorization.Enums;
using Domain.Authorization.Models;
using Domain.Shared.Serialization;
using Domain.Trading.ValueObjects;

namespace Domain.Tests.Serialization;

public class DomainJsonContextTests
{
    [Fact]
    public void Money_RoundTrips_WithCamelCaseProperties()
    {
        // Arrange
        var money = Money.Create(123.456m, "usd");
    var typeInfo = DomainJsonContext.Default.Money;

    // Act
    var json = JsonSerializer.Serialize(money, typeInfo);
    var roundTripped = JsonSerializer.Deserialize(json, typeInfo);

        // Assert
        Assert.Contains("\"amount\":123.456", json);
        Assert.Contains("\"currency\":\"USD\"", json);
        Assert.Equal(money, roundTripped);
    }

    [Fact]
    public void Enum_Serializes_As_CamelCase_String()
    {
        // Arrange
    var json = JsonSerializer.Serialize(PermissionAccessCategory.Write, DomainJsonContext.CreateOptions());

        // Assert
        Assert.Equal("\"write\"", json);
    }

    [Fact]
    public void Candle_RoundTrips_With_Nested_ValueObjects()
    {
        // Arrange
        var baseAsset = Asset.CreateCryptocurrency("BTC", "Bitcoin");
        var quoteAsset = Asset.CreateFiat("USD", "US Dollar");
        var pair = TradingPair.Create(baseAsset, quoteAsset, 0.0001m, 1000m, 4, 2);
        var candle = Candle.Create(
            pair,
            TimeSpan.FromMinutes(1),
            Price.Create(100m, "USD", DateTimeOffset.Parse("2024-01-01T00:00:00Z")),
            Price.Create(110m, "USD", DateTimeOffset.Parse("2024-01-01T00:30:00Z")),
            Price.Create(95m, "USD", DateTimeOffset.Parse("2024-01-01T00:15:00Z")),
            Price.Create(108m, "USD", DateTimeOffset.Parse("2024-01-01T00:59:00Z")),
            123.45m,
            DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
            DateTimeOffset.Parse("2024-01-01T00:01:00Z"));
    var typeInfo = DomainJsonContext.Default.Candle;

    // Act
    var json = JsonSerializer.Serialize(candle, typeInfo);
    var roundTripped = JsonSerializer.Deserialize(json, typeInfo);

        // Assert
        Assert.Contains("\"tradingPair\"", json);
        Assert.Equal(candle.TradingPair.Symbol, roundTripped!.TradingPair.Symbol);
        Assert.Equal(candle.Open.Value, roundTripped.Open.Value);
        Assert.Equal(candle.Close.Value, roundTripped.Close.Value);
        Assert.Equal(candle.Volume, roundTripped.Volume);
    }

    [Fact]
    public void PermissionParsedIdentifier_RoundTrips()
    {
        var parsed = Permission.ParseIdentifier("api:portfolio:accounts:update");
        var typeInfo = (System.Text.Json.Serialization.Metadata.JsonTypeInfo<Permission.ParsedIdentifier>)
            DomainJsonContext.Default.GetTypeInfo(typeof(Permission.ParsedIdentifier))!;

        var json = JsonSerializer.Serialize(parsed, typeInfo);
        var roundTripped = JsonSerializer.Deserialize(json, typeInfo);

        Assert.Equal(parsed.Canonical, roundTripped.Canonical);
        Assert.Equal(parsed.Identifier, roundTripped.Identifier);
    }
}
