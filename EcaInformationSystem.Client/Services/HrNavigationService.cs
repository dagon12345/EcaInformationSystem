namespace EcaInformationSystem.Client.Services
{
    // Where the HR & Admin app lives, and how to get a user there. One place,
    // so the Feed's profile card and the navbar links can't drift apart when the
    // HR site moves.
    public class HrNavigationService
    {
        public const string BaseUrl = "https://eca-hradmin.runasp.net";

        // Signed-out visitors (and the fallback below): HR's own sign-in page.
        public const string LoginUrl = BaseUrl + "/login";

        private readonly AuthService _authService;

        public HrNavigationService(AuthService authService) => _authService = authService;

        // For a signed-in ECA user: the HR sign-in page carrying their current ECA
        // session token, so HR offers their account instead of asking for
        // credentials a second time. The token rides in the URL FRAGMENT — never
        // sent to a server and never written to an access log (unlike
        // ?eca_token=); HR stashes it and scrubs it from the address bar on
        // arrival. With no token, or if HR has no EcaSso:SharedKey configured, HR
        // just shows its normal password form, so this degrades safely.
        public async Task<string> GetSignedInUrlAsync()
        {
            var ecaToken = await _authService.GetTokenAsync();
            return string.IsNullOrWhiteSpace(ecaToken)
                ? LoginUrl
                : $"{LoginUrl}#eca_token={Uri.EscapeDataString(ecaToken)}";
        }
    }
}
