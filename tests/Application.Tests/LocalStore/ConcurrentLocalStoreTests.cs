using Application.LocalStore.Extensions;
using Application.LocalStore.Interfaces;
using Infrastructure.Storage.Features;
using NSubstitute;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Tests.LocalStore;

public class ConcurrentLocalStoreTests
{
	[Fact]
	public async Task Get_ReturnsNullWhenMissing()
	{
		var service = Substitute.For<ILocalStoreService>();
		service.Get("demo", "key", Arg.Any<CancellationToken>()).Returns((string?)null);

		using var store = CreateStore(service, "demo");

		var result = await store.Get(" key ");

		Assert.Null(result);
		await service.Received(1).Get("demo", "key", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Set_NormalizesIdentifiers()
	{
		var service = Substitute.For<ILocalStoreService>();

		using var store = CreateStore(service, " demo ");

		await store.Set(" identifier ", "value");

		await service.Received(1).Set("demo", "identifier", "value", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ContainsOrError_ThrowsWhenNotFound()
	{
		var service = Substitute.For<ILocalStoreService>();
		service.Contains("group", "missing", Arg.Any<CancellationToken>()).Returns(false);

		using var store = CreateStore(service, "group");

		await Assert.ThrowsAsync<KeyNotFoundException>(() => store.ContainsOrError("missing"));
	}

	[Fact]
	public void Dispose_DisposesUnderlyingService()
	{
		var service = Substitute.For<ILocalStoreService>();

		var store = CreateStore(service, "group");
		store.Dispose();

		service.Received(1).Dispose();
	}

	[Fact]
	public async Task MultiThreadedSetOperations_AreSerialized()
	{
		var service = new TrackingLocalStoreService();
		using var store = CreateStore(service, "orders");

		var tasks = Enumerable.Range(0, 25)
			.Select(i => Task.Run(async () =>
			{
				await store.Set($"item-{i}", $"value-{i}");
			}));

		await Task.WhenAll(tasks);

		Assert.Equal(1, service.MaxConcurrency);
		var ids = await store.GetIds();
		Assert.Equal(25, ids.Length);
	}

	[Fact]
	public async Task MultiThreadedGetOperations_AreSerialized()
	{
		var service = new TrackingLocalStoreService();
		service.Seed("reports", "item-0", "value-0");
		using var store = CreateStore(service, "reports");

		var tasks = Enumerable.Range(0, 20)
			.Select(_ => Task.Run(async () =>
			{
				return await store.Get("item-0");
			}));

		var results = await Task.WhenAll(tasks);

		Assert.All(results, value => Assert.Equal("value-0", value));
		Assert.Equal(1, service.MaxConcurrency);
	}

	[Fact]
	public async Task MultiThreadedDeleteOperations_AreSerialized()
	{
		var service = new TrackingLocalStoreService();
		service.Seed("products", "sku", "value");
		using var store = CreateStore(service, "products");

		var tasks = Enumerable.Range(0, 20)
			.Select(_ => Task.Run(async () =>
			{
				await store.Delete("sku");
			}));

		await Task.WhenAll(tasks);

		Assert.Equal(1, service.MaxConcurrency);
		var ids = await store.GetIds();
		Assert.Empty(ids);
	}

	[Fact]
	public async Task Set_GenericSerializesPayloadUsingTypeInfo()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, " group ");
		var payload = new SamplePayload { Name = "Widget", Quantity = 3 };
		var typeInfo = GetTypeInfo<SamplePayload>();

		await store.Set(" payload ", payload, typeInfo);

		await service.Received(1).Set(
			"group",
			"payload",
			Arg.Is<string>(json => json.Contains("\"Name\":\"Widget\"", StringComparison.Ordinal) && json.Contains("\"Quantity\":3", StringComparison.Ordinal)),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_GenericDeserializesPayloadUsingTypeInfo()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "demo");
		var expected = new SamplePayload { Name = "Gizmo", Quantity = 7 };
		var typeInfo = GetTypeInfo<SamplePayload>();
		var json = JsonSerializer.Serialize(expected, typeInfo);
		service.Get("demo", "item", Arg.Any<CancellationToken>()).Returns(json);

		var result = await store.Get(" item ", typeInfo);

		Assert.NotNull(result);
		Assert.Equal(expected.Name, result!.Name);
		Assert.Equal(expected.Quantity, result.Quantity);
	}

	[Fact]
	public async Task Delete_DelegatesNullSetToService()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "inventory");

		await store.Delete(" sku ");

		await service.Received(1).Set("inventory", "sku", null, Arg.Any<CancellationToken>());
	}

