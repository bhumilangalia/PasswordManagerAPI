namespace PasswordManagerApi.Middleware;

/// <summary>
/// Middleware to add security headers to all responses.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Use OnStarting to set headers just before response is sent
        // This ensures headers are applied after all middleware runs but before response starts
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // Remove server header (information disclosure)
            // Note: Also requires Kestrel configuration: options.AddServerHeader = false
            headers.Remove("Server");
            headers.Remove("X-Powered-By");

            // Prevent MIME type sniffing
            headers["X-Content-Type-Options"] = "nosniff";

            // Prevent clickjacking (API doesn't need frames)
            headers["X-Frame-Options"] = "DENY";

            // XSS protection (defense in depth, JSON API shouldn't need this)
            headers["X-XSS-Protection"] = "1; mode=block";

            // Content Security Policy for API
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

            // Referrer policy
            headers["Referrer-Policy"] = "no-referrer";

            // Permissions policy (disable unnecessary features)
            headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

            return Task.CompletedTask;
        });

        await _next(context);
    }
}

/// <summary>
/// Extension method to easily add security headers middleware.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
