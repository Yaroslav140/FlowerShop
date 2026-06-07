using FlowerShop.Data;
using FlowerShop.Web.ApiKey;
using FlowerShop.Web.RateLimit;
using FlowerShop.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Security.Claims;
using System.Threading.RateLimiting;
using ApiKeyOptions = FlowerShop.Web.ApiKey.ApiKeyOptions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<FlowerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString(nameof(FlowerDbContext))));

builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection("ApiKey"));
builder.Services.AddHttpClient();
builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("MailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    options.OnRejected = async (ctx, _) =>
    {
        ctx.HttpContext.Response.StatusCode = 429;
        ctx.HttpContext.Response.Headers["Retry-After"] = "60";
        await ctx.HttpContext.Response.WriteAsync("Too many requests. Please try again later.");
    };
});

builder.Services.AddSingleton<NotFoundRateLimitMiddleware>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, o =>
    {
        o.Cookie.Name = ".myapp.auth";
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Home";
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        o.SlidingExpiration = true;

        o.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<FlowerDbContext>();

                var idStr = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(idStr, out var id))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync();
                    return;
                }

                var exists = await db.UserDomains.AnyAsync(u => u.Id == id);
                if (!exists)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync();
                }
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizePage("/Account/Profile");
    o.Conventions.AuthorizePage("/Account/Purchases");
    o.Conventions.AllowAnonymousToPage("/Account/Login");
    o.Conventions.AllowAnonymousToPage("/Account/Register");
    o.Conventions.AllowAnonymousToPage("/Account/ConfirmEmail");
    o.Conventions.AllowAnonymousToPage("/Account/EmailSent");
    o.Conventions.AllowAnonymousToPage("/Account/ConfirmEmailChange");
    o.Conventions.AllowAnonymousToPage("/Account/ForgotPassword");
    o.Conventions.AllowAnonymousToPage("/Account/ResetPassword");
    o.Conventions.AllowAnonymousToPage("/PrivacyPolicy");
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto
});

if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRateLimiter();
app.UseMiddleware<NotFoundRateLimitMiddleware>();

app.UseRouting();

app.UseMiddleware<ApiKeyMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapControllers();
app.MapRazorPages();

app.MapGet("/", () => Results.Redirect("/Home", permanent: false));

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<FlowerDbContext>();

        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "������ ��� �������� ���� ������.");
    }
}

app.Run();
