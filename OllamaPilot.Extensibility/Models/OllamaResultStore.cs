using OllamaPilot.Core.Models;

namespace OllamaPilot.Extensibility.Models;

internal sealed class OllamaResultStore
{
    public OllamaParsedResponse Current { get; private set; } = new();
    public event Action? ResultChanged;

    public void SetResult(OllamaParsedResponse result)
    {
        Current = result;
        ResultChanged?.Invoke();
    }
}
