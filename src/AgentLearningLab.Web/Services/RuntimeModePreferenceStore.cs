using AgentLearningLab.Agent;
using Microsoft.AspNetCore.Http;

namespace AgentLearningLab.Web.Services;

public sealed class RuntimeModePreferenceStore(IHttpContextAccessor httpContextAccessor) : IRuntimeModePreferenceStore
{
    private const string CookieName = "agent-learning-lab-runtime-mode";

    public AgentExecutionMode GetPreferredMode(bool apiKeyAvailable)
    {
        var cookieValue = httpContextAccessor.HttpContext?.Request.Cookies[CookieName];
        if (string.Equals(cookieValue, "api-key", StringComparison.OrdinalIgnoreCase) && apiKeyAvailable)
        {
            return AgentExecutionMode.ApiKey;
        }

        return AgentExecutionMode.Offline;
    }

    public void Save(AgentExecutionMode mode)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        httpContext.Response.Cookies.Append(
            CookieName,
            mode == AgentExecutionMode.ApiKey ? "api-key" : "offline",
            new CookieOptions
            {
                HttpOnly = false,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = httpContext.Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });
    }
}
