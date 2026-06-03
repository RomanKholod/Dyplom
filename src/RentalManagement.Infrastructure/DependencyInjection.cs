using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Hangfire;
using Hangfire.SqlServer;
using RentalManagement.Application.Features.Auth;
using RentalManagement.Application.Features.Contracts;
using RentalManagement.Application.Features.Invoices;
using RentalManagement.Application.Features.Notifications;
using RentalManagement.Application.Features.Reports;
using RentalManagement.Domain.Interfaces;
using RentalManagement.Infrastructure.BackgroundJobs;
using RentalManagement.Infrastructure.Identity;
using RentalManagement.Infrastructure.Persistence;
using RentalManagement.Infrastructure.Services;
using RentalManagement.Infrastructure.Services.Email;
using RentalManagement.Infrastructure.Services.Export;

namespace RentalManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Database
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseSqlServer(
                config.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        // Identity
        services.AddIdentity<ApplicationUser, IdentityRole>(opts =>
        {
            opts.Password.RequiredLength = 8;
            opts.Password.RequireDigit = true;
            opts.Password.RequireUppercase = false;
            opts.Password.RequireNonAlphanumeric = false;
            opts.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        // JWT
        services.AddAuthentication(opts =>
        {
            opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            opts.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = config["Jwt:Issuer"],
                ValidAudience = config["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(config["Jwt:SecretKey"]!)),
                ClockSkew = TimeSpan.Zero
            };
        });

        // Hangfire
        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(config.GetConnectionString("DefaultConnection"),
                new SqlServerStorageOptions { PrepareSchemaIfNecessary = true }));
        services.AddHangfireServer();

        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Domain services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IContractNumberGenerator, ContractNumberService>();
        services.AddScoped<IInvoiceNumberService, InvoiceNumberService>();

        // Export services
        services.AddScoped<IExcelExportService, ExcelExportService>();
        services.AddScoped<IPdfExportService, PdfExportService>();

        // Email service
        services.AddScoped<IEmailService, EmailService>();

        // Background jobs
        services.AddScoped<OverdueInvoiceNotificationJob>();
        services.AddScoped<ExpiringContractNotificationJob>();
        services.AddScoped<MonthlyInvoiceGenerationJob>();

        return services;
    }
}
