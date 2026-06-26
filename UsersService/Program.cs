using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using sib_api_v3_sdk.Client;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using UsersService.Configuration;
using UsersService.DbContext;
using UsersService.IdentityConfig;
using UsersService.Interfaces;
using UsersService.Models.Entities;
using UsersService.Services;
var isService = !(Debugger.IsAttached || args.Contains("--console"));

var options = new WebApplicationOptions
{
    ContentRootPath = isService
        ? Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!
        : Directory.GetCurrentDirectory(),
    Args = args
};

var builder = WebApplication.CreateBuilder(options);

// ------------------- Logging & Configuration -------------------
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddEventSourceLogger();

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddAutoMapper(typeof(Program).Assembly);
builder.Services.AddHttpClient();

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});



// ------------------- Forwarded Headers (behind Nginx) -------------------
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                         ForwardedHeaders.XForwardedProto |
                         ForwardedHeaders.XForwardedHost;
    o.KnownProxies.Clear();
    o.KnownNetworks.Clear();
});

// ------------------- Localization (robust defaults) -------------------
var defaultCulture = new CultureInfo("en-US");
var supportedCultures = new[]
{
    new CultureInfo("en-US"),
    new CultureInfo("bs-BA"),
    new CultureInfo("hr-HR"),
    new CultureInfo("de-DE"),
};

CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;


// ------------------- Options binding + validation (fail fast) -------------------
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection("JwtSettings"))
    .Validate(s =>
        !string.IsNullOrWhiteSpace(s.Audience) ||
        (s.Audiences?.Length ?? 0) > 0,
        "JwtSettings:Audience or JwtSettings:Audiences is required.")
    .ValidateOnStart();

builder.Services.AddOptions<JwtKeyOptions>()
    .Bind(builder.Configuration.GetSection("JwtKeys"))
    .ValidateOnStart();

builder.Services.AddOptions<CorsOptions>()
    .Bind(builder.Configuration.GetSection("Cors"))
    .Validate(c => c.AllowedOrigins.Length > 0,
        "Cors:AllowedOrigins must contain at least one origin for cross-origin requests to work.")
     .ValidateOnStart();

builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<JwtSettings>>().Value);

var applicationSettings = new ApplicationSettings();
builder.Configuration.GetSection("ApplicationSettings").Bind(applicationSettings);
builder.Services.AddSingleton(applicationSettings);

// ------------------- Brevo -------------------
var brevoApiKey =
    Environment.GetEnvironmentVariable("BREVO_API_KEY")
    ?? builder.Configuration["BrevoApi:ApiKey"];

if (!string.IsNullOrWhiteSpace(brevoApiKey))
{
    Configuration.Default.ApiKey["api-key"] = brevoApiKey;
}

// ------------------- MVC / JSON -------------------
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();

// ------------------- Swagger -------------------
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "User Service API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer 12345abcdef'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                Scheme = "Bearer",
                Name = "Authorization",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
});

// ------------------- Database -------------------
var connectionString = Environment.GetEnvironmentVariable("USERS_DB_CONNECTION_STRING")
                       ?? builder.Configuration.GetConnectionString("UsersDb");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "UsersDb connection string not configured. Set USERS_DB_CONNECTION_STRING or ConnectionStrings:UsersDb.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.ConfigureWarnings(w =>
        w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});


// ------------------- Identity -------------------

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-@._+";
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddRoles<ApplicationRole>();

// ------------------- Services -------------------
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<IEmailBuilder, EmailBuilder>();
builder.Services.AddScoped<ITokenManager, TokenManager>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUtilityService, UtilityService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IUserRoleCache, UserRoleCache>();


// ------------------- RSA Keys (ENV or file paths or config PEM) -------------------
static string? ReadPemFile(string? path)
{
    if (string.IsNullOrWhiteSpace(path)) return null;
    return File.Exists(path) ? File.ReadAllText(path) : null;
}

var jwtKeyOptions = builder.Configuration.GetSection("JwtKeys").Get<JwtKeyOptions>() ?? new JwtKeyOptions();

var privateKeyPem =
    Environment.GetEnvironmentVariable("JWT_PRIVATE_KEY")
    ?? (string.IsNullOrWhiteSpace(jwtKeyOptions.PrivateKeyPem) ? null : jwtKeyOptions.PrivateKeyPem)
    ?? ReadPemFile(jwtKeyOptions.PrivateKeyPath)
    ?? throw new InvalidOperationException(
        $"JWT private key not configured. Checked: JWT_PRIVATE_KEY env var, JwtKeys:PrivateKeyPem config, " +
        $"JwtKeys:PrivateKeyPath='{jwtKeyOptions.PrivateKeyPath ?? "(not set)"}'.");

