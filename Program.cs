using cheongdo_backend.Guidance;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;

const string ConsentedGuestPolicy = "ConsentedGuest";

var builder = WebApplication.CreateBuilder(args);

var useMockData = builder.Configuration.GetValue("Data:UseMock", true);

if (builder.Environment.IsProduction() && useMockData)
{
    throw new InvalidOperationException("Mock data cannot be enabled in Production.");
}

if (!useMockData)
{
    throw new InvalidOperationException("No verified education data provider is configured.");
}

var mcpEndpointValue = builder.Configuration["Mcp:Endpoint"];
if (!Uri.TryCreate(mcpEndpointValue, UriKind.Absolute, out var mcpEndpoint))
{
    throw new InvalidOperationException("Mcp:Endpoint must be an absolute MCP server URL.");
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-cheongdo";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(ConsentedGuestPolicy, policy =>
        policy.RequireAuthenticatedUser()
            .RequireClaim(GuestSession.LoginTypeClaim, GuestSession.LoginType)
            .RequireClaim(GuestSession.ConsentVersionClaim, GuestSession.ConsentVersion));
});
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddSingleton<IEducationMaterialProvider>(
    new McpEducationMaterialProvider(mcpEndpoint));
builder.Services.AddSingleton<IGuidanceAgent, CopilotGuidanceAgent>();
builder.Services.AddSingleton<GuidanceService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous();

app.MapGet("/api/v1/session", (HttpContext context) =>
{
    var isAuthenticated = context.User.Identity?.IsAuthenticated == true
        && context.User.HasClaim(GuestSession.LoginTypeClaim, GuestSession.LoginType)
        && context.User.HasClaim(GuestSession.ConsentVersionClaim, GuestSession.ConsentVersion);
    return Results.Ok(new { authenticated = isAuthenticated });
}).AllowAnonymous();

app.MapGet("/api/v1/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Ok(new { requestToken = tokens.RequestToken });
}).AllowAnonymous();

app.MapPost("/auth/guest", async (
    HttpContext context,
    IAntiforgery antiforgery,
    GuestLoginRequest request) =>
{
    if (!await antiforgery.IsRequestValidAsync(context))
    {
        return Results.Problem(
            title: "요청을 확인할 수 없습니다.",
            detail: "유효한 CSRF 토큰이 필요합니다.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    var principal = GuestSession.CreatePrincipal(request.ConsentAccepted, request.ConsentVersion);
    if (principal is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.ConsentAccepted)] = ["현재 동의 안내를 확인하고 동의해야 게스트로 이용할 수 있습니다."]
        });
    }

    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal,
        new AuthenticationProperties { IsPersistent = false });
    return Results.NoContent();
}).AllowAnonymous();

app.MapPost("/auth/logout", async (
    HttpContext context,
    IAntiforgery antiforgery) =>
{
    if (!await antiforgery.IsRequestValidAsync(context))
    {
        return Results.Problem(
            title: "요청을 확인할 수 없습니다.",
            detail: "유효한 CSRF 토큰이 필요합니다.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.NoContent();
}).RequireAuthorization(ConsentedGuestPolicy);

app.MapPost("/api/v1/guidance", async (
    HttpContext context,
    IAntiforgery antiforgery,
    GuidanceRequest request,
    GuidanceService guidanceService,
    CancellationToken cancellationToken) =>
{
    if (!await antiforgery.IsRequestValidAsync(context))
    {
        return Results.Problem(
            title: "요청을 확인할 수 없습니다.",
            detail: "유효한 CSRF 토큰이 필요합니다.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    if (string.IsNullOrWhiteSpace(request.ContextSummary) || request.ContextSummary.Length > 500)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.ContextSummary)] = ["요약은 1자 이상 500자 이하여야 합니다."]
        });
    }

    if (request.Region?.Length > 100)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.Region)] = ["지역은 100자 이하여야 합니다."]
        });
    }

    var result = await guidanceService.AnswerAsync(request, cancellationToken);
    return Results.Ok(result);
})
    .RequireAuthorization(ConsentedGuestPolicy);

app.Run();

public partial class Program;
