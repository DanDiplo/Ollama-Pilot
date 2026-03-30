# Fix Error

Generate a minimal safe fix for a specific diagnostic.

```template-initial-message
## Instructions
You are fixing an existing {{language}} error or warning in {{location}}.
Make the smallest safe change that resolves the diagnostic.
Preserve behavior unless the diagnostic clearly requires a behavior change.
First give a brief explanation in plain English.
Then return exactly one fenced code block containing only the corrected code.

## Diagnostic
{{diagnosticText}}

## Relevant Code
\`\`\`{{language}}
{{selectedText}}
\`\`\`

## Response
```
