using Microsoft.EntityFrameworkCore;
using AppValetParking.Data;
using AppValetParking.Services;




var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("wwwroot/Config/configuracionValet.json", optional: true, reloadOnChange: true);


builder.Services.AddCors(options =>
{
    options.AddPolicy("ValetPolicy", policy =>
    {
        policy
            .AllowAnyOrigin()    // Permite Flutter web, móvil, cualquier origen
            .AllowAnyMethod()    // GET, POST, PUT, DELETE, OPTIONS
            .AllowAnyHeader();   // Content-Type, Authorization, etc.
    });
});

// Add services to the container.
builder.Services.AddAuthentication("MyCookieAuth")
    .AddCookie("MyCookieAuth", options =>
    {
        options.LoginPath = "/Account/Login";
    });
builder.Services.AddSingleton<PrinterConfigService>();


builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDbContext<PegasysDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("PegasysConnection")));
builder.Services.AddDbContext<TcabdopeDbContext>(options =>
    options.UseSqlServer("Server=NUV01WINDBINT04,2705;Database=TCADBOPE;User Id=intranet;Password=1nTR4n3t.2O2O;TrustServerCertificate=True;"));
builder.Services.AddDbContext<TcabdopeNewDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TCABDOPEConnection")));
builder.Services.AddDbContext<ValetParkingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // sesión expira tras 30 minutos de inactividad
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();
app.UseCors("ValetPolicy");   // ⚠️ Este orden importa mucho



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



app.UseStaticFiles(); // sirve wwwroot/uploads/* (fotos de inspección subidas en runtime)
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");



app.Run();
