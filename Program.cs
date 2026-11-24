using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Photon;
using Photon.Models;
using Serilog;
using System.Security.Claims;
using System.DirectoryServices.Protocols;
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Windows & Microsoft Account Auth API",
        Version = "v1",
        Description = "Use POST /login first to set the WinAuthSession cookie, then test protected endpoints."
    });
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "WinAuthSession";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

        // Automatically expire the server-side cookie after 5 minutes of API inactivity
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));


var app = builder.Build();

app.UseHttpsRedirection();

app.UseFileServer();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("./v1/swagger.json", "Windows Auth API v1");

    // Crucial: Ensure Swagger UI's fetch client includes HTTP-only cookies on every request
    options.UseRequestInterceptor("(req) => { req.credentials = 'include'; return req; }");
});


app.UseDefaultFiles();

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        ctx.Context.Response.Headers.Append("Pragma", "no-cache");
        ctx.Context.Response.Headers.Append("Expires", "0");
    }
});
app.MapFallbackToFile("index.html");

app.UseDeveloperExceptionPage();


app.UseAuthentication();
app.UseAuthorization();


// Fix for CS8604: Ensure connectionString is not null before using it
var connectionString = builder.Configuration.GetConnectionString("default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Database connection string 'default' is missing or invalid.");
}

Photon.ProtonRepository repository = new(connectionString);

app.MapGet("/EntityTypes", async () =>
    await repository.GetEntityTypes()).RequireAuthorization();

app.MapGet("/FilteredLookups/{attributeId},{searchTerm},{nRows}", async (int attributeId, string? searchTerm, int nRows) =>
    await repository.GetFilteredLookups(attributeId, searchTerm, nRows)).RequireAuthorization();

app.MapGet("/AttributeConfig/{attributeId}", async (int attributeId) =>
    await repository.GetAttributeConfig(attributeId)).RequireAuthorization();

app.MapGet("/AttributeUse/{attributeId}", async (int attributeId) =>
    await repository.GetAttributeUse(attributeId)).RequireAuthorization();

app.MapGet("/ViewValues/{viewId},{entityid},{page}", async (int viewId, int entityId, int page = 0) =>
    await repository.GetViewValues(viewId, entityId, page)).RequireAuthorization();

app.MapGet("/IdValues/{entityid}", async (int entityId) =>
    await repository.GetIdValues(entityId)).RequireAuthorization();

app.MapGet("/LongText/{entityId},{attributeId},{seq}", async (int entityId, int attributeId, int seq) =>
{
    var data = await repository.GetLongText(entityId, attributeId, seq);
    return Results.Ok(new { text = data });
}).RequireAuthorization();

app.MapGet("/Indexes/{indexTypeId},{page},{nRows}/{searchterm}", async (int indexTypeId, string searchterm = "", int page = 0, int nRows = 16) =>
    await repository.GetIndexes(indexTypeId, page, nRows, searchterm)).RequireAuthorization();

app.MapGet("/lookups/{attributeId},{page},{nRows}", async (int attributeId, int page, int nRows) =>
    await repository.GetLookups(attributeId, page, nRows)).RequireAuthorization();

app.MapGet("/DatedValues/{entityId},{attributeId}", (int entityId, short attributeId) =>
    repository.GetDatedValues(entityId, attributeId)).RequireAuthorization();

app.MapGet("/NPages/{viewId},{entityId}", (short viewId, int entityId) =>
    repository.NPages(viewId, entityId)).RequireAuthorization();

app.MapGet("/Menu/{Id}", (short id) =>
    repository.GetMenu(id)).RequireAuthorization();

app.MapGet("/user", [Authorize(AuthenticationSchemes = NegotiateDefaults.AuthenticationScheme)] async (ClaimsPrincipal user) =>
{
    return Results.Ok(new InitialResponse
    {
        userName = user.Identity?.Name ?? string.Empty,
        requireNetworkLogin = builder.Configuration.GetValue<bool>("AuthSettings:RequireNetworkLogin")
    });
});

