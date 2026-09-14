namespace Portfolio.Core.Abstractions;

/// <summary>
/// Fecha a transacao implicita de um caso de uso. Os repositorios apenas montam
/// as alteracoes em memoria; nada vai para o banco antes de <see cref="SaveAsync"/>.
/// </summary>
public interface IUnitOfWork
{
    Task SaveAsync(CancellationToken ct = default);
}
