using Microsoft.EntityFrameworkCore;
using AppValetParking.Data;



var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("wwwroot/Config/configuracionValet.json", optional: false, reloadOnChange: true);


// Add services to the container.
builder.Services.AddAuthentication("MyCookieAuth")
    .AddCookie("MyCookieAuth", options =>
    {
        options.LoginPath = "/Account/Login";
    });

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDbContext<PegasysDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("PegasysConnection")));
builder.Services.AddDbContext<TcabdopeDbContext>(options =>
    options.UseSqlServer("Server=NUV01WINDBINT04,2705;Database=TCADBOPE;User Id=intranet;Password=1nTR4n3t.2O2O;TrustServerCertificate=True;"));
builder.Services.AddDbContext<TcabdopeNewDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TCABDOPEConnection")));



builder.Services.AddSession();


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();  

app.UseAuthentication();   

app.UseAuthorization();



app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");



app.Run();
