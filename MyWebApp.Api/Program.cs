using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using MyWebApp.Data;
using MyWebApp.Services;
using Microsoft.OpenApi.Models;
using MyWebApp.Api.Helpers;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------
// 1️⃣ Cấu hình Controller + Swagger
// ---------------------------
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

    options.OperationFilter<FileUploadOperationFilter>();
});

// ---------------------------
// 2️⃣ Kết nối SQL Server (nếu bạn dùng Identity/User)
// ---------------------------
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------------------------
// 3️⃣ Kết nối MongoDB + GridFS
// ---------------------------
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017";
var mongoClient = new MongoClient(mongoConnectionString);
var mongoDatabase = mongoClient.GetDatabase("SoundAudioDB");

Console.WriteLine($"✅ Connected to MongoDB: {mongoDatabase.DatabaseNamespace.DatabaseName}");

builder.Services.AddSingleton<IMongoDatabase>(mongoDatabase);

// ---------------------------
// 4️⃣ Đăng ký các Service
// ---------------------------
builder.Services.AddScoped<MusicService>();
builder.Services.AddScoped<LyricService>();
builder.Services.AddScoped<PlaylistService>();

// ---------------------------
// 5️⃣ Cấu hình CORS
// ---------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ---------------------------
// 6️⃣ Xây dựng app
// ---------------------------
var app = builder.Build();

// ---------------------------
// 7️⃣ Middleware pipeline
// ---------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "🎵 SoundAudio API v1");
        options.DocumentTitle = "SoundAudio API Explorer";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Nếu có wwwroot
app.UseRouting();

app.UseCors("AllowAll");

app.UseAuthentication(); // nếu bạn có dùng Identity
app.UseAuthorization();

app.MapControllers();

// ✅ Redirect từ "/" → "/swagger"
app.MapGet("/", context =>
{
    context.Response.Redirect("/swagger");
    return Task.CompletedTask;
});

// ---------------------------
// 8️⃣ Chạy ứng dụng
// ---------------------------
app.Run();
