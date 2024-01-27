using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public class HomeController : ControllerBase
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        return Redirect("/swagger");
    }
}
