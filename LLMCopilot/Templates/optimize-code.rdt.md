# Optimize Code

Improve the readability and performance of the selected code.

## Template

### Initial Message Prompt

```template-initial-message
## Instructions
How could the readability and performance of the code below be improved?
The programming language is {{language}}.
Consider overall readability, performance and idiomatic constructs.

## Selected Code
\`\`\`
{{selectedText}}
\`\`\`

## Task
How could the readability and performance of the code be improved?
The programming language is {{language}}.
Consider overall readability, performance and idiomatic constructs.
Provide potential improvement suggestions where possible.
Consider that the code might be perfect and no improvements are possible.
Include code snippets (using Markdown) and examples where appropriate.
The code snippets must contain valid {{language}} code.
Always provide a visible response.
If the code is already fine, say that explicitly and briefly explain why.

## Readability and Performance Improvements

```
