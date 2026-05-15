using CivicOps.Models;
using CivicOps.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Threading.Tasks;

namespace CivicOps.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class DemoAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public UserRole? RequiredRole { get; set; }

        public DemoAuthorizeAttribute()
        {
        }

        public DemoAuthorizeAttribute(UserRole requiredRole)
        {
            RequiredRole = requiredRole;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var authService = context.HttpContext.RequestServices.GetService<IDemoAuthService>();
            if (authService == null)
            {
                context.Result = new RedirectToActionResult("Login", "Auth", new { returnUrl = context.HttpContext.Request.Path });
                return;
            }

            var sessionId = context.HttpContext.Session.GetString("SessionId");
            if (string.IsNullOrEmpty(sessionId))
            {
                context.Result = new RedirectToActionResult("Login", "Auth", new { returnUrl = context.HttpContext.Request.Path });
                return;
            }

            var session = await authService.GetSessionAsync(sessionId);
            if (session == null)
            {
                context.HttpContext.Session.Clear();
                context.Result = new RedirectToActionResult("Login", "Auth", new { returnUrl = context.HttpContext.Request.Path });
                return;
            }

            // Check role if specified
            if (RequiredRole.HasValue)
            {
                var isAuthorized = await authService.IsAuthorizedAsync(sessionId, RequiredRole.Value);
                if (!isAuthorized)
                {
                    context.Result = new RedirectToActionResult("AccessDenied", "Auth", null);
                    return;
                }
            }

            // Store user info in HttpContext for easy access
            context.HttpContext.Items["UserEmail"] = session.Email;
            context.HttpContext.Items["UserRole"] = session.Role;
            context.HttpContext.Items["AssignedDepartment"] = session.AssignedDepartment;
        }
    }
}
