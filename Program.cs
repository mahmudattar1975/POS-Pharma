using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PharmaApp.DataBase;
using PharmaApp.Entity.Models;

var builder = WebApplication.CreateBuilder(args);

// WebApplicationBuilder only auto-loads User Secrets when the host environment resolves to
// "Development" - if this is run in a way that doesn't set ASPNETCORE_ENVIRONMENT/
// DOTNET_ENVIRONMENT (direct `dotnet run`, running the built DLL directly, some IDE launch
// configs), secrets silently don't load and GetConnectionString("Default") comes back null.
// Loading them unconditionally here makes the connection string available regardless of how
// the app is launched.
builder.Configuration.AddUserSecrets<Program>(optional: true);

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Default' is not configured. Set it via: " +
        "dotnet user-secrets set \"ConnectionStrings:Default\" \"Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True;\" " +
        "(run from the dotnet-pos project folder).");
}

var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "Jwt:Key is not configured. Set it via: dotnet user-secrets set \"Jwt:Key\" \"<a long random string>\" " +
        "(run from the dotnet-pos project folder).");
}

// Add services to the container.
builder.Services.AddControllersWithViews()
    // Defense in depth: every controller here is expected to return explicit DTOs, never raw EF
    // entities (see e.g. PosController.ToResponse) - but if one ever slips through with a
    // bidirectional navigation property (Shift -> Sales -> Pos -> Shift -> ...), this stops it
    // from crashing the response with a circular-reference exception.
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// The React frontend is a separate origin from this API, so auth is JWT bearer tokens (sent as
// "Authorization: Bearer <token>") rather than cookies - no cross-origin cookie/CORS-credentials
// complications. AccountController.Login issues the token; every other controller requires it.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

// Dev-time CORS for the React app (Vite's default port). Adjust/expand the allowed origin(s) for
// your actual frontend dev server and production domain.
const string CorsPolicy = "ReactApp";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// One-time seed: if no cashier accounts exist yet (first run), create a default admin so there's
// a way to log in at all - CashierController.Create requires an existing admin, which would
// otherwise be a chicken-and-egg problem. No-ops on every later startup once any cashier exists.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!db.Cashiers.Any())
    {
        var hasher = new PasswordHasher<Cashier>();
        var admin = new Cashier
        {     
            Username = "admin",
            FullName = "Administrator",
            IsAdmin = true,
            IsActive = true,
            CreatedOn = DateTime.Now,
        };
        admin.PasswordHash = hasher.HashPassword(admin, "Admin@12345");
        db.Cashiers.Add(admin);
        db.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseCors(CorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

app.MapGet("/", () => "POS API Running");
app.Run();
