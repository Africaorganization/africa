using Microsoft.EntityFrameworkCore;

namespace Africa.Server.Data
{
    /// <summary>
    /// Deletes applications older than Cleanup:MaxAgeHours. Runs once when the app
    /// starts and then every Cleanup:IntervalHours while it stays running.
    /// On the Free App Service plan the app sleeps when idle, so in practice the
    /// cleanup happens on the first visit after the app wakes up.
    /// </summary>
    public class ApplicationsCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApplicationsCleanupService> _logger;
        private readonly IHostApplicationLifetime _lifetime;

        public ApplicationsCleanupService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<ApplicationsCleanupService> logger,
            IHostApplicationLifetime lifetime)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
            _lifetime = lifetime;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_configuration.GetValue("Cleanup:Enabled", true))
            {
                return;
            }

            var maxAge = TimeSpan.FromHours(_configuration.GetValue("Cleanup:MaxAgeHours", 12.0));
            var interval = TimeSpan.FromHours(_configuration.GetValue("Cleanup:IntervalHours", 12.0));

            // Hosted services start before the web app runs its startup code, which
            // applies database migrations. Wait until startup has finished so the
            // schema is up to date before querying it.
            var started = new TaskCompletionSource();
            using (_lifetime.ApplicationStarted.Register(() => started.TrySetResult()))
            using (stoppingToken.Register(() => started.TrySetCanceled(stoppingToken)))
            {
                await started.Task;
            }

            using var timer = new PeriodicTimer(interval);
            do
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationsContext>();
                    var cutoff = DateTime.UtcNow - maxAge;
                    int deleted = await context.Applications
                        .Where(a => a.CreatedAt < cutoff)
                        .ExecuteDeleteAsync(stoppingToken);
                    _logger.LogInformation("Deleted {Count} applications older than {Cutoff:u}", deleted, cutoff);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Don't crash the app; try again on the next tick.
                    _logger.LogError(ex, "Failed to delete old applications");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
