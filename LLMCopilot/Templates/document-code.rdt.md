# Document Code

Document the selected code.

## Initial Message Prompt

```template-initial-message
## Instructions
Add clear, concise comments to the following {{language}} code.
Preserve the existing behavior and formatting.
Return the complete updated snippet in exactly one fenced `{{language}}` code block.
Do not include any explanation before or after the code block.
Do not use placeholders such as `...`.
Keep the code valid and compilable.

## Code
\`\`\`{{language}}
{{selectedText}}
\`\`\`

## Documented Code
Return the final answer now as one complete fenced code block.
```
