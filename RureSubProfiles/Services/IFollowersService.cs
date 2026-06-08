namespace RureSubProfiles.Services
{
    public interface IFollowersService
    {
        Task<bool?> IsFollowed(Guid followerId, Guid followingId);
    }
}