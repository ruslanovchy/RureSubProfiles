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
}
