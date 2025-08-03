using bgdb.Common;

namespace bgdb.Web.Middlewares;

public class AdminMiddleware
{
    private readonly RequestDelegate _next;

    public AdminMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        if (context.Request.Path.Value != null && context.Request.Path.Value.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) &&
            (!context.Request.Cookies.TryGetValue("AdminSecret", out var token) || token != Settings.AdminToken))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await _next(context);
    }
}