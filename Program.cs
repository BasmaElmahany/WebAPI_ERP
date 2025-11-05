
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
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

            var conn = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();

            var jwtSettingsSection = builder.Configuration.GetSection("JwtSettings");
            builder.Services.Configure<JwtSettings>(jwtSettingsSection);
            var jwtSettings = jwtSettingsSection.Get<JwtSettings>();


            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "JwtBearer";
                options.DefaultChallengeScheme = "JwtBearer";
            })
                       .AddJwtBearer("JwtBearer", options =>
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

            // Add services to the container.
            builder.Services.AddControllers()
                .AddJsonOptions(opts =>
                {
                    // consistent JSON output
                    opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                    opts.JsonSerializerOptions.WriteIndented = false;
                });


            // Ensure controllers always produce JSON responses
            builder.Services.Configure<MvcOptions>(opts =>
            {
                opts.Filters.Add(new ProducesAttribute("application/json"));
            });

            builder.Services.AddControllers();

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "ERP system API",
                    Version = "v1",
                    Description = "📚 ERP system",
                    Contact = new OpenApiContact
                    {
                        Name = "Basma Khalaf",
                        Email = "basmakhalaf974@gmail.com"
                    }
                });


                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
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


               /* var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));*/
            });


            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();




            // Erp master context (Projects table) - simple context
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

            // 🔐 Prevent redirecting on unauthorized access
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

            // Factory for project-level dynamic context
            builder.Services.AddSingleton(new ProjectDbContextFactory(conn));
            builder.Services.AddScoped<AccountingService>();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAngularDevClient", policy =>
                {
                    policy.WithOrigins("http://localhost:4200") // 👈 your Angular app URL
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials(); // optional if you use cookies
                });
            });

            var app = builder.Build();
            app.Use(async (context, next) =>
            {
                Console.WriteLine($"➡️ Incoming Request: {context.Request.Method} {context.Request.Path}");
                await next();
                Console.WriteLine($"⬅️ Response Status: {context.Response.StatusCode}");
            });
            if (app.Environment.IsDevelopment()|| app.Environment.IsProduction())
            {
                app.UseDeveloperExceptionPage();

                app.UseSwagger();
                app.UseSwaggerUI();
            }
            app.UseHttpsRedirection();
           

            app.UseAuthentication();
            app.UseCors("AllowAngularDevClient");

            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