var publicKeyPem =
    Environment.GetEnvironmentVariable("JWT_PUBLIC_KEY")
    ?? (string.IsNullOrWhiteSpace(jwtKeyOptions.PublicKeyPem) ? null : jwtKeyOptions.PublicKeyPem)
    ?? ReadPemFile(jwtKeyOptions.PublicKeyPath)
    ?? throw new InvalidOperationException("JWT public key not configured. Set JWT_PUBLIC_KEY or JwtKeys settings.");

// Load private key (IdentityServer signing)
RSA rsaPrivate = RSA.Create();
rsaPrivate.ImportFromPem(privateKeyPem);
var rsaPrivateKey = new RsaSecurityKey(rsaPrivate);

// Load public key (JWT validation)
RSA rsaPublic = RSA.Create();
rsaPublic.ImportFromPem(publicKeyPem);
var rsaPublicKey = new RsaSecurityKey(rsaPublic);

// Register signing key
builder.Services.AddSingleton(rsaPrivateKey);
builder.Services.AddSingleton(new SigningCredentials(rsaPrivateKey, SecurityAlgorithms.RsaSha256));

// ------------------- IdentityServer -------------------
builder.Services.AddIdentityServer(options =>
{
    options.IssuerUri = builder.Configuration["JwtSettings:Issuer"];
})
.AddSigningCredential(rsaPrivateKey, SecurityAlgorithms.RsaSha256)
.AddInMemoryApiScopes(Config.GetApiScopes(builder.Configuration))
.AddInMemoryApiResources(Config.GetApiResources(builder.Configuration))
.AddInMemoryClients(Config.GetClients(builder.Configuration))
.AddInMemoryIdentityResources(Config.GetIdentityResources())
.AddExtensionGrantValidator<DynamicEmailExtensionGrantValidator>()
.AddProfileService<CustomProfileService>();

// ------------------- Authentication -------------------
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()
    ?? throw new InvalidOperationException("JwtSettings section is missing.");


var validIssuers = (jwtSettings.Issuers?.Length ?? 0) > 0
    ? jwtSettings.Issuers
    : new[] { jwtSettings.Issuer };

var validAudiences = (jwtSettings.Audiences?.Length ?? 0) > 0
    ? jwtSettings.Audiences
    : (!string.IsNullOrWhiteSpace(jwtSettings.Audience) ? new[] { jwtSettings.Audience } : Array.Empty<string>());

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; 
    options.SaveToken = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = rsaPublicKey,

        ValidateIssuer = true,
        ValidIssuers = validIssuers,

        ValidateAudience = true,
        ValidAudiences = validAudiences,

        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// ------------------- Authorization -------------------
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"));
});

// ------------------- CORS (config-driven) -------------------
var corsCfg = builder.Configuration.GetSection("Cors").Get<CorsOptions>() ?? new CorsOptions();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCors", policy =>
    {
        if (corsCfg.AllowedOrigins.Length > 0)
        {
            policy.WithOrigins(corsCfg.AllowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
            .AllowCredentials();
        }
    });
});

var app = builder.Build();

// -------------------Startup diagnostics(DELETE IT LATER)------------------ -
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
var privateKeyPathExists = !string.IsNullOrWhiteSpace(jwtKeyOptions.PrivateKeyPath) &&
    File.Exists(jwtKeyOptions.PrivateKeyPath);
var publicKeyPathExists = !string.IsNullOrWhiteSpace(jwtKeyOptions.PublicKeyPath) &&
    File.Exists(jwtKeyOptions.PublicKeyPath);

startupLogger.LogInformation("Startup check — UsersDb connection string present: {Present}",
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("USERS_DB_CONNECTION_STRING")) ||
    !string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("UsersDb")));

startupLogger.LogInformation("JWT_PRIVATE_KEY present: {Present}",
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JWT_PRIVATE_KEY")) ||
    privateKeyPathExists ||
    !string.IsNullOrWhiteSpace(jwtKeyOptions.PrivateKeyPath) ||
    !string.IsNullOrWhiteSpace(jwtKeyOptions.PrivateKeyPem));

startupLogger.LogInformation("JWT_PUBLIC_KEY present: {Present}",
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JWT_PUBLIC_KEY")) ||
    publicKeyPathExists ||
    !string.IsNullOrWhiteSpace(jwtKeyOptions.PublicKeyPath) ||
    !string.IsNullOrWhiteSpace(jwtKeyOptions.PublicKeyPem));



