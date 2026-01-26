using FlowerShop.Data;
using FlowerShop.Web.ApiKey;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<FlowerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString(nameof(FlowerDbContext))));

builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection("ApiKey"));

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
});

builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseMiddleware<ApiKeyMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.MapGet("/", context =>
{
    context.Response.Redirect("/Home");
    return Task.CompletedTask;
});

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
        logger.LogError(ex, "Ошибка при миграции базы данных.");
    }
}

app.Run();
