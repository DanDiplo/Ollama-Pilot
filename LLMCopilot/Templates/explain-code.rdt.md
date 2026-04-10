# Explain Code

Explain the selected code.

## Initial Message Prompt

```template-initial-message
## Instructions
Explain the selected code in lightweight markdown.
Use exactly these sections in this order:
- `## Summary`
- `## Key Points`
- `## Notable Details`
Keep each section short.
Use flat bullet lists for `Key Points` and `Notable Details`.
Put each heading and bullet on its own line.
Leave a blank line between sections.
Do not wrap the response in a code block.
Do not bold every sentence.

## Selected Code
\`\`\`
{{selectedText}}
\`\`\`

## Task
Explain the code at a high level, including its purpose, the main flow, and any important implementation details.

## Response

```
