using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using School.API.Filters;
using School.API.Hubs;
using School.Application.Interfaces;
using School.Infrastructure.BackgroundJobs;
using School.Infrastructure.Data;
using School.Infrastructure.Identity;
using School.Infrastructure.Services;
using StackExchange.Redis;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var resetSeedRequested = args.Contains("--reset-seed", StringComparer.OrdinalIgnoreCase)
    || Environment.GetEnvironmentVariable("SCHOOL_RESET_SEED") == "1";
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var allowedOriginsFromConfig = builder.Configuration["Cors:AllowedOrigins"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL");
var allowedOriginsList = new List<string> { "http://localhost:4200", "https://localhost:4200" };

if (!string.IsNullOrEmpty(frontendUrl))
{
    allowedOriginsList.AddRange(frontendUrl.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

if (allowedOriginsFromConfig != null)
{
    allowedOriginsList.AddRange(allowedOriginsFromConfig);
}

var allowedOrigins = allowedOriginsList.Distinct().ToArray();

var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
if (!string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(dbPassword))
{
    connectionString = connectionString.Replace("${DB_PASSWORD}", dbPassword);
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection is not configured. Set CONNECTION_STRING and DB_PASSWORD in Railway.");
}

builder.Services.AddDbContext<SchoolDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<SchoolDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSingleton<IConnectionMultiplexer>(c =>
{
    var configuration = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379", true);
    configuration.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(configuration);
});
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdministrationService, AdministrationService>();
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<IEmailOtpService, EmailOtpService>();
builder.Services.AddHttpClient<IEmailService, EmailService>();
builder.Services.AddScoped<IQrCodeService, QrCodeService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<ITeacherWorkflowService, TeacherWorkflowService>();
builder.Services.AddScoped<ILiveSessionService, LiveSessionService>();
builder.Services.AddScoped<IStudentQueryService, StudentQueryService>();
builder.Services.AddScoped<IGradeManagementService, GradeManagementService>();
builder.Services.AddHttpClient<IFaceRecognitionService, FaceRecognitionService>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<RequireActiveAccountFilter>();

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

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(School.Application.Interfaces.ITokenService).Assembly);
});

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
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidAudience = builder.Configuration["Jwt:ValidAudience"] ?? builder.Configuration["Jwt:Audience"] ?? "SchoolAppUsers",
        ValidIssuer = builder.Configuration["Jwt:ValidIssuer"] ?? builder.Configuration["Jwt:Issuer"] ?? "SchoolApp",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? builder.Configuration["Jwt:Secret"]
            ?? "super_secret_secure_key_for_school_api_with_enough_length_to_be_valid"))
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) &&
                (path.StartsWithSegments("/chatHub") || path.StartsWithSegments("/chathub")))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddHostedService<AttendanceSyncWorker>();

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

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    options.AddFixedWindowLimiter("strict", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5; // 5 requests per minute for sensitive endpoints
        opt.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.Window = TimeSpan.FromSeconds(10);
        opt.PermitLimit = 100; // 100 requests per 10 seconds for general API
        opt.QueueLimit = 0;
    });
});

builder.Services.AddSignalR();

builder.Services.AddControllers(options =>
{
    options.Filters.AddService<RequireActiveAccountFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();

startupLogger.LogInformation(
    "Starting School API on 0.0.0.0:{Port}. Allowed CORS origins: {AllowedOrigins}",
    port,
    string.Join(", ", allowedOrigins));

app.UseCors();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/chathub");
app.MapHealthChecks("/health");

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<SchoolDbContext>();
        await context.Database.MigrateAsync();

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var runFullSeed = resetSeedRequested
            || app.Environment.IsDevelopment()
            || Environment.GetEnvironmentVariable("SCHOOL_RUN_SEED") == "1";

        if (runFullSeed)
        {
            logger.LogInformation(
                "Running database seed. Environment={Environment}; ResetSeed={ResetSeed}",
                app.Environment.EnvironmentName,
                resetSeedRequested);

            if (resetSeedRequested)
            {
                await CleanSchoolSeed.ResetDataAsync(context);
            }

            await SchoolDbContextSeed.SeedAsync(context, userManager, roleManager);
            await GradeManagementSeed.SeedAsync(context, userManager, roleManager);
        }
        else
        {
            logger.LogInformation(
                "Skipping demo database seed in {Environment}. Set SCHOOL_RUN_SEED=1 to run it explicitly.",
                app.Environment.EnvironmentName);

            await CleanSchoolSeed.BootstrapIdentityAsync(userManager, roleManager);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during seeding the database. This might be because the database server is not reachable.");
    }
}

if (resetSeedRequested)
{
    return;
}

app.Run($"http://0.0.0.0:{port}");
