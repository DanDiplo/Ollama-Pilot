namespace OllamaPilot.Core.Models;

public sealed class GitChangesContext
{
    public string RepositoryRoot { get; init; } = string.Empty;
    public string StatusText { get; init; } = string.Empty;
    public string DiffText { get; init; } = string.Empty;
}
