using StackExchange.Redis;

namespace RureSubProfiles.Services;

public class FollowersService : IFollowersService
{
    private readonly IConnectionMultiplexer redis;

    public FollowersService(
        [FromKeyedServices("followers")] IConnectionMultiplexer redis)
    {
        this.redis = redis;
    }
    public async Task<bool?> IsFollowed(Guid followerId, Guid followingId)
    {
        var db = redis.GetDatabase();

        var userRedisId = await db.StringGetAsync($"user:id:{followerId}");
        var profileRedisId = await db.StringGetAsync($"user:id:{followingId}");

        if (userRedisId.IsNull || profileRedisId.IsNull)
        {
            return null;
        }

        var result = await db.SortedSetRankAsync($"user:{userRedisId}:followings", profileRedisId);

        return result.HasValue;
    }

    public async Task<long[]> GetUserFollowers(Guid userId, int pageSize, int page)
    {
        var redisDb = redis.GetDatabase();

        string? userRedisId = await redisDb.StringGetAsync($"user:id:{userId}");

        if (string.IsNullOrEmpty(userRedisId))
        {
            return [];
        }

        int start = (page - 1) * pageSize;
        int stop = start + pageSize - 1;

        var followingsIds = (await redisDb.SortedSetRangeByRankAsync(
            $"user:{userRedisId}:followers",
            start,
            stop,
            Order.Descending
        ))
        .Select(x =>
        {
            if (!long.TryParse(x.ToString(), out var id))
                return (long?)null;

            return id;
        })
        .Where(x => x != null)
        .Select(x => x!.Value)
        .ToArray();

        return followingsIds ?? [];
    }

    public async Task<long[]> GetUserFollowings(Guid userId, int pageSize, int page)
    {
        var redisDb = redis.GetDatabase();

        string? userRedisId = await redisDb.StringGetAsync($"user:id:{userId}");

        if (string.IsNullOrEmpty(userRedisId))
        {
            return [];
        }

        int start = (page - 1) * pageSize;
        int stop = start + pageSize - 1;

        var followingsIds = (await redisDb.SortedSetRangeByRankAsync(
            $"user:{userRedisId}:followings",
            start,
            stop,
            Order.Descending
        ))
        .Select(x =>
        {
            if (!long.TryParse(x.ToString(), out var id))
                return (long?)null;

            return id;
        })
        .Where(x => x != null)
        .Select(x => x!.Value)
        .ToArray();

        return followingsIds ?? [];
    }

    public async Task<bool[]> IsFollowed(string userId, string?[] followingIds)
    {
        var db = redis.GetDatabase();

        var tasks = new Task<long?>[followingIds.Length];

        string sortedSetKey = $"user:{userId}:followings";

        for (int i = 0; i < tasks.Length; i++)
        {
            if (string.IsNullOrEmpty(followingIds[i]))
            {
                tasks[i] = Task.FromResult<long?>(null);
            }
            var member = followingIds[i];

            tasks[i] = db.SortedSetRankAsync(sortedSetKey, member);
        }

        long?[] results = await Task.WhenAll(tasks);

        return [.. results.Select(r => r.HasValue)];
    }

    public async Task<bool[]> IsFollowed(Guid userId, Guid[] followingIds)
    {
        var db = redis.GetDatabase();

        var userRedisId = await db.StringGetAsync($"user:id:{userId}");

        if (string.IsNullOrEmpty(userRedisId))
        {
            var result = new bool[followingIds.Length];
            Array.Fill(result, false);
            return result;
        }

        string?[] followersRedisIds = await GetUserRedisIds(followingIds);
        var tasks = new Task<long?>[followingIds.Length];

        return await IsFollowed(userRedisId.ToString(), followersRedisIds);
    }

    public async Task<bool[]> IsFollowed(Guid userId, string?[] followingIds)
    {
        var db = redis.GetDatabase();

        var userRedisId = await db.StringGetAsync($"user:id:{userId}");

        if (string.IsNullOrEmpty(userRedisId))
        {
            var result = new bool[followingIds.Length];
            Array.Fill(result, false);
            return result;
        }

        return await IsFollowed(userRedisId.ToString(), followingIds);
    }

    private async Task<string?[]> GetUserRedisIds(Guid[] userIds)
    {
        var db = redis.GetDatabase();

        var tasks = new Task<RedisValue>[userIds.Length];

        for (int i = 0; i < tasks.Length; i++)
        {
            var key = $"user:id:{userIds[i]}";

            tasks[i] = db.StringGetAsync(key);
        }

        var result = await Task.WhenAll(tasks);

        return [.. result.Select(r => (string?)r)];
    }
}
