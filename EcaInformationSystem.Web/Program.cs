using EcaInformationSystem.Application;
using EcaInformationSystem.Infrastructure;
using EcaInformationSystem.Web.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────
// 1️⃣  Core Blazor Server setup
// ──────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ──────────────────────────────────────────────
// 2️⃣  Add API controllers + Swagger (optional)
// ──────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo { Title = "ECA Information System API", Version = "v1" });
});

// ──────────────────────────────────────────────
// 3️⃣  Register DDD layers
//     (Application, Infrastructure, Domain)
// ──────────────────────────────────────────────
builder.Services.AddApplication();                     // e.g., MediatR, CQRS handlers
builder.Services.AddInfrastructure(builder.Configuration); // EF Core DbContext, repositories, services

// ──────────────────────────────────────────────
// 4️⃣  General services shared between UI & API
// ──────────────────────────────────────────────
//builder.Services.AddScoped<ProductService>(); // your UI service, if any
builder.Services.AddScoped<HttpClient>(sp =>
{
    var navigation = sp.GetRequiredService<NavigationManager>();
    return new HttpClient
    {
        BaseAddress = new Uri(navigation.BaseUri)
    };
});



var app = builder.Build();

// ──────────────────────────────────────────────
// 5️⃣  Configure middleware pipeline
// ──────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

// ──────────────────────────────────────────────
// 6️⃣  Map endpoints
// ──────────────────────────────────────────────
app.MapControllers(); // REST API controllers under /api/*
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();
