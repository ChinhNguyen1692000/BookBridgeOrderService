using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OrderService.Application.Interface;
using OrderService.Application.MappingProfile;
using OrderService.Application.Services;
using OrderService.Application.Services.Payment;
using OrderService.Domain.Entities;
using OrderService.Infracstructure.DBContext;
using OrderService.Infracstructure.Repositories;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using OrderService.Application.Models; 
using Microsoft.Extensions.Logging;


using VNPayConfig = OrderService.Application.Services.Payment.VNPayConfig; 
using IPaymentService = OrderService.Application.Interface.IPaymentService;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;


// Add services to the container.
builder.Services.AddControllers();
//Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddDbContext<OrderDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("OrderServiceConnection"));
    options.LogTo(Console.WriteLine, LogLevel.Information);
    options.EnableSensitiveDataLogging();
});


builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddAutoMapper(typeof(OrderMappingProfile).Assembly);

builder.Services.AddScoped<IOrderServices, OrderServices>();
builder.Services.AddScoped<PaymentTransactionRepository>();
builder.Services.AddScoped<OrderRepository>();
builder.Services.AddScoped<OrderItemRepository>();
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// =========================================================================
//  ĐĂNG KÝ CẤU HÌNH VÀ PAYMENT SERVICE 
// =========================================================================

// 1. Đăng ký Cấu hình VNPay: Sử dụng IOptions Pattern (Cách chuẩn để đọc config vào service)
builder.Services.Configure<VNPayConfig>(
    builder.Configuration.GetSection("VNPaySettings")); 

// 2. Đăng ký Payment Service
// LƯU Ý: Không cần đăng ký VNPayConfig là Singleton/Scoped riêng, 
// nó sẽ được injected vào VNPayService thông qua IOptions<VNPayConfig>
builder.Services.AddScoped<IPaymentService, VNPayService>(); 

// =========================================================================


// 3. JWT
var jwtKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
var jwtIssuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
var jwtAudience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        NameClaimType = "nameid"
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var jti = context.Principal!.FindFirstValue(JwtRegisteredClaimNames.Jti);

            if (string.IsNullOrEmpty(jti))
            {
                context.Fail("JWT missing jti.");
                return;
            }

            var cacheService = context.HttpContext.RequestServices.GetRequiredService<ICacheService>();

            if (await cacheService.IsBlacklistedAsync(jti))
            {
                context.Fail("This token has been revoked.");
            }

            await Task.CompletedTask;
        }
    };
});


// Swagger/OpenAPI Configuration
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Your API Name", Version = "v1" });

    // 1. Định nghĩa Security Scheme (Security Definition)
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Vui lòng nhập Bearer Token vào trường text bên dưới. Ví dụ: 'Bearer {token}'",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // 2. Yêu cầu Security (Security Requirement)
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});


// Redis Configuration
var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? Environment.GetEnvironmentVariable("ConnectionStrings__Redis");

if (redisConnection!.StartsWith("redis://"))
{
    redisConnection = redisConnection.Replace("redis://", "");
}

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    try
    {
        return ConnectionMultiplexer.Connect(redisConnection);
    }
    catch (Exception ex)
    {
        Console.WriteLine($" Redis connection failed: {ex.Message}");
        throw;
    }
});


var app = builder.Build();


// Tự động áp dụng migrations VÀ XỬ LÝ LỖI
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider; 
    try
    {
        var context = services.GetRequiredService<OrderDbContext>();
        context.Database.Migrate();
        Console.WriteLine("Database migration applied successfully.");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during database migration.");
    }
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();