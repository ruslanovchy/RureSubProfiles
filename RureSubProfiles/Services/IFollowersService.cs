namespace RureSubProfiles.Services
{
    public interface IFollowersService
    {
        Task<bool?> IsFollowed(Guid followerId, Guid followingId);
        Task<long[]> GetUserFollowers(Guid userId, int pageSize, int page);
        Task<long[]> GetUserFollowings(Guid userId, int pageSize, int page);
        Task<bool[]> IsFollowed(Guid userId, Guid[] followingIds);
        Task<bool[]> IsFollowed(Guid userId, string?[] followingIds);
    }
}