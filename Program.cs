using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using Microsoft.AspNetCore.Identity; 
using SpaN5.Services;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Controllers with Views & NewtonsoftJson (cho logic backend)
builder.Services.AddControllersWithViews(options =>
    {
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

// DbContext - Ưu tiên Sqlite theo cấu hình hiện tại của HEAD
builder.Services.AddDbContext<SpaDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SpaConnection")));

// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login"; // Mặc định cho khách
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "SpaN5.Auth";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

// PasswordHasher & Services
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
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

// Localization Options
var supportedCultures = new[] { "vi-VN", "en-US" };
var localizationOptions = new RequestLocalizationOptions()
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("vi-VN"),
    SupportedCultures = supportedCultures.Select(c => new System.Globalization.CultureInfo(c)).ToList(),
    SupportedUICultures = supportedCultures.Select(c => new System.Globalization.CultureInfo(c)).ToList()
};
app.UseRequestLocalization(localizationOptions);

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// === ROUTING ===
// 1. Area Staff
app.MapAreaControllerRoute(
    name: "staff",
    areaName: "Staff",
    pattern: "Staff/{controller=Home}/{action=Index}/{id?}");

// 2. Area Admin
app.MapAreaControllerRoute(
    name: "admin",
    areaName: "Admin",
    pattern: "Admin/{controller=Home}/{action=Index}/{id?}");

// 3. Login Route đặc biệt (nếu cần)
app.MapControllerRoute(
    name: "login",
    pattern: "Login",
    defaults: new { area = "Admin", controller = "Auth", action = "Login" });

// 4. Default Route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// === SEED DATA - Khởi tạo dữ liệu mẫu ===
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SpaDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

    // Đảm bảo Database đã sẵn sàng
    context.Database.EnsureCreated();

    // 1. Seed Staff & Admin
    if (!context.Users.Any(u => u.Role == "Admin"))
    {
        var staff = context.Staffs.FirstOrDefault(s => s.FullName == "System Admin");
        if (staff == null)
        {
            staff = new Staff { FullName = "System Admin", Status = "active", Position = "Manager" };
            context.Staffs.Add(staff);
            context.SaveChanges();
        }

        var admin = new User
        {
            Username = "admin",
            Role = "Admin",
            FullName = "Administrator",
            StaffId = staff.StaffId,
            CreatedAt = DateTime.Now
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, "admin123");
        context.Users.Add(admin);
        context.SaveChanges();
    }

    // 2. Seed Services & Categories (từ nhánh Long)
    if (!context.ServiceCategories.Any())
    {
        var cat = new ServiceCategory { Name = "Chăm sóc da & cơ thể" };
        context.ServiceCategories.Add(cat);
        context.SaveChanges();
        
        context.Services.AddRange(
            new Service { ServiceName = "Massage Thư Giãn", Description = "Xua tan căng thẳng cơ bắp bằng liệu pháp massage Thụy Điển kết hợp đá nóng thiên nhiên.", Price = 500000, Duration = 60, IsActive = true, MaxCapacity = 10, Image = "https://images.unsplash.com/photo-1515377905703-c4788e51af15?auto=format&fit=crop&w=600&q=80", CategoryId = cat.Id },
            new Service { ServiceName = "Chăm Sóc Da Mặt", Description = "Thanh lọc và trẻ hóa làn da với tinh chất hoa cúc và serum hữu cơ nhập khẩu từ Pháp.", Price = 700000, Duration = 90, IsActive = true, MaxCapacity = 10, Image = "https://images.unsplash.com/photo-1570172619644-dfd03ed5d881?auto=format&fit=crop&w=600&q=80", CategoryId = cat.Id },
            new Service { ServiceName = "Tắm Thảo Dược", Description = "Đào thải độc tố, kích thích tuần hoàn máu qua liệu trình ngâm mình with 15 loại thảo mộc quý.", Price = 450000, Duration = 45, IsActive = true, MaxCapacity = 10, Image = "https://images.unsplash.com/photo-1519823551278-64ac92734fb1?auto=format&fit=crop&w=600&q=80", CategoryId = cat.Id },
            new Service { ServiceName = "VIP Chăm Sóc Toàn Tâm", Description = "Liệu trình chăm sóc toàn diện cao cấp với sự kết hợp hoàn hảo từ A-Z, mang đến sự thư giãn tuyệt đối cho khách hàng VIP.", Price = 1200000, Duration = 120, IsActive = true, IsVip = true, MaxCapacity = 10, Image = "https://images.unsplash.com/photo-1544161515-4ab6ce6db874?auto=format&fit=crop&w=600&q=80", CategoryId = cat.Id },
            new Service { ServiceName = "VIP Phục Hồi Năng Lượng", Description = "Phục hồi chuyên sâu sinh lực với những nguyên liệu quý giá, kỹ thuật massage bậc thầy đánh thức mọi giác quan.", Price = 1500000, Duration = 150, IsActive = true, IsVip = true, MaxCapacity = 10, Image = "https://images.unsplash.com/photo-1600334089648-f0d9d3d53352?auto=format&fit=crop&w=600&q=80", CategoryId = cat.Id },
            new Service { ServiceName = "VIP Trẻ Hóa Hoàng Gia", Description = "Trải nghiệm liệu trình chống lão hóa đỉnh cao với tinh chất vàng 24K, đem lại diện mạo thanh xuân ngay lần đầu.", Price = 2000000, Duration = 180, IsActive = true, IsVip = true, MaxCapacity = 10, Image = "https://images.unsplash.com/photo-1540555700478-4be289fbecef?auto=format&fit=crop&w=600&q=80", CategoryId = cat.Id }
        );
        context.SaveChanges();
    }
    
    if (!context.Branches.Any())
    {
        context.Branches.Add(new Branch { BranchName = "SpaN5 Premium", Address = "123 Le Loi", City = "HCM", Phone = "012345678" });
        context.SaveChanges();
    }
}

app.Run();