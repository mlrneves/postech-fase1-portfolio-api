using Portfolio.UseCases.Abstractions;

namespace Portfolio.UseCases.Security;

/// <summary>
/// BCrypt com work factor 12 (cerca de 250 ms por hash em hardware atual): caro o
/// bastante para forca bruta e ainda aceitavel dentro de uma requisicao de login.
/// O salt vai embutido no proprio hash, por isso nao existe coluna separada.
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Matches(string password, string hash)
    {
        // Hash corrompido ou gravado por outra implementacao nao deve derrubar o login.
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
