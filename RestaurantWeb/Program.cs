using RestaurantWeb.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using RestaurantWeb.Services; 




var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
var connectionString = builder.Configuration.GetConnectionString("PostgreSqlConnection");

builder.Services.AddSingleton<INpgsqlConnectionFactory, NpgsqlConnectionFactory>();


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


builder.Services.AddAuthorization();

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

try
{
    DbSeeder.SeedMasalar(connectionString!, masaAdedi: 20, defaultKapasite: 4);
    DbSeeder.SeedAdmin(connectionString!);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Veritabanı seed işlemi sırasında bir hata oluştu.");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // 01/26/2026
app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
