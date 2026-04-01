# Explain Code

Explain the selected code.

## Initial Message Prompt

```template-initial-message
## Instructions
Summarize the code below (emphasizing its key functionality).

## Selected Code
\`\`\`
{{selectedText}}
\`\`\`

## Task
Summarize the code at a high level (including goal and purpose) with an emphasis on its key functionality.

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
Include code snippets (using Markdown) and examples where appropriate.

## Response
Bot:
```
