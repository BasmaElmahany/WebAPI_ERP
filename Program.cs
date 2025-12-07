using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WebAPI.Data;
using WebAPI.Data.Entities;
using WebAPI.Services;
using WebAPI.Settings;

namespace WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 🧩 Connection String
            var conn = builder.Configuration.GetConnectionString("DefaultConnection");

            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();

            // 🧩 JWT Configuration
            var jwtSettingsSection = builder.Configuration.GetSection("JwtSettings");
            builder.Services.Configure<JwtSettings>(jwtSettingsSection);
            var jwtSettings = jwtSettingsSection.Get<JwtSettings>();

            // ✅ Fix for role claim mapping issues
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            // 🧩 Authentication & JWT
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
                    RoleClaimType = ClaimTypes.Role
                };
            });

            builder.Services.AddAuthorization();

            // 🧩 Controllers + JSON Options
            builder.Services.AddControllers()
                .AddJsonOptions(opts =>
                {
                    opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                    opts.JsonSerializerOptions.WriteIndented = false;
                });

            // Ensure JSON responses always
            builder.Services.Configure<MvcOptions>(opts =>
            {
                opts.Filters.Add(new ProducesAttribute("application/json"));
            });

            builder.Services.AddEndpointsApiExplorer();

            // 🧩 Swagger setup
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "ERP System API",
                    Version = "v1",
                    Description = "📚 ERP System built with ASP.NET Core",
                    Contact = new OpenApiContact
                    {
                        Name = "Basma Khalaf",
                        Email = "basmakhalaf974@gmail.com"
                    }
                });

                // ✅ JWT Bearer Support in Swagger
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter your JWT token like: Bearer {your token}"
                });

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

            // 🧩 DbContext and Identity
            builder.Services.AddDbContext<ErpMasterContext>(options =>
                options.UseSqlServer(conn));

            builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ErpMasterContext>()
                .AddDefaultTokenProviders();

            // 🧩 Identity Options
            builder.Services.Configure<IdentityOptions>(options =>
            {
                options.Password.RequiredLength = 6;
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            });

            // 🔐 Prevent redirects on unauthorized access
            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

            // 🧩 Custom Services
            builder.Services.AddSingleton(new ProjectDbContextFactory(conn));
            builder.Services.AddScoped<AccountingService>();

            // 🧩 CORS (Angular)
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAngularDevClient", policy =>
                {
                    policy.WithOrigins("http://localhost:4200" , "http://172.16.1.36:81", "http://localhost:81")
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                });
            });
            builder.Services.Configure<JsonOptions>(options =>
            {
                options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                options.JsonSerializerOptions.WriteIndented = false;
            });


            // 🧩 Build App
            var app = builder.Build();

            app.Use(async (context, next) =>
            {
                Console.WriteLine($"➡️ Incoming Request: {context.Request.Method} {context.Request.Path}");
                await next();
                Console.WriteLine($"⬅️ Response Status: {context.Response.StatusCode}");
            });
            app.Use(async (context, next) =>
            {
                context.Response.Headers["Content-Type"] = "application/json; charset=utf-8";
                await next();
            });
            app.UseStaticFiles();

            // 🧩 Swagger & Dev Tools
            if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // 🧩 Middleware order (important)
            app.UseHttpsRedirection();
            app.Use(async (context, next) =>
            {
                context.Response.Headers["Content-Type"] = "application/json";
                await next();
            });
            app.UseRouting();

            app.UseCors("AllowAngularDevClient");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
