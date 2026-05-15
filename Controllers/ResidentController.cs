using Microsoft.AspNetCore.Mvc;
using CivicOps.Models;
using CivicOps.Services;
using System.Threading.Tasks;
using System.Linq;

namespace CivicOps.Controllers
{
    public class ResidentController : Controller
    {
        private readonly IResidentAuthService _residentAuthService;
        private readonly IDataService _dataService;

        public ResidentController(IResidentAuthService residentAuthService, IDataService dataService)
        {
            _residentAuthService = residentAuthService;
            _dataService = dataService;
        }

        // GET: /Resident/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Resident/Login
        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            var user = await _residentAuthService.AuthenticateAsync(email, password);
            
            if (user == null)
            {
                ViewBag.Error = "Invalid email or password";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            // Set session
            HttpContext.Session.SetString("ResidentUserId", user.Id);
            HttpContext.Session.SetString("ResidentUserEmail", user.Email);
            HttpContext.Session.SetString("ResidentUserName", user.FullName);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("MyReports");
        }

        // GET: /Resident/Signup
        [HttpGet]
        public IActionResult Signup()
        {
            return View();
        }

        // POST: /Resident/Signup
        [HttpPost]
        public async Task<IActionResult> Signup(string email, string password, string confirmPassword, string fullName, string? phoneNumber = null)
        {
            if (password != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match";
                return View();
            }

            try
            {
                var user = await _residentAuthService.CreateUserAsync(email, password, fullName, phoneNumber);
                
                // Set session
                HttpContext.Session.SetString("ResidentUserId", user.Id);
                HttpContext.Session.SetString("ResidentUserEmail", user.Email);
                HttpContext.Session.SetString("ResidentUserName", user.FullName);

                return RedirectToAction("MyReports");
            }
            catch (InvalidOperationException ex)
            {
                ViewBag.Error = ex.Message;
                return View();
            }
        }

        // GET: /Resident/Logout
        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Remove("ResidentUserId");
            HttpContext.Session.Remove("ResidentUserEmail");
            HttpContext.Session.Remove("ResidentUserName");
            return RedirectToAction("Index", "Home");
        }

        // GET: /Resident/MyReports
        [HttpGet]
        public async Task<IActionResult> MyReports()
        {
            var userId = HttpContext.Session.GetString("ResidentUserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", new { returnUrl = "/Resident/MyReports" });
            }

            var user = await _residentAuthService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var allIncidents = await _dataService.GetAllIncidentsAsync();
            var myReports = allIncidents
                .Where(i => user.SubmittedReportReferences.Contains(i.ReferenceNumber))
                .OrderByDescending(i => i.CreatedAt)
                .ToList();

            ViewBag.User = user;
            return View(myReports);
        }

        // GET: /Resident/MyAlerts
        [HttpGet]
        public async Task<IActionResult> MyAlerts()
        {
            var userId = HttpContext.Session.GetString("ResidentUserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", new { returnUrl = "/Resident/MyAlerts" });
            }

            var user = await _residentAuthService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var allAlerts = await _dataService.GetAllAlertsAsync();
            var myAlerts = allAlerts
                .Where(a => user.FollowedSuburbs.Contains(a.Suburb) || user.FollowedWards.Contains(a.Ward))
                .OrderByDescending(a => a.CreatedAt)
                .ToList();

            ViewBag.User = user;
            return View(myAlerts);
        }

        // GET: /Resident/Profile
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = HttpContext.Session.GetString("ResidentUserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", new { returnUrl = "/Resident/Profile" });
            }

            var user = await _residentAuthService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            return View(user);
        }

        // POST: /Resident/UpdateProfile
        [HttpPost]
        public async Task<IActionResult> UpdateProfile(string fullName, string? phoneNumber, bool emailNotifications, bool smsNotifications, bool alertNotifications)
        {
            var userId = HttpContext.Session.GetString("ResidentUserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login");
            }

            var user = await _residentAuthService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            user.FullName = fullName;
            user.PhoneNumber = phoneNumber;
            user.EmailNotifications = emailNotifications;
            user.SMSNotifications = smsNotifications;
            user.AlertNotifications = alertNotifications;

            await _residentAuthService.UpdateUserAsync(user);

            ViewBag.Success = "Profile updated successfully";
            return View("Profile", user);
        }

        // POST: /Resident/AddFollowedArea
        [HttpPost]
        public async Task<IActionResult> AddFollowedArea(string areaType, string areaValue)
        {
            var userId = HttpContext.Session.GetString("ResidentUserId");
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "Not logged in" });
            }

            if (areaType == "suburb")
            {
                await _residentAuthService.AddFollowedSuburbAsync(userId, areaValue);
            }
            else if (areaType == "ward")
            {
                await _residentAuthService.AddFollowedWardAsync(userId, areaValue);
            }

            return Json(new { success = true });
        }

        // POST: /Resident/RemoveFollowedArea
        [HttpPost]
        public async Task<IActionResult> RemoveFollowedArea(string areaType, string areaValue)
        {
            var userId = HttpContext.Session.GetString("ResidentUserId");
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "Not logged in" });
            }

            if (areaType == "suburb")
            {
                await _residentAuthService.RemoveFollowedSuburbAsync(userId, areaValue);
            }
            else if (areaType == "ward")
            {
                await _residentAuthService.RemoveFollowedWardAsync(userId, areaValue);
            }

            return Json(new { success = true });
        }
    }
}
