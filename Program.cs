
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Services;

namespace WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

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

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var conn = builder.Configuration.GetConnectionString("DefaultConnection");

            // Erp master context (Projects table) - simple context
            builder.Services.AddDbContext<ErpMasterContext>(options =>
                options.UseSqlServer(conn));

            // Factory for project-level dynamic context
            builder.Services.AddSingleton(new ProjectDbContextFactory(conn));
            builder.Services.AddScoped<AccountingService>();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
