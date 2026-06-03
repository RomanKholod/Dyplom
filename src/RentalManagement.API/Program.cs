using Microsoft.EntityFrameworkCore;
using Hangfire;
using RentalManagement.Application;
using RentalManagement.Infrastructure;
using RentalManagement.Infrastructure.BackgroundJobs;
using RentalManagement.Infrastructure.Persistence;
using RentalManagement.API.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .WriteTo.Console()
       .WriteTo.File("logs/rental-.log", rollingInterval: RollingInterval.Day));

// Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Rental Management API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Введіть токен: Bearer {token}"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// CORS for React dev server
builder.Services.AddCors(opts =>
    opts.AddPolicy("ReactApp", policy =>
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

var app = builder.Build();

// Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Rental Management API v1");
        c.RoutePrefix = string.Empty; // swagger буде на http://localhost:5000/
    });
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSerilogRequestLogging();
app.UseCors("ReactApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Hangfire Dashboard (тільки для адмінів у продакшні)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [] // у продакшні замінити на авторизацію
});

// ПРИМУСОВА ініціалізація: мігруємо і заповнюємо базу за будь-якого запуску (завжди)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<AppDbContext>();

        // Крок 1: Насильно накочуємо структуру таблиць в єдину базу даних
        await db.Database.MigrateAsync();

        // Крок 2: Передаємо цей же context-провайдер у SeedData
        await SeedData.SeedAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Сталася критична помилка під час підготовки або ініціалізації бази даних.");
    }
}

// Register Hangfire recurring jobs
RecurringJob.AddOrUpdate<OverdueInvoiceNotificationJob>(
    "overdue-invoice-notifications",
    job => job.ExecuteAsync(),
    "0 9 * * *"); // щодня о 09:00

RecurringJob.AddOrUpdate<ExpiringContractNotificationJob>(
    "expiring-contract-notifications",
    job => job.ExecuteAsync(),
    "0 9 * * *"); // щодня о 09:00

RecurringJob.AddOrUpdate<MonthlyInvoiceGenerationJob>(
    "monthly-invoice-generation",
    job => job.ExecuteAsync(),
    "0 8 1 * *"); // 1-го числа кожного місяця о 08:00

app.Run();