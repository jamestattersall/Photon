using JwtAuthApp.JWT;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Diagnostics;
using TestJwt.Identity;
using TestJwt.Model;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
   .AddNegotiate();

builder.Services.AddAuthorization(options =>
{
    // By default, all incoming requests will be authorized according to the default policy.
    options.FallbackPolicy = options.DefaultPolicy;
});

//uncomment the 4 lines below to enable jwt
//var jwtConfig = builder.Configuration.GetSection("Jwt").Get<JwtConfiguration>();
//builder.Services.AddSingleton(jwtConfig);
//builder.Services.AddScoped<IdentityService>();
//builder.Services.AddJwtAuthentication(jwtConfig);

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseFileServer();
app.UseStaticFiles();
app.UseDefaultFiles("/index.html");

app.UseDeveloperExceptionPage();

var connectionString = builder.Configuration.GetConnectionString("default");

ProtonRepository repository = new(connectionString);

app.MapGet("/TryLogin/{pwd}",(HttpContext context, string pwd) =>  repository.TryLogin(context, pwd)).RequireAuthorization();

app.MapGet("/EntityTypes", (HttpContext context) =>  repository.GetEntityTypes(context)).RequireAuthorization();

app.MapGet("/ViewValues/{viewId},{entityid},{page}", (HttpContext context, int viewId, int entityId, int page=0) =>
    repository.GetViewValues(context, viewId, entityId, page)).RequireAuthorization();

app.MapGet("/Indexes/{indexTypeId},{page},{nRows}/{searchterm}", (HttpContext context, int indexTypeId, string? searchterm, int page = 0, int nRows = 16) =>
    repository.GetIndexes(context, indexTypeId, page, nRows, searchterm)).RequireAuthorization();

app.MapGet("/Indexes/{indexTypeId},{page},{nRows}", (HttpContext context, int indexTypeId, int page = 0, int nRows = 16, string searchterm="" ) =>
    repository.GetIndexes(context, indexTypeId, page, nRows, searchterm)).RequireAuthorization();

app.MapGet("/DatedValues/{entityId},{attributeId},{daysBack}", (HttpContext context, int entityId, short attributeId, int daysBack = int.MaxValue) =>
    repository.GetDatedValues(context, entityId, attributeId, daysBack)).RequireAuthorization();

app.MapGet("/NPages/{viewId},{entityId}", (HttpContext context,  short viewId, int entityId) =>
    repository.NPages(context, viewId, entityId)).RequireAuthorization();

//app.MapPost("/login", [Authorize(AuthenticationSchemes = NegotiateDefaults.AuthenticationScheme)] async (LoginRequest request, IdentityService identityService, IConfiguration config, ILogger<Program> logger, HttpContext context) =>
//{
//    // Validate user credentials
//    if (string.IsNullOrWhiteSpace(request.Password))
//    {
//        logger.LogWarning("Login failed: Empty password.");
//        return Results.BadRequest(new { message = "password is required." });
//    }
//    if (context.User.Identity.IsAuthenticated)
//    {
//        var userStarter = repository.TryLogin(context, request.Password);
//        var userIsAuthenticated = userStarter != null;

//        if (!userIsAuthenticated)
//        {
//            logger.LogWarning("Login failed for user: {Username}", context.User.Identity.Name);
//            return Results.Unauthorized();
//        }

//        // Generate JWT token
//        var token = await identityService.GenerateToken(context.User.Identity.Name);

//        logger.LogInformation("User {Username} authenticated successfully.", context.User.Identity.Name);

//        return Results.Ok(new LoginResponse
//        {
//            Message = "Login successful",
//            Token = token,
//            UserStarter = userStarter
//        });
//    }

//    logger.LogWarning("Windows Login failed");
//    return Results.BadRequest(new { message = "User must be logged in to Windows." });
//})
//.RequireAuthorization();

app.Run();
