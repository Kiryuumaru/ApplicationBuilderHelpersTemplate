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

	[Fact]
	public async Task TypedSetters_FormatValuesInvariantly()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "numbers");
		var timestamp = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);

		await store.SetInt("answer", 42);
		await store.SetDouble("pi", Math.PI);
		await store.SetDateTime("timestamp", timestamp);

		await service.Received(1).Set("numbers", "answer", "42", Arg.Any<CancellationToken>());
		await service.Received(1).Set("numbers", "pi", Math.PI.ToString("R", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
		await service.Received(1).Set("numbers", "timestamp", timestamp.ToString("O", CultureInfo.InvariantCulture), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task TypedGetters_ParseStoredValues()
	{
		var service = Substitute.For<ILocalStoreService>();
		using var store = CreateStore(service, "numbers");
		var timestamp = new DateTime(2023, 6, 1, 10, 30, 0, DateTimeKind.Utc);
		var offset = new DateTimeOffset(2023, 6, 1, 10, 30, 0, TimeSpan.FromHours(-5));
		var span = TimeSpan.FromMinutes(90.5);

		service.Get("numbers", "flag", Arg.Any<CancellationToken>()).Returns("True");
		service.Get("numbers", "count", Arg.Any<CancellationToken>()).Returns("42");
		service.Get("numbers", "timestamp", Arg.Any<CancellationToken>()).Returns(timestamp.ToString("O", CultureInfo.InvariantCulture));
		service.Get("numbers", "offset", Arg.Any<CancellationToken>()).Returns(offset.ToString("O", CultureInfo.InvariantCulture));
		service.Get("numbers", "span", Arg.Any<CancellationToken>()).Returns(span.ToString("c", CultureInfo.InvariantCulture));

		var flag = await store.GetBool("flag");
		var count = await store.GetInt("count");
		var restoredTimestamp = await store.GetDateTime("timestamp");
		var restoredOffset = await store.GetDateTimeOffset("offset");
		var restoredSpan = await store.GetTimeSpan("span");

		Assert.True(flag!.Value);
		Assert.Equal(42, count!.Value);
		Assert.Equal(timestamp, restoredTimestamp!.Value);
		Assert.Equal(offset, restoredOffset!.Value);
		Assert.Equal(span, restoredSpan!.Value);
	}

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
