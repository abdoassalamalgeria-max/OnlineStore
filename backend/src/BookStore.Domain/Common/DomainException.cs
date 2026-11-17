namespace BookStore.Domain.Common;

/// <summary>Raised when an operation would break a business rule (e.g. negative stock). Maps to HTTP 400/409.</summary>
public sealed class DomainException(string message) : Exception(message);
