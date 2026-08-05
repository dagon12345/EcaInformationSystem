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
builder.Services.AddScoped<FormDocumentClientService>();
builder.Services.AddScoped<FormFolderClientService>();
builder.Services.AddScoped<StickyNoteClientService>();
builder.Services.AddScoped<PostsClientService>();
builder.Services.AddScoped<ActivityClientService>();
builder.Services.AddScoped<DocumentTrackingClientService>();
builder.Services.AddScoped<SystemUpdateClientService>();
builder.Services.AddScoped<BiometricStatusClientService>();
builder.Services.AddScoped<DarReportClientService>();
await builder.Build().RunAsync();