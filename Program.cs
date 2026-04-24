using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using Microsoft.AspNetCore.Identity; 
using SpaN5.Services;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews(options => { options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true; })
    .AddViewLocalization().AddDataAnnotationsLocalization();

builder.Services.AddDbContext<SpaDbContext>(options => options.UseSqlite(builder.Configuration.GetConnectionString("SpaConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "SpaN5.Auth";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IAIntelligenceService, AIntelligenceService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseHttpsRedirection();
app.UseStaticFiles();

var supportedCultures = new[] { "vi-VN", "en-US" };
var localizationOptions = new RequestLocalizationOptions() {
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("vi-VN"),
    SupportedCultures = supportedCultures.Select(c => new System.Globalization.CultureInfo(c)).ToList(),
    SupportedUICultures = supportedCultures.Select(c => new System.Globalization.CultureInfo(c)).ToList()
};
app.UseRequestLocalization(localizationOptions);
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapAreaControllerRoute(name: "staff", areaName: "Staff", pattern: "Staff/{controller=Home}/{action=Index}/{id?}");
app.MapAreaControllerRoute(name: "admin", areaName: "Admin", pattern: "Admin/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(name: "login", pattern: "Login", defaults: new { area = "Admin", controller = "Auth", action = "Login" });
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope()) {
    var context = scope.ServiceProvider.GetRequiredService<SpaDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    context.Database.EnsureCreated();
    void SafeExecuteSql(string sql) { try { context.Database.ExecuteSqlRaw(sql); } catch { } }
    try {
        var materialCols = context.Database.SqlQueryRaw<string>("SELECT name FROM pragma_table_info('Materials')").ToList();
        if (!materialCols.Contains("RowVersion")) SafeExecuteSql("ALTER TABLE Materials ADD COLUMN RowVersion BLOB");
        if (!materialCols.Contains("SupplierId")) SafeExecuteSql("ALTER TABLE Materials ADD COLUMN SupplierId INTEGER");
        var smCols = context.Database.SqlQueryRaw<string>("SELECT name FROM pragma_table_info('ServiceMaterials')").ToList();
        if (!smCols.Contains("ServiceStepId")) SafeExecuteSql("ALTER TABLE ServiceMaterials ADD COLUMN ServiceStepId INTEGER");

        var noteCols = context.Database.SqlQueryRaw<string>("SELECT name FROM pragma_table_info('CustomerNotes')").ToList();
        if (!noteCols.Contains("SkinType")) SafeExecuteSql("ALTER TABLE CustomerNotes ADD COLUMN SkinType TEXT");
        if (!noteCols.Contains("ImprovementStatus")) SafeExecuteSql("ALTER TABLE CustomerNotes ADD COLUMN ImprovementStatus TEXT");
        if (!noteCols.Contains("RecommendedService")) SafeExecuteSql("ALTER TABLE CustomerNotes ADD COLUMN RecommendedService TEXT");
        if (!noteCols.Contains("NextServiceAfterDays")) SafeExecuteSql("ALTER TABLE CustomerNotes ADD COLUMN NextServiceAfterDays INTEGER");
        if (!noteCols.Contains("InternalNote")) SafeExecuteSql("ALTER TABLE CustomerNotes ADD COLUMN InternalNote TEXT");
    } catch { }

    string[] tableCmds = {
        "CREATE TABLE IF NOT EXISTS Shifts (ShiftId INTEGER PRIMARY KEY AUTOINCREMENT, ShiftName TEXT NOT NULL, StartTime TEXT NOT NULL, EndTime TEXT NOT NULL, IsActive INTEGER NOT NULL DEFAULT 1)",
        "CREATE TABLE IF NOT EXISTS StaffSchedules (Id INTEGER PRIMARY KEY AUTOINCREMENT, StaffId INTEGER NOT NULL, DayOfWeek INTEGER NOT NULL, ShiftId INTEGER NOT NULL, IsOff INTEGER NOT NULL DEFAULT 0)",
        "CREATE TABLE IF NOT EXISTS WorkSchedules (Id INTEGER PRIMARY KEY AUTOINCREMENT, StaffId INTEGER NOT NULL, Date TEXT NOT NULL, ShiftId INTEGER NULL, Status INTEGER NOT NULL DEFAULT 0, Note TEXT NULL)",
        "CREATE TABLE IF NOT EXISTS LeaveRequests (Id INTEGER PRIMARY KEY AUTOINCREMENT, StaffId INTEGER NOT NULL, FromDate TEXT NOT NULL, ToDate TEXT NOT NULL, Reason TEXT NOT NULL, Status INTEGER NOT NULL DEFAULT 0, CreatedAt TEXT NOT NULL, AdminNote TEXT NULL)",
        "CREATE TABLE IF NOT EXISTS Suppliers (SupplierId INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, ContactName TEXT NULL, Phone TEXT NULL, Email TEXT NULL, Address TEXT NULL, TaxCode TEXT NULL, Note TEXT NULL, IsActive INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS MaterialBatches (BatchId INTEGER PRIMARY KEY AUTOINCREMENT, MaterialId INTEGER NOT NULL, BatchCode TEXT NOT NULL, ManufactureDate TEXT NULL, ExpiryDate TEXT NOT NULL, OriginalQuantity INTEGER NOT NULL, CurrentQuantity INTEGER NOT NULL, UnitCost DECIMAL NOT NULL, ReceivedDate TEXT NOT NULL, IsActive INTEGER NOT NULL DEFAULT 1)",
        "CREATE TABLE IF NOT EXISTS MaterialConversions (Id INTEGER PRIMARY KEY AUTOINCREMENT, MaterialId INTEGER NOT NULL, FromUnit TEXT NOT NULL, ToUnit TEXT NOT NULL DEFAULT 'gram', Ratio REAL NOT NULL, Note TEXT NULL)",
        "CREATE TABLE IF NOT EXISTS MaterialConsumptions (Id INTEGER PRIMARY KEY AUTOINCREMENT, BookingDetailId INTEGER NOT NULL, MaterialId INTEGER NOT NULL, StaffId INTEGER NOT NULL, StandardQuantity REAL NOT NULL, ActualQuantity REAL NOT NULL, BatchId INTEGER NULL, Note TEXT NULL, CreatedAt TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS ServiceSteps (Id INTEGER PRIMARY KEY AUTOINCREMENT, ServiceId INTEGER NOT NULL, StepName TEXT NOT NULL, [Order] INTEGER NOT NULL, Duration INTEGER NULL)",
        "CREATE TABLE IF NOT EXISTS PurchaseOrders (POId INTEGER PRIMARY KEY AUTOINCREMENT, POCode TEXT NOT NULL, SupplierId INTEGER NOT NULL, OrderDate TEXT NOT NULL, ExpectedDate TEXT NULL, Status INTEGER NOT NULL DEFAULT 0, TotalAmount DECIMAL NOT NULL, Note TEXT NULL, ApprovedById INTEGER NULL)",
        "CREATE TABLE IF NOT EXISTS PurchaseOrderDetails (Id INTEGER PRIMARY KEY AUTOINCREMENT, POId INTEGER NOT NULL, MaterialId INTEGER NOT NULL, Quantity INTEGER NOT NULL, UnitPrice DECIMAL NOT NULL, TotalPrice DECIMAL NOT NULL, Note TEXT NULL)"
    };
    foreach (var cmd in tableCmds) SafeExecuteSql(cmd);

    if (!context.Shifts.Any()) { context.Shifts.AddRange(new Shift { ShiftName = "Ca Sáng", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(14, 0, 0) }, new Shift { ShiftName = "Ca Chiều", StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(20, 0, 0) }); context.SaveChanges(); }
    if (!context.Users.Any(u => u.Role == "Admin")) {
        var staff = context.Staffs.FirstOrDefault(s => s.FullName == "System Admin") ?? new Staff { FullName = "System Admin", Status = "active", Position = "Manager" };
        if (staff.StaffId == 0) { context.Staffs.Add(staff); context.SaveChanges(); }
        var admin = new User { Username = "admin", Role = "Admin", FullName = "Administrator", StaffId = staff.StaffId, CreatedAt = DateTime.Now };
        admin.PasswordHash = passwordHasher.HashPassword(admin, "admin123");
        context.Users.Add(admin); context.SaveChanges();
    }
    if (!context.ServiceCategories.Any()) {
        var cat = new ServiceCategory { Name = "Chăm sóc da & cơ thể" }; context.ServiceCategories.Add(cat); context.SaveChanges();
        context.Services.AddRange(
            new Service { ServiceName = "Massage Thư Giãn", Description = "Massage Thụy Điển kết hợp đá nóng.", Price = 500000, Duration = 60, CategoryId = cat.Id, Image = "https://images.unsplash.com/photo-1515377905703-c4788e51af15?w=600" },
            new Service { ServiceName = "Chăm Sóc Da Mặt", Description = "Thanh lọc da với tinh chất hoa cúc.", Price = 700000, Duration = 90, CategoryId = cat.Id, Image = "https://images.unsplash.com/photo-1570172619644-dfd03ed5d881?w=600" },
            new Service { ServiceName = "Tắm Thảo Dược", Description = "Đào thải độc tố qua liệu trình ngâm mình.", Price = 450000, Duration = 45, CategoryId = cat.Id, Image = "https://images.unsplash.com/photo-1519823551278-64ac92734fb1?w=600" },
            new Service { ServiceName = "VIP Chăm Sóc Toàn Tâm", Description = "Liệu trình cao cấp kết hợp từ A-Z.", Price = 1200000, Duration = 120, IsVip = true, CategoryId = cat.Id, Image = "https://images.unsplash.com/photo-1544161515-4ab6ce6db874?w=600" },
            new Service { ServiceName = "VIP Phục Hồi Năng Lượng", Description = "Phục hồi sinh lực với nguyên liệu quý giá.", Price = 1500000, Duration = 150, IsVip = true, CategoryId = cat.Id, Image = "https://images.unsplash.com/photo-1600334089648-f0d9d3d53352?w=600" },
            new Service { ServiceName = "VIP Trẻ Hóa Hoàng Gia", Description = "Trải nghiệm liệu trình tinh chất vàng 24K.", Price = 2000000, Duration = 180, IsVip = true, CategoryId = cat.Id, Image = "https://images.unsplash.com/photo-1540555700478-4be289fbecef?w=600" }
        );
        context.SaveChanges();
    }

    if (!context.Suppliers.Any()) {
        context.Suppliers.Add(new Supplier { Name = "Tổng kho Vật tư Spa Azure", ContactName = "Lê Hồng Sơn", Phone = "0988-777-666", Email = "supply@azure-spa.vn", Address = "123 Đường Công Nghệ, Hà Nội", CreatedAt = DateTime.Now });
        context.SaveChanges();
    }
    var supplier = context.Suppliers.First();

    if (context.Materials.Count() < 10) {
        var mats = new List<Material> {
            new Material { MaterialName = "Tinh dầu Oải hương", Unit = "ml", CurrentStock = 1000, MinStock = 100, PurchasePrice = 500, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Tinh dầu Cam ngọt", Unit = "ml", CurrentStock = 1000, MinStock = 100, PurchasePrice = 450, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Tinh sả chanh", Unit = "ml", CurrentStock = 1000, MinStock = 100, PurchasePrice = 300, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Serum Vitamin C", Unit = "ml", CurrentStock = 500, MinStock = 50, PurchasePrice = 2500, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Serum Collagen Tươi", Unit = "ml", CurrentStock = 300, MinStock = 30, PurchasePrice = 4500, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Serum Hyaluronic Acid", Unit = "ml", CurrentStock = 400, MinStock = 40, PurchasePrice = 3500, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Mặt nạ Cánh hoa hồng", Unit = "miếng", CurrentStock = 100, MinStock = 10, PurchasePrice = 1500, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Mặt nạ Bùn khoáng", Unit = "g", CurrentStock = 2000, MinStock = 200, PurchasePrice = 20, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Tẩy tế bào chết Cà phê", Unit = "g", CurrentStock = 5000, MinStock = 500, PurchasePrice = 15, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Kem dưỡng Body Bơ", Unit = "ml", CurrentStock = 2000, MinStock = 200, PurchasePrice = 120, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Nước hoa hồng (Toner)", Unit = "ml", CurrentStock = 1500, MinStock = 150, PurchasePrice = 80, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Sữa rửa mặt Thảo mộc", Unit = "ml", CurrentStock = 1000, MinStock = 100, PurchasePrice = 150, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Dầu dừa tinh khiết", Unit = "ml", CurrentStock = 3000, MinStock = 300, PurchasePrice = 60, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Khăn bông cao cấp", Unit = "cái", CurrentStock = 200, MinStock = 20, PurchasePrice = 4500, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Nến lá dứa thư giãn", Unit = "cái", CurrentStock = 50, MinStock = 5, PurchasePrice = 3000, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Đá muối Hi-ma-lay-a", Unit = "viên", CurrentStock = 80, MinStock = 10, PurchasePrice = 12000, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Bột ngọc trai nguyên chất", Unit = "g", CurrentStock = 500, MinStock = 50, PurchasePrice = 850, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Tinh chất Tổ yến", Unit = "ml", CurrentStock = 100, MinStock = 10, PurchasePrice = 15000, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Kem massage Trà xanh", Unit = "g", CurrentStock = 2500, MinStock = 200, PurchasePrice = 45, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Sáp Waxing mật ong", Unit = "ml", CurrentStock = 1000, MinStock = 100, PurchasePrice = 250, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Bông tẩy trang", Unit = "gói", CurrentStock = 100, MinStock = 10, PurchasePrice = 3500, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Mặt nạ Tinh chất Vàng", Unit = "tuýp", CurrentStock = 30, MinStock = 5, PurchasePrice = 65000, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Sữa tắm cánh hoa", Unit = "ml", CurrentStock = 2000, MinStock = 200, PurchasePrice = 95, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Dầu Gội Bồ Kết", Unit = "ml", CurrentStock = 2000, MinStock = 200, PurchasePrice = 85, SupplierId = supplier.SupplierId },
            new Material { MaterialName = "Dầu Xả Bưởi rừng", Unit = "ml", CurrentStock = 2000, MinStock = 200, PurchasePrice = 90, SupplierId = supplier.SupplierId }
        };
        foreach(var m in mats) { if (!context.Materials.Any(existing => existing.MaterialName == m.MaterialName)) context.Materials.Add(m); }
        context.SaveChanges();
    }

    var allMaterials = context.Materials.ToList();
    var allServices = context.Services.ToList();
    foreach (var s in allServices) {
        if (context.ServiceMaterials.Count(sm => sm.ServiceId == s.ServiceId) > 2) continue;
        var r = new Random();
        var selectedMats = allMaterials.OrderBy(x => Guid.NewGuid()).Take(8).ToList();
        foreach (var m in selectedMats) {
            if (!context.ServiceMaterials.Any(sm => sm.ServiceId == s.ServiceId && sm.MaterialId == m.MaterialId))
                context.ServiceMaterials.Add(new ServiceMaterial { ServiceId = s.ServiceId, MaterialId = m.MaterialId, Quantity = r.Next(1, 10) * 5 });
        }
    }
    context.SaveChanges();

    if (!context.MaterialConsumptions.Any()) {
        var completedBookings = context.BookingDetails
            .Include(bd => bd.Booking)
            .Include(bd => bd.Service).ThenInclude(s => s.ServiceMaterials)
            .Where(bd => bd.Booking.Status == BookingStatus.Completed)
            .Take(50).ToList();
        
        var staffList = context.Staffs.Where(s => s.Status == "active").ToList();
        if (completedBookings.Any() && staffList.Any()) {
            var rand = new Random();
            foreach (var bd in completedBookings) {
                var assignedStaff = staffList[rand.Next(staffList.Count)];
                foreach (var sm in bd.Service.ServiceMaterials) {
                    double actual = sm.Quantity * (0.9 + rand.NextDouble() * 0.3);
                    context.MaterialConsumptions.Add(new MaterialConsumption {
                        BookingDetailId = bd.DetailId,
                        MaterialId = sm.MaterialId,
                        StaffId = assignedStaff.StaffId,
                        StandardQuantity = sm.Quantity,
                        ActualQuantity = Math.Round(actual, 1),
                        CreatedAt = bd.Booking.BookingDate
                    });
                }
            }
            context.SaveChanges();
        }
    }
}
app.Run();
