namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IPublicPageViewRepository
    {
        // Finds-or-creates the row for pageKey, increments it, and returns
        // the new total in one call.
        Task<int> IncrementAndGetCountAsync(string pageKey);
        Task<int> GetCountAsync(string pageKey);
    }
}
