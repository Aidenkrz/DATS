using DATS.Web.Data;
using DATS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DATS.Web.Controllers;

public class UserRoleDto
{
    public string Role { get; set; }
    public bool IsAgentOrAdmin { get; set; }
}

[Route("api/[controller]")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AccountController(ApplicationDbContext context)
    {
        _context = context;
    }

    [Authorize]
    [HttpGet("role")]
    public async Task<ActionResult<UserRoleDto>> GetCurrentUserRole()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized("User identifier not found in token.");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.OidcSubjectId == userIdClaim);
            
        if (user == null)
        {
            return Unauthorized("User not found in the system.");
        }

        var isAgentOrAdmin = user.Role == UserRole.Agent || user.Role == UserRole.Admin;
        
        return Ok(new UserRoleDto
        {
            Role = user.Role.ToString(),
            IsAgentOrAdmin = isAgentOrAdmin
        });
    }

    [Authorize]
    [HttpGet("current")]
    public async Task<ActionResult<object>> GetCurrentUser()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized("User identifier not found in token.");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.OidcSubjectId == userIdClaim);
            
        if (user == null)
        {
            return Unauthorized("User not found in the system.");
        }

        return Ok(new
        {
            id = user.Id,
            oidcSubjectId = user.OidcSubjectId,
            email = user.Email,
            name = user.Name,
            role = user.Role.ToString()
        });
    }

    [AllowAnonymous]
    [Route("AccessDenied")]
    public IActionResult AccessDenied()
    {
        return Unauthorized("You do not have permission to access this resource. Please ensure you are logged in with the appropriate role.");
    }
}