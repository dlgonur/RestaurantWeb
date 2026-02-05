// Uygulama başlangıç noktası.
// DI kayıtları, auth ayarları, middleware pipeline ve başlangıç seed işlemleri burada tanımlanır.

using RestaurantWeb.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using RestaurantWeb.Services; 

var builder = WebApplication.CreateBuilder(args);

// MVC (Controller + View) altyapısı
builder.Services.AddControllersWithViews();

// PostgreSQL connection string
var connectionString = builder.Configuration.GetConnectionString("PostgreSqlConnection");

// Npgsql bağlantı üretimi (tek merkez)
builder.Services.AddSingleton<INpgsqlConnectionFactory, NpgsqlConnectionFactory>();


// --- Repository katmanı (DB erişimi) ---
builder.Services.AddScoped<SiparisHistoryRepository>(); 
builder.Services.AddScoped<SiparisLogRepository>();
builder.Services.AddScoped<PersonelRepository>();
builder.Services.AddScoped<PersonelLogRepository>();
builder.Services.AddScoped<MasaRepository>();      
builder.Services.AddScoped<SiparisRepository>();   
builder.Services.AddScoped<RezervasyonRepository>(); 
builder.Services.AddScoped<KategoriRepository>(); 
builder.Services.AddScoped<UrunRepository>();
builder.Services.AddScoped<MutfakRepository>();
builder.Services.AddScoped<RaporRepository>();

// --- Service katmanı (iş kuralları) ---
builder.Services.AddScoped<IKategoriService, KategoriService>(); 
builder.Services.AddScoped<IUrunService, UrunService>();
builder.Services.AddScoped<IMasaService, MasaService>();
builder.Services.AddScoped<IPersonelService, PersonelService>(); 
builder.Services.AddScoped<ISiparisService, SiparisService>();
builder.Services.AddScoped<IPersonelLogService, PersonelLogService>();
builder.Services.AddScoped<CatalogRepository>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<IRezervasyonService, RezervasyonService>();
builder.Services.AddScoped<IMutfakService, MutfakService>();
builder.Services.AddScoped<IRaporService, RaporService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBackupService, BackupService>();

// Yetkilendirme (rol bazlı erişim)
builder.Services.AddAuthorization();

// Cookie tabanlı kimlik doğrulama
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/Denied";
        options.Cookie.Name = "RestaurantWeb.Auth";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });


var app = builder.Build();

// Başlangıç seed işlemleri (masa + admin)
try
{
    DbSeeder.SeedMasalar(connectionString!, masaAdedi: 20, defaultKapasite: 4);
    DbSeeder.SeedAdmin(connectionString!);
}
catch (Exception ex)
{
    // Seed hatası uygulamayı durdurmaz, loglanır
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Veritabanı seed işlemi sırasında bir hata oluştu.");
}

// Production ortamı için hata ve güvenlik ayarları
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}


app.UseHttpsRedirection();
app.UseStaticFiles();       // CSS, JS, img
app.UseRouting();

app.UseAuthentication();    // Kimlik doğrulama
app.UseAuthorization();     // Yetkilendirme

// Static asset mapping (ASP.NET Core 8+)
app.MapStaticAssets();

// Varsayılan MVC route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
