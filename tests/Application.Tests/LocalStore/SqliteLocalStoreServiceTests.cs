using Application.LocalStore.Services;
using Infrastructure.Storage.Features;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Tests.LocalStore;

public class SqliteLocalStoreServiceTests
{
	[Fact]
	public async Task SetAndGet_PersistsDataAfterCommit()
	{
		var dbPath = CreateTempDbPath();
		ResetGlobalInitializationState();
		CleanupDatabaseFiles(dbPath);

		var configuration = BuildConfiguration(dbPath);

		using (var service = new SqliteLocalStoreService(configuration))
		{
			await service.Open(CancellationToken.None);
			await service.Set("orders", "42", "payload", CancellationToken.None);
			await service.CommitAsync(CancellationToken.None);
		}

		using (var verifier = new SqliteLocalStoreService(configuration))
		{
			await verifier.Open(CancellationToken.None);
			var value = await verifier.Get("orders", "42", CancellationToken.None);

			Assert.Equal("payload", value);

			await verifier.RollbackAsync(CancellationToken.None);
		}

		CleanupDatabaseFiles(dbPath);
	}

	[Fact]
	public async Task RollbackAsync_RemovesUncommittedChanges()
	{
		var dbPath = CreateTempDbPath();
		ResetGlobalInitializationState();
		CleanupDatabaseFiles(dbPath);

		var configuration = BuildConfiguration(dbPath);

		using (var service = new SqliteLocalStoreService(configuration))
		{
			await service.Open(CancellationToken.None);
			await service.Set("orders", "temp", "value", CancellationToken.None);
			await service.RollbackAsync(CancellationToken.None);
		}

		using (var verifier = new SqliteLocalStoreService(configuration))
		{
			await verifier.Open(CancellationToken.None);
			var value = await verifier.Get("orders", "temp", CancellationToken.None);

			Assert.Null(value);

			await verifier.RollbackAsync(CancellationToken.None);
		}

		CleanupDatabaseFiles(dbPath);
	}

	[Fact]
	public async Task ConcurrentLocalStore_CommitsAllParallelWrites()
	{
		var dbPath = CreateTempDbPath();
		ResetGlobalInitializationState();
		CleanupDatabaseFiles(dbPath);
		var configuration = BuildConfiguration(dbPath);
		const int itemCount = 25;

		using (var service = new SqliteLocalStoreService(configuration))
		{
			await service.Open(CancellationToken.None);
			using var store = new ConcurrentLocalStore(service, "bulk");

			var tasks = Enumerable.Range(0, itemCount)
				.Select(i => Task.Run(async () =>
				{
					await store.Set($"item-{i}", $"value-{i}", CancellationToken.None);
				}));

			await Task.WhenAll(tasks);
			await store.CommitAsync(CancellationToken.None);
		}

		using (var verifier = new SqliteLocalStoreService(configuration))
		{
			await verifier.Open(CancellationToken.None);
			for (var i = 0; i < itemCount; i++)
			{
				var value = await verifier.Get("bulk", $"item-{i}", CancellationToken.None);
				Assert.Equal($"value-{i}", value);
			}
			await verifier.RollbackAsync(CancellationToken.None);
		}

		CleanupDatabaseFiles(dbPath);
	}

	[Fact]
	public async Task Contains_ReflectsCommittedInsertAndDelete()
	{
		var dbPath = CreateTempDbPath();
		ResetGlobalInitializationState();
		CleanupDatabaseFiles(dbPath);
		var configuration = BuildConfiguration(dbPath);

		using (var writer = new SqliteLocalStoreService(configuration))
		{
			await writer.Open(CancellationToken.None);
			Assert.False(await writer.Contains("orders", "alpha", CancellationToken.None));
			await writer.Set(" orders ", " alpha ", "first", CancellationToken.None);
			await writer.CommitAsync(CancellationToken.None);
		}

		using (var reader = new SqliteLocalStoreService(configuration))
		{
			await reader.Open(CancellationToken.None);
			Assert.True(await reader.Contains("orders", "alpha", CancellationToken.None));
			await reader.Set("orders", "alpha", null, CancellationToken.None);
			await reader.CommitAsync(CancellationToken.None);
		}

		using (var verifier = new SqliteLocalStoreService(configuration))
		{
			await verifier.Open(CancellationToken.None);
			Assert.False(await verifier.Contains(" orders ", " alpha ", CancellationToken.None));
			await verifier.RollbackAsync(CancellationToken.None);
		}

		CleanupDatabaseFiles(dbPath);
	}

	[Fact]
	public async Task GetIds_ReturnsNormalizedIdentifiersForSpecifiedGroup()
	{
		var dbPath = CreateTempDbPath();
		ResetGlobalInitializationState();
		CleanupDatabaseFiles(dbPath);
		var configuration = BuildConfiguration(dbPath);

		using (var writer = new SqliteLocalStoreService(configuration))
		{
			await writer.Open(CancellationToken.None);
			await writer.Set(" portfolio ", "  ID-1  ", "value1", CancellationToken.None);
			await writer.Set("portfolio", "ID-2", "value2", CancellationToken.None);
			await writer.Set("other", "ignored", "value3", CancellationToken.None);
			await writer.CommitAsync(CancellationToken.None);
		}

		using (var verifier = new SqliteLocalStoreService(configuration))
		{
			await verifier.Open(CancellationToken.None);
			var ids = await verifier.GetIds("  portfolio  ", CancellationToken.None);
			Assert.Equal(new[] { "ID-1", "ID-2" }, ids);
			await verifier.RollbackAsync(CancellationToken.None);
		}

		CleanupDatabaseFiles(dbPath);
	}

