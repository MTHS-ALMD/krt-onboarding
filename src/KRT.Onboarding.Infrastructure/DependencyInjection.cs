using Amazon.SimpleNotificationService;
using KRT.Onboarding.Application.Accounts;
using KRT.Onboarding.Domain.Accounts;
using KRT.Onboarding.Domain.Common;
using KRT.Onboarding.Infrastructure.Caching;
using KRT.Onboarding.Infrastructure.Messaging;
using KRT.Onboarding.Infrastructure.Outbox;
using KRT.Onboarding.Infrastructure.Persistence;
using KRT.Onboarding.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KRT.Onboarding.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        AddPersistence(services, configuration);
        AddCaching(services, configuration);
        AddMessaging(services, configuration);

        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SqlServer")
            ?? throw new InvalidOperationException("ConnectionStrings:SqlServer não configurada.");

        services.AddDbContext<KrtDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<KrtDbContext>());
        services.AddScoped<IAccountRepository, AccountRepository>();
    }

    private static void AddCaching(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AccountCacheOptions>(configuration.GetSection(AccountCacheOptions.SectionName));

        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "krt-onboarding:";
            });
        }
        else
        {
            // Sem Redis configurado (ex.: rodando local sem docker): cache em memória.
            services.AddDistributedMemoryCache();
        }

        services.AddSingleton<IAccountCache, DistributedAccountCache>();
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration)
    {
        var messagingSection = configuration.GetSection(MessagingOptions.SectionName);
        services.Configure<MessagingOptions>(messagingSection);
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));

        if (!string.IsNullOrWhiteSpace(messagingSection.Get<MessagingOptions>()?.TopicArn))
        {
            services.AddDefaultAWSOptions(configuration.GetAWSOptions());
            services.AddAWSService<IAmazonSimpleNotificationService>();
            services.AddSingleton<IEventPublisher, SnsEventPublisher>();
        }
        else
        {
            services.AddSingleton<IEventPublisher, LoggingEventPublisher>();
        }

        services.AddScoped<OutboxDispatcher>();
        services.AddHostedService<OutboxProcessorService>();
    }
}
