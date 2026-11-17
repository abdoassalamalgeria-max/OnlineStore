namespace BookStore.Application.Common;

/// <summary>
/// Credentials or refresh token rejected. Mapped to 401 ProblemDetails. The message is deliberately
/// generic so responses never reveal whether an email is registered.
/// </summary>
public sealed class AuthenticationFailedException(string message = "Invalid credentials.") : Exception(message);
