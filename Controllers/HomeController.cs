
using Microsoft.AspNetCore.Mvc;
namespace JobMatch.Controllers
{
    // This part mostly deals with {desc}.
    public class HomeController : Controller
    {
        public IActionResult Index() => View();
        public IActionResult Privacy() => View();
    }
}