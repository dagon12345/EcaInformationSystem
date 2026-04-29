// EcaInformationSystem.Api/Program.cs
// This is a brand new minimal API project — NO Blazor, NO cookies
using EcaInformationSystem.Application;
using EcaInformationSystem.Infrastructure;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Unified Swagger Registration (Remove the second call later in your file)
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ECAReS Caraga API", Version = "v1" });

    // JWT Security Definition
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
{
    {
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        },
        Array.Empty<string>()
    }
});

});
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing from configuration.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer is missing from configuration.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Jwt:Audience is missing from configuration.");


// JWT ONLY — no cookies needed, WASM client sends Bearer tokens
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
      {
          options.TokenValidationParameters = new TokenValidationParameters
          {
              ValidateIssuer = true,
              ValidateAudience = true,
              ValidateLifetime = true,
              ValidateIssuerSigningKey = true,
              ValidIssuer = jwtIssuer,
              ValidAudience = jwtAudience,
              IssuerSigningKey = new SymmetricSecurityKey(
                                             Encoding.UTF8.GetBytes(jwtKey)),
              ClockSkew = TimeSpan.Zero   // no grace period on expiry
          };
      });

builder.Services.AddAuthorization();
builder.Services.AddAuthorization(options =>
{
    // Any authenticated user — via cookie OR JWT — can hit API controllers
    options.AddPolicy(AuthPolicies.CookieOrJwt, policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
    });
});


// ─── CORS (needed when WASM runs on a different port in dev) ────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("WasmPolicy", policy =>
    {
        policy
            // During dev your WASM client will be on a different port
            .WithOrigins(
                builder.Configuration["Cors:WasmOrigin"] ?? "https://localhost:5002"
            )
            .AllowAnyMethod()
            .AllowAnyHeader();
            //.AllowCredentials();  // needed if you ever send cookies cross-origin
    });
});

// ─── Caching & Compression ──────────────────────────────────────────────────
builder.Services.AddMemoryCache();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// No cookies, no Blazor, no DataProtection needed here
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMemoryCache();

builder.Services.AddCors(options =>
{
    options.AddPolicy("WasmPolicy", policy =>
        policy.WithOrigins(builder.Configuration["Cors:WasmOrigin"]!)
              .AllowAnyMethod()
              .AllowAnyHeader());
});


// ════════════════════════════════════════════════════════════════════════════
var app = builder.Build();
// ════════════════════════════════════════════════════════════════════════════

app.UseResponseCompression();

// ─── DB Migration ───────────────────────────────────────────────────────────
//using (var scope = app.Services.CreateScope())
//{
//    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//    dbContext.Database.Migrate();
//}

// 2. Fix the Middleware Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    // Keep these INSIDE development for security, but ensure your 
    // launchSettings.json environment is explicitly set to "Development"
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        // Explicitly set the endpoint to avoid relative path 404s
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ECAReS Caraga API v1");
    });
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("WasmPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();