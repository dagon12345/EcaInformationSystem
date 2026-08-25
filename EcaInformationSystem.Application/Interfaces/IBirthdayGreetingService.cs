namespace EcaInformationSystem.Application.Interfaces
{
    public interface IBirthdayGreetingService
    {
        // Finds every active account whose birthday (month/day) is today and
        // hasn't been greeted yet this year, posts a greeting for each, and
        // marks them greeted. Safe to call repeatedly the same day — already-
        // greeted accounts are skipped via LastBirthdayGreetedYear.
        Task CheckAndPostTodaysBirthdaysAsync();
    }
}
