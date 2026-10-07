using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

namespace BusinessSearcher.API.Extensions
{
    public static class ApiServiceExtensions
    {
        public static IServiceCollection AddJwtAuthentication(
            this IServiceCollection services, IConfiguration config)
        {
            var secret = config["Jwt:Secret"]
                ?? throw new InvalidOperationException("Jwt:Secret no configurado en appsettings.");

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme     = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ValidateIssuer           = true,
                    ValidIssuer              = config["Jwt:Issuer"],
                    ValidateAudience         = true,
                    ValidAudience            = config["Jwt:Audience"],
                    ValidateLifetime         = true,
                    ClockSkew                = TimeSpan.Zero,
                    // El claim de rol se emite como "role" (MapInboundClaims = false),
                    // para que [Authorize(Roles = "Client"|"Store"|"Admin")] funcione.
                    RoleClaimType            = "role"
                };

                // Permite enviar el JWT como query string para SignalR
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path        = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });

            services.AddAuthorization();
            return services;
        }

        public static IServiceCollection AddCorsPolicy(
            this IServiceCollection services, IConfiguration config)
        {
            var configured = config.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? new[] { "http://localhost:3000", "http://localhost:5173" };

            // Orígenes de los frontends de producción, siempre permitidos aunque
            // no estén en la config/env (el frontend web vive en Cloudflare Workers;
            // GitHub Pages sigue permitido por el despliegue anterior).
            var always = new[]
            {
                "https://foodfinder.raikomh1241.workers.dev",
                "https://raikomh.github.io",
                "http://localhost:4321",
                "http://localhost:5173",
            };

            var allowedOrigins = configured.Concat(always)
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Select(o => o.TrimEnd('/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            services.AddCors(options =>
            {
                options.AddPolicy("DefaultPolicy", policy =>
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials(); // Requerido para SignalR
                });
            });

            return services;
        }

        public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title       = "BusinessSearcher API",
                    Version     = "v1",
                    Description = "API para gestión de mipymes de comida con búsqueda de disponibilidad en tiempo real."
                });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header usando el esquema Bearer. Ej: \"Bearer {token}\"",
                    Name        = "Authorization",
                    In          = ParameterLocation.Header,
                    Type        = SecuritySchemeType.ApiKey,
                    Scheme      = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id   = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            return services;
        }
    }
}
