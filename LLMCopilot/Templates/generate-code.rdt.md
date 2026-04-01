# Generate Code

Generate code using instructions.

## Response Prompt

```template-response
## Instructions
Generate code for the following specification.

## Specification
{{#each messages}}
{{#if (eq author "user")}}
{{content}}
{{/if}}
{{/each}}

## Instructions
Generate code for the specification.

## Code
\`\`\`

```
