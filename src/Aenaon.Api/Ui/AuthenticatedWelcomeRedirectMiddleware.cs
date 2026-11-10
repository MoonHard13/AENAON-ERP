namespace Aenaon.Api.Ui;

/// <summary>Παραπέμπει τον ήδη συνδεδεμένο χρήστη από την αρχική σελίδα στον χώρο εργασίας του.</summary>
public sealed class AuthenticatedWelcomeRedirectMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method) &&
            context.Request.Path == "/" &&
            context.User.Identity?.IsAuthenticated == true)
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Redirect("/dashboard");
            return Task.CompletedTask;
        }

        return next(context);
    }
}
