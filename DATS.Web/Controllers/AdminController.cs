using DATS.Web.Data;
using DATS.Web.Models;
using DATS.Web.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DATS.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireAdminRole")] 
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IWebhookService _webhookService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        ApplicationDbContext context,
        IConfiguration configuration,
        IWebhookService webhookService,
        ILogger<AdminController> logger)
    {
        _context = context;
        _configuration = configuration;
        _webhookService = webhookService;
        _logger = logger;
    }


    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.Users
            .Select(u => new 
            {
                u.Id,
                u.Email,
                u.Name,
                u.Role,
                u.IsActive,
                u.CreatedAt,
                u.OidcSubjectId, 
                u.OidcIssuer     
            })
            .ToListAsync();

        return Ok(users);
    }

    public class UpdateUserRoleRequest
    {
        public UserRole NewRole { get; set; }
    }


    [HttpPut("users/{userId}/role")]
    public async Task<IActionResult> UpdateUserRole(Guid userId, [FromBody] UpdateUserRoleRequest request)
    {
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
        {
            return NotFound($"User with ID {userId} not found.");
        }

        if (user.Role != request.NewRole)
        {
            user.Role = request.NewRole;
            user.ForceLogout = true;
        }
        _context.Entry(user).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("The user record was modified by another process. Please reload and try again.");
        }

        return NoContent(); 
    }
    

    [HttpPost("users/{userId}/force-logout")]
    public async Task<IActionResult> ForceUserLogout(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        
        if (user == null)
        {
            return NotFound($"User with ID {userId} not found.");
        }
        
        user.ForceLogout = true;
        _context.Entry(user).State = EntityState.Modified;
        
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("The user record was modified by another process. Please reload and try again.");
        }
        
        return Ok(new { message = $"User {user.Email} will be logged out on their next request." });
    }


    public class UpdateUserStatusRequest
    {
        public bool IsActive { get; set; }
    }


    [HttpPut("users/{userId}/status")]
    public async Task<IActionResult> UpdateUserStatus(Guid userId, [FromBody] UpdateUserStatusRequest request)
    {
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
        {
            return NotFound($"User with ID {userId} not found.");
        }



        if (user.IsActive != request.IsActive)
        {
            user.IsActive = request.IsActive;

            if (!user.IsActive) 
            {
                user.ForceLogout = true;
            }
            _context.Entry(user).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("The user record was modified by another process. Please reload and try again.");
            }
        }

        return Ok(new { message = $"User status updated successfully. IsActive: {user.IsActive}" });
    }



    public class WebhookSettingsDto
    {
        public string Url { get; set; }
        public bool Enabled { get; set; }
    }

    [HttpGet("settings/webhook")]
    public IActionResult GetWebhookSettings()
    {
        var url = _configuration["Webhooks:Discord:Url"] ?? "";
        var enabled = bool.TryParse(_configuration["Webhooks:Discord:Enabled"], out var isEnabled) ? isEnabled : true;

        return Ok(new WebhookSettingsDto
        {
            Url = url,
            Enabled = enabled
        });
    }

    [HttpPost("settings/webhook")]
    public async Task<IActionResult> SaveWebhookSettings([FromBody] WebhookSettingsDto settings)
    {
        try
        {
            var appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            var json = await System.IO.File.ReadAllTextAsync(appSettingsPath);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var updatedJson = new System.Text.Json.Nodes.JsonObject();
            
            foreach (var property in root.EnumerateObject())
            {
                updatedJson[property.Name] = System.Text.Json.Nodes.JsonNode.Parse(property.Value.GetRawText());
            }
            
            if (!updatedJson.ContainsKey("Webhooks"))
            {
                updatedJson["Webhooks"] = new System.Text.Json.Nodes.JsonObject();
            }
            
            var webhooks = updatedJson["Webhooks"].AsObject();
            if (!webhooks.ContainsKey("Discord"))
            {
                webhooks["Discord"] = new System.Text.Json.Nodes.JsonObject();
            }
            
            var discord = webhooks["Discord"].AsObject();
            discord["Url"] = settings.Url;
            discord["Enabled"] = settings.Enabled;
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            await System.IO.File.WriteAllTextAsync(appSettingsPath, updatedJson.ToJsonString(options));
            
            return Ok(new { message = "Webhook settings saved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving webhook settings");
            return StatusCode(500, new { message = "Error saving webhook settings", error = ex.Message });
        }
    }

    [HttpPost("settings/webhook/test")]
    public async Task<IActionResult> TestWebhook()
    {
        try
        {
            var testTicket = new Ticket
            {
                Id = Guid.NewGuid(),
                Title = "Test Webhook Notification",
                Description = "This is a test notification sent from the admin panel.",
                Status = TicketStatus.Open,
                CreatedAt = DateTimeOffset.UtcNow,
                ReporterUserId = Guid.Empty 
            };

            await _webhookService.SendTicketCreatedWebhookAsync(testTicket);

            return Ok(new { message = "Test webhook notification sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test webhook notification");
            return StatusCode(500, new { message = "Error sending test webhook notification", error = ex.Message });
        }
    }
}