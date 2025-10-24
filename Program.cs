using JwtAuthApp.JWT;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using TestJwt.Identity;
using TestJwt.Model;
using TestJwt.Swagger;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

var jwtConfig = builder.Configuration.GetSection("Jwt").Get<JwtConfiguration>();
builder.Services.AddSingleton(jwtConfig);

builder.Services.AddScoped<IdentityService>();
builder.Services.AddJwtAuthentication(jwtConfig);
builder.Services.AddSwaggerGen(SwaggerConfiguration.Configure);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseFileServer();
app.UseStaticFiles();
app.UseDefaultFiles("/index.html");

app.UseDeveloperExceptionPage();

var connectionString = builder.Configuration.GetConnectionString("default");

ProtonRepository repository = new(connectionString);

app.MapGet("/EntityTypes", () =>  
    repository.GetEntityTypes()).RequireAuthorization(); ; 

app.MapGet("/ViewValues/{viewId},{entityid},{page}", ( int viewId, int entityId, int page=0) =>
    repository.GetViewValues( viewId, entityId, page)).RequireAuthorization();

app.MapGet("/Indexes/{indexTypeId},{page},{nRows}/{searchterm}", ( int indexTypeId, string? searchterm, int page = 0, int nRows = 16) =>
    repository.GetIndexes( indexTypeId, page, nRows, searchterm)).RequireAuthorization();

app.MapGet("/Indexes/{indexTypeId},{page},{nRows}", ( int indexTypeId, int page = 0, int nRows = 16, string searchterm="" ) =>
    repository.GetIndexes( indexTypeId, page, nRows, searchterm)).RequireAuthorization();

app.MapGet("/DatedValues/{entityId},{attributeId},{daysBack}", ( int entityId, short attributeId, int daysBack = int.MaxValue) =>
    repository.GetDatedValues( entityId, attributeId, daysBack)).RequireAuthorization();

app.MapGet("/NPages/{viewId},{entityId}", (short viewId, int entityId) =>
    repository.NPages( viewId, entityId)).RequireAuthorization();

app.MapPost("/login", [Authorize(AuthenticationSchemes = NegotiateDefaults.AuthenticationScheme)] async (LoginRequest request, IdentityService identityService, IConfiguration config, ILogger<Program> logger, HttpContext context) =>
{
    // Validate user credentials
    if (string.IsNullOrWhiteSpace(request.Password))
    {
        logger.LogWarning("Login failed: Empty password.");
        return Results.BadRequest(new { message = "password is required." });
    }
    if (context.User.Identity != null && context.User.Identity.IsAuthenticated)
    {
        var userName = context.User.Identity.Name;
        var userStarter = repository.TryLogin(context, request.Password);
        
        if (userStarter == null)
        {
            logger.LogWarning("Login failed for user: {Username}", userName);
            return Results.Unauthorized();
        }

        // Generate JWT token
        var token = await identityService.GenerateToken(userName!);

        logger.LogInformation("User {Username} authenticated successfully.", userName);

        return Results.Ok(new LoginResponse
        {
            Message = "Login successful",
            Token = token,
            UserStarter = userStarter,
            Username = userName
        });
    }

    logger.LogWarning("Windows Login failed");
    return Results.BadRequest(new { message = "User must be logged in to Windows." });
})
.RequireAuthorization();

app.Run();
