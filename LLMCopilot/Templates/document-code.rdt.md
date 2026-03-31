# Document Code

Document the selected code.

## Template

### Configuration

````json conversation-template
{
  "id": "document-code",
  "engineVersion": 0,
  "label": "Document Code",
  "tags": ["generate", "document"],
  "description": "Document the selected code.",
  "header": {
    "title": "Document Code {{location}}",
    "icon": {
      "type": "codicon",
      "value": "output"
    }
  },
  "variables": [
    {
      "name": "selectedText",
      "time": "conversation-start",
      "type": "selected-text",
      "constraints": [{ "type": "text-length", "min": 1 }]
    },
    {
      "name": "language",
      "time": "conversation-start",
      "type": "language",
      "constraints": [{ "type": "text-length", "min": 1 }]
    }
  ],
  "chatInterface": "instruction-refinement",
  "initialMessage": {
    "placeholder": "Documenting selection",
    "maxTokens": 2048,
    "stop": ["```"],
    "completionHandler": {
      "type": "active-editor-diff",
      "botMessage": "Generated documentation."
    }
  },
  "response": {
    "placeholder": "Documenting selection",
    "maxTokens": 2048,
    "stop": ["```"],
    "completionHandler": {
      "type": "active-editor-diff",
      "botMessage": "Generated documentation."
    }
  }
}
````

### Initial Message Prompt

```template-initial-message
## Instructions
Document the selected code only.
The programming language is {{language}}.
Only add comments to the existing snippet.
Do not refactor, simplify, optimize, rename, reorder, wrap, unwrap, or reformat the code.
Preserve behavior, control flow, APIs, identifiers, existing statements, and existing code structure exactly as provided.
Do not add or remove using/import statements, namespace declarations, class declarations, helper methods, parameters, attributes, or surrounding boilerplate.
If the input is only a method or fragment, return only that same method or fragment with comments added.
If the selected snippet is currently undocumented, add at least one meaningful comment.
For csharp snippets, XML documentation comments (///) immediately above an existing method, property, class, constructor, or enum declaration are allowed when they fit naturally.
If you add XML documentation comments, do not make any other code changes.
Otherwise, the first non-empty line of code in your answer must match the first non-empty line of the input snippet.
The last non-empty line of code in your answer must match the last non-empty line of the input snippet.
If you are unsure, return the original snippet with minimal comments rather than inventing new code.
Prefer one concise method, class, or block comment over many small comments.
Do not narrate every statement or restate obvious code.
Only add comments that explain intent, purpose, side effects, invariants, or non-obvious behavior.
Do not mention APIs, types, methods, variables, threads, or behaviors that are not literally present in the code.
Do not describe the code as using a different API than the one shown.
If the code is already self-explanatory but undocumented, add a brief summary comment rather than returning it unchanged.
For csharp member declarations, prefer concise XML documentation comments over inline comments when that better matches normal C# style.
Return exactly one fenced code block containing the commented snippet and nothing else.

## Code
\`\`\`{{language}}
{{selectedText}}
\`\`\`

## Documented Code
\`\`\`{{language}}

```

### Response Prompt

```template-response
## Instructions
Document the selected code only.
The programming language is {{language}}.
Only add comments to the existing snippet.
Do not refactor, simplify, optimize, rename, reorder, wrap, unwrap, or reformat the code.
Preserve behavior, control flow, APIs, identifiers, existing statements, and existing code structure exactly as provided.
Do not add or remove using/import statements, namespace declarations, class declarations, helper methods, parameters, attributes, or surrounding boilerplate.
If the input is only a method or fragment, return only that same method or fragment with comments added.
If the selected snippet is currently undocumented, add at least one meaningful comment.
For csharp snippets, XML documentation comments (///) immediately above an existing method, property, class, constructor, or enum declaration are allowed when they fit naturally.
If you add XML documentation comments, do not make any other code changes.
Otherwise, the first non-empty line of code in your answer must match the first non-empty line of the input snippet.
The last non-empty line of code in your answer must match the last non-empty line of the input snippet.
If you are unsure, return the original snippet with minimal comments rather than inventing new code.
Prefer one concise method, class, or block comment over many small comments.
Do not narrate every statement or restate obvious code.
Only add comments that explain intent, purpose, side effects, invariants, or non-obvious behavior.
Do not mention APIs, types, methods, variables, threads, or behaviors that are not literally present in the code.
Do not describe the code as using a different API than the one shown.
If the code is already self-explanatory but undocumented, add a brief summary comment rather than returning it unchanged.
For csharp member declarations, prefer concise XML documentation comments over inline comments when that better matches normal C# style.
Return exactly one fenced code block containing the commented snippet and nothing else.

Consider the following instructions:
{{#each messages}}
{{#if (eq author "user")}}
{{content}}
{{/if}}
{{/each}}

## Code
\`\`\`{{language}}
{{selectedText}}
\`\`\`

## Documented Code
\`\`\`{{language}}

```
