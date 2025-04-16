using DATS.Web.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;
using DATS.Web.Models;
using DATS.Web.Interfaces;
using DATS.Web.Services;
using DATS.Web.Middleware;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DATSConnection")
    ?? throw new InvalidOperationException("Connection string 'DATSConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();
builder.Services.AddHttpClient<IWebhookService, DiscordWebhookService>();
builder.Services.AddHttpClient(); 

builder.Services.AddHostedService<ImageCleanupService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme) 
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.Authority = builder.Configuration["Oidc:Authority"]; 
    options.ClientId = builder.Configuration["Oidc:ClientId"];
    options.ClientSecret = builder.Configuration["Oidc:ClientSecret"];
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.UsePkce = true; 
    options.SaveTokens = true; 
    options.GetClaimsFromUserInfoEndpoint = true; 

    options.TokenValidationParameters = new TokenValidationParameters
    {
        NameClaimType = "name",
    };
    
    options.ClaimActions.Clear(); 

    options.Scope.Add("openid");
    options.Scope.Add("profile"); 

    options.Events = new OpenIdConnectEvents
    {
        OnTokenValidated = async context =>
        {
            var dbContext = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var claims = context.Principal?.Claims.ToList() ?? new List<Claim>();

            logger.LogInformation("Claims present in OnTokenValidated:");
            foreach (var claim in claims) { logger.LogInformation("  Claim: {Type} = {Value}", claim.Type, claim.Value); }

            var subjectId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value; 
            var issuer = claims.FirstOrDefault(c => c.Type == "iss")?.Value ?? context.Options.Authority ?? "default-issuer";

            if (string.IsNullOrEmpty(subjectId))
            {
                logger.LogError("Subject ID (ClaimTypes.NameIdentifier) claim is missing in OnTokenValidated.");
                context.Fail("Subject ID claim is missing.");
                return;
            }
            
            var user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.OidcSubjectId == subjectId && u.OidcIssuer == issuer);

            if (user == null)
            {
                logger.LogInformation("User not found in DB, creating basic record for sub {SubjectId}", subjectId);
                user = new User
                {
                    OidcSubjectId = subjectId,
                    OidcIssuer = issuer,
                    Email = "unknown@example.com", 
                    Name = null, 
                    Role = UserRole.User, 
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync();
            }
            else
            {
                 logger.LogInformation("User found in DB for sub {SubjectId}. ID: {UserId}", subjectId, user.Id);
                 if (!user.IsActive)
                 {
                    logger.LogWarning("Inactive user {SubjectId} attempted login.", subjectId);
                    context.Fail("User account is inactive."); 
                    return;
                 }
            }

            var claimsIdentity = context.Principal?.Identity as ClaimsIdentity;
            if (claimsIdentity != null)
            {
                var existingRoleClaim = claimsIdentity.FindFirst(ClaimTypes.Role);
                if (existingRoleClaim != null) { claimsIdentity.RemoveClaim(existingRoleClaim); }
                claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));
                logger.LogInformation("Added internal role claim '{Role}' for user {SubjectId}", user.Role.ToString(), subjectId);
            }
            await Task.CompletedTask;
        },

        OnUserInformationReceived = async context =>
        {
            var dbContext = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            
            if (context.Principal?.Identity == null || !context.Principal.Identity.IsAuthenticated)
            {
                 logger.LogWarning("OnUserInformationReceived: Principal or Identity is missing or not authenticated.");
                 return;
            }

            var subjectId = context.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var issuer = context.Principal.FindFirstValue("iss") ?? context.Options.Authority ?? "default-issuer";

            if (string.IsNullOrEmpty(subjectId))
            {
                logger.LogWarning("OnUserInformationReceived: Subject ID claim is missing.");
                return;
            }

            string? nameFromUserInfo = null;
            string? emailFromUserInfo = null; 

            if (context.User.RootElement.TryGetProperty("name", out var nameElement))
            {
                nameFromUserInfo = nameElement.GetString();
            }
             if (context.User.RootElement.TryGetProperty("email", out var emailElement))
            {
                emailFromUserInfo = emailElement.GetString();
            }

            logger.LogInformation("OnUserInformationReceived for sub {SubjectId}: Name='{Name}', Email='{Email}'", 
                subjectId, nameFromUserInfo, emailFromUserInfo);

            var user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.OidcSubjectId == subjectId && u.OidcIssuer == issuer);

            if (user != null)
            {
                bool needsUpdate = false;
                if (!string.IsNullOrEmpty(nameFromUserInfo) && user.Name != nameFromUserInfo)
                {
                    logger.LogInformation("Updating user {SubjectId} Name from '{OldName}' to '{NewName}'", subjectId, user.Name, nameFromUserInfo);
                    user.Name = nameFromUserInfo;
                    needsUpdate = true;
                }
                if (!string.IsNullOrEmpty(emailFromUserInfo) && user.Email != emailFromUserInfo)
                {
                     logger.LogInformation("Updating user {SubjectId} Email from '{OldEmail}' to '{NewEmail}'", subjectId, user.Email, emailFromUserInfo);
                    user.Email = emailFromUserInfo;
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    try
                    {
                        await dbContext.SaveChangesAsync();
                        logger.LogInformation("Successfully updated user data for {SubjectId} from UserInfo.", subjectId);
                    }
                    catch (Exception ex)
                    {
                         logger.LogError(ex, "Failed to save updated user data for {SubjectId} from UserInfo.", subjectId);
                    }
                }
            }
            else
            {
                 logger.LogWarning("OnUserInformationReceived: User with sub {SubjectId} not found in DB. Should have been created in OnTokenValidated.", subjectId);
            }

            await Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy =>
        policy.RequireRole(UserRole.Admin.ToString()));

    options.AddPolicy("RequireAgentRole", policy =>
        policy.RequireRole(UserRole.Agent.ToString(), UserRole.Admin.ToString())); 

    options.AddPolicy("RequireUserRole", policy =>
        policy.RequireRole(UserRole.User.ToString(), UserRole.Agent.ToString(), UserRole.Admin.ToString())); 
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseForceLogout();

app.UseAuthorization();

app.MapControllers();

var rewriteOptions = new RewriteOptions()
    .AddRewrite("^(?!(api|auth|files|signin-oidc)/)([^./]+)$", "$2.html", skipRemainingRules: false);

app.UseRewriter(rewriteOptions);

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        const int durationInSeconds = 60 * 60 * 24; 
        ctx.Context.Response.Headers[HeaderNames.CacheControl] =
            "public,max-age=" + durationInSeconds;
    }
});

app.MapFallbackToFile("index.html");

app.Run();
