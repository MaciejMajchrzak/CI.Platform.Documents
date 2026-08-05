using MassTransit;
using CI.Kernel;
using CI.Kernel.Http;
using CI.Kernel.InMemory;
using CI.Kernel.Redis;
using CI.Platform.Documents.Core;
using CI.Platform.Documents.Core.Commands;
using CI.Platform.Documents.Core.DTOs;
using CI.Platform.Documents.Core.Handlers;
using CI.Platform.Documents.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
namespace CI.Platform.Documents.API.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddDocumentsServices(this IServiceCollection services, IConfiguration config)
    {
        // Tenant context from JWT claims
        services.AddHttpTenantContext();

        // Data-plane: per-tenant DB routing
        var tenantsUrl = config["Services:Tenants"];
        if (!string.IsNullOrEmpty(tenantsUrl))
        {
            services.AddHttpTenantDbRouter(tenantsUrl);
        }
        else
        {
            var cs = config.GetConnectionString("Documents") ?? string.Empty;
            services.AddSingleton<ITenantDbRouter>(new NullTenantDbRouter(cs));
        }

        services.AddSingleton<ITenantDbContextFactory<DocumentsDbContext>>(sp =>
            new TenantDbContextFactory<DocumentsDbContext>(
                sp.GetRequiredService<ITenantDbRouter>(),
                opts => new DocumentsDbContext(opts),
                "documents"));

        services.AddScoped(sp =>
        {
            var factory   = sp.GetRequiredService<ITenantDbContextFactory<DocumentsDbContext>>();
            var tenantCtx = sp.GetRequiredService<ITenantContext>();
            return factory.CreateAsync(tenantCtx.TenantId).GetAwaiter().GetResult();
        });

        services.AddScoped<IDocumentsRepository, DocumentsRepository>();
        services.AddScoped<IDocumentsOutbox, DocumentsOutbox>();

        // Template CRUD handlers
        services.AddScoped<ICommandHandler<CreateTemplateCommand, Guid>, CreateTemplateHandler>();
        services.AddScoped<ICommandHandler<UpdateTemplateCommand>, UpdateTemplateHandler>();
        services.AddScoped<ICommandHandler<DeleteTemplateCommand>, DeleteTemplateHandler>();

        // Render handler
        services.AddScoped<ICommandHandler<RenderTemplateCommand, RenderedDocumentDto>, RenderTemplateHandler>();

        // Query handlers
        services.AddScoped<ICommandHandler<GetTemplateQuery, DocumentTemplateDto>, GetTemplateHandler>();
        services.AddScoped<ICommandHandler<ListTemplatesQuery, PagedResult<DocumentTemplateDto>>, ListTemplatesHandler>();

        // Bus
        services.AddScoped<ICommandBus, HandlerDispatcher>();

        services.AddSingleton<IModuleManifest, DocumentsModuleManifest>();

        var redis = config.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redis))
            services.AddRedisKernel(redis);
        else
            services.AddSingleton<IDistributedLock, NullDistributedLock>();

        return services;
    }

    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration config)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                opts.Authority = config["Keycloak:Authority"];
                opts.Audience  = config["Keycloak:Audience"] ?? "account";
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = !string.IsNullOrEmpty(config["Keycloak:Authority"]),
                    ValidateAudience         = false,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                };
                opts.RequireHttpsMetadata = false;
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration config)
    {
        var otlpEndpoint = config["OTEL_EXPORTER_OTLP_ENDPOINT"];
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("ci-platform-documents"))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation()
                 .AddEntityFrameworkCoreInstrumentation();
                if (!string.IsNullOrEmpty(otlpEndpoint))
                    t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            });
        return services;
    }

    public static IServiceCollection AddOutboxPublisher(this IServiceCollection services, IConfiguration config)
    {
        var rabbitHost = config["RabbitMQ:Host"];
        if (string.IsNullOrEmpty(rabbitHost))
            return services;

        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(rabbitHost, h =>
                {
                    h.Username(config["RabbitMQ:Username"] ?? "ci");
                    h.Password(config["RabbitMQ:Password"] ?? "ci");
                });
                cfg.ConfigureEndpoints(ctx);
            });
        });
        services.AddHostedService<OutboxPublisher>();
        return services;
    }
}
