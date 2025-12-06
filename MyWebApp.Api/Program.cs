using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MyWebApp.Data;
using MyWebApp.Services;
using Microsoft.OpenApi.Models;
using MyWebApp.Api.Helpers;
using Microsoft.AspNetCore.Identity;
using MyWebApp.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using MyWebApp.Api.Hubs;


var builder = WebApplication.CreateBuilder(args);

// 1️⃣ Add Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "🎵 SoundAudio REST API",
        Description = "API backend phục vụ dữ liệu nhạc, lời bài hát, hình ảnh và người dùng cho app mobile.",
        Contact = new OpenApiContact
        {
            Name = "Nguyễn Hữu Gia Lâm",
            Email = "lameem2004@gmail.com"
        }
    });

    // ✅ Thêm cấu hình hiển thị nút Authorize (JWT)
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Nhập token theo định dạng: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });

    options.OperationFilter<FileUploadOperationFilter>();
});


// 🔐 JWT + Cookie Authentication (for SignalR)
var jwtSettings = builder.Configuration.GetSection("JwtSettings");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["SecretKey"])
        )
    };
    
    // Allow SignalR to use JWT from query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
})
.AddCookie(options =>
{
    options.Cookie.Name = ".AspNetCore.Identity.Application";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});



// 2️⃣ SQL + Identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();



// 3️⃣ MongoDB
var mongoClient = new MongoClient(builder.Configuration.GetConnectionString("MongoDB"));
var mongoDatabase = mongoClient.GetDatabase("SoundAudioDB");
builder.Services.AddSingleton<IMongoDatabase>(mongoDatabase);

// 4️⃣ Services
builder.Services.AddScoped<MusicService>();
builder.Services.AddScoped<LyricService>();
builder.Services.AddScoped<PlaylistService>();
builder.Services.AddScoped<AlbumService>();
builder.Services.AddScoped<JwtService>();

// 5️⃣ CORS - Allow credentials for cookie authentication
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5255", 
                "https://localhost:5255",
                "https://wyoming-similarly-zoo-chips.trycloudflare.com",
                "https://est-epson-consists-ingredients.trycloudflare.com"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 5️⃣ SignalR
builder.Services.AddSignalR();

var app = builder.Build();

// 6️⃣ Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MusicPlaybackHub>("/hubs/playback");

app.MapGet("/", context =>
{
    context.Response.Redirect("/swagger");
    return Task.CompletedTask;
});

app.Run();
