using DATS.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DATS.Web.Middleware;

public class ForceLogoutMiddleware
{
    private readonly RequestDelegate _next;

    public ForceLogoutMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            if (!string.IsNullOrEmpty(userIdClaim))
            {

                var user = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.OidcSubjectId == userIdClaim && u.ForceLogout);
                
                if (user != null)
                {

                    user.ForceLogout = false;
                    await dbContext.SaveChangesAsync();
                    

                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    

                    context.Response.Redirect("/auth/login");
                    return;
                }
            }
        }
        

        await _next(context);
    }
}


public static class ForceLogoutMiddlewareExtensions
{
    public static IApplicationBuilder UseForceLogout(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ForceLogoutMiddleware>();
    }
}