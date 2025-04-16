using DATS.Web.Data;
using DATS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO; 
using System.Security.Claims;
using DATS.Web.Interfaces; 
using Microsoft.AspNetCore.Http; 
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; 

namespace DATS.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] 
public class TicketsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService; 
    private readonly ILogger<TicketsController> _logger; 
    private readonly IWebhookService _webhookService; 

    public TicketsController(
        ApplicationDbContext context, 
        IFileStorageService fileStorageService, 
        ILogger<TicketsController> logger,
        IWebhookService webhookService) 
    {
        _context = context;
        _fileStorageService = fileStorageService; 
        _logger = logger; 
        _webhookService = webhookService; 
    }


    
    public class UpdateTicketStatusDto
    {
        public string Status { get; set; }
    }
    
    public class TicketCreateDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class TicketViewDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty; 
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public DateTimeOffset? ClosedAt { get; set; }
        public Guid ReporterUserId { get; set; }
        public string? ReporterName { get; set; }
        public string? ReporterEmail { get; set; } 
        public Guid? AssigneeUserId { get; set; }
        public string? AssigneeName { get; set; }
        public string? AssigneeEmail { get; set; } 
        public List<ImageAttachmentDto> ImageAttachments { get; set; } = new(); 
    }

    public class ImageAttachmentDto
    {
        public Guid Id { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTimeOffset UploadedAt { get; set; }
        public string Url { get; set; } = string.Empty; 
    }

    public class TicketReplyCreateDto
    {
        public string Content { get; set; } = string.Empty;
        public bool IsInternal { get; set; } = false;
    }

    public class TicketReplyViewDto
    {
        public Guid Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public Guid AuthorUserId { get; set; }
        public string? AuthorName { get; set; }
        public string? AuthorEmail { get; set; }
        public bool IsInternal { get; set; }
    }





    [HttpPost]
    [Authorize(Policy = "RequireUserRole")] 
    public async Task<ActionResult<TicketViewDto>> CreateTicket([FromBody] TicketCreateDto ticketDto)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? User.FindFirstValue("sub")
                       ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        
        var issuerClaim = User.FindFirstValue("iss")
                       ?? User.FindFirstValue("issuer")
                       ?? "default-issuer"; 

        if (string.IsNullOrEmpty(userIdClaim))
        {
            _logger.LogError("User identifier not found in token. Available claims: {Claims}",
                string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
            return Unauthorized("User identifier not found in token.");
        }

        var reporterUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.OidcSubjectId == userIdClaim);

        if (reporterUser == null)
        {
            var allUsers = await _context.Users.Select(u => u.OidcSubjectId).ToListAsync();
            _logger.LogWarning("User not found. Subject ID: {SubjectId}, Available users: {Users}",
                userIdClaim, string.Join(", ", allUsers));
            return Unauthorized("User not found in the system.");
        }

        var ticket = new Ticket
        {
            Title = ticketDto.Title,
            Description = ticketDto.Description,
            Status = TicketStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow,
            ReporterUserId = reporterUser.Id,
        };

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();


        var createdTicketDto = MapToTicketViewDto(ticket, reporterUser, null); 
        


        await _webhookService.SendTicketCreatedWebhookAsync(ticket); 

        return CreatedAtAction(nameof(GetTicketById), new { id = ticket.Id }, createdTicketDto);
    }


    [HttpGet]
    [Authorize(Policy = "RequireAgentRole")] 
    public async Task<ActionResult<IEnumerable<TicketViewDto>>> GetTickets()
    {
        var tickets = await _context.Tickets
            .Include(t => t.ReporterUser) 
            .Include(t => t.AssigneeUser)
            .Include(t => t.ImageAttachments) 
            .OrderByDescending(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync();


        var ticketDtos = tickets.Select(t => MapToTicketViewDto(t, t.ReporterUser, t.AssigneeUser)); 

        return Ok(ticketDtos);
    }
    

    [HttpGet("my")]
    [Authorize(Policy = "RequireUserRole")] 
    public async Task<ActionResult<IEnumerable<TicketViewDto>>> GetMyTickets()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.OidcSubjectId == userIdClaim);
            
        if (user == null)
        {
            return Unauthorized("User not found in the system.");
        }
        
        var tickets = await _context.Tickets
            .Include(t => t.ReporterUser)
            .Include(t => t.AssigneeUser)
            .Include(t => t.ImageAttachments)
            .Where(t => t.ReporterUserId == user.Id)
            .OrderByDescending(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
            

        var ticketDtos = tickets.Select(t => MapToTicketViewDto(t, t.ReporterUser, t.AssigneeUser)); 
        
        return Ok(ticketDtos);
    }


    [HttpPost("{ticketId}/attachments")]
    [Authorize(Policy = "RequireUserRole")] 
    public async Task<ActionResult<ImageAttachmentDto>> UploadAttachment(Guid ticketId, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        var ticket = await _context.Tickets.FindAsync(ticketId);
        if (ticket == null)
        {
            return NotFound($"Ticket with ID {ticketId} not found.");
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userRole = User.FindFirstValue(ClaimTypes.Role);

        var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.OidcSubjectId == userIdClaim);
        if (currentUser == null) return Unauthorized(); 

        bool isAgentOrAdmin = userRole == UserRole.Agent.ToString() || userRole == UserRole.Admin.ToString();
        if (ticket.ReporterUserId != currentUser.Id && !isAgentOrAdmin)
        {
            return Forbid(); 
        }

        var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/gif" }; 
        if (!allowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            return BadRequest($"Invalid file type. Allowed types: {string.Join(", ", allowedContentTypes)}");
        }
        long maxFileSize = 10 * 1024 * 1024;
        if (file.Length > maxFileSize)
        {
            return BadRequest($"File size exceeds the limit of {maxFileSize / 1024 / 1024} MB.");
        }

        var attachment = new ImageAttachment
        {
            TicketId = ticketId,
            OriginalFileName = Path.GetFileName(file.FileName), 
            ContentType = file.ContentType,
            FileSize = file.Length,
            UploadedAt = DateTimeOffset.UtcNow
        };

        try
        {
            var storedPath = await _fileStorageService.SaveFileAsync(file, ticketId, attachment.Id);
            attachment.StoredFilePath = storedPath; 

            _context.ImageAttachments.Add(attachment);
            await _context.SaveChangesAsync();

            var attachmentDto = MapToImageAttachmentDto(attachment);

            return CreatedAtAction(nameof(GetAttachmentById), 
                                   new { ticketId = ticketId, attachmentId = attachment.Id },
                                   attachmentDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading attachment for ticket {TicketId}", ticketId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while uploading the file.");
        }
    }


    [HttpGet("{ticketId}/attachments/{attachmentId}")]
    [Authorize(Policy = "RequireUserRole")]
    public async Task<ActionResult<ImageAttachmentDto>> GetAttachmentById(Guid ticketId, Guid attachmentId)
    {
        var attachment = await _context.ImageAttachments
            .Include(a => a.Ticket)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.TicketId == ticketId && a.Id == attachmentId);

        if (attachment == null)
        {
            return NotFound();
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAgentOrAdmin = User.IsInRole(UserRole.Agent.ToString()) || User.IsInRole(UserRole.Admin.ToString());
        
        if (!isAgentOrAdmin)
        {
            var reporterUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.OidcSubjectId == userIdClaim);
                
            if (reporterUser == null || attachment.Ticket.ReporterUserId != reporterUser.Id)
            {
                return Forbid("You can only view attachments for tickets you reported.");
            }
        }

        return Ok(MapToImageAttachmentDto(attachment));
    }



    [HttpGet("{id}")]
    [Authorize(Policy = "RequireUserRole")] 
    public async Task<ActionResult<TicketViewDto>> GetTicketById(Guid id)
    {
        var ticket = await _context.Tickets
            .Include(t => t.ReporterUser) 
            .Include(t => t.AssigneeUser)
            .Include(t => t.ImageAttachments) 
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null)
        {
            return NotFound();
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAgentOrAdmin = User.IsInRole(UserRole.Agent.ToString()) || User.IsInRole(UserRole.Admin.ToString());
        
        if (!isAgentOrAdmin)
        {
            var reporterUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.OidcSubjectId == userIdClaim);
                
            if (reporterUser == null || ticket.ReporterUserId != reporterUser.Id)
            {
                return Forbid("You can only view tickets you reported.");
            }
        }


        var ticketDto = MapToTicketViewDto(ticket, ticket.ReporterUser, ticket.AssigneeUser); 
        return Ok(ticketDto);
    }



    

    [HttpPost("{ticketId}/replies")]
    [Authorize(Policy = "RequireUserRole")] 
    public async Task<ActionResult<TicketReplyViewDto>> CreateReply(Guid ticketId, [FromBody] TicketReplyCreateDto replyDto)
    {
        var ticket = await _context.Tickets
            .Include(t => t.ReporterUser)
            .FirstOrDefaultAsync(t => t.Id == ticketId);
            
        if (ticket == null)
        {
            return NotFound("Ticket not found.");
        }
        
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.OidcSubjectId == userIdClaim);
            
        if (user == null)
        {
            return Unauthorized("User not found in the system.");
        }
        
        var isAgentOrAdmin = User.IsInRole(UserRole.Agent.ToString()) || User.IsInRole(UserRole.Admin.ToString());
        
        if (replyDto.IsInternal && !isAgentOrAdmin)
        {
            return Forbid("Only agents or admins can create internal notes.");
        }
        
        if (!isAgentOrAdmin && ticket.ReporterUserId != user.Id)
        {
            return Forbid("You can only reply to your own tickets.");
        }
        
        var reply = new TicketReply
        {
            Id = Guid.NewGuid(),
            Content = replyDto.Content,
            CreatedAt = DateTimeOffset.UtcNow,
            TicketId = ticketId,
            AuthorUserId = user.Id,
            IsInternal = replyDto.IsInternal
        };
        
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        
        _context.TicketReplies.Add(reply);
        await _context.SaveChangesAsync();
        

        var replyViewDto = MapToTicketReplyViewDto(reply, user); 
        
        return CreatedAtAction(nameof(GetRepliesByTicketId), new { ticketId = ticketId }, replyViewDto);
    }
    

    [HttpGet("{ticketId}/replies")]
    [Authorize(Policy = "RequireUserRole")] 
    public async Task<ActionResult<IEnumerable<TicketReplyViewDto>>> GetRepliesByTicketId(Guid ticketId)
    {
        var ticket = await _context.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == ticketId);
            
        if (ticket == null)
        {
            return NotFound("Ticket not found.");
        }
        
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.OidcSubjectId == userIdClaim);
            
        if (user == null)
        {
            return Unauthorized("User not found in the system.");
        }
        
        var isAgentOrAdmin = User.IsInRole(UserRole.Agent.ToString()) || User.IsInRole(UserRole.Admin.ToString());
        
        if (!isAgentOrAdmin && ticket.ReporterUserId != user.Id)
        {
            return Forbid("You can only view replies to your own tickets.");
        }
        
        var replies = await _context.TicketReplies
            .Include(r => r.AuthorUser)
            .Where(r => r.TicketId == ticketId)
            .Where(r => isAgentOrAdmin || !r.IsInternal)
            .OrderBy(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
            

        var replyDtos = replies.Select(r => MapToTicketReplyViewDto(r, r.AuthorUser)); 
        
        return Ok(replyDtos);
    }
    

    [HttpPut("{ticketId}/status")]
    [Authorize(Policy = "RequireAgentRole")] 
    public async Task<IActionResult> UpdateTicketStatus(Guid ticketId, [FromBody] UpdateTicketStatusDto statusDto)
    {

        var ticket = await _context.Tickets
            .Include(t => t.ReporterUser) 
            .FirstOrDefaultAsync(t => t.Id == ticketId);
        
        if (ticket == null)
        {
            return NotFound($"Ticket with ID {ticketId} not found.");
        }
        
        if (!Enum.TryParse<TicketStatus>(statusDto.Status, out var newStatus))
        {
            return BadRequest($"Invalid status: {statusDto.Status}");
        }
        
        var oldStatus = ticket.Status;
        
        if (oldStatus != newStatus)
        {
            ticket.Status = newStatus;
            ticket.UpdatedAt = DateTimeOffset.UtcNow;
            
            if (newStatus == TicketStatus.Closed)
            {
                ticket.ClosedAt = DateTimeOffset.UtcNow;
            } else {
                ticket.ClosedAt = null;
            }
            
            await _context.SaveChangesAsync();
            

            await _webhookService.SendTicketStatusChangedWebhookAsync(ticket, oldStatus);
        }
        
        return Ok(new { message = $"Ticket status updated to {newStatus}" });
    }
    



    private static TicketViewDto MapToTicketViewDto(Ticket ticket, User? reporterUser, User? assigneeUser) 
    {
        return new TicketViewDto
        {
            Id = ticket.Id,
            Title = ticket.Title,
            Description = ticket.Description,
            Status = ticket.Status.ToString(), 
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            ClosedAt = ticket.ClosedAt,
            ReporterUserId = ticket.ReporterUserId,
            ReporterName = reporterUser?.Name,
            ReporterEmail = reporterUser?.Email,
            AssigneeUserId = ticket.AssigneeUserId,
            AssigneeName = assigneeUser?.Name,
            AssigneeEmail = assigneeUser?.Email,
            ImageAttachments = ticket.ImageAttachments?.Select(MapToImageAttachmentDto).ToList() ?? new List<ImageAttachmentDto>(),
        };
    }

    private static ImageAttachmentDto MapToImageAttachmentDto(ImageAttachment attachment)
    {
        return new ImageAttachmentDto
        {
            Id = attachment.Id,
            OriginalFileName = attachment.OriginalFileName,
            ContentType = attachment.ContentType,
            FileSize = attachment.FileSize,
            UploadedAt = attachment.UploadedAt,
            Url = $"/api/files/{attachment.Id}" 
        };
    }


    private static TicketReplyViewDto MapToTicketReplyViewDto(TicketReply reply, User? authorUser)
    {
         return new TicketReplyViewDto
        {
            Id = reply.Id,
            Content = reply.Content,
            CreatedAt = reply.CreatedAt,
            AuthorUserId = reply.AuthorUserId,
            AuthorName = authorUser?.Name,
            AuthorEmail = authorUser?.Email,
            IsInternal = reply.IsInternal
        };
    }
}