// ------------------- DB Migrations -------------------
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
    
        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                ""MigrationId"" character varying(150) NOT NULL,
                ""ProductVersion"" character varying(32) NOT NULL,
                CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY (""MigrationId"")
            )");

        var alreadyApplied = context.Database
            .SqlQueryRaw<int>(@"SELECT COUNT(*)::int AS ""Value"" FROM ""__EFMigrationsHistory"" WHERE ""MigrationId"" = '20260206171126_InitialClean'")
            .Single();

        if (alreadyApplied == 0)
        {
            
            var tablesExist = context.Database
                .SqlQueryRaw<int>(@"SELECT COUNT(*)::int AS ""Value"" FROM information_schema.tables WHERE table_name = 'Organizations'")
                .Single() > 0;

            if (tablesExist)
            {
                context.Database.ExecuteSqlRaw(
                    "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('20260206171126_InitialClean', '10.0.0')");
                logger.LogInformation("Baselined InitialClean migration — tables already existed.");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Baseline check failed (non-fatal, proceeding with migration).");
    }

    for (int i = 0; i < 10; i++)
    {
        try
        {
            logger.LogInformation("Attempting database migration (attempt {Attempt}/10)...", i + 1);
            context.Database.Migrate();
            logger.LogInformation("Database migration completed successfully.");
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Migration attempt {Attempt}/10 failed. Retrying in 3 seconds...", i + 1);
            if (i == 9) logger.LogError(ex, "All migration attempts failed.");
            Thread.Sleep(3000);
        }
    }
}

// ------------------- Seed Admin User -------------------
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (await roleManager.FindByNameAsync("CBrainUser") == null)
        {
            var roleResult = await roleManager.CreateAsync(new ApplicationRole { Name = "CBrainUser", NormalizedName = "CBRAINUSER" });
            if (roleResult.Succeeded)
                logger.LogInformation("Seeded role: CBrainUser");
            else
                logger.LogError("Failed to seed CBrainUser role: {Errors}",
                    string.Join(", ", roleResult.Errors.Select(e => e.Description)));
        }

        var org = dbContext.Organizations.FirstOrDefault(o => o.Name == "C-Brain");
        if (org == null)
        {
            org = new Organization
            {
                Name = "C-Brain",
                Email = "admin@c-brain.org",
                Description = "C-Brain Platform",
                Address = "",
                City = "",
                Country = "",
                Phone = "",
                Website = "",
                Logo = ""
            };
            dbContext.Organizations.Add(org);
            dbContext.SaveChanges();
            logger.LogInformation("Seeded organization: C-Brain (Id={OrgId})", org.Id);
        }

        var adminEmail =
            Environment.GetEnvironmentVariable("ADMIN_EMAIL")
            ?? app.Configuration["AdminUser:Email"];

        var adminPassword =
            Environment.GetEnvironmentVariable("ADMIN_PASSWORD")
            ?? app.Configuration["AdminUser:Password"];

        if (string.IsNullOrWhiteSpace(adminEmail) ||
            string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning(
                "Admin seed skipped because ADMIN_EMAIL or ADMIN_PASSWORD is not configured.");

            return;
        }
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "C-Brain",
                LastName = "Admin",
                EmailConfirmed = true,
                IsUsingTemporaryPassword = false,
                OrganizationId = org.Id
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger.LogInformation("Seeded admin user: {Email}", adminEmail);
            }
            else
            {
                logger.LogError("Failed to seed admin user: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            bool updated = false;
            if (!adminUser.EmailConfirmed)
            {
                adminUser.EmailConfirmed = true;
                updated = true;
            }
            if (updated)
            {
                await userManager.UpdateAsync(adminUser);
            }

            var resetToken = await userManager.GeneratePasswordResetTokenAsync(adminUser);
            var resetResult = await userManager.ResetPasswordAsync(adminUser, resetToken, adminPassword);
            if (resetResult.Succeeded)
            {
                logger.LogInformation("Admin password reset to default for: {Email}", adminEmail);
            }

            if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger.LogInformation("Added Admin role to: {Email}", adminEmail);
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding admin user.");
    }
}

// ------------------- Middleware Order -------------------
app.UseForwardedHeaders();
app.UseSerilogRequestLogging();

app.UseRouting();

app.UseCors("DefaultCors");

app.Use(async (context, next) =>
{
    if (context.Request.Method == HttpMethods.Options)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        return;
    }
    await next();
});

app.UseIdentityServer();
app.UseAuthentication();
app.UseAuthorization();

app.UseHttpsRedirection();

// Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "User Service API v1");
    c.RoutePrefix = "swagger";
});

app.MapControllers();

app.Lifetime.ApplicationStopping.Register(Log.CloseAndFlush);

app.Run();

