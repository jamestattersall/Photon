using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi;
using Photon.Jwt;
using Photon.Models;
using Serilog;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();


var jwtConfig = builder.Configuration.GetSection("Jwt").Get<JwtConfiguration>() ?? throw new InvalidOperationException("Jwt configuration is missing or invalid.");

builder.Services.AddSingleton(jwtConfig);

builder.Services.AddScoped<IdentityService>();
builder.Services.AddJwtAuthentication(jwtConfig);

builder.Services.AddSwaggerGen(options =>
{
    // ...

    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT Authorization header using the Bearer scheme."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = []
    });
});

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));


var app = builder.Build();

app.UseHttpsRedirection();

app.UseFileServer();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseDefaultFiles();
if (app.Environment.IsDevelopment())
{
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            ctx.Context.Response.Headers.Append("Pragma", "no-cache");
            ctx.Context.Response.Headers.Append("Expires", "0");
        }
    });
}
else
{
    app.UseStaticFiles();
}


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

app.MapGet("/user", [Authorize(AuthenticationSchemes = NegotiateDefaults.AuthenticationScheme)] async (HttpContext context) =>
{
    var usr = context.User;
    return usr.Identity.Name;
})
.RequireAuthorization();

app.MapPost("/login", async (LoginRequest request, IdentityService identityService, HttpContext context) =>
{
    var resp = new LoginResponse();
    // Validate user credentials
    if (string.IsNullOrWhiteSpace(request.userName) || string.IsNullOrWhiteSpace(request.nPwd))
    {
        resp.Message = "Network user name and password are required.";
        resp.IsAuthenticated = false;
        return Results.BadRequest(resp);
    }


    var authResult = WindowsAuthHelper.Authenticate(request.userName, request.nPwd);
    if (!authResult.IsAuthenticated || string.IsNullOrEmpty(authResult.ResolvedUser))
    {
        resp.IsAuthenticated = false;
        resp.Message = "Invalid network credentials. Code:" + authResult.ErrorCode.ToString();
        return Results.Ok(resp);
    }

    try
    {
        var userStarter = await repository.TryLogin(request.pPwd);
        if (userStarter == null)
        {
            resp.IsAuthenticated = false;
            resp.Message = "Password does not match any in Proton.";
            return Results.Ok(resp);
        }  // Generate JWT token
        var token = identityService.GenerateToken(request.userName!);
        Log.Information("User {UserName} logged in successfully.", request.userName);
        resp.Message = "Login successful";
        resp.IsAuthenticated = true;
        resp.UserStarter = userStarter;
        resp.Token = token;
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
