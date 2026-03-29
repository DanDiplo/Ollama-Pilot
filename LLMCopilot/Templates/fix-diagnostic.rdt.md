# Fix Diagnostic

Handle a project, build, restore, or configuration diagnostic that is not tied to a specific source file.

```template-initial-message
## Instructions
You are helping fix a project or build diagnostic for {{location}}.
Focus on the specific warning or error text below.
Do not invent source files, classes, methods, namespaces, or unit tests.
Do not rewrite unrelated code.
If the diagnostic is about package versions, restore, SDK resolution, or project configuration, explain the likely cause and the smallest practical fix.
Be concrete and actionable.

## Diagnostic
{{diagnosticText}}

## Task
Respond with:
1. A brief explanation of the likely cause.
2. The smallest practical fix.
3. Any exact project/package/version changes to make if they can be inferred safely.

If the diagnostic text is insufficient to identify a safe fix, say what additional file or context is needed.

## Response
```