app.MapPost("/login", [Authorize(AuthenticationSchemes = NegotiateDefaults.AuthenticationScheme)] async (LoginRequest request, HttpContext httpContext, IConfiguration config) =>
{
    var resp = new LoginResponse();
    ClaimsPrincipal princ ;
    var allowedRoles = new HashSet<string>(config.GetSection("AuthSettings:AllowedRoles").Get<string[]>() ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
    
    if (builder.Configuration.GetValue<bool>("AuthSettings:RequireNetworkLogin"))
    {
        if (string.IsNullOrWhiteSpace(request.userName) || string.IsNullOrWhiteSpace(request.nPwd))
        {
            resp.IsAuthenticated = false;
            resp.Message = "Username and password are required.";
            return Results.Ok(resp);
        }

        // Parse "UHB\pamJamesT" or "pamJamesT"
        string domain = "UHB"; // Default domain if user omits "UHB\"
        string username = request.userName.Trim();

        if (username.Contains('\\'))
        {
            var parts = username.Split('\\', 2);
            domain = parts[0];
            username = parts[1];
        }
        else if (username.Contains('@'))
        {
            // Handle UPN format (e.g., pamJamesT@uhb.nhs.uk)
            var parts = username.Split('@', 2);
            username = parts[1];
            domain = parts[0];
        }

        if (!WindowsAuthHelper.ValidateAdCredentialsLdaps(domain, username, request.nPwd, out string error))
        {
            resp.IsAuthenticated = false;
            resp.Message = $"Login failed for {domain}\\{username}. Reason: {error}";
            
            Log.Warning($"Login failed for {domain}\\{username}. Reason: {error}");
            return Results.Ok(resp);
        }

        // Credentials are valid! 
        // Fetch roles from AD or your database and issue a session cookie / JWT for your petite-vue app
        var roles = WindowsAuthHelper.GetUserRoles(domain, username);

        string fullUserName = $"{domain}\\{username}";

        // 4. Build ClaimsIdentity and ClaimsPrincipal
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, fullUserName),
            new Claim(ClaimTypes.NameIdentifier, fullUserName)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        princ = new ClaimsPrincipal(claimsIdentity);


    } else
    {
        princ = httpContext.User; 
    }
    var c = princ.Claims.Where(c => allowedRoles.Contains(WindowsAuthHelper.GetClaimName(c)));
    if (c.Count()==0)
    {
        resp.IsAuthenticated = false;
        resp.Message = $"User {request.userName} is not in any allowed roles.";
        Log.Warning("User {UserName} attempted to log in but is not in any allowed roles.", request.userName);
        return Results.Ok(resp);
    }

    try
    {
        var userStarter = await repository.TryLogin(request.pPwd);
        if (userStarter == null)
        {
            resp.IsAuthenticated = false;
            resp.Message = "Password does not match any in Proton.";
            Log.Warning("User {UserName} attempted to log in but provided an incorrect Proton password.", request.userName);
            return Results.Ok(resp);
        }  
        Log.Information("User {UserName} logged in successfully.", request.userName);
        resp.Message = "Login successful, loading...";
        resp.IsAuthenticated = true;
        resp.UserStarter = userStarter;
        
        await httpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        princ,
        new AuthenticationProperties
        {
            IsPersistent = false, // Session cookie (clears when browser closes)
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        });

        return Results.Ok(resp);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error during login attempt.");
        resp.Message = "An error occurred during login: " + ex.Message;
        resp.IsAuthenticated = false;
        return Results.Ok(resp);
    }
});

app.MapPost("/BatchUpdate", async (Photon.BatchRequestDto request) =>
{
    try
    {
        var result = await repository.ProcessUpdates(request);
        return result;
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500, title: "An error occurred while processing the batch.");
    }
}).RequireAuthorization();

app.Run();
