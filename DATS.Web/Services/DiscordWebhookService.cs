using DATS.Web.Interfaces;
using DATS.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DATS.Web.Services
{
    public class DiscordWebhookService : IWebhookService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DiscordWebhookService> _logger;
        private readonly HttpClient _httpClient;
        private readonly Data.ApplicationDbContext _dbContext;

        public DiscordWebhookService(
            IConfiguration configuration,
            ILogger<DiscordWebhookService> logger,
            HttpClient httpClient,
            Data.ApplicationDbContext dbContext)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClient;
            _dbContext = dbContext;
        }

        public async Task SendTicketCreatedWebhookAsync(Ticket ticket)
        {
            var webhookUrl = _configuration["Webhooks:Discord:Url"];
            if (string.IsNullOrEmpty(webhookUrl))
            {
                _logger.LogWarning("Discord webhook URL not configured. Skipping ticket created notification.");
                return;
            }

            try
            {

                var reporter = await _dbContext.Users.FindAsync(ticket.ReporterUserId);
                var reporterName = reporter?.Name ?? reporter?.Email ?? "Unknown User";


                var embed = new
                {
                    title = "🎫 New Ticket Created",
                    color = 0x7E86BF,
                    fields = new object[]
                    {
                        new { name = "Ticket ID", value = ticket.Id.ToString(), inline = true },
                        new { name = "Title", value = ticket.Title, inline = true },
                        new { name = "Status", value = ticket.Status.ToString(), inline = true },
                        new { name = "Created By", value = reporterName, inline = true },
                        new { name = "Created At", value = ticket.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"), inline = true },
                        new { name = "Description", value = TruncateDescription(ticket.Description, 1024) }
                    },
                    timestamp = DateTime.UtcNow
                };


                var payload = new
                {
                    username = "DATS Ticket System",
                    embeds = new[] { embed }
                };

                await SendWebhookAsync(webhookUrl, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending ticket created webhook notification");
            }
        }

        public async Task SendTicketStatusChangedWebhookAsync(Ticket ticket, TicketStatus oldStatus)
        {
            var webhookUrl = _configuration["Webhooks:Discord:Url"];
            if (string.IsNullOrEmpty(webhookUrl))
            {
                _logger.LogWarning("Discord webhook URL not configured. Skipping ticket status changed notification.");
                return;
            }

            try
            {

                var reporter = await _dbContext.Users.FindAsync(ticket.ReporterUserId);
                var reporterName = reporter?.Name ?? reporter?.Email ?? "Unknown User";


                int color = GetColorForStatus(ticket.Status);


                var embed = new
                {
                    title = "🔄 Ticket Status Changed",
                    color = color,
                    fields = new object[]
                    {
                        new { name = "Ticket ID", value = ticket.Id.ToString(), inline = true },
                        new { name = "Title", value = ticket.Title, inline = true },
                        new { name = "Old Status", value = oldStatus.ToString(), inline = true },
                        new { name = "New Status", value = ticket.Status.ToString(), inline = true },
                        new { name = "Created By", value = reporterName, inline = true },
                        new { name = "Updated At", value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), inline = true },
                        new { name = "Description", value = TruncateDescription(ticket.Description, 1024) }
                    },
                    timestamp = DateTime.UtcNow
                };


                var payload = new
                {
                    username = "DATS Ticket System",
                    embeds = new[] { embed }
                };

                await SendWebhookAsync(webhookUrl, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending ticket status changed webhook notification");
            }
        }

        private async Task SendWebhookAsync(string webhookUrl, object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(webhookUrl, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Discord webhook request failed with status {StatusCode}: {ErrorContent}", 
                    response.StatusCode, errorContent);
            }
        }

        private string TruncateDescription(string description, int maxLength)
        {
            if (string.IsNullOrEmpty(description))
                return "No description";

            if (description.Length <= maxLength)
                return description;

            return description.Substring(0, maxLength - 3) + "...";
        }

        private int GetColorForStatus(TicketStatus status)
        {
            return status switch
            {
                TicketStatus.Open => 0x7E86BF,
                TicketStatus.InProgress => 0xFFB74D,
                TicketStatus.Resolved => 0x00D363,
                TicketStatus.Closed => 0x4B5072,
                _ => 0x7E86BF
            };
        }
    }
}