using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FlowerShop.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<FlowerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString(nameof(FlowerDbContext))));

builder.Services.AddDefaultIdentity<IdentityUser>(o =>
    o.SignIn.RequireConfirmedAccount = true).AddEntityFrameworkStores<FlowerDbContext>();

builder.Services.AddRazorPages();
builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapControllers();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
