using AgentLearningLab.Agent.DependencyInjection;
using AgentLearningLab.Application.Configuration;
using AgentLearningLab.Infrastructure.DependencyInjection;
using AgentLearningLab.Infrastructure.Persistence;
using AgentLearningLab.Tools.DependencyInjection;
using AgentLearningLab.Web.Components;
using AgentLearningLab.Web.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.Configure<OpenAIOptions>(builder.Configuration.GetSection(OpenAIOptions.SectionName));
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.Services.Configure<RetrievalOptions>(builder.Configuration.GetSection(RetrievalOptions.SectionName));
builder.Services.Configure<ApprovalOptions>(builder.Configuration.GetSection(ApprovalOptions.SectionName));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<DemoIdentityService>();
builder.Services.AddScoped<AgentLearningLab.Agent.IRuntimeModePreferenceStore, RuntimeModePreferenceStore>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAgentTools();
builder.Services.AddAgentRuntime();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
else
{
    Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data"));
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapGet("/health", async (AgentLearningLabDbContext dbContext, CancellationToken cancellationToken) =>
{
    var ready = await dbContext.Database.CanConnectAsync(cancellationToken);
    return Results.Json(new
    {
        status = ready ? "Healthy" : "Unhealthy",
        database = ready ? "Ready" : "Unavailable"
    });
});

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<LabDbInitializer>().InitializeAsync(CancellationToken.None);
}

app.Run();
