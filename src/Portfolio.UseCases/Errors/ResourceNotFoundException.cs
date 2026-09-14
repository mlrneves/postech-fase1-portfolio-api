namespace Portfolio.UseCases.Errors;

/// <summary>Recurso inexistente ou que nao pertence ao investidor autenticado. Virara HTTP 404.</summary>
public class ResourceNotFoundException(string message) : Exception(message);
