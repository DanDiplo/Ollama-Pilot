# Ollama Pilot

`Ollama Pilot` is a Visual Studio 2026 extension that brings local Ollama-powered coding assistance into the editor. It stays on the classic in-process VSIX model, talks to an Ollama server you control, and keeps code and prompts on your own machine or reverse proxy.

This project is a renamed and actively cleaned-up continuation of the original `LLMCopilot` extension.

## What It Does

Ollama Pilot currently supports:
- chat with a local or proxied Ollama model
- explain selected code
- explain likely errors from editor context
- review the current file
- generate tests for a selection or the current file
- add comments to selected code
- find bugs and suggest improvements
- optimize selected code
- fix diagnostics from the Error List
- summarize current Git working changes
- inline code completion in the editor

The chat window also supports:
- markdown replies
- syntax-highlighted fenced code blocks
- copy / insert / replace actions where they make sense
- optional thinking-depth support for reasoning-capable Ollama models such as `gpt-oss`

## Requirements

- Visual Studio 2026
- `Visual Studio extension development` workload
- local or reachable [Ollama](https://ollama.com/) server

Implementation note:
- this VSIX currently targets `.NET Framework 4.7.2`
- it uses the classic `Microsoft.VisualStudio.SDK` extension model, not the newer out-of-process `VisualStudio.Extensibility` model

## Setup

1. Install and start Ollama.
2. Pull at least one model for chat and optionally another for completion.
3. Build or install the VSIX.
4. Open `Extensions > Ollama Pilot > Settings`.
5. Configure:
   - `Base URL`
   - `Chat Model`
   - `Code Complete Model`
   - chat and completion context lengths
   - optional reverse proxy access token
   - optional thinking depth for chat models that support it

## Recommended Model Split

The extension works best when chat and completion are treated differently:
- use a stronger reasoning or code model for `Chat Model`
- use a faster, lighter coder model for `Code Complete Model`

Examples that have worked well during development:
- chat: `qwen2.5-coder:14b`
- chat: `gpt-oss:20b`
- completion: `qwen2.5-coder:1.5b` or `qwen2.5-coder:14b`

If your machine has limited VRAM, using one model for both chat and completion may feel more consistent than constantly swapping between large models.

## Settings Highlights

- `Reverse Proxy Access Token`
  Sends `Authorization: Bearer <token>` to a proxied Ollama endpoint.

- `Thinking Depth`
  Lets you choose `Off`, `Low`, `Medium`, or `High` for thinking-capable chat models.

- `FIM Tokens`
  Lets you customize fill-in-the-middle tokens for completion models that require specific prompt markers.

- `Auto Complete`
  Inline completion is optional and can be tuned with trigger mode, delay, and minimum prefix length.

Default acceptance behavior:
- `Alt+Q` accepts the full suggestion
- number keys can accept the top N lines
- `Esc` dismisses the suggestion

## Building From Source

Open [OllamaPilot.csproj](D:/Websites/Projects/Ollama-Pilot/LLMCopilot/vs2026/OllamaPilot.csproj) in Visual Studio 2026 and run with `F5` to launch the Experimental Instance.

Notes:
- the project uses the VS SDK build targets and is best built from Visual Studio
- the VSIX version in [source.extension.vsixmanifest](D:/Websites/Projects/Ollama-Pilot/LLMCopilot/vs2026/source.extension.vsixmanifest) is auto-incremented only for `Release` builds

## Project Structure

- [LLMCopilot](D:/Websites/Projects/Ollama-Pilot/LLMCopilot): extension source
- [Templates](D:/Websites/Projects/Ollama-Pilot/LLMCopilot/Templates): prompt templates
- [Images](D:/Websites/Projects/Ollama-Pilot/Images): shared images
- [CHANGELOG.md](D:/Websites/Projects/Ollama-Pilot/CHANGELOG.md): release history
- [FAQ.md](D:/Websites/Projects/Ollama-Pilot/FAQ.md): common questions

## Acknowledgements

- [Ollama](https://ollama.com/)
- [OllamaSharp](https://github.com/awaescher/OllamaSharp)
- [MdXaml](https://github.com/whistyun/MdXaml)
- [ICSharpCode.AvalonEdit](https://github.com/icsharpcode/AvalonEdit)

## License

This project is released under the MIT license. See [LICENSE.md](D:/Websites/Projects/Ollama-Pilot/LICENSE.md).
