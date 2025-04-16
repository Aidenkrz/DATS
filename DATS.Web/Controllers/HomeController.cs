using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace DATS.Web.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{

    [HttpGet("/")]
    public IActionResult Index()
    {
        return PhysicalFile(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html"), "text/html");
    }


    [HttpGet("/dashboard")]
    public IActionResult Dashboard()
    {
        return PhysicalFile(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "dashboard.html"), "text/html");
    }


    [HttpGet("/agent")]
    public IActionResult Agent()
    {
        return PhysicalFile(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "agent.html"), "text/html");
    }


    [HttpGet("/admin")]
    public IActionResult Admin()
    {
        return PhysicalFile(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "admin.html"), "text/html");
    }
}