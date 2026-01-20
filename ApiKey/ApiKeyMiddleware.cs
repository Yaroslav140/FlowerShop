using Microsoft.Extensions.Options;

namespace FlowerShop.Web.ApiKey
{
    public class ApiKeyMiddleware(RequestDelegate next, IOptions<ApiKeyOptions> options)
    {
        private readonly RequestDelegate _next = next;
        private readonly ApiKeyOptions _options = options.Value;

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                if (!context.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey) || _options.Key != extractedApiKey)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Invalid or missing API Key");
                    return;
                }
            }
            await _next(context);
        }
    }
}
