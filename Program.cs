using System.Security.Claims;
using cheongdo_backend.Guidance;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Antiforgery;

var builder = WebApplication.CreateBuilder(args);

var googleClientId = builder.Configuration["Google:ClientId"];
var googleClientSecret = builder.Configuration["Google:ClientSecret"];
var googleLoginConfigured = !string.IsNullOrWhiteSpace(googleClientId)
    && !string.IsNullOrWhiteSpace(googleClientSecret);
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
        options.DefaultChallengeScheme = googleLoginConfigured
            ? OpenIdConnectDefaults.AuthenticationScheme
            : CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-cheongdo";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/auth/login";
        options.SlidingExpiration = false;
    });

if (googleLoginConfigured)
{
    builder.Services.AddAuthentication()
        .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.Authority = "https://accounts.google.com";
            options.ClientId = googleClientId!;
            options.ClientSecret = googleClientSecret!;
            options.ResponseType = "code";
            options.UsePkce = true;
            options.SaveTokens = false;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.ClaimActions.MapUniqueJsonKey(ClaimTypes.Name, "name");
            options.TokenValidationParameters.NameClaimType = ClaimTypes.Name;
        });
}

builder.Services.AddAuthorization();
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

app.MapGet("/auth/login", (IConfiguration configuration) =>
{
    var clientId = configuration["Google:ClientId"];
    var clientSecret = configuration["Google:ClientSecret"];
    if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
    {
        return Results.Problem(
            title: "Google 로그인 설정이 필요합니다.",
            detail: "Google:ClientId와 Google:ClientSecret을 서버 환경 변수로 설정하세요.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Challenge(
        new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = "/?signedIn=1" },
        [OpenIdConnectDefaults.AuthenticationScheme]);
}).AllowAnonymous();

app.MapGet("/api/v1/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Ok(new { requestToken = tokens.RequestToken });
}).RequireAuthorization();

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
    .RequireAuthorization();

app.Run();

public partial class Program;
