using DATS.Web.Data;
using DATS.Web.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DATS.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    public FilesController(ApplicationDbContext context, IFileStorageService fileStorageService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
    }


    [HttpGet("{attachmentId}")]
    [Authorize(Policy = "RequireUserRole")]
    public async Task<IActionResult> GetFile(Guid attachmentId)
    {

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized("User identifier not found in token.");
        }


        var currentUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.OidcSubjectId == userIdClaim);
            
        if (currentUser == null)
        {
            return Unauthorized("User not found in the system.");
        }


        bool isAgentOrAdmin = currentUser.Role == Models.UserRole.Agent || currentUser.Role == Models.UserRole.Admin;


        var attachment = await _context.ImageAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId);

        if (attachment == null || string.IsNullOrEmpty(attachment.StoredFilePath))
        {
            return NotFound("Attachment not found or path is invalid.");
        }


        var ticket = await _context.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == attachment.TicketId);
            
        if (ticket == null)
        {
            return NotFound("Associated ticket not found.");
        }
        

        if (!isAgentOrAdmin && ticket.ReporterUserId != currentUser.Id)
        {
            return Forbid("You can only access attachments for tickets you reported.");
        }

        var physicalPath = _fileStorageService.GetPhysicalPath(attachment.StoredFilePath);

        if (!System.IO.File.Exists(physicalPath))
        {

            return NotFound("File not found on storage.");
        }




        return PhysicalFile(physicalPath, attachment.ContentType, attachment.OriginalFileName);
    }
}