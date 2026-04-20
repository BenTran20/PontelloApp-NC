using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using PontelloApp.Data;
using PontelloApp.Models;
using PontelloApp.Services;
using PontelloApp.Ultilities;
using PontelloApp.Utilities;
using PontelloApp.ViewModels;
using QuestPDF.Infrastructure;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("PontelloAppContext")
    ?? throw new InvalidOperationException("Connection string 'PontelloAppContext' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContext<PontelloAppContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("PontelloAppContext")));
builder.Services.AddScoped<OrderService>();

//For email service configuration
builder.Services.AddSingleton<IEmailConfiguration>(
    builder.Configuration.GetSection("EmailConfiguration").Get<EmailConfiguration>()!
);

//For the Identity System
builder.Services.AddTransient<EmailSender>();
builder.Services.AddTransient<IEmailSender, EmailSender>();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddScoped<RecurringOrderService>();

builder.Services.AddHangfire(config => config
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseInMemoryStorage());
builder.Services.AddScoped<RecurringEmailService>();

builder.Services.AddScoped<RecurringOrderProcessorJob>();
builder.Services.AddHostedService<RecurringOrderBackgroundService>();

builder.Services.AddHostedService<EmailSchedulerService>();
builder.Services.AddScoped<IEmailSender, EmailSender>();


builder.Services.AddHangfireServer();

builder.Services.AddDefaultIdentity<User>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.Configure<IdentityOptions>(options =>
{
    // Password settings.
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings.
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings.
    options.User.AllowedUserNameCharacters =
    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    options.User.RequireUniqueEmail = true;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    // Cookie settings
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);

    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
});

builder.Services.AddSignalR();

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.MapHub<NotificationHub>("/notificationHub");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.UseHangfireDashboard("/hangfire");

RecurringJob.AddOrUpdate<RecurringOrderProcessorJob>(
    "check-recurring-orders",
    job => job.RunAsync(),
    "* * * * *");

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    await ApplicationDbInitializer.Initialize(serviceProvider: services, useMigrations: true, seedSampleData: true);

    PontelloAppInitializer.Initialize(serviceProvider: services, DeleteDatabase: false,
        UseMigrations: true, SeedSampleData: true);
}

QuestPDF.Settings.License = LicenseType.Community;

app.Run();
