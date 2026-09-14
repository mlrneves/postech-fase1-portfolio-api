using Portfolio.UseCases.Security;

namespace Portfolio.UnitTests.UseCases;

public class PasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_nao_deve_guardar_a_senha_em_texto_puro()
    {
        var hash = _hasher.Hash("Senha@123");

        hash.Should().NotBe("Senha@123");
        hash.Should().NotContain("Senha@123");

        // "$2" identifica o BCrypt e "$12$" o work factor configurado.
        hash.Should().StartWith("$2").And.Contain("$12$");
    }

    [Fact]
    public void Cada_hash_da_mesma_senha_deve_ser_diferente_por_causa_do_salt()
    {
        _hasher.Hash("Senha@123").Should().NotBe(_hasher.Hash("Senha@123"));
    }

    [Fact]
    public void Matches_deve_aceitar_a_senha_correta_e_recusar_as_demais()
    {
        var hash = _hasher.Hash("Senha@123");

        _hasher.Matches("Senha@123", hash).Should().BeTrue();
        _hasher.Matches("senha@123", hash).Should().BeFalse();
        _hasher.Matches("", hash).Should().BeFalse();
    }

    [Fact]
    public void Matches_com_hash_corrompido_deve_recusar_em_vez_de_estourar()
    {
        var acao = () => _hasher.Matches("Senha@123", "nao-e-um-hash-bcrypt");

        acao.Should().NotThrow();
        _hasher.Matches("Senha@123", "nao-e-um-hash-bcrypt").Should().BeFalse();
    }
}
