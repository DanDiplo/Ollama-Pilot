# Document Code

Document the selected code.

## Initial Message Prompt

```template-initial-message
## Instructions
Document the selected code only.
The programming language is {{language}}.
Only add comments to the existing snippet.
Do not refactor, simplify, optimize, rename, reorder, wrap, unwrap, or reformat the code.
Preserve behavior, control flow, APIs, identifiers, existing statements, and existing code structure exactly as provided.
Preserve the original line structure as closely as possible.
Do not collapse the code onto one line.
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
The opening fence must be followed by a newline, then the code, then a closing fence on its own line.
Keep comments and code on separate lines so the result remains valid compilable {{language}} code.

## Code
\`\`\`{{language}}
{{selectedText}}
\`\`\`

## Documented Code
Return the final answer now as one complete fenced code block.
```

### Response Prompt

```template-response
## Instructions
Document the selected code only.
The programming language is {{language}}.
Only add comments to the existing snippet.
Do not refactor, simplify, optimize, rename, reorder, wrap, unwrap, or reformat the code.
Preserve behavior, control flow, APIs, identifiers, existing statements, and existing code structure exactly as provided.
Preserve the original line structure as closely as possible.
Do not collapse the code onto one line.
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
The opening fence must be followed by a newline, then the code, then a closing fence on its own line.
Keep comments and code on separate lines so the result remains valid compilable {{language}} code.

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
Return the final answer now as one complete fenced code block.
```
