using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Common;
using AgentLearningLab.Infrastructure.Persistence;
using AgentLearningLab.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentLearningLab.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=src/AgentLearningLab.Web/App_Data/agent-learning-lab.db";

        services.AddDbContext<AgentLearningLabDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<LabDbInitializer>();
        services.AddScoped<IConversationStore, ConversationStore>();
        services.AddScoped<IAgentRunStore, AgentRunStore>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<IKnowledgeSearchService, KnowledgeSearchService>();
        services.AddScoped<IOutboxService, OutboxService>();
        services.AddScoped<IMemoryFactService, MemoryFactService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IClubDataService, ClubDataService>();
        services.AddSingleton<ISystemClock, SystemClock>();
        return services;
    }
}
