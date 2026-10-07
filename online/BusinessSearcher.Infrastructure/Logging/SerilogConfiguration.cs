using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace BusinessSearcher.Infrastructure.Logging
{
    public static class SerilogConfiguration
    {
        public static void ConfigureSerilog(this WebApplicationBuilder builder)
        {
            var logConfig = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

            if (!builder.Environment.IsDevelopment())
            {
                // En producción, también escribe a archivo
                logConfig.WriteTo.File(
                    path:             "logs/businesssearcher-.log",
                    rollingInterval:  Serilog.RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate:   "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
            }

            Log.Logger = logConfig.CreateLogger();
            builder.Host.UseSerilog();
        }
    }
}
