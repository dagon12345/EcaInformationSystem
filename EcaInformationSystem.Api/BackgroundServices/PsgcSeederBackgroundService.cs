using EcaInformationSystem.Application.Interfaces.Services;

namespace EcaInformationSystem.Api.BackgroundServices;

/// <summary>
/// Runs the PSGC geography seeder after the web server has fully started.
///
/// Why a background service instead of running in Program.cs startup:
///   The PSGC seeder downloads ~42,000 barangays from psgc.gitlab.io on the
///   first ever deployment (when the database is empty). This takes several
///   minutes. Running it synchronously in startup code — before app.Run() —
///   blocks the process from responding to IIS health checks. IIS AspNetCore
///   Module V2 kills the process after its startupTimeLimit (default 120 s),
///   logging "Managed server didn't initialize after 120000 ms".
///
///   Moving seeding here means:
///     • The web server starts and responds immediately (IIS is satisfied).
///     • Seeding runs in the background after the first request is accepted.
///     • On all subsequent restarts the early-exit check (barangays >= 40,000)
///       returns in milliseconds — startup is effectively instant.
///     • A seeder failure is logged but never crashes the app.
/// </summary>
public sealed class PsgcSeederBackgroundService(
    IServiceProvider services,
    ILogger<PsgcSeederBackgroundService> logger,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait until the web server is fully started and accepting requests
        // before doing any seeding work. This guarantees IIS health checks
        // succeed regardless of how long seeding takes.
        await WaitForAppStartedAsync(stoppingToken);

        if (stoppingToken.IsCancellationRequested)
            return;

        logger.LogInformation("PSGC seeder background service starting.");

        try
        {
            // PsgcSeederService is registered as Scoped — create a dedicated scope.
            using var scope = services.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<IPsgcSeederService>();
            await seeder.SeedAsync(stoppingToken);
            logger.LogInformation("PSGC seeder completed successfully.");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("PSGC seeder was cancelled (app shutting down).");
        }
        catch (Exception ex)
        {
            // Log and swallow — a geography data failure must not crash the API.
            // All features that don't depend on PSGC data continue to work normally.
            logger.LogError(ex, "PSGC seeder failed. Geography lookup features may be unavailable until the next restart.");
        }
    }

    private Task WaitForAppStartedAsync(CancellationToken stoppingToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lifetime.ApplicationStarted.Register(() => tcs.TrySetResult());
        stoppingToken.Register(() => tcs.TrySetCanceled());
        return tcs.Task;
    }
}
