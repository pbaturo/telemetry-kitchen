using Microsoft.AspNetCore.Mvc;

namespace Web.Mvc.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Sensors");
    }

    public IActionResult Error()
    {
        return View();
    }
}
