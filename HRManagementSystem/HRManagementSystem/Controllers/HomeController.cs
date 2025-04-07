using HRManagementSystem.Models;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HRManagementSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly MyDbContext _context;

        public HomeController(MyDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            var userRole = Request.Cookies["UserRole"];

            if (userRole != null)
            {
                if (userRole == "Employee")
                    return RedirectToAction("Dashboard", "Employee");
                else if (userRole == "HR")
                    return RedirectToAction("Dashboard", "HR");
                else if (userRole == "Manager")
                    return RedirectToAction("Dashboard", "Manager");
            }

            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Services()
        {
            return View();
        }

        public IActionResult OurTeam()
        {
            return View();
        }

        public IActionResult Testimonials()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Contact(Feedback feedback)
        {
            if (ModelState.IsValid)
            {
                feedback.ReceivedAt = DateTime.Now;
                _context.Feedbacks.Add(feedback);
                await _context.SaveChangesAsync();

                TempData["Message"] = "Your message has been sent successfully.";

                return RedirectToAction("Contact");
            }

            return View(feedback);
        }


        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
