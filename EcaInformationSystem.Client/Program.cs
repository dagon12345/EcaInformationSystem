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
    client => client.BaseAddress = new Uri("https://localhost:5001/"))
    .AddHttpMessageHandler<AuthorizedHttpHandler>();

// Make the named client available as the default HttpClient
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>()
      .CreateClient("AuthorizedClient"));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ToastService>(); // ← THIS WAS MISSING
builder.Services.AddScoped<AuthenticationStateProvider, JwtAuthStateProvider>();
builder.Services.AddAuthorizationCore();
builder.Services.AddLocalization();
builder.Services.AddHxServices();
await builder.Build().RunAsync();