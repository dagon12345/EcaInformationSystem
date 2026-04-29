// EcaInformationSystem.Client/Program.cs
// Blazor WebAssembly — talks to the API via HttpClient + JWT

using EcaInformationSystem.Client;
using EcaInformationSystem.Client.Services;
using Havit.Blazor.Components.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Point HttpClient at your API
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://localhost:5001") // your API URL
});


builder.Services.AddScoped<AuthService>();
// Auth — WASM uses a custom provider that reads JWT from memory
builder.Services.AddScoped<AuthenticationStateProvider,
                           JwtAuthStateProvider>();

builder.Services.AddAuthorizationCore();
builder.Services.AddHxServices();

await builder.Build().RunAsync();