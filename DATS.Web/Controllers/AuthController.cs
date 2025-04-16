using DATS.Web.Data;
using DATS.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DATS.Web.Controllers;


[Route("[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AuthController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = "/dashboard")
    {



        var properties = new AuthenticationProperties { RedirectUri = returnUrl ?? "/dashboard" };
        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }


    [HttpGet("logout")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);




        return SignOut(new AuthenticationProperties { RedirectUri = "/" },
                       OpenIdConnectDefaults.AuthenticationScheme);
    }







    

    [HttpPost("refresh-claims")]
    [Authorize]
    public async Task<IActionResult> RefreshClaims()
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


        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.OidcSubjectId),
            new Claim(ClaimTypes.Name, user.Name ?? user.Email ?? "Unknown"),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);


        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return Ok(new { message = "Claims refreshed successfully", role = user.Role.ToString() });
    }
}