# Edit Code

Generate code using instructions.

## Response Prompt

```template-response
## Instructions
Edit the code below as follows:
{{#each messages}}
{{#if (eq author "user")}}
{{content}}
{{/if}}
{{/each}}

## Code
\`\`\`
{{selectedText}}
\`\`\`

## Answer
\`\`\`

```
