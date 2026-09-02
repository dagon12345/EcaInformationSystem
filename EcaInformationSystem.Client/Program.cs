using EcaInformationSystem.Client;
using EcaInformationSystem.Client.Services;
using Havit.Blazor.Components.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register the handler
builder.Services.AddScoped<AuthorizedHttpHandler>();

// Register HttpClient WITH the auth handler
builder.Services.AddHttpClient("AuthorizedClient",
    client => client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://REDACTED_INTERNAL_IP:8080/"))
    .AddHttpMessageHandler<AuthorizedHttpHandler>();

// Make the named client available as the default HttpClient
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>()
      .CreateClient("AuthorizedClient"));
      
// ✅ NEW — plain client, no auth handler, for anonymous/public endpoints
builder.Services.AddHttpClient("PublicClient",
    client => client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://REDACTED_INTERNAL_IP:8080/"));

// Same auth handler as "AuthorizedClient", but a much longer timeout — for
// calls that can legitimately take minutes (large-file upload, and especially
// server-side PDF/DOCX/XLSX shrink-compression, which re-encodes every
// embedded image and can run long on shared hosting). HttpClient.Timeout can
// only be set before a client's first request, so this has to be a distinct
// named client rather than mutating "AuthorizedClient" at call time.
builder.Services.AddHttpClient("AuthorizedClientLongRunning",
    client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://REDACTED_INTERNAL_IP:8080/");
        client.Timeout = TimeSpan.FromMinutes(10);
    })
    .AddHttpMessageHandler<AuthorizedHttpHandler>();

builder.Services.AddScoped<BeneficiaryStateService>();
builder.Services.AddScoped<LiquidationStateService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ToastService>(); // ← THIS WAS MISSING
builder.Services.AddScoped<AuthenticationStateProvider, JwtAuthStateProvider>();
builder.Services.AddScoped<BeneficiaryFindingClientService>();
builder.Services.AddScoped<DocumentUploadQueueService>();
builder.Services.AddMemoryCache();
builder.Services.AddAuthorizationCore();
builder.Services.AddLocalization();
builder.Services.AddHxServices();
builder.Services.AddHxMessenger();
builder.Services.AddSingleton<ChatClientService>();
builder.Services.AddScoped<ChatStateService>();
builder.Services.AddScoped<NavbarFlyoutCoordinator>();
// Explicit factory — this service needs both the normal-timeout
// "AuthorizedClient" (fast metadata calls) and the long-timeout
// "AuthorizedClientLongRunning" (upload/shrink calls), so plain constructor
// injection of HttpClient can't disambiguate the two.
builder.Services.AddScoped(sp => new FormDocumentClientService(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("AuthorizedClient"),
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("AuthorizedClientLongRunning"),
    sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
    sp.GetRequiredService<FormFolderClientService>()));
builder.Services.AddScoped<FormFolderClientService>();
builder.Services.AddScoped<StickyNoteClientService>();
builder.Services.AddScoped<VoiceCallClientService>();
builder.Services.AddScoped<FocalInviteClientService>();
builder.Services.AddScoped<DirectoryClientService>();
builder.Services.AddScoped<VoiceCallLogClientService>();
builder.Services.AddScoped<FocalBeneficiaryClientService>();
builder.Services.AddScoped<VoiceCallStateService>();
builder.Services.AddScoped<PostsClientService>();
builder.Services.AddScoped<ActivityClientService>();
builder.Services.AddScoped<ApplicationTrackingClientService>();
builder.Services.AddScoped<DocumentTrackingClientService>();
builder.Services.AddScoped<LivenessNotificationClientService>();
builder.Services.AddScoped<SystemUpdateClientService>();
builder.Services.AddScoped<BiometricStatusClientService>();
builder.Services.AddScoped<DarReportClientService>();
builder.Services.AddScoped<SeniorCitizenDirectoryClientService>();
builder.Services.AddScoped<NcscTeamDirectoryClientService>();
builder.Services.AddScoped<TransactionTierHubClientService>();
builder.Services.AddScoped<TransactionTierClientService>();
await builder.Build().RunAsync();