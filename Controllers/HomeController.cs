
using Microsoft.AspNetCore.Mvc;
namespace JobMatch.Controllers
{
    // This part mostly deals with home and privacy pages.
    public class HomeController : Controller
    {
        public IActionResult Index() => View();
        public IActionResult Privacy() => View();
    }
}