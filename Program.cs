using JobMatch.Data;
using JobMatch.Data.Seed;
using JobMatch.Infrastructure;
using JobMatch.Services.CoverLetters;
using JobMatch.Services.Parsing;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// email config bits
builder.Services.AddTransient<JobMatch.Services.Email.IAppEmailSender, JobMatch.Services.Email.SmtpEmailSender>();

// hook up the database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// identity / auth setup
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();


builder.Services.AddHttpClient<JobMatch.Services.GeminiClient>();

// something to pull text out of uploaded CV files
builder.Services.AddScoped<IResumeTextExtractor, BasicResumeTextExtractor>();

builder.Services.AddScoped<IResumeParser, GeminiResumeParser>();

builder.Services.AddScoped<ICoverLetterGenerator, GeminiCoverLetterGenerator>();

// LARGE FILE UPLOAD SETTINGS

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = long.MaxValue;          // Allow very large uploads
});

builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = null;                
});

builder.Services.Configure<IISServerOptions>(o =>
{
    o.MaxRequestBodySize = int.MaxValue;              
});

var app = builder.Build();

// quick check to make sure the folders we rely on actually exist

try
{
    var appDataDir = Path.Combine(app.Environment.ContentRootPath, "App_Data");
    if (!Directory.Exists(appDataDir)) Directory.CreateDirectory(appDataDir);

    var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    var uploads = Path.Combine(webRoot, "uploads");
    if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);
}
catch
{
}

// from here down is just the normal middleware pipeline

if (app.Environment.IsDevelopment())
{
    app.UseMiddleware<AuditMiddleware>();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// hook up the database MIGRATIONS + SEEDING

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var db = services.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    string[] roles = new[] { "Jobseeker", "Recruiter", "Admin" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
    var adminEmail = "admin@jobmatch.local";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new IdentityUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(adminUser, "TempPass!234");
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }

    await IdentitySeed.EnsureSeedAsync(scope.ServiceProvider);
}


app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// usual MVC + Razor routing

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();
app.MapDefaultControllerRoute();

app.Run();
