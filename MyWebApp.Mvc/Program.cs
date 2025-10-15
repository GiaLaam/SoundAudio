using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MyWebApp.Models;
using MyWebApp.Services;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Cookies;
// Add the following using if ApplicationDbContext is in MyWebApp.Data
using MyWebApp.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// ➤ Add SQL Server for Identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


// ➤ Add ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => false;
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie()
.AddGoogle("Google", options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
    options.CallbackPath = "/signin-google";
});
// .AddFacebook("Facebook", options =>
// {
//     options.AppId = builder.Configuration["Authentication:Facebook:AppId"]!;
//     options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"]!;
// });




// ➤ Configure cookie login path
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Home/DangNhap";
    options.AccessDeniedPath = "/Home/DangKy";
});

// ➤ Configure MongoDB (for playlist, music, lyric, etc.)
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017";
var mongoClient = new MongoClient(mongoConnectionString);
var mongoDatabase = mongoClient.GetDatabase("SoundAudioDB");

builder.Services.AddSingleton<IMongoDatabase>(mongoDatabase);

// ➤ Register MongoDB-related services
builder.Services.AddScoped<MusicService>();
// builder.Services.AddScoped<UserService>();        // nếu có xử lý user qua Mongo (cache)
builder.Services.AddScoped<PlaylistService>();
builder.Services.AddScoped<LyricService>();

// ➤ Configure session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// using (var scope = app.Services.CreateScope())
// {
//     var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

//     foreach (var role in SD.Roles)
//     {
//         if (!await roleManager.RoleExistsAsync(role))
//         {
//             await roleManager.CreateAsync(new IdentityRole(role));
//             Console.WriteLine($"✅ Đã tạo vai trò: {role}");
//         }
//     }
// }


// ➤ HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseCookiePolicy();
app.UseAuthentication();  // ✨ Identity
app.UseAuthorization();

// ➤ Routing
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
