using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using XCloneAPI.Data;
using XCloneAPI.Services;

// Create builder
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container

// Values committed to the repo are placeholders containing this marker; real values come from
// user-secrets (Development) or environment variables, and the app refuses to start with a placeholder.
const string PlaceholderMarker = "CHANGE-ME";

// 1. Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains(PlaceholderMarker, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is missing or still the placeholder from appsettings.json. In development run: " +
        "dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"Host=localhost;Port=5432;Database=x_clone_db;Username=postgres;Password=<your password>\". " +
        "In other environments set the ConnectionStrings__DefaultConnection environment variable.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// 2. JWT Authentication Configuration
var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSettings["Key"];
if (string.IsNullOrWhiteSpace(jwtKey)
    || Encoding.UTF8.GetByteCount(jwtKey) < 32
    || jwtKey.Contains(PlaceholderMarker, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException(
        "Jwt:Key is missing, shorter than 32 bytes, or still the placeholder from appsettings.json. In development run: dotnet user-secrets set \"Jwt:Key\" \"<random string of 64+ characters>\". " +
        "In other environments set the Jwt__Key environment variable.");
var secretKey = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// 3. Authorization
builder.Services.AddAuthorization();

// 4. CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policyBuilder =>
    {
        policyBuilder
            .WithOrigins("http://localhost:4200", "http://localhost:4300")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// 5. Rate limiting (brute-force protection for login/register, and a cap on uploads per user)
var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
var uploadPermitLimit = builder.Configuration.GetValue("RateLimiting:UploadPermitLimit", 30);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Too many attempts. Please try again in a minute." }, cancellationToken);
    };
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitLimit, Window = TimeSpan.FromMinutes(1) }));

    // Uploads are counted per signed-in user (the limiter runs after authentication), falling back to the address
    options.AddPolicy("upload", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = uploadPermitLimit, Window = TimeSpan.FromMinutes(1) }));
});

// Uploaded images live in a folder (relative paths are inside the app's folder) and are served from /uploads
var uploadsDirectory = Path.GetFullPath(
    builder.Configuration["Uploads:Directory"] ?? "uploads", builder.Environment.ContentRootPath);
var mediaStorage = new LocalMediaStorage(uploadsDirectory);
builder.Services.AddSingleton<IMediaStorage>(mediaStorage);

// 5b. Add Controllers. A file parameter would otherwise make its action reachable only with a multipart body, and any
// other kind of request would get a misleading "Endpoint not found"; this way it reaches the action and is told what is wrong.
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options => options.SuppressConsumesConstraintForFormFileParameters = true);

// 6. Add Services (Dependency Injection)
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPostService, PostService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ILikeService, LikeService>();
builder.Services.AddScoped<IRetweetService, RetweetService>();
builder.Services.AddScoped<IFollowService, FollowService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IHashtagService, HashtagService>();

// 8. Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 9. Logging
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});

// 10. HTTP Client
builder.Services.AddHttpClient();

// Build the application
var app = builder.Build();

// Configure the HTTP request pipeline

// 1. Swagger in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 2. HTTPS Redirection
app.UseHttpsRedirection();

// 3. CORS Middleware (Must be before Authentication)
app.UseCors("AllowAngular");

// Uploaded images. Only the four image types are served (anything else in the folder is a 404), browsers must not
// second-guess the type, and the names are never reused so the files can be cached for good.
var imageTypes = new FileExtensionContentTypeProvider();
imageTypes.Mappings.Clear();
foreach (var kind in Enum.GetValues<ImageKind>())
    imageTypes.Mappings["." + MediaNames.Extension(kind)] = MediaNames.ContentType(kind);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaStorage.Directory),
    RequestPath = "/uploads",
    ContentTypeProvider = imageTypes,
    ServeUnknownFileTypes = false,
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
    }
});

// 4. Authentication, then the rate limiter (so limits can be per user; it still runs after CORS, so 429 responses
// carry the CORS headers the browser needs to read them), then Authorization
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

// 5. Route Prefix for API
app.MapControllers();

// 6. Global Exception Handler (Optional but recommended)
app.MapFallback(() => Results.NotFound(new { message = "Endpoint not found" }));

// 7. Run the application
app.Run();

// Makes the entry point visible to the integration test project (WebApplicationFactory<Program>)
public partial class Program { }
