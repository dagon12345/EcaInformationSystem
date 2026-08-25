using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;

namespace EcaInformationSystem.Application.Services
{
    public class BirthdayGreetingService : IBirthdayGreetingService
    {
        private readonly IPendingUserRegistrationRepository _userRepo;
        private readonly IPostService _postService;

        public BirthdayGreetingService(IPendingUserRegistrationRepository userRepo, IPostService postService)
        {
            _userRepo = userRepo;
            _postService = postService;
        }

        public async Task CheckAndPostTodaysBirthdaysAsync()
        {
            var today = DateTime.Today;
            var users = await _userRepo.GetAllAsync();

            foreach (var user in users)
            {
                // Default(DateTime) means "never set" for this non-nullable column —
                // only greet active, approved accounts, same eligibility as the
                // "upcoming birthdays" widget (UserProfileService.GetUpcomingBirthdaysAsync).
                if (user.BirthDate == default || !user.IsActivated || user.ApprovalStatus != 1 || user.IsDeactivated)
                    continue;

                if (user.BirthDate.Month != today.Month || user.BirthDate.Day != today.Day)
                    continue;

                if (user.LastBirthdayGreetedYear == today.Year)
                    continue; // already posted this year

                var turningAge = today.Year - user.BirthDate.Year;
                await _postService.CreateBirthdayGreetingPostAsync(user.Id, user.FullName, turningAge);

                user.LastBirthdayGreetedYear = today.Year;
            }

            await _userRepo.SaveChangesAsync();
        }
    }
}
