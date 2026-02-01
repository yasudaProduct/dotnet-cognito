using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CognitoSample.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthRequiredAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var accessToken = context.HttpContext.Session.GetString("AccessToken");

        if (string.IsNullOrEmpty(accessToken))
        {
            context.HttpContext.Items["ReturnUrl"] = context.HttpContext.Request.Path;
            context.Result = new RedirectToActionResult("SignIn", "Auth", null);
        }
    }
}
