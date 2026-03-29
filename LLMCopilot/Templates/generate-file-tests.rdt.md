# Generate File Tests

Generate a practical test file for the current source file.

```template-initial-message
## Instructions
You are writing tests for the current {{language}} file from {{location}}.
Use the most likely testing style and framework for this language.
Return exactly one fenced code block containing the test code only.
Do not include explanation outside the code block.

## Current File
```{{language}}
{{selectedText}}
```

## Task
Generate a useful starting test file that covers:
- happy paths
- edge cases
- likely regressions

Prefer directly usable code over commentary.

## Response
```
