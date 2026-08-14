using EcaInformationSystem.Api.BackgroundServices;
using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.API.Hubs;
using EcaInformationSystem.Application;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Infrastructure;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Lets this same Api project run as a proper Windows Service (see
// LocalSyncService/windows) - integrates with the Service Control Manager
// so it correctly reports "started"/"stopped" and handles stop signals.
// No effect when run normally (dotnet run/watch, or the real Linux
// deployment) - only activates when actually launched as a Windows Service.
builder.Services.AddWindowsService();

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();

// ─── Swagger ────────────────────────────────────────────────────────────────
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ECAReS Caraga API", Version = "v1" });
    c.MapType<IFormFile>(() => new OpenApiSchema { Type = "string", Format = "binary" }); // ✅ NEW
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
builder.Services.AddSingleton<ChatPresenceTracker>();
builder.Services.AddSingleton<EcaInformationSystem.Api.Hubs.VoiceCallTracker>();

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

                // ✅ Only redirect token-from-querystring for hub paths that need it —
                // every other endpoint keeps using the normal Authorization header.
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/chatHub") || path.StartsWithSegments("/biometricStatusHub") || path.StartsWithSegments("/voiceCallHub")))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },

            // ✅ NEW — active-session revocation check. Tokens are otherwise fully
            // stateless, so this is what lets a user log a device out remotely before
            // its natural 8h expiry. Cached for a short window so the common case
            // (non-revoked token) doesn't cost a DB round-trip on every request.
            OnTokenValidated = async context =>
            {
                var jti = context.Principal?.FindFirst(
                    System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;

                if (string.IsNullOrEmpty(jti))
                {
                    context.Fail("Invalid token.");
                    return;
                }

                var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
                var cacheKey = $"revoked-session:{jti}";

                if (!cache.TryGetValue(cacheKey, out bool isRevoked))
                {
                    var sessionRepository = context.HttpContext.RequestServices
                        .GetRequiredService<EcaInformationSystem.Application.Interfaces.Repositories.IUserSessionRepository>();
                    var session = await sessionRepository.GetByJtiAsync(jti);
                    isRevoked = session == null || session.RevokedAt != null;

                    cache.Set(cacheKey, isRevoked, TimeSpan.FromSeconds(30));
                }

                if (isRevoked)
                {
                    context.Fail("Session has been revoked.");
                }
            }
        };
    });

// In Program.cs — replace the AddAuthorization block
builder.Services.AddAuthorization(options =>
{
    // 🔒 "Focal" is a deliberately call-only role (external partner-LGU contacts,
    // onboarded via PDO-invite — see FocalInviteService) that must NEVER reach
    // beneficiary/financial/document data. Most controllers in this API use a
    // bare [Authorize] (→ the default policy) or [Authorize(Policy = CookieOrJwt)],
    // neither of which used to check role — so instead of auditing every one of
    // those controllers one by one, both are hardened here to exclude Focal by
    // default. Endpoints Focals DO need (voice calling, the provincial directory)
    // opt back in explicitly via the AnyAuthenticatedIncludingFocal policy below.
    bool NotFocal(Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext ctx) => !ctx.User.IsInRole("Focal");

    options.AddPolicy(AuthPolicies.CookieOrJwt, policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(NotFocal);
    });

    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireAssertion(NotFocal)
        .Build();

    // ✅ Explicit opt-in for the small set of endpoints Focal accounts are
    // allowed to reach — everyone authenticated, including Focal.
    options.AddPolicy("AnyAuthenticatedIncludingFocal",
        policy => policy.RequireAuthenticatedUser());

    // ✅ SuperAdmin — user management only
    options.AddPolicy("SuperAdminOnly",
        policy => policy.RequireRole("SuperAdmin"));

    // ✅ Finance — same user-management privileges as SuperAdmin (approve/reject/
    // assign roles/jurisdictions, deactivate or delete accounts)
    options.AddPolicy("SuperAdminOrFinance",
        policy => policy.RequireRole("SuperAdmin", "Finance"));

    // ✅ Admin — all operational features
    options.AddPolicy("AdminOnly",
        policy => policy.RequireRole("Admin", "SuperAdmin"));

    // ✅ PDO — can create and assign ref numbers
    options.AddPolicy("AdminOrPDO",
        policy => policy.RequireRole("Admin", "PDO", "SuperAdmin"));

    // ✅ Encoder — narrow, office-only role: can Create and Edit a grantee
    // record (View is already open to everyone via the default policy) and
    // nothing else. Deliberately its OWN policy rather than added to
    // "AdminOrPDO" above, since that policy is reused by bulk ops, imports,
    // COE generation, document uploads, and focal invites — none of which
    // Encoder should reach. Only apply this policy to Beneficiary
    // Create/Update endpoints.
    options.AddPolicy("GranteeEncodeAccess",
        policy => policy.RequireRole("Admin", "PDO", "SuperAdmin", "Encoder"));

    // ✅ Viewer — "view and upload only" role, plus Admins, can upload Forms Gateway documents
    options.AddPolicy("AdminOrViewer",
        policy => policy.RequireRole("Admin", "Viewer", "SuperAdmin"));

    // ✅ Call Logs — SuperAdmin/Admin see every call, PDO/Focal see only their
    // own (enforced again in VoiceCallLogService, not just here). Deliberately
    // excludes Finance/Viewer.
    options.AddPolicy("CallLogViewers",
        policy => policy.RequireRole("SuperAdmin", "Admin", "PDO", "Focal"));

    // ✅ Focal's read-only, single-municipality grantee view — kept exclusive
    // to Focal (not staff too) so this narrower, less-audited surface can't
    // become an accidental bypass path for anyone else.
    options.AddPolicy("FocalOnly",
        policy => policy.RequireRole("Focal"));
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

// ─── Rate Limiting (brute-force protection on auth endpoints) ───────────────
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";

        int retryAfterSeconds = 60;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
        }

        await context.HttpContext.Response.WriteAsync(
            JsonSerializer.Serialize(new
            {
                status = 429,
                message = "Too many login attempts.",
                retryAfterSeconds
            }),
            token);
    };
});

