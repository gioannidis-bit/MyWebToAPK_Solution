using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Serilog;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();

// 1) configure which headers to trust
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // pick up X-Forwarded-For and X-Forwarded-Proto
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // if you know the exact proxy IP(s), you can add them here
    //options.KnownProxies.Add(IPAddress.Parse("203.0.113.45"));

    // if you want to trust *any* proxy (less secure):
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});


// 1️⃣ Build the path to your Logs folder under the app’s content root
var logsDir = Path.Combine(builder.Environment.ContentRootPath, "Logs");

// 2️⃣ Make sure it exists (this will do nothing if it already does)
Directory.CreateDirectory(logsDir);

// 3️⃣ Configure Serilog to write daily rolling files into that folder
builder.Host.UseSerilog((ctx, lc) => lc
    .WriteTo.File(
        Path.Combine(logsDir, "requests-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 90,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .Enrich.FromLogContext()
);


// Make sure you inject IWebHostEnvironment so you can find a stable folder
var env = builder.Environment;

// 1) Configure DataProtection to persist keys under your content root
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(env.ContentRootPath, "KeyRing")))
    .SetApplicationName("URLToAPK");

// 2) (Optional) Relax the antiforgery cookie so it works over HTTP
builder.Services.AddAntiforgery(options =>
{
    // By default in Production, antiforgery cookies are marked Secure=true.
    // If you're running only HTTP, set this to None.
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.HeaderName = "X-XSRF-TOKEN";
});


// Read the URL(s) from configuration (appsettings.json)
string urls = builder.Configuration["Urls"];

// Configure the WebHost to listen on the specified URL(s)
builder.WebHost.UseUrls(urls);

// Add services to the container.
builder.Services.AddControllersWithViews();

#if DEBUG
builder.Logging.AddDebug();
#endif


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseForwardedHeaders();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
