namespace BookStore.Application.Common;

/// <summary>The user is signed in but not allowed to do this (e.g. review a book they did not buy). Mapped to 403.</summary>
public sealed class ForbiddenException(string message) : Exception(message);