// ─── Caching & Compression ───────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// ─── Data Protection (used to encrypt MFA secrets at rest) ──────────────────
// Keys stored in the database instead of the filesystem — survives MSDeploy
// republishes the same way our EF migrations already do.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .SetApplicationName("EcaInformationSystem");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Overrides Infrastructure's no-op ITransactionTierBroadcaster registration
// (last registration wins) with the real SignalR one — must be registered
// AFTER AddInfrastructure().
builder.Services.AddScoped<EcaInformationSystem.Application.Interfaces.ITransactionTierBroadcaster,
    EcaInformationSystem.Api.Services.TransactionTierHubBroadcaster>();

// ─── Sync-only mode ───────────────────────────────────────────────────────────
// For running this exact same API project locally on a machine that's on the
// biometric device's network (since there's no port-forward to it and no
// dedicated always-on local server) — set SyncOnlyMode=true (see
// appsettings.LocalSync.json / ASPNETCORE_ENVIRONMENT=LocalSync) so ONLY
// ZkDirectPollingService runs. Without this guard, running the full API
// locally against the production connection string would also fire off
// payroll processing, activity reminder emails, and PSGC seeding against
// real production data — none of which should ever run from an ad-hoc
// machine someone happens to be using at the office.
var syncOnlyMode = builder.Configuration.GetValue<bool>("SyncOnlyMode");

if (!syncOnlyMode)
{
    builder.Services.AddHostedService<PsgcCacheRefreshBackgroundService>();
    // ─── PSGC Background Seeder ──────────────────────────────────────────────
    // Runs geography seeding AFTER the web server has started (not during startup).
    // This prevents IIS from killing the process for exceeding startupTimeLimit
    // when the database is empty on first deployment.
    builder.Services.AddHostedService<PsgcSeederBackgroundService>();
    builder.Services.AddHostedService<PayrollQueueProcessor>();

    builder.Services.AddSingleton<ActivityReminderScheduler>();
    builder.Services.AddHostedService<ActivityReminderResyncService>();

    // ✅ Weekly transaction-leaderboard reset — every Sunday 11:59 PM
    // Philippine Time, see LeaderboardWeeklyResetBackgroundService.
    builder.Services.AddHostedService<LeaderboardWeeklyResetBackgroundService>();
}

builder.Services.Configure<EcaInformationSystem.Api.ZkDevice.ZkDirectOptions>(
    builder.Configuration.GetSection(EcaInformationSystem.Api.ZkDevice.ZkDirectOptions.SectionName));
builder.Services.AddSingleton<EcaInformationSystem.Api.ZkDevice.ZkSyncRunner>();
builder.Services.AddHostedService<EcaInformationSystem.Api.BackgroundServices.ZkDirectPollingService>();

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

// The Client's wwwroot/web.config sets Cross-Origin-Embedder-Policy: require-corp
// in production, which makes the browser block EVERY cross-origin response —
// SignalR hub negotiate/WebSocket-upgrade responses included — unless that
// response carries Cross-Origin-Resource-Policy: cross-origin. A couple of
// controller actions (avatar images) already set this by hand; doing it here
// for all responses covers the hubs too instead of requiring the same header
// on every action/hub individually. CORS above already restricts who can
// actually read the response data, so this doesn't loosen access — it only
// tells the browser embedding itself is allowed.
app.Use(async (context, next) =>
{
    context.Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
    await next();
});

app.UseRateLimiter(); //Must come after CORS, before MapControllers

app.MapHub<PostsHub>("/postsHub");
app.MapHub<ActivityHub>("/activityHub");
app.MapHub<PublicActivityHub>("/publicActivityHub");
app.MapHub<DocumentTrackingHub>("/documentTrackingHub");
app.MapHub<SystemUpdateHub>("/systemUpdateHub");
app.MapHub<EcaInformationSystem.Api.Hubs.BiometricStatusHub>("/biometricStatusHub");
app.MapHub<EcaInformationSystem.Api.Hubs.SeniorCitizenDirectoryHub>("/seniorCitizenDirectoryHub");
app.MapHub<EcaInformationSystem.Api.Hubs.TransactionTierHub>("/transactionTierHub");

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
app.MapHub<EcaInformationSystem.Api.Hubs.VoiceCallHub>("/voiceCallHub");

app.Run();