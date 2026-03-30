using Microsoft.EntityFrameworkCore;
using SpaN5.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// === ĐĂNG KÝ DbContext - PHẦN QUAN TRỌNG NHẤT ===
builder.Services.AddDbContext<SpaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SpaConnection")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SpaDbContext>();
    
    if (!context.ServiceCategories.Any())
    {
        var cat = new SpaN5.Models.SpaN5.Models.ServiceCategory { Name = "Chăm sóc da & cơ thể" };
        context.ServiceCategories.Add(cat);
        context.SaveChanges();
        
        context.Services.AddRange(
            new Service { ServiceId = 1, ServiceName = "Massage Thư Giãn", Description = "Xua tan căng thẳng cơ bắp bằng liệu pháp massage Thụy Điển kết hợp đá nóng thiên nhiên.", Price = 500000, Duration = 60, IsActive = true, Image = "https://images.unsplash.com/photo-1515377905703-c4788e51af15?auto=format&fit=crop&w=600&q=80", CategoryId = cat.Id },
            new Service { ServiceId = 2, ServiceName = "Chăm Sóc Da Mặt", Description = "Thanh lọc và trẻ hóa làn da với tinh chất hoa cúc và serum hữu cơ nhập khẩu từ Pháp.", Price = 700000, Duration = 90, IsActive = true, Image = "https://images.unsplash.com/photo-1570172619644-dfd03ed5d881?auto=format&fit=crop&w=600&q=80", CategoryId = cat.Id },
            new Service { ServiceId = 3, ServiceName = "Tắm Thảo Dược", Description = "Đào thải độc tố, kích thích tuần hoàn máu qua liệu trình ngâm mình với 15 loại thảo mộc quý.", Price = 450000, Duration = 45, IsActive = true, Image = "https://images.unsplash.com/photo-1519823551278-64ac92734fb1?auto=format&fit=crop&w=600&q=80", CategoryId = cat.Id },
            new Service { ServiceId = 4, ServiceName = "VIP Chăm Sóc Toàn Tâm", Description = "Liệu trình chăm sóc toàn diện cao cấp với sự kết hợp hoàn hảo từ A-Z, mang đến sự thư giãn tuyệt đối cho khách hàng VIP.", Price = 1200000, Duration = 120, IsActive = true, Image = "https://images.unsplash.com/photo-1544161515-4ab6ce6db874?auto=format&fit=crop&w=600&q=80", CategoryId = cat.Id },
            new Service { ServiceId = 5, ServiceName = "VIP Phục Hồi Năng Lượng", Description = "Phục hồi chuyên sâu sinh lực với những nguyên liệu quý giá, kỹ thuật massage bậc thầy đánh thức mọi giác quan.", Price = 1500000, Duration = 150, IsActive = true, Image = "https://images.unsplash.com/photo-1600334089648-f0d9d3d53352?auto=format&fit=crop&w=600&q=80", CategoryId = cat.Id },
            new Service { ServiceId = 6, ServiceName = "VIP Trẻ Hóa Hoàng Gia", Description = "Trải nghiệm liệu trình chống lão hóa đỉnh cao với tinh chất vàng 24K, đem lại diện mạo thanh xuân ngay lần đầu.", Price = 2000000, Duration = 180, IsActive = true, Image = "https://images.unsplash.com/photo-1540555700478-4be289fbecef?auto=format&fit=crop&w=600&q=80", CategoryId = cat.Id }
        );
        context.SaveChanges();
    }
    
    // Seed branch for booking flow (BranchId = 1)
    if (!context.Branches.Any())
    {
        context.Branches.Add(new Branch { BranchName = "SpaN5 Premium", Address = "123 Le Loi", City = "HCM", Phone = "012345678" });
        context.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();