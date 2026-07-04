using EcaInformationSystem.Api.BackgroundServices;
using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.API.Hubs;
using EcaInformationSystem.Application;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Infrastructure;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();

// ─── Swagger ────────────────────────────────────────────────────────────────
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ECAReS Caraga API", Version = "v1" });
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

// ─── JWT ─────────────────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer is missing.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Jwt:Audience is missing.");

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = false; // ⚠️ TEMP — remove/set false once debugging is done
});
builder.Services.AddSingleton<IUserIdProvider, ChatUserIdProvider>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // ✅ ADD THIS — keeps "sub" as "sub", no silent renaming

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
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];

                // ✅ Only redirect token-from-querystring for the chat hub path —
                // every other endpoint keeps using the normal Authorization header.
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// In Program.cs — replace the AddAuthorization block
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.CookieOrJwt, policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
    });

    // ✅ SuperAdmin — user management only
    options.AddPolicy("SuperAdminOnly",
        policy => policy.RequireRole("SuperAdmin"));

    // ✅ Admin — all operational features
    options.AddPolicy("AdminOnly",
        policy => policy.RequireRole("Admin", "SuperAdmin"));

    // ✅ PDO — can create and assign ref numbers
    options.AddPolicy("AdminOrPDO",
        policy => policy.RequireRole("Admin", "PDO", "SuperAdmin"));
});
// ─── CORS ────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("WasmPolicy", policy =>
    {
        policy
            .WithOrigins(
                builder.Configuration["Cors:WasmOrigin"] ?? "https://REDACTED_INTERNAL_IP",
                "https://eca-client.runasp.net",
                "http://eca-client.runasp.net",   // ✅ add this
                "https://REDACTED_INTERNAL_IP",
                "http://REDACTED_INTERNAL_IP",
                "https://localhost:5002",
                "http://localhost:5002",
                "http://127.0.0.1:5500"   // ✅ TEMP — match Live Server's actual origin
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// ─── Caching & Compression ───────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<PsgcCacheRefreshBackgroundService>();
// ─── PSGC Background Seeder ──────────────────────────────────────────────────
// Runs geography seeding AFTER the web server has started (not during startup).
// This prevents IIS from killing the process for exceeding startupTimeLimit
// when the database is empty on first deployment.
builder.Services.AddHostedService<PsgcSeederBackgroundService>();
builder.Services.AddHostedService<PayrollQueueProcessor>(); // ✅ new

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 209_715_200;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 209_715_200;
});

// ════════════════════════════════════════════════════════════════════════════
var app = builder.Build();
// ════════════════════════════════════════════════════════════════════════════

// CORS must come before any redirect middleware so that preflight OPTIONS responses
// always carry Access-Control-Allow-Origin headers.
app.UseCors("WasmPolicy");


app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/chatHub"),
    branch => branch.UseResponseCompression()
);

// ─── DB Migration ────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
    await SuperAdminSeeder.SeedAsync(dbContext);

    // ✅ Warm the PSGC cache so the FIRST real grid request after deploy
    // isn't the one paying the cold-cache cost.
    var psgcCache = scope.ServiceProvider.GetRequiredService<IPsgcNameCache>();
    await psgcCache.RefreshAsync();
}

// ─── Middleware Pipeline ──────────────────────────────────────────────────────
// Note: PSGC geography seeding is handled by PsgcSeederBackgroundService
// which runs AFTER the server starts — see BackgroundServices/ folder.
if (app.Environment.IsDevelopment())
{
    // In development: show full exception details and expose Swagger
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ECAReS Caraga API v1");
    });
}
else
{
    // In production: JSON error handler (CORS-aware).
    // NOTE: UseHsts() is intentionally omitted — this API runs over plain HTTP on the
    // LAN (IIS port 8080).  Sending an HSTS header would instruct browsers to refuse
    // all future HTTP connections to this origin, which would permanently break the
    // app until the HSTS max-age expires.
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
            var exception = exceptionFeature?.Error;

            var wasmOrigin = builder.Configuration["Cors:WasmOrigin"] ?? "https://REDACTED_INTERNAL_IP";
            context.Response.Headers.Append("Access-Control-Allow-Origin", wasmOrigin);
            context.Response.ContentType = "application/json";

            var (statusCode, message) = exception switch
            {
                SqlException { Number: -2 } or
                Microsoft.EntityFrameworkCore.DbUpdateException { InnerException: SqlException { Number: -2 } }
                    => (StatusCodes.Status504GatewayTimeout,
                        "The request took too long to complete. Please try again or refine your search filters."),

                SqlException
                    => (StatusCodes.Status503ServiceUnavailable,
                        "A database error occurred. Please contact your system administrator."),

                UnauthorizedAccessException
                    => (StatusCodes.Status401Unauthorized,
                        "You are not authorized to perform this action."),

                KeyNotFoundException
                    => (StatusCodes.Status404NotFound,
                        "The requested record was not found."),

                OperationCanceledException
                    => (StatusCodes.Status499ClientClosedRequest,
                        "The request was cancelled."),

                _ => (StatusCodes.Status500InternalServerError,
                        "An unexpected error occurred. Please try again later.")
            };

            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { status = statusCode, message }));
        });
    });
}


// Only redirect to HTTPS in local development (Kestrel with a dev cert).
// In production the API is hosted by IIS on a plain-HTTP port (8080); issuing an
// HTTPS redirect there would send every request to a non-existent HTTPS endpoint
// and cause HTTP 307 / 500 errors for all API calls from the Blazor client.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/chatHub");

app.Run();