namespace BookStore.Application.Common;

/// <summary>The requested resource does not exist. Mapped to 404 ProblemDetails by the Api.</summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.")
{
    public string Resource { get; } = resource;
    public object Key { get; } = key;
}
