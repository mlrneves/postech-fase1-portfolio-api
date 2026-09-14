namespace Portfolio.Core.Abstractions;

/// <summary>
/// Regra de negocio violada. E lancada pelas entidades quando alguem tenta
/// coloca-las em um estado invalido. A API traduz para HTTP 400.
/// </summary>
public class BusinessRuleException(string message) : Exception(message);
