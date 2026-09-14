using Portfolio.Core.Investors;

namespace Portfolio.UseCases.Abstractions;

public record AccessToken(string Value, DateTime ExpiresAtUtc);

public interface IAccessTokenFactory
{
    AccessToken IssueFor(Investor investor);
}
