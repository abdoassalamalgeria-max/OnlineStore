namespace BookStore.Application.Common;

/// <summary>The request clashes with existing state (e.g. email already registered). Mapped to 409.</summary>
public sealed class ConflictException(string message) : Exception(message);
