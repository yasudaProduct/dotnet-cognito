using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CognitoMfaSample.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthRequiredAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var isAuthenticated = context.HttpContext.User.Identity?.IsAuthenticated ?? false;
        if (!isAuthenticated)
        {
            context.Result = new RedirectToActionResult("SignIn", "Auth", null);
        }
    }
}
