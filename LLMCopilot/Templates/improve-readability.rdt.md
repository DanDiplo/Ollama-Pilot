# Improve Readability

The improve readability analysis suggests ways to make the selected code easier to read.

## Initial Message Prompt

```template-initial-message
## Instructions
How could the readability of the code below be improved?
The programming language is {{language}}.
Consider overall readability and idiomatic constructs.

## Selected Code
\`\`\`
{{selectedText}}
\`\`\`

## Task
How could the readability of the code be improved?
The programming language is {{language}}.
Consider overall readability and idiomatic constructs.
Provide potential improvements suggestions where possible.
Consider that the code might be perfect and no improvements are possible.
Include code snippets (using Markdown) and examples where appropriate.
The code snippets must contain valid {{language}} code.

## Readability Improvements

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

## Conversation
{{#each messages}}
{{#if (eq author "bot")}}
Bot: {{content}}
{{else}}
Developer: {{content}}
{{/if}}
{{/each}}

## Task
Write a response that continues the conversation.
Stay focused on current developer request.
Consider the possibility that there might not be a solution.
Ask for clarification if the message does not make sense or more input is needed.
Omit any links.
Include code snippets (using Markdown) and examples where appropriate.
The code snippets must contain valid {{language}} code.

## Response
Bot:
```
