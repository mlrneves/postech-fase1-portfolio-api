namespace Portfolio.UseCases.Errors;

/// <summary>Colisao com um registro ja existente, ou remocao bloqueada por dependencia. Virara HTTP 409.</summary>
public class DuplicateResourceException(string message) : Exception(message);
