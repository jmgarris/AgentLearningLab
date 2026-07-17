using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Identity;

namespace AgentLearningLab.Web.Services;

public sealed class DemoIdentityService(IHostEnvironment environment, IClubDataService clubDataService)
{
    private static readonly IReadOnlyList<DemoIdentityOption> Options =
    [
        new("member@example.test", "Member"),
        new("admin@example.test", "Administrator")
    ];

    private string _selectedEmail = "member@example.test";

    public bool IsEnabled => environment.IsDevelopment();

    public IReadOnlyList<DemoIdentityOption> AvailableOptions => Options;

    public string SelectedEmail => _selectedEmail;

    public void SetSelectedEmail(string email)
    {
        if (!IsEnabled || Options.All(x => !string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _selectedEmail = email;
    }

    public async Task<AuthenticatedUserContext> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("Development identity switching is unavailable outside Development.");
        }

        var member = await clubDataService.GetMemberByEmailAsync(_selectedEmail, cancellationToken)
            ?? throw new InvalidOperationException($"No seeded club member exists for {_selectedEmail}.");

        return new AuthenticatedUserContext(member.Id, member.Email, member.DisplayName, member.Role);
    }
}

public sealed record DemoIdentityOption(string Email, string Label);
