using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using Microsoft.AspNetCore.Identity; // cho IPasswordHasher
using SpaN5.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// DbContext
builder.Services.AddDbContext<SpaDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SpaConnection")));

// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

// PasswordHasher
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// HttpContextAccessor (để dùng trong View)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditService>();

var app = builder.Build();


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // ← thêm
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SpaDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

    // Tạo admin nếu chưa có
    if (!context.Users.Any(u => u.Role == "Admin"))
    {
        var admin = new User
        {
            Username = "admin",
            Role = "Admin",
            FullName = "Administrator",
            CreatedAt = DateTime.Now
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, "admin123");
        context.Users.Add(admin);
        context.SaveChanges();
    }
}

app.Run();