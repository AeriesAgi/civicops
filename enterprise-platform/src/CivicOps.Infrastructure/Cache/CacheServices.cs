using CivicOps.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace CivicOps.Infrastructure.Cache;

public class RedisCacheService : ICacheService, ICivicOpsCacheService
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisCacheService> _logger;
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            var value = await _db.StringGetAsync(key);
            if (value.IsNullOrEmpty) return null;
            return JsonSerializer.Deserialize<T>(value!, _json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis GET failed for key {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
        where T : class
    {
        try
        {
            var serialized = JsonSerializer.Serialize(value, _json);
            await _db.StringSetAsync(key, serialized, expiry ?? TimeSpan.FromMinutes(5));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis SET failed for key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try { await _db.KeyDeleteAsync(key); }
        catch (Exception ex) { _logger.LogError(ex, "Redis DEL failed for key {Key}", key); }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try { return await _db.KeyExistsAsync(key); }
        catch { return false; }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory,
        TimeSpan? expiry = null, CancellationToken ct = default) where T : class
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null) return cached;

        var value = await factory();
        await SetAsync(key, value, expiry, ct);
        return value;
    }
}

/// <summary>
/// High-performance live fleet cache using Redis Hashes.
/// Each vehicle position is stored as a Redis hash for O(1) field access.
/// Fleet overview (all positions) uses a sorted set per tenant.
/// </summary>
public class LiveFleetCache : ILiveFleetCache
{
    private readonly IDatabase _db;
    private readonly ILogger<LiveFleetCache> _logger;
    private const string VehicleHashPrefix = "fleet:vehicle:";
    private const string TenantFleetSetPrefix = "fleet:tenant:";
    private static readonly TimeSpan VehicleExpiry = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public LiveFleetCache(IConnectionMultiplexer redis, ILogger<LiveFleetCache> logger)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    public async Task SetVehiclePositionAsync(Guid vehicleId, VehiclePositionCacheItem position,
        CancellationToken ct = default)
    {
        try
        {
            var key = $"{VehicleHashPrefix}{vehicleId}";
            var tenantKey = $"{TenantFleetSetPrefix}{position.TenantId}";
            var serialized = JsonSerializer.Serialize(position, _json);

            var batch = _db.CreateBatch();

            // Store position as string (fast update)
            _ = batch.StringSetAsync(key, serialized, VehicleExpiry);

            // Maintain tenant fleet index (sorted by last update time)
            _ = batch.SortedSetAddAsync(tenantKey,
                vehicleId.ToString(),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            // Expire tenant set after 15 minutes of no updates
            _ = batch.KeyExpireAsync(tenantKey, TimeSpan.FromMinutes(15));

            batch.Execute();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache vehicle position {VehicleId}", vehicleId);
        }
    }

    public async Task<VehiclePositionCacheItem?> GetVehiclePositionAsync(Guid vehicleId,
        CancellationToken ct = default)
    {
        try
        {
            var key = $"{VehicleHashPrefix}{vehicleId}";
            var value = await _db.StringGetAsync(key);
            if (value.IsNullOrEmpty) return null;
            return JsonSerializer.Deserialize<VehiclePositionCacheItem>(value!, _json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get vehicle position {VehicleId}", vehicleId);
            return null;
        }
    }

    public async Task<IEnumerable<VehiclePositionCacheItem>> GetAllPositionsAsync(Guid tenantId,
        CancellationToken ct = default)
    {
        try
        {
            var tenantKey = $"{TenantFleetSetPrefix}{tenantId}";

            // Get all vehicle IDs in tenant fleet (updated in last 10 minutes)
            var minScore = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
            var vehicleIds = await _db.SortedSetRangeByScoreAsync(
                tenantKey, minScore, double.PositiveInfinity);

            if (!vehicleIds.Any()) return Enumerable.Empty<VehiclePositionCacheItem>();

            // Batch fetch all positions
            var keys = vehicleIds.Select(id => (RedisKey)$"{VehicleHashPrefix}{id}").ToArray();
            var values = await _db.StringGetAsync(keys);

            var positions = new List<VehiclePositionCacheItem>();
            foreach (var value in values)
            {
                if (!value.IsNullOrEmpty)
                {
                    try
                    {
                        var pos = JsonSerializer.Deserialize<VehiclePositionCacheItem>(value!, _json);
                        if (pos is not null) positions.Add(pos);
                    }
                    catch { /* skip malformed entries */ }
                }
            }

            return positions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get fleet positions for tenant {TenantId}", tenantId);
            return Enumerable.Empty<VehiclePositionCacheItem>();
        }
    }

    public async Task RemoveVehicleAsync(Guid vehicleId, CancellationToken ct = default)
    {
        try
        {
            var key = $"{VehicleHashPrefix}{vehicleId}";
            await _db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove vehicle {VehicleId} from cache", vehicleId);
        }
    }
}
