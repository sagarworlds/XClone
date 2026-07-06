using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using XCloneAPI.Data;
using XCloneAPI.Services;

// Create builder
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container

// 1. Database Configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. JWT Authentication Configuration
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Key"]);

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

// 5. Add Controllers
builder.Services.AddControllers();

// 6. Add Services (Dependency Injection)
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPostService, PostService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ILikeService, LikeService>();
builder.Services.AddScoped<IFollowService, FollowService>();

// 7. AutoMapper Configuration (Optional - only if you use AutoMapper)
// builder.Services.AddAutoMapper(typeof(Program));

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

// 4. Authentication & Authorization Middleware
app.UseAuthentication();
app.UseAuthorization();

// 5. Route Prefix for API
app.MapControllers();

// 6. Global Exception Handler (Optional but recommended)
app.MapFallback(() => Results.NotFound(new { message = "Endpoint not found" }));

// 7. Run the application
app.Run();