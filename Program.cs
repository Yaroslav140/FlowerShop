using FlowerShop.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<FlowerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString(nameof(FlowerDbContext))));

builder.Services.AddDefaultIdentity<IdentityUser>(o =>
    o.SignIn.RequireConfirmedAccount = true).AddEntityFrameworkStores<FlowerDbContext>();

builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", o =>
    {
        o.Cookie.Name = ".myapp.auth";          
        o.LoginPath = "/Account/login";            
        o.AccessDeniedPath = "/Home";
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        o.SlidingExpiration = true;             
        o.Cookie.HttpOnly = true;               
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });
builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizePage("/Account/Profile");
    o.Conventions.AllowAnonymousToPage("/Account/Login");
    o.Conventions.AllowAnonymousToPage("/Account/Register");
});
builder.Services.AddControllers();

var app = builder.Build();

app.MapGet("/", context =>
{
    context.Response.Redirect("/Home");  
    return Task.CompletedTask;
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapControllers();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
