namespace Portfolio.UseCases.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Matches(string password, string hash);
}
