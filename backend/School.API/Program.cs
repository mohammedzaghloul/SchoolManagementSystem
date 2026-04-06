using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using School.API.Filters;
using School.Application.Interfaces;
using School.Infrastructure.Data;
using School.Infrastructure.Identity;
using School.Infrastructure.Services;
using StackExchange.Redis;
using System.Text;
using School.API.Hubs;
using School.Infrastructure.BackgroundJobs;

var builder = WebApplication.CreateBuilder(args);
var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

if (allowedOrigins is not { Length: > 0 })
{
    allowedOrigins = new[] { "http://localhost:4200", "https://localhost:4200" };
}

// Add DbContext
builder.Services.AddDbContext<SchoolDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<SchoolDbContext>()
    .AddDefaultTokenProviders();

// Add Services
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSingleton<IConnectionMultiplexer>(c => 
{
    var configuration = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379", true);
    configuration.AbortOnConnectFail = false; // Allow app to start even if Redis is down
    return ConnectionMultiplexer.Connect(configuration);
});
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<IQrCodeService, QrCodeService>();
builder.Services.AddScoped<ILiveSessionService, LiveSessionService>();
builder.Services.AddHttpClient<IFaceRecognitionService, FaceRecognitionService>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<RequireActiveAccountFilter>();

// Storage Service Registration
var isCloudinaryEnabled = builder.Configuration.GetValue<bool>("Cloudinary:Enabled");
var isGoogleDriveEnabled = builder.Configuration.GetValue<bool>("GoogleDrive:Enabled");

if (isCloudinaryEnabled)
{
    builder.Services.AddScoped<IFileStorageService, School.Infrastructure.Services.Storage.CloudinaryStorageService>();
}
else if (isGoogleDriveEnabled)
{
    builder.Services.AddScoped<IFileStorageService, School.Infrastructure.Services.Storage.GoogleDriveStorageService>();
}
else
{
    builder.Services.AddScoped<IFileStorageService, School.Infrastructure.Services.Storage.LocalFileStorageService>();
}

// Configure MediatR
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(School.Application.Interfaces.ITokenService).Assembly);
});

// Configure JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidAudience = builder.Configuration["Jwt:ValidAudience"] ?? builder.Configuration["Jwt:Audience"] ?? "SchoolAppUsers",
        ValidIssuer = builder.Configuration["Jwt:ValidIssuer"] ?? builder.Configuration["Jwt:Issuer"] ?? "SchoolApp",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"] ?? "super_secret_secure_key_for_school_api_with_enough_length_to_be_valid"))
    };
    
    // We have to hook the OnMessageReceived event in order to 
    // allow the JWT authentication handler to read the access token from the query string 
    // when a WebSocket or Server-Sent Events request comes in.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];

            // If the request is for our hub...
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                (path.StartsWithSegments("/chatHub") || path.StartsWithSegments("/chathub")))
            {
                // Read the token out of the query string
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// Add Background Services
builder.Services.AddHostedService<AttendanceSyncWorker>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add SignalR
builder.Services.AddSignalR();

builder.Services.AddControllers(options =>
{
    options.Filters.AddService<RequireActiveAccountFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors(); // Uses Default Policy

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Removed UseHttpsRedirection entirely for maximum compatibility
app.UseStaticFiles(); // Serve uploaded chat files
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/chathub");

// Seed Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<SchoolDbContext>();
        
        // Ensure the database is created and up to date with migrations
        await context.Database.MigrateAsync();

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        await SchoolDbContextSeed.SeedAsync(context, userManager, roleManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during seeding the database. This might be because the database server is not reachable.");
    }
}

app.Run();
