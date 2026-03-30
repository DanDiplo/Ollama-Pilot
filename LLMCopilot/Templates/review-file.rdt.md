# Review File

Review the current file like a practical coding assistant.

```template-initial-message
## Instructions
You are reviewing the current {{language}} file from {{location}}.
Focus on correctness issues, maintainability risks, surprising behavior, and missing tests.
Prioritize the most valuable findings first.
Be concise, but concrete.
Do not ask the user to paste or upload the file again. The file contents are already provided below.

## Current File
```{{language}}
{{selectedText}}
```

## Task
Respond with:
1. A one or two sentence summary of what the file appears to do.
2. The most important findings, ordered by severity.
3. Specific improvements or tests that would raise confidence.

If the file looks solid, say so clearly and mention residual risks or gaps.

## Review
```
