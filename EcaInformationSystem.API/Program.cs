using EcaInformationSystem.Application;
using EcaInformationSystem.Infrastructure;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Net;
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

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
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
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.CookieOrJwt, policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
    });
});

// ─── CORS ────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("WasmPolicy", policy =>
    {
        policy
            .WithOrigins(
                builder.Configuration["Cors:WasmOrigin"] ?? "https://REDACTED_INTERNAL_IP",
                "https://REDACTED_INTERNAL_IP",   // ✅ explicit HTTPS
                "http://REDACTED_INTERNAL_IP",    // ✅ keep HTTP as fallback
                "https://localhost:5002",  // ✅ local dev HTTPS
                "http://localhost:5002"    // ✅ local dev HTTP
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

// ════════════════════════════════════════════════════════════════════════════
var app = builder.Build();
// ════════════════════════════════════════════════════════════════════════════

app.UseResponseCompression();

// ─── DB Migration ────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

// ─── Middleware Pipeline ──────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ECAReS Caraga API v1");
    });
}
else
{
    app.UseHsts();
}

// ✅ Add this back
app.UseHttpsRedirection();

// ─── Global Exception Handler ────────────────────────────────────────────────
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionFeature?.Error;

        // ✅ Update to include both HTTP and HTTPS origins
        var wasmOrigin = builder.Configuration["Cors:WasmOrigin"] ?? "https://REDACTED_INTERNAL_IP";
        context.Response.Headers.Append("Access-Control-Allow-Origin", wasmOrigin);
        context.Response.ContentType = "application/json";

        // Map exception types to appropriate status codes and messages
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

        // In development, include the real exception message for debugging
        var detail = app.Environment.IsDevelopment()
            ? exception?.ToString()
            : null;

        var response = new
        {
            status = statusCode,
            message,
            detail
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    });
});

// ❌ REMOVED: app.UseHttpsRedirection()
// ❌ REMOVED: app.UseBlazorFrameworkFiles()
// ❌ REMOVED: app.MapStaticAssets()
// ❌ REMOVED: app.MapFallbackToFile(...)

app.UseCors("WasmPolicy");        // ← Must be BEFORE Auth
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();