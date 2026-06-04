using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartKitchen.API.Data;
using SmartKitchen.API.Extensions;
using SmartKitchen.API.Hubs;
using SmartKitchen.API.Models;
using SmartKitchen.API.Options;
using SmartKitchen.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure();
        }
    ));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

var jwtSection = builder.Configuration.GetSection("Jwt");

var jwtKey = Encoding.UTF8.GetBytes(jwtSection["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;

    options.SaveToken = true;

    options.TokenValidationParameters =
        new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],

            IssuerSigningKey =
                new SymmetricSecurityKey(jwtKey),

            ClockSkew = TimeSpan.Zero
        };
});

builder.Services.AddAuthorization();

builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

builder.Services.AddScoped<IRecipeService, RecipeService>();

builder.Services.AddImageSearchService(builder.Configuration);

builder.Services.AddSingleton<IImageUrlBuilder, ImageUrlBuilder>();

builder.Services.AddScoped<IFavoriteService, FavoriteService>();

builder.Services.AddScoped<ISearchService, SearchService>();

builder.Services.AddScoped<IChatbotService, ChatbotService>();

builder.Services.AddScoped<IOtpService, OtpService>();

builder.Services.AddScoped<IDetectionService, DetectionService>();

builder.Services.AddSingleton<OnnxPredictionService>();

builder.Services.AddScoped<IAlertService, AlertService>();

// ✅ ده السطر اللي كان ناقص
builder.Services.AddScoped<EmailService>();

builder.Services.AddScoped<IDatasetIngestionService, DatasetIngestionService>();

builder.Services.AddScoped<IRecommendationService, RecommendationService>();

builder.Services.AddScoped<IMediaStorageService, MediaStorageService>();

builder.Services.AddScoped<IChatContextService, ChatContextService>();



builder.Services.AddScoped<JwtTokenService>();

builder.Services.AddScoped<DataSeeder>();

builder.Services.Configure<DatasetOptions>(
    builder.Configuration.GetSection("Datasets"));

builder.Services.Configure<MediaStorageOptions>(
    builder.Configuration.GetSection("MediaStorage"));

builder.Services.Configure<ImageSettingsOptions>(
    builder.Configuration.GetSection("ImageSettings"));

builder.Services.Configure<FormOptions>(options =>
{
    const long defaultMaxUploadBytes = 60_000_000;

    var max =
        builder.Configuration.GetValue<long>(
            "MediaStorage:MaxUploadBytes",
            defaultMaxUploadBytes);

    options.MultipartBodyLengthLimit = max;
});

builder.Services.AddSignalR();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1",
        new OpenApiInfo
        {
            Title = "SmartKitchen API",
            Version = "v1",
            Description = "Smart Kitchen Backend API"
        });

    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter: Bearer {your token}"
        });

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference =
                        new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                },

                Array.Empty<string>()
            }
        });
});

var app = builder.Build();

app.UseSwagger();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "/swagger/v1/swagger.json",
        "SmartKitchen API V1");

    options.RoutePrefix = "swagger";
});

app.MapGet("/", () => Results.Redirect("/swagger"));

var webRoot =
    app.Environment.WebRootPath ??
    Path.Combine(
        app.Environment.ContentRootPath,
        "wwwroot"
    );

app.Environment.WebRootPath = webRoot;

Directory.CreateDirectory(
    Path.Combine(webRoot, "uploads", "images"));

Directory.CreateDirectory(
    Path.Combine(webRoot, "uploads", "videos"));

Directory.CreateDirectory(
    Path.Combine(webRoot, "uploads", "files"));

var smartKitchenImageSettings =
    builder.Configuration
        .GetSection("ImageSettings")
        .Get<ImageSettingsOptions>() ?? new ImageSettingsOptions();

var smartKitchenImageFolders = new Dictionary<string, string>
{
    ["/uploads/recipe"] =
    Path.Combine(smartKitchenImageSettings.StorageRoot, "recipe"),

    ["/uploads/ingredient"] =
        Path.Combine(smartKitchenImageSettings.StorageRoot, "ingredient"),

    ["/uploads/step"] =
        Path.Combine(smartKitchenImageSettings.StorageRoot, "step")
};

foreach (var folder in smartKitchenImageFolders.Values)
{
    Directory.CreateDirectory(folder);
}

var forwardedHeadersOptions =
    new ForwardedHeadersOptions
    {
        ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto
    };

if (builder.Configuration.GetValue(
        "ForwardedHeaders:AllowAll",
        false))
{
    forwardedHeadersOptions.KnownNetworks.Clear();

    forwardedHeadersOptions.KnownProxies.Clear();
}
else
{
    var proxies =
        builder.Configuration
            .GetSection("ForwardedHeaders:KnownProxies")
            .Get<string[]>() ?? [];

    foreach (var proxy in proxies)
    {
        if (System.Net.IPAddress.TryParse(
                proxy,
                out var address))
        {
            forwardedHeadersOptions
                .KnownProxies
                .Add(address);
        }
    }

    var networks =
        builder.Configuration
            .GetSection("ForwardedHeaders:KnownNetworks")
            .Get<string[]>() ?? [];

    foreach (var network in networks)
    {
        var parts = network.Split('/');

        if (parts.Length == 2 &&
            System.Net.IPAddress.TryParse(
                parts[0],
                out var address) &&
            int.TryParse(
                parts[1],
                out var prefix))
        {
            forwardedHeadersOptions
                .KnownNetworks
                .Add(new IPNetwork(address, prefix));
        }
    }
}

app.UseForwardedHeaders(forwardedHeadersOptions);

var contentTypeProvider =
    new FileExtensionContentTypeProvider();

contentTypeProvider.Mappings[".webp"] =
    "image/webp";

contentTypeProvider.Mappings[".mov"] =
    "video/quicktime";

contentTypeProvider.Mappings[".avi"] =
    "video/x-msvideo";

contentTypeProvider.Mappings[".docx"] =
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider
});

foreach (var (requestPath, physicalPath) in smartKitchenImageFolders)
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(physicalPath),
        RequestPath = requestPath,
        ContentTypeProvider = contentTypeProvider
    });
}

app.UseCors("AllowAll");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapHub<SafetyHub>("/hubs/safety");

// لو عندك Seeder سيبيه متكومنت
/*
using (var scope = app.Services.CreateScope())
{
    var seeder =
        scope.ServiceProvider
            .GetRequiredService<DataSeeder>();

    var filePath =
        Path.Combine(
            Directory.GetCurrentDirectory(),
            "SMART_RECIPES_WITH_IMAGES.csv"
        );

    if (File.Exists(filePath))
    {
        await seeder.SeedAsync(filePath);
    }
}
*/
var path = Path.Combine(
    smartKitchenImageSettings.StorageRoot,
    "recipe");

Console.WriteLine(path);
Console.WriteLine(Directory.Exists(path));
app.Run();
