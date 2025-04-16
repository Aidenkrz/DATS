using DATS.Web.Data;
using DATS.Web.Interfaces;
using DATS.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCrontab;
using NCrontab.Scheduler;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DATS.Web.Services;

public class ImageCleanupService : IHostedService, IDisposable
{
    private readonly ILogger<ImageCleanupService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _schedule;
    private readonly int _retentionDays;
    private readonly Scheduler _scheduler;
    private readonly CancellationTokenSource _stoppingCts = new();
    private Task? _executingTask;

    public ImageCleanupService(ILogger<ImageCleanupService> logger, IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        _schedule = configuration["CleanupJob:Schedule"] ?? "0 2 * * *";
        if (!int.TryParse(configuration["CleanupJob:RetentionDays"], out _retentionDays))
        {
            _retentionDays = 7;
        }

        _scheduler = new Scheduler();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Image Cleanup Service starting.");
        _logger.LogInformation("Cleanup schedule: {Schedule}, Retention: {RetentionDays} days.", _schedule, _retentionDays);

        _scheduler.AddTask(CrontabSchedule.Parse(_schedule), async ct =>
        {
            _logger.LogInformation("Image Cleanup Task is running.");
            await DoWorkAsync(ct);
        });

        _executingTask = _scheduler.StartAsync(_stoppingCts.Token);


        return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
    }

    private async Task DoWorkAsync(CancellationToken cancellationToken)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ImageCleanupService>>();

            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-_retentionDays);
            logger.LogInformation("Looking for images from tickets closed before {CutoffDate}", cutoffDate);

            try
            {
                var attachmentsToDelete = await dbContext.ImageAttachments
                    .Where(a => a.Ticket != null
                                && (a.Ticket.Status == TicketStatus.Closed || a.Ticket.Status == TicketStatus.Resolved)
                                && a.Ticket.ClosedAt.HasValue
                                && a.Ticket.ClosedAt.Value < cutoffDate)
                    .ToListAsync(cancellationToken);

                if (!attachmentsToDelete.Any())
                {
                    logger.LogInformation("No images found eligible for deletion.");
                    return;
                }

                logger.LogInformation("Found {Count} images eligible for deletion.", attachmentsToDelete.Count);

                foreach (var attachment in attachmentsToDelete)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    logger.LogInformation("Deleting file: {FilePath} (Attachment ID: {AttachmentId})",
                        attachment.StoredFilePath, attachment.Id);

                    bool deleted = await fileStorageService.DeleteFileAsync(attachment.StoredFilePath);

                    if (deleted)
                    {
                        dbContext.ImageAttachments.Remove(attachment);
                        logger.LogInformation("Removed attachment record from DB: {AttachmentId}", attachment.Id);
                    }
                    else
                    {
                        logger.LogWarning("Failed to delete file {FilePath}, skipping DB removal for attachment {AttachmentId}",
                            attachment.StoredFilePath, attachment.Id);


                    }
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    logger.LogInformation("Image cleanup database changes saved.");
                }
                else
                {
                     logger.LogInformation("Image cleanup cancelled before saving database changes.");
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Image cleanup task was cancelled.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during the image cleanup task.");
            }
        }
    }


    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Image Cleanup Service stopping.");


        try
        {
            _stoppingCts.Cancel();
        }
        finally
        {

            await Task.WhenAny(_executingTask ?? Task.CompletedTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }

    public void Dispose()
    {
        _scheduler.Dispose();
        _stoppingCts.Cancel();
        _stoppingCts.Dispose();
        GC.SuppressFinalize(this);
    }
}