	#region Primitive Type Tests - Bool

	[Fact]
	public async Task Set_Bool_True_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("flag", true);

		await service.Received(1).Set("test", "flag", "True", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Set_Bool_False_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("flag", false);

		await service.Received(1).Set("test", "flag", "False", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_Bool_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		service.Get("test", "flag", Arg.Any<CancellationToken>()).Returns("True");
		using var store = CreateStore(service, "test");

		var result = await store.Get<bool>("flag");

		Assert.True(result);
	}

	[Fact]
	public async Task Get_Bool_ReturnsDefaultWhenMissing()
	{
		var service = Substitute.For<ILocalStoreService>();
		service.Get("test", "flag", Arg.Any<CancellationToken>()).Returns((string?)null);
		using var store = CreateStore(service, "test");

		var result = await store.Get<bool>("flag");

		Assert.False(result);
	}

	#endregion

	#region Primitive Type Tests - Byte/SByte

	[Fact]
	public async Task Set_Byte_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("value", (byte)255);

		await service.Received(1).Set("test", "value", "255", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_Byte_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		service.Get("test", "value", Arg.Any<CancellationToken>()).Returns("200");
		using var store = CreateStore(service, "test");

		var result = await store.Get<byte>("value");

		Assert.Equal((byte)200, result);
	}

	[Fact]
	public async Task Set_SByte_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("value", (sbyte)-100);

		await service.Received(1).Set("test", "value", "-100", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_SByte_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		service.Get("test", "value", Arg.Any<CancellationToken>()).Returns("-50");
		using var store = CreateStore(service, "test");

		var result = await store.Get<sbyte>("value");

		Assert.Equal((sbyte)-50, result);
	}

	#endregion

	#region Primitive Type Tests - Short/UShort

	[Fact]
	public async Task Set_Short_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("value", (short)-32000);

		await service.Received(1).Set("test", "value", "-32000", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_Short_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		service.Get("test", "value", Arg.Any<CancellationToken>()).Returns("12345");
		using var store = CreateStore(service, "test");

		var result = await store.Get<short>("value");

		Assert.Equal((short)12345, result);
	}

	[Fact]
	public async Task Set_UShort_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("value", (ushort)65000);

		await service.Received(1).Set("test", "value", "65000", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_UShort_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		service.Get("test", "value", Arg.Any<CancellationToken>()).Returns("50000");
		using var store = CreateStore(service, "test");

		var result = await store.Get<ushort>("value");

		Assert.Equal((ushort)50000, result);
	}

	#endregion

	#region Primitive Type Tests - Int/UInt

	[Fact]
	public async Task Set_Int_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("answer", 42);

		await service.Received(1).Set("test", "answer", "42", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_Int_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		service.Get("test", "count", Arg.Any<CancellationToken>()).Returns("42");
		using var store = CreateStore(service, "test");

		var result = await store.Get<int>("count");

		Assert.Equal(42, result);
	}

	[Fact]
	public async Task Set_Int_NegativeValue_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("value", -2147483648);

		await service.Received(1).Set("test", "value", "-2147483648", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Set_UInt_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("value", 4000000000u);

		await service.Received(1).Set("test", "value", "4000000000", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_UInt_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		service.Get("test", "value", Arg.Any<CancellationToken>()).Returns("3000000000");
		using var store = CreateStore(service, "test");

		var result = await store.Get<uint>("value");

		Assert.Equal(3000000000u, result);
	}

	#endregion

	#region Primitive Type Tests - Long/ULong

	[Fact]
	public async Task Set_Long_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("value", 9223372036854775807L);

		await service.Received(1).Set("test", "value", "9223372036854775807", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_Long_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		service.Get("test", "value", Arg.Any<CancellationToken>()).Returns("-9223372036854775808");
		using var store = CreateStore(service, "test");

		var result = await store.Get<long>("value");

		Assert.Equal(-9223372036854775808L, result);
	}

	[Fact]
	public async Task Set_ULong_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("value", 18446744073709551615UL);

		await service.Received(1).Set("test", "value", "18446744073709551615", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_ULong_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		service.Get("test", "value", Arg.Any<CancellationToken>()).Returns("10000000000000000000");
		using var store = CreateStore(service, "test");

		var result = await store.Get<ulong>("value");

		Assert.Equal(10000000000000000000UL, result);
	}

	#endregion

	#region Primitive Type Tests - Float

	[Fact]
	public async Task Set_Float_FormatsWithRoundTrip()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");
		var value = 3.14159265f;

		await store.Set("pi", value);

		await service.Received(1).Set("test", "pi", value.ToString("R", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_Float_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		var expected = 2.71828f;
		service.Get("test", "e", Arg.Any<CancellationToken>()).Returns(expected.ToString("R", CultureInfo.InvariantCulture));
		using var store = CreateStore(service, "test");

		var result = await store.Get<float>("e");

		Assert.Equal(expected, result);
	}

	[Fact]
	public async Task Set_Float_SpecialValues_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("infinity", float.PositiveInfinity);
		await store.Set("nan", float.NaN);

		await service.Received(1).Set("test", "infinity", float.PositiveInfinity.ToString("R", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	#endregion

	#region Primitive Type Tests - Double

	[Fact]
	public async Task Set_Double_FormatsWithRoundTrip()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("pi", Math.PI);

		await service.Received(1).Set("test", "pi", Math.PI.ToString("R", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_Double_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		service.Get("test", "value", Arg.Any<CancellationToken>()).Returns(Math.E.ToString("R", CultureInfo.InvariantCulture));
		using var store = CreateStore(service, "test");

		var result = await store.Get<double>("value");

		Assert.Equal(Math.E, result);
	}

	[Fact]
	public async Task Set_Double_VerySmallNumber_PreservesPrecision()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");
		var value = 1.23456789012345E-300;

		await store.Set("tiny", value);

		await service.Received(1).Set("test", "tiny", value.ToString("R", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	#endregion

	#region Primitive Type Tests - Decimal

	[Fact]
	public async Task Set_Decimal_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("price", 19.99m);

		await service.Received(1).Set("test", "price", "19.99", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_Decimal_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		service.Get("test", "price", Arg.Any<CancellationToken>()).Returns("1234567890.123456789");
		using var store = CreateStore(service, "test");

		var result = await store.Get<decimal>("price");

		Assert.Equal(1234567890.123456789m, result);
	}

	[Fact]
	public async Task Set_Decimal_LargeValue_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");
		var value = 79228162514264337593543950335m; // decimal.MaxValue

		await store.Set("max", value);

		await service.Received(1).Set("test", "max", value.ToString(CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	#endregion

	#region Primitive Type Tests - Char

	[Fact]
	public async Task Set_Char_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("letter", 'A');

		await service.Received(1).Set("test", "letter", "A", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_Char_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		service.Get("test", "letter", Arg.Any<CancellationToken>()).Returns("Z");
		using var store = CreateStore(service, "test");

		var result = await store.Get<char>("letter");

		Assert.Equal('Z', result);
	}

	[Fact]
	public async Task Set_Char_UnicodeCharacter_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("emoji", '★');

		await service.Received(1).Set("test", "emoji", "★", Arg.Any<CancellationToken>());
	}

	#endregion

	#region DateTime Tests

	[Fact]
	public async Task Set_DateTime_FormatsAsIso8601()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");
		var timestamp = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);

		await store.Set("timestamp", timestamp);

		await service.Received(1).Set("test", "timestamp", timestamp.ToString("O", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_DateTime_ParsesIso8601()
	{
		var service = Substitute.For<ILocalStoreService>();
		var expected = new DateTime(2023, 12, 25, 10, 30, 0, DateTimeKind.Utc);
		service.Get("test", "timestamp", Arg.Any<CancellationToken>()).Returns(expected.ToString("O", CultureInfo.InvariantCulture));
		using var store = CreateStore(service, "test");

		var result = await store.Get<DateTime>("timestamp");

		// IParsable<DateTime>.Parse may convert to local time, so compare as UTC
		Assert.Equal(expected.ToUniversalTime(), result.ToUniversalTime());
	}

	[Fact]
	public async Task Set_DateTime_LocalTime_PreservesKind()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");
		var localTime = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Local);

		await store.Set("local", localTime);

		await service.Received(1).Set("test", "local", localTime.ToString("O", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Set_DateTime_MinMax_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("min", DateTime.MinValue);
		await store.Set("max", DateTime.MaxValue);

		await service.Received(1).Set("test", "min", DateTime.MinValue.ToString("O", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
		await service.Received(1).Set("test", "max", DateTime.MaxValue.ToString("O", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	#endregion

	#region DateTimeOffset Tests

	[Fact]
	public async Task Set_DateTimeOffset_FormatsAsIso8601()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");
		var offset = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.FromHours(-5));

		await store.Set("offset", offset);

		await service.Received(1).Set("test", "offset", offset.ToString("O", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_DateTimeOffset_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		var expected = new DateTimeOffset(2023, 6, 1, 10, 30, 0, TimeSpan.FromHours(-5));
		service.Get("test", "offset", Arg.Any<CancellationToken>()).Returns(expected.ToString("O", CultureInfo.InvariantCulture));
		using var store = CreateStore(service, "test");

		var result = await store.Get<DateTimeOffset>("offset");

		Assert.Equal(expected, result);
		Assert.Equal(TimeSpan.FromHours(-5), result.Offset);
	}

	[Fact]
	public async Task Set_DateTimeOffset_UtcOffset_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");
		var utcOffset = new DateTimeOffset(2024, 3, 10, 12, 0, 0, TimeSpan.Zero);

		await store.Set("utc", utcOffset);

		await service.Received(1).Set("test", "utc", utcOffset.ToString("O", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Set_DateTimeOffset_PositiveOffset_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");
		var positiveOffset = new DateTimeOffset(2024, 7, 20, 18, 45, 30, TimeSpan.FromHours(9));

		await store.Set("tokyo", positiveOffset);

		await service.Received(1).Set("test", "tokyo", positiveOffset.ToString("O", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	#endregion

	#region TimeSpan Tests

	[Fact]
	public async Task Set_TimeSpan_FormatsAsConstant()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");
		var span = TimeSpan.FromMinutes(90.5);

		await store.Set("duration", span);

		await service.Received(1).Set("test", "duration", span.ToString("c", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_TimeSpan_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		var expected = TimeSpan.FromHours(2.5);
		service.Get("test", "duration", Arg.Any<CancellationToken>()).Returns(expected.ToString("c", CultureInfo.InvariantCulture));
		using var store = CreateStore(service, "test");

		var result = await store.Get<TimeSpan>("duration");

		Assert.Equal(expected, result);
	}

	[Fact]
	public async Task Set_TimeSpan_Days_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");
		var span = new TimeSpan(7, 12, 30, 45, 123);

		await store.Set("week", span);

		await service.Received(1).Set("test", "week", span.ToString("c", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Set_TimeSpan_Negative_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");
		var span = TimeSpan.FromHours(-5);

		await store.Set("negative", span);

		await service.Received(1).Set("test", "negative", span.ToString("c", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Set_TimeSpan_MaxMin_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("max", TimeSpan.MaxValue);
		await store.Set("min", TimeSpan.MinValue);

		await service.Received(1).Set("test", "max", TimeSpan.MaxValue.ToString("c", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
		await service.Received(1).Set("test", "min", TimeSpan.MinValue.ToString("c", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	#endregion

	#region Guid Tests

	[Fact]
	public async Task Set_Guid_FormatsWithHyphens()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");
		var guid = Guid.Parse("12345678-1234-1234-1234-123456789abc");

		await store.Set("id", guid);

		await service.Received(1).Set("test", "id", "12345678-1234-1234-1234-123456789abc", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_Guid_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		var expected = Guid.Parse("abcdef12-3456-7890-abcd-ef1234567890");
		service.Get("test", "id", Arg.Any<CancellationToken>()).Returns(expected.ToString("D"));
		using var store = CreateStore(service, "test");

		var result = await store.Get<Guid>("id");

		Assert.Equal(expected, result);
	}

	[Fact]
	public async Task Set_Guid_Empty_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");

		await store.Set("empty", Guid.Empty);

		await service.Received(1).Set("test", "empty", "00000000-0000-0000-0000-000000000000", Arg.Any<CancellationToken>());
	}

	#endregion

	#region DateOnly/TimeOnly Tests (.NET 6+)

	[Fact]
	public async Task Set_DateOnly_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");
		var date = new DateOnly(2024, 12, 25);

		await store.Set("date", date);

		await service.Received(1).Set("test", "date", date.ToString(null, CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_DateOnly_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		var expected = new DateOnly(2024, 1, 1);
		service.Get("test", "date", Arg.Any<CancellationToken>()).Returns(expected.ToString(null, CultureInfo.InvariantCulture));
		using var store = CreateStore(service, "test");

		var result = await store.Get<DateOnly>("date");

		Assert.Equal(expected, result);
	}

	[Fact]
	public async Task Set_TimeOnly_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");
		var time = new TimeOnly(14, 30, 45);

		await store.Set("time", time);

		await service.Received(1).Set("test", "time", time.ToString("O", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_TimeOnly_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		var expected = new TimeOnly(9, 15, 0);
		service.Get("test", "time", Arg.Any<CancellationToken>()).Returns(expected.ToString("O", CultureInfo.InvariantCulture));
		using var store = CreateStore(service, "test");

		var result = await store.Get<TimeOnly>("time");

		Assert.Equal(expected, result);
	}

	#endregion

	#region Half Tests (.NET 5+)

	[Fact]
	public async Task Set_Half_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");
		var value = (Half)3.14;

		await store.Set("half", value);

		await service.Received(1).Set("test", "half", value.ToString("R", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_Half_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		var expected = (Half)2.5;
		service.Get("test", "half", Arg.Any<CancellationToken>()).Returns(expected.ToString("R", CultureInfo.InvariantCulture));
		using var store = CreateStore(service, "test");

		var result = await store.Get<Half>("half");

		Assert.Equal(expected, result);
	}

	#endregion

	#region Int128/UInt128 Tests (.NET 7+)

	[Fact]
	public async Task Set_Int128_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");
		var value = Int128.MaxValue;

		await store.Set("big", value);

		await service.Received(1).Set("test", "big", value.ToString(null, CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_Int128_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		var expected = Int128.Parse("170141183460469231731687303715884105727");
		service.Get("test", "big", Arg.Any<CancellationToken>()).Returns(expected.ToString(null, CultureInfo.InvariantCulture));
		using var store = CreateStore(service, "test");

		var result = await store.Get<Int128>("big");

		Assert.Equal(expected, result);
	}

	[Fact]
	public async Task Set_UInt128_FormatsCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "test");
		var value = UInt128.MaxValue;

		await store.Set("huge", value);

		await service.Received(1).Set("test", "huge", value.ToString(null, CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_UInt128_ParsesCorrectly()
	{
		var service = Substitute.For<ILocalStoreService>();
		var expected = UInt128.Parse("340282366920938463463374607431768211455");
		service.Get("test", "huge", Arg.Any<CancellationToken>()).Returns(expected.ToString(null, CultureInfo.InvariantCulture));
		using var store = CreateStore(service, "test");

		var result = await store.Get<UInt128>("huge");

		Assert.Equal(expected, result);
	}

	#endregion

	#region Round-Trip Tests

	[Fact]
	public async Task RoundTrip_AllPrimitiveTypes_PreserveValues()
	{
		var service = new TrackingLocalStoreService();
		using var store = CreateStore(service, "roundtrip");

		// Set values
		await store.Set("bool", true);
		await store.Set("byte", (byte)200);
		await store.Set("sbyte", (sbyte)-100);
		await store.Set("short", (short)-30000);
		await store.Set("ushort", (ushort)60000);
		await store.Set("int", -2000000000);
		await store.Set("uint", 4000000000u);
		await store.Set("long", -9000000000000000000L);
		await store.Set("ulong", 18000000000000000000UL);
		await store.Set("float", 3.14159f);
		await store.Set("double", Math.PI);
		await store.Set("decimal", 12345.6789m);
		await store.Set("char", 'X');
		await store.Set("guid", Guid.Parse("12345678-1234-1234-1234-123456789abc"));

		// Get and verify
		Assert.True(await store.Get<bool>("bool"));
		Assert.Equal((byte)200, await store.Get<byte>("byte"));
		Assert.Equal((sbyte)-100, await store.Get<sbyte>("sbyte"));
		Assert.Equal((short)-30000, await store.Get<short>("short"));
		Assert.Equal((ushort)60000, await store.Get<ushort>("ushort"));
		Assert.Equal(-2000000000, await store.Get<int>("int"));
		Assert.Equal(4000000000u, await store.Get<uint>("uint"));
		Assert.Equal(-9000000000000000000L, await store.Get<long>("long"));
		Assert.Equal(18000000000000000000UL, await store.Get<ulong>("ulong"));
		Assert.Equal(3.14159f, await store.Get<float>("float"));
		Assert.Equal(Math.PI, await store.Get<double>("double"));
		Assert.Equal(12345.6789m, await store.Get<decimal>("decimal"));
		Assert.Equal('X', await store.Get<char>("char"));
		Assert.Equal(Guid.Parse("12345678-1234-1234-1234-123456789abc"), await store.Get<Guid>("guid"));
	}

	[Fact]
	public async Task RoundTrip_DateTimeTypes_PreserveValues()
	{
		var service = new TrackingLocalStoreService();
		using var store = CreateStore(service, "datetime");

		var dateTime = new DateTime(2024, 6, 15, 14, 30, 45, 123, DateTimeKind.Utc);
		var dateTimeOffset = new DateTimeOffset(2024, 6, 15, 14, 30, 45, 123, TimeSpan.FromHours(-5));
		var timeSpan = new TimeSpan(7, 12, 30, 45, 500);
		var dateOnly = new DateOnly(2024, 12, 25);
		var timeOnly = new TimeOnly(14, 30, 45, 123);

		await store.Set("datetime", dateTime);
		await store.Set("datetimeoffset", dateTimeOffset);
		await store.Set("timespan", timeSpan);
		await store.Set("dateonly", dateOnly);
		await store.Set("timeonly", timeOnly);

		// DateTime may be converted to local time during parsing, compare as UTC
		var restoredDateTime = await store.Get<DateTime>("datetime");
		Assert.Equal(dateTime.ToUniversalTime(), restoredDateTime.ToUniversalTime());
		Assert.Equal(dateTimeOffset, await store.Get<DateTimeOffset>("datetimeoffset"));
		Assert.Equal(timeSpan, await store.Get<TimeSpan>("timespan"));
		Assert.Equal(dateOnly, await store.Get<DateOnly>("dateonly"));
		Assert.Equal(timeOnly, await store.Get<TimeOnly>("timeonly"));
	}

	[Fact]
	public async Task RoundTrip_DateTimeTypes_PreserveMicrosecondPrecision()
	{
		var service = new TrackingLocalStoreService();
		using var store = CreateStore(service, "precision");

		// Create values with sub-millisecond precision (ticks = 100 nanoseconds)
		// 1234567 ticks = 123.4567 milliseconds = 123456.7 microseconds
		var dateTime = new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc).AddTicks(1234567);
		var dateTimeOffset = new DateTimeOffset(2024, 6, 15, 14, 30, 45, TimeSpan.FromHours(-5)).AddTicks(9876543);
		var timeSpan = TimeSpan.FromTicks(123456789012345); // Very precise
		var timeOnly = new TimeOnly(14, 30, 45).Add(TimeSpan.FromTicks(1234567));

		await store.Set("datetime", dateTime);
		await store.Set("datetimeoffset", dateTimeOffset);
		await store.Set("timespan", timeSpan);
		await store.Set("timeonly", timeOnly);

		// Verify tick-level precision is preserved
		var restoredDateTime = await store.Get<DateTime>("datetime");
		Assert.Equal(dateTime.Ticks, restoredDateTime.ToUniversalTime().Ticks);

		var restoredOffset = await store.Get<DateTimeOffset>("datetimeoffset");
		Assert.Equal(dateTimeOffset.Ticks, restoredOffset.Ticks);

		var restoredTimeSpan = await store.Get<TimeSpan>("timespan");
		Assert.Equal(timeSpan.Ticks, restoredTimeSpan.Ticks);

		var restoredTimeOnly = await store.Get<TimeOnly>("timeonly");
		Assert.Equal(timeOnly.Ticks, restoredTimeOnly.Ticks);
	}

	#endregion

	private static ConcurrentLocalStore CreateStore(ILocalStoreService service, string group)
		=> new(service, group);

	private static JsonTypeInfo<T?> GetTypeInfo<T>() where T : class
		=> (JsonTypeInfo<T?>)SerializerOptions.GetTypeInfo(typeof(T))!;

	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		TypeInfoResolver = new DefaultJsonTypeInfoResolver()
	};

	private sealed class SamplePayload
	{
		public string Name { get; set; } = string.Empty;
		public int Quantity { get; set; }
	}

	private sealed class TrackingLocalStoreService : ILocalStoreService
	{
		private readonly ConcurrentDictionary<string, string?> storage = new();
		private int activeOperations;
		private int maxConcurrency;
		public int MaxConcurrency => Volatile.Read(ref maxConcurrency);

		public Task<string?> Get(string group, string id, CancellationToken cancellationToken)
			=> TrackAsync(async () =>
			{
				await Task.Delay(5, cancellationToken);
				storage.TryGetValue(Key(group, id), out var value);
				return value;
			});

		public Task<string[]> GetIds(string group, CancellationToken cancellationToken = default)
			=> TrackAsync(async () =>
			{
				await Task.Delay(5, cancellationToken);
				return storage.Keys
					.Where(k => k.StartsWith(group + ":", StringComparison.Ordinal))
					.Select(k => k[(group.Length + 1)..])
					.ToArray();
			});

		public Task Set(string group, string id, string? data, CancellationToken cancellationToken)
			=> TrackAsync(async () =>
			{
				await Task.Delay(5, cancellationToken);
				if (data is null)
				{
					storage.TryRemove(Key(group, id), out _);
				}
				else
				{
					storage[Key(group, id)] = data;
				}
			});

		public Task<bool> Contains(string group, string id, CancellationToken cancellationToken)
			=> TrackAsync(async () =>
			{
				await Task.Delay(1, cancellationToken);
				return storage.ContainsKey(Key(group, id));
			});

		public Task Open(CancellationToken cancellationToken)
			=> TrackAsync(() => Task.CompletedTask);

		public Task CommitAsync(CancellationToken cancellationToken)
			=> TrackAsync(() => Task.CompletedTask);

		public Task RollbackAsync(CancellationToken cancellationToken)
			=> TrackAsync(() => Task.CompletedTask);

		private async Task<T> TrackAsync<T>(Func<Task<T>> operation)
		{
			var current = Interlocked.Increment(ref activeOperations);
			UpdateMax(current);
			try
			{
				return await operation().ConfigureAwait(false);
			}
			finally
			{
				Interlocked.Decrement(ref activeOperations);
			}
		}

		private Task TrackAsync(Func<Task> operation)
			=> TrackAsync(async () =>
			{
				await operation().ConfigureAwait(false);
				return true;
			});

		private void UpdateMax(int current)
		{
			int snapshot, updated;
			do
			{
				snapshot = Volatile.Read(ref maxConcurrency);
				updated = Math.Max(snapshot, current);
			}
			while (snapshot != Interlocked.CompareExchange(ref maxConcurrency, updated, snapshot));
		}

		private static string Key(string group, string id) => $"{group}:{id}";

		public void Seed(string group, string id, string value)
		{
			storage[Key(group, id)] = value;
		}

		public void Dispose()
		{
			storage.Clear();
		}
	}
}
