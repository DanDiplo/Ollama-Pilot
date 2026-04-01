# Find Bugs

Template to find bugs in the selected code.

## Initial Message Prompt

```template-initial-message
## Instructions
What could be wrong with the code below?
Only consider defects that would lead to incorrect behavior.
The programming language is {{language}}.

## Selected Code
\`\`\`
{{selectedText}}
\`\`\`

## Task
Describe what could be wrong with the code?
Only consider defects that would lead to incorrect behavior.
Provide potential fix suggestions where possible.
Consider that there might not be any problems with the code.
Include code snippets (using Markdown) and examples where appropriate.
Do not rewrite or modify the original code unless showing a small illustrative fix snippet.

## Analysis

```

### Response Prompt

```template-response
## Instructions
Continue the conversation below.
Pay special attention to the current developer request.
The programming language is {{language}}.

## Current Request
Developer: {{lastMessage}}

{{#if selectedText}}
## Selected Code
\`\`\`
{{selectedText}}
\`\`\`
{{/if}}

## Potential Bugs
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
Use the style of a documentation article.
Omit any links.
Include code snippets (using Markdown) and examples where appropriate.

## Response
Bot:
```
