namespace Portfolio.UseCases.Errors;

/// <summary>Login recusado. Virara HTTP 401.</summary>
public class InvalidCredentialsException(string message) : Exception(message);
