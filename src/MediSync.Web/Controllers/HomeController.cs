using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace MediSync.Web.Controllers;

public class HomeController : Controller
{
    [AllowAnonymous]
    public IActionResult About() => View();

    [AllowAnonymous]
    public IActionResult Error() => View();
}
