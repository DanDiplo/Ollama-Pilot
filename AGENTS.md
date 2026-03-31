# AGENTS.md

## Project Overview

`Ollama Pilot` is a classic in-process Visual Studio VSIX extension for Visual Studio 2026 that connects to a local or proxied [Ollama](https://ollama.com/) server.

The extension currently:
- targets `.NET Framework 4.7.2`
- uses the classic `Microsoft.VisualStudio.SDK` / `AsyncPackage` VSIX model
- is not a `VisualStudio.Extensibility` out-of-process extension
- uses the `OllamaSharp` NuGet package through a local adapter layer

## Repository Layout

- `README.md`: user-facing overview
- `CHANGELOG.md`: release notes
- `FAQ.md`: end-user FAQ
- `Images/`: shared repo images
- `LLMCopilot/`: extension source
- `LLMCopilot/Commands/`: VS command handlers
- `LLMCopilot/Commands/Executors/`: shared command request builders/executors
- `LLMCopilot/UI/Chat/`: chat tool window UI
- `LLMCopilot/UI/Settings/`: settings UI and options page
- `LLMCopilot/Services/Ollama/`: Ollama adapter, options, validation
- `LLMCopilot/Services/VisualStudio/`: VS/editor/git helpers and adornment logic
- `LLMCopilot/Infrastructure/`: prompt rendering, logging, shims
- `LLMCopilot/Package/`: package registration and VSCT assets
- `LLMCopilot/vs2026/OllamaPilot.csproj`: VSIX project
- `LLMCopilot/vs2026/source.extension.vsixmanifest`: VSIX metadata
- `LLMCopilot/Templates/*.rdt.md`: prompt templates copied into the VSIX

## Architecture Notes

### Visual Studio integration

The extension is built around these areas:
- `Package/LLMCopilotPackage.cs`: package registration, option page, tool window, command registration
- `UI/Chat/LLMChatWindow*.cs` and `UI/Chat/LLMChatWindowControl.xaml*`: chat tool window and reply actions
- `Services/VisualStudio/LLMAdornment.cs`: inline completion / editor adornment behavior
- `Services/VisualStudio/VsHelper.cs`: editor, document, formatting, and DTE/VS utility code
- command files under `Commands/` such as `ExplainCommand.cs`, `AddCommentCommand.cs`, `ReviewFileCommand.cs`, `ErrorListFixCommand.cs`

### Ollama integration

The extension should talk to Ollama through the local abstraction layer, not directly from UI/command code:
- `Services/Ollama/OllamaAbstractions.cs`
- `Services/Ollama/OllamaSharpService.cs`
- `Services/Ollama/OllamaHelper.cs`

Keep Ollama-specific package API changes inside the adapter when possible.

### Prompt templates

Prompt text lives in `LLMCopilot/Templates/*.rdt.md` and is rendered by `PromptTemplateService.cs`.

Important:
- the current renderer extracts fenced blocks from `.rdt.md` files
- nested raw triple-backtick fences inside a template block can break prompt extraction
- when embedding code fences inside template content, escape or structure them carefully

## Build and Debug

Preferred workflow:
1. Open `D:\Websites\Projects\Ollama-Pilot\LLMCopilot\vs2026\OllamaPilot.csproj` in Visual Studio 2026.
2. Ensure the `Visual Studio extension development` workload is installed.
3. Build and run with `F5`.
4. Test in the Experimental Instance started with `/rootsuffix Exp`.

Notes:
- `dotnet build` in a plain shell is not a reliable validation path for this project because the full VS SDK toolchain is required.
- The project auto-increments the VSIX version only for `Release|AnyCPU` builds.

## Runtime Constraints

- Stay on `.NET Framework` for this VSIX unless the extension is re-architected onto the newer out-of-process model.
- Do not assume Visual Studio editor services are available off the UI thread.
- Prefer `ThreadHelper.JoinableTaskFactory.Run(() => ExecuteAsync(...))` only at actual VS command boundaries.
- Keep internal work async where possible to avoid `VSTHRD102` issues.

## Editing Guidelines

- Prefer updating prompt behavior in templates before changing many command handlers.
- For command-specific safety rules, deterministic validation in code is often better than relying only on prompt wording.
- Keep assistant action buttons context-aware. Not every reply should offer replace/apply actions.
- If you touch chat rendering, test both:
  - normal markdown/code-block replies
  - thinking-only or partial responses from reasoning models such as `gpt-oss`

## Known Important Behaviors

- The chat tool window can reset conversation state for command-driven requests to avoid cross-command contamination.
- `Review Current File` and similar actions should operate on the active document, even if the chat tool window currently has focus.
- `Summarize Working Changes` should consider staged, unstaged, and untracked files.
- Some reasoning models may emit content via a thinking stream. The UI should handle that gracefully.

## Manual Smoke Tests

Before shipping meaningful changes, manually test these flows in the Experimental Instance:
- `Test Connection`
- `Open Chat Window`
- `Explain Code`
- `Add Comment`
- `Review Current File`
- `Generate Tests for File`
- `Fix Error with Ollama Pilot`
- `Summarize Working Changes`
- inline completion / accept / dismiss flow
- chat code-block rendering, copy, insert, replace selection, replace file

## Documentation Expectations

If behavior, settings, supported VS versions, or model guidance changes, update:
- `README.md`
- `FAQ.md` if the user-facing setup story changed
- `CHANGELOG.md` if the change is release-worthy
