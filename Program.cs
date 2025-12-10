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

            // CONNECTION STRING
            var conn = builder.Configuration.GetConnectionString("DefaultConnection");

            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();

            // JWT CONFIGURATION
            var jwtSettingsSection = builder.Configuration.GetSection("JwtSettings");
            builder.Services.Configure<JwtSettings>(jwtSettingsSection);
            var jwtSettings = jwtSettingsSection.Get<JwtSettings>();

            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
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

            // CONTROLLERS + JSON
            builder.Services.AddControllers()
                .AddJsonOptions(opts =>
                {
                    opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                });

            builder.Services.Configure<MvcOptions>(opts =>
            {
                opts.Filters.Add(new ProducesAttribute("application/json"));
            });

            builder.Services.AddEndpointsApiExplorer();

            // SWAGGER
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "ERP System API",
                    Version = "v1",
                    Description = "ERP System built with ASP.NET Core",
                    Contact = new OpenApiContact
                    {
                        Name = "Basma Khalaf",
                        Email = "basmakhalaf974@gmail.com"
                    }
                });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter your token: Bearer {token}"
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
                        Array.Empty<string>()
                    }
                });
            });

            // DATABASE + IDENTITY
            builder.Services.AddDbContext<ErpMasterContext>(options =>
                options.UseSqlServer(conn));

            builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ErpMasterContext>()
                .AddDefaultTokenProviders();

            builder.Services.Configure<IdentityOptions>(options =>
            {
                options.Password.RequiredLength = 6;
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            });

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = 401;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = 403;
                    return Task.CompletedTask;
                };
            });

            // CUSTOM SERVICES
            builder.Services.AddSingleton(new ProjectDbContextFactory(conn));
            builder.Services.AddScoped<AccountingService>();

            // CORS POLICY
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAngular", policy =>
                {
                    policy.WithOrigins(
                        "https://finance.minya.gov.eg",
                        "http://localhost:4200",
                        "http://172.16.1.36:81",
                        "http://localhost:81"
                    )
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
                });
            });

            // BUILD APP
            var app = builder.Build();

            // LOG REQUESTS
            app.Use(async (context, next) =>
            {
                Console.WriteLine($"➡️ {context.Request.Method} {context.Request.Path}");
                await next();
                Console.WriteLine($"⬅️ Status: {context.Response.StatusCode}");
            });

            // STATIC FILES WITH CORS
            var allowedOrigins = new[]
            {
                "https://finance.minya.gov.eg",
                "http://localhost:4200",
                "http://172.16.1.36:81"
            };

            app.UseStaticFiles(new StaticFileOptions
            {
                ServeUnknownFileTypes = true,
                OnPrepareResponse = ctx =>
                {
                    var origin = ctx.Context.Request.Headers["Origin"].ToString();
                    if (allowedOrigins.Contains(origin))
                    {
                        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", origin);
                    }

                    ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET,HEAD,OPTIONS");
                    ctx.Context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type, Authorization");
                }
            });

            // SWAGGER
            if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseCors("AllowAngular");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
