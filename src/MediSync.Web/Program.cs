using MediSync.Web.Data;
using MediSync.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    // Revalidate the Identity security stamp on every request so an account
    // deactivated by Admin cannot keep using an existing session.
    options.Events.OnValidatePrincipal = async context =>
    {
        var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        if (context.Principal is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }
        var user = await userManager.GetUserAsync(context.Principal);
        if (user is null || (user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    };
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<ApplicationDbContext>();

    // Local development bootstrap:
    // EnsureCreatedAsync only creates a schema when the database has no tables.
    // If a previous run created an empty/partial database, Identity's AspNetRoles
    // table can be missing and seeding will fail. Recreate that incomplete local
    // development database so the complete Identity + application schema is built.
    var databaseExists = await db.Database.CanConnectAsync();
    if (!databaseExists)
    {
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT CASE WHEN OBJECT_ID(N'[dbo].[AspNetRoles]', N'U') IS NULL THEN 0 ELSE 1 END";
        await db.Database.OpenConnectionAsync();
        var identityRolesTableExists = Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        await db.Database.CloseConnectionAsync();

        if (!identityRolesTableExists)
        {
            // MediSync uses EnsureCreatedAsync rather than EF Core migrations.
            // If an empty/partial database was created before first deployment,
            // rebuild it so the complete Identity + application schema can be
            // created before demo seeding. This path is intended for a fresh
            // college/demo database only.
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }
    }

    // Demo seed is enabled for local development by default. For a deployed
    // college/demo instance, set MediSync__EnableDemoSeed=true explicitly.
    // This keeps the rich demo accounts/data out of an accidental production
    // deployment unless the owner intentionally enables them.
    var enableDemoSeed = app.Environment.IsDevelopment()
        || app.Configuration.GetValue<bool>("MediSync:EnableDemoSeed");

    if (enableDemoSeed)
    {
        await SeedData.InitializeAsync(services);
    }
}

app.MapControllerRoute(name: "default", pattern: "{controller=Dashboard}/{action=Index}/{id?}");
app.MapRazorPages();
app.Run();
