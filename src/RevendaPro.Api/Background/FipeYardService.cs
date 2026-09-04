using Microsoft.Extensions.Options;
using RevendaPro.Application.Fipe;
using RevendaPro.Shared.Settings;

namespace RevendaPro.Api.Background
{
    /// <summary>
    /// Wakes up now and then and hands the yard to <see cref="IFipeYardRefresher"/>.
    ///
    /// It lives in the API because it is the only layer allowed to know both the application
    /// and the host. What it does is deliberately thin: wait, open one scope, run, log. Every
    /// decision about what to update belongs to the refresher, where a test can reach it.
    ///
    /// <b>One scope per run, and never one per car.</b> The reader resolves the published
    /// table once per scope and remembers each quote it fetched, so ten cars of the same model
    /// cost one call. See ADR-0005.
    ///
    /// It never throws out of the loop. A source that failed, a database that blinked, a
    /// mirror that changed shape — all of it is logged and waits for the next round. A
    /// reference table is not allowed to take the API down with it.
    /// </summary>
    public class FipeYardService(
        IServiceScopeFactory scopes,
        IOptions<FipeSettings> options,
        ILogger<FipeYardService> logger) : BackgroundService
    {
        private readonly FipeSettings settings = options.Value;

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!settings.Enabled || !settings.RefreshYard)
            {
                logger.LogInformation("FIPE yard routine is off by configuration.");

                return;
            }

            // Out of the way while the API is still applying migrations and answering its
            // first requests.
            await Task.Delay(
                TimeSpan.FromSeconds(settings.RefreshFirstRunAfterSeconds), stoppingToken)
                .ConfigureAwait(false);

            using var clock = new PeriodicTimer(TimeSpan.FromHours(settings.RefreshEveryHours));

            do
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            while (await clock.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }

        private async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = scopes.CreateScope();

                var refresher = scope.ServiceProvider.GetRequiredService<IFipeYardRefresher>();
                var run = await refresher.RefreshAsync(stoppingToken).ConfigureAwait(false);

                if (run.PublishedMonth is null)
                {
                    logger.LogWarning("FIPE yard run skipped: the table is out of reach.");

                    return;
                }

                if (run.Looked == 0)
                {
                    logger.LogInformation(
                        "FIPE yard is current with the table of {Month:yyyy-MM}.", run.PublishedMonth);

                    return;
                }

                logger.LogInformation(
                    "FIPE yard run for {Month:yyyy-MM}: {Looked} behind, {Updated} updated, "
                    + "{LeftAlone} left as typed, {Outside} outside the table, {Queries} query(ies).",
                    run.PublishedMonth, run.Looked, run.Updated, run.LeftAlone,
                    run.OutsideTheTable, run.Queries);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // The API is shutting down. Nothing to say about it.
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The FIPE yard run failed. The next round tries again.");
            }
        }
    }
}
