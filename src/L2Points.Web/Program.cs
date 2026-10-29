using System.Globalization;
using L2Points.Infrastructure;
using L2Points.Infrastructure.Identity;
using L2Points.Infrastructure.Persistence;
using L2Points.Web.Components;
using L2Points.Web.Hosting;
using L2Points.Web.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IdentityErrorDescriber, CzechIdentityErrorDescriber>();

// AddIdentityCore registers no authentication scheme, so the Identity cookies are added here.
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.ReturnUrlParameter = "returnUrl";
    options.AccessDeniedPath = "/";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});
// Revalidate the security stamp often so deactivation and password resets take effect quickly.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(tags: ["ready"]);

// Behind a reverse proxy (Azure) the proxy IP is not fixed; trusting all proxies is opt-in via configuration.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    ForwardedHeadersSetup.Configure(
        options, builder.Configuration.GetValue<bool>(ForwardedHeadersSetup.TrustAllProxiesKey)));

var app = builder.Build();

if (!app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(app.Configuration[DependencyInjection.KeyVaultKeyUriKey]))
{
    app.Logger.LogWarning(
        "{Key} is not configured: Data Protection keys are stored unencrypted in the database.",
        DependencyInjection.KeyVaultKeyUriKey);
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

var culture = new CultureInfo("cs-CZ");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = [culture],
    SupportedUICultures = [culture],
});

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
// Liveness runs no checks (the process is up); readiness and the legacy /health include the database check.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") }).AllowAnonymous();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") }).AllowAnonymous();

// Anonymous on purpose: a tab with an expired cookie must still end on /login instead of a 401/405 page.
app.MapPost("/logout", async (HttpContext context, IAntiforgery antiforgery, SignInManager<AppUser> signInManager) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(context);
    }
    catch (AntiforgeryValidationException)
    {
        // Stale or missing token (e.g. expired session): sign out anyway and show the login page, not a 400.
    }

    await signInManager.SignOutAsync();
    return Results.LocalRedirect("/login");
}).AllowAnonymous();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await DatabaseInitializer.MigrateAndSeedAsync(app.Services);

app.Run();
