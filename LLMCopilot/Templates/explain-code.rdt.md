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

### Response Prompt

```template-response
## Instructions
Continue the conversation below.
Pay special attention to the current developer request.

## Current Request
Developer: {{lastMessage}}

{{#if selectedText}}
## Selected Code
\`\`\`
{{selectedText}}
\`\`\`
{{/if}}

## Code Summary
{{firstMessage}}

## Conversation
{{#each messages}}
{{#if (neq @index 0)}}
{{#if (eq author "bot")}}
Bot: {{content}}
{{else}}
Developer: {{content}}
{{/if}}
{{/if}}
{{/each}}

## Task
Write a response that continues the conversation.
Stay focused on current developer request.
Consider the possibility that there might not be a solution.
Ask for clarification if the message does not make sense or more input is needed.
Omit any links.
For non-code answers, use lightweight markdown with short sections and flat bullet lists.
Put each heading and bullet on its own line and avoid dense paragraphs.
Only include fenced code blocks when the developer explicitly asks for code.

## Response
Bot:
```
