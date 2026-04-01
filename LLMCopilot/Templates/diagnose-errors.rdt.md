# Explain Code

Diagnoses any errors or warnings the selected code.

## Initial Message Prompt

```template-initial-message
## Instructions
Read through the errors and warnings in the code below.

## Selected Code
\`\`\`
{{selectedTextWithDiagnostics}}
\`\`\`

## Task
For each error or warning, write a paragraph that describes the most likely cause and a potential fix.
Include code snippets where appropriate.

## Answer

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
{{selectedTextWithDiagnostics}}
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
Consider possibility that there might not be a solution.
Ask for clarification if the message does not make sense or more input is needed.
Omit any links.
Include code snippets (using Markdown) and examples where appropriate.

## Response
Bot:
```
