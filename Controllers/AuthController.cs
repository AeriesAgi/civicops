using CivicOps.Models;
using CivicOps.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace CivicOps.Controllers
{
    public class AuthController : Controller
    {
        private readonly IDemoAuthService _authService;

        public AuthController(IDemoAuthService authService)
        {
            _authService = authService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "Email and password are required");
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            var session = await _authService.LoginAsync(email, password);
            if (session == null)
            {
                ModelState.AddModelError("", "Invalid email or password");
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            // Store session ID in cookie
            HttpContext.Session.SetString("SessionId", session.SessionId);
            HttpContext.Session.SetString("UserEmail", session.Email);
            HttpContext.Session.SetString("UserRole", session.Role.ToString());

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Dashboard", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            var sessionId = HttpContext.Session.GetString("SessionId");
            if (!string.IsNullOrEmpty(sessionId))
            {
                await _authService.LogoutAsync(sessionId);
            }

            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