	[Fact]
	public async Task LocalStoreScenario_SerializesAndDeletesVariousTypes()
	{
		var dbPath = CreateTempDbPath();
		ResetGlobalInitializationState();
		CleanupDatabaseFiles(dbPath);
		var configuration = BuildConfiguration(dbPath);
		var payloadTypeInfo = GetTypeInfo<SamplePayload>();

		var decimalValue = 42.42m;
		var timestamp = new DateTime(2024, 2, 3, 4, 5, 6, DateTimeKind.Utc);
		var timestampOffset = new DateTimeOffset(2024, 2, 3, 4, 5, 6, TimeSpan.FromHours(-3));
		var duration = TimeSpan.FromMinutes(12.5);

		using (var service = new SqliteLocalStoreService(configuration))
		{
			await service.Open(CancellationToken.None);
			using var store = new ConcurrentLocalStore(service, "common_group");

			var payload = new SamplePayload
			{
				Name = "Widget",
				Quantity = 3,
				Tags = new[] { "alpha", "beta" }
			};

			await store.Set("TestKey", "TestValue", CancellationToken.None);
			await store.Set("Sample:Payload", payload, payloadTypeInfo, CancellationToken.None);
			await store.SetDecimal("Sample:Number", decimalValue, CancellationToken.None);
			await store.SetBool("Sample:Flag", true, CancellationToken.None);
			await store.SetDateTime("Sample:Timestamp", timestamp, CancellationToken.None);
			await store.SetDateTimeOffset("Sample:TimestampOffset", timestampOffset, CancellationToken.None);
			await store.SetTimeSpan("Sample:Duration", duration, CancellationToken.None);
			await store.Set("Sample:Array", JsonSerializer.Serialize(new[] { "one", "two", "three" }, SerializerOptions), CancellationToken.None);
			await store.Set("Sample:Object", JsonSerializer.Serialize(new Dictionary<string, object?>
			{
				["Nested"] = new { Enabled = true, Count = 4 },
				["Timestamp"] = DateTimeOffset.UtcNow
			}, SerializerOptions), CancellationToken.None);

			var keys = await store.GetIds(CancellationToken.None);
			Assert.Contains("Sample:Payload", keys);
			Assert.Contains("Sample:Array", keys);
			Assert.Contains("Sample:Number", keys);

			Assert.True(await store.Contains("Sample:Array", CancellationToken.None));
			await store.Delete("Sample:Array", CancellationToken.None);
			Assert.False(await store.Contains("Sample:Array", CancellationToken.None));

			await store.CommitAsync(CancellationToken.None);
		}

		using (var verifierService = new SqliteLocalStoreService(configuration))
		{
			await verifierService.Open(CancellationToken.None);
			using var store = new ConcurrentLocalStore(verifierService, "common_group");

			var restored = await store.Get("Sample:Payload", payloadTypeInfo, CancellationToken.None);
			Assert.NotNull(restored);
			Assert.Equal("Widget", restored!.Name);
			Assert.Equal(3, restored.Quantity);
			Assert.Equal(new[] { "alpha", "beta" }, restored.Tags);

			var number = await store.GetDecimal("Sample:Number", CancellationToken.None);
			Assert.Equal(decimalValue, number!.Value);

			var flag = await store.GetBool("Sample:Flag", CancellationToken.None);
			Assert.True(flag!.Value);

			var restoredTimestamp = await store.GetDateTime("Sample:Timestamp", CancellationToken.None);
			Assert.Equal(timestamp, restoredTimestamp!.Value);

			var restoredTimestampOffset = await store.GetDateTimeOffset("Sample:TimestampOffset", CancellationToken.None);
			Assert.Equal(timestampOffset, restoredTimestampOffset!.Value);

			var restoredDuration = await store.GetTimeSpan("Sample:Duration", CancellationToken.None);
			Assert.Equal(duration, restoredDuration!.Value);

			var arrayJson = await store.Get("Sample:Array", CancellationToken.None);
			Assert.Null(arrayJson);

			var ids = await store.GetIds(CancellationToken.None);
			Assert.DoesNotContain("Sample:Array", ids);
			Assert.Contains("Sample:Object", ids);
			Assert.Contains("Sample:Timestamp", ids);

			await store.RollbackAsync(CancellationToken.None);
		}

		CleanupDatabaseFiles(dbPath);
	}

	private static IConfiguration BuildConfiguration(string dbPath)
		=> new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RUNTIME_LOCAL_STORE_DB_PATH"] = dbPath
			})
			.Build();

	private static string CreateTempDbPath()
		=> Path.Combine(Path.GetTempPath(), $"localstore-tests-{Guid.NewGuid():N}.db");

	private static void CleanupDatabaseFiles(string dbPath)
	{
		foreach (var path in EnumerateDatabaseArtifacts(dbPath))
		{
			try
			{
				if (File.Exists(path))
				{
					File.Delete(path);
				}
			}
			catch
			{
				// Best-effort cleanup; ignore failures so tests can proceed.
			}
		}
	}

	private static IEnumerable<string> EnumerateDatabaseArtifacts(string dbPath)
	{
		yield return dbPath;
		yield return $"{dbPath}-wal";
		yield return $"{dbPath}-shm";
	}

	private static void ResetGlobalInitializationState()
	{
		var field = typeof(SqliteLocalStoreService)
			.GetField("isGloballyInitialized", BindingFlags.Static | BindingFlags.NonPublic);

		field?.SetValue(null, false);
	}

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
		public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
	}
}
