# Summarize Changes

Summarize the current Git diff like a practical coding assistant.

```template-initial-message
## Instructions
You are reviewing the current Git changes for the repository {{location}}.
Draft a concise commit title, summarize the changes, and call out any obvious risks or follow-up checks.

## Git Status
{{statusText}}

## Diff
\`\`\`diff
{{selectedText}}
\`\`\`

## Task
Respond with:
1. A one-line commit title.
2. A short paragraph summarizing the changes.
3. A short list of risks, assumptions, or follow-up checks if any stand out.

## Summary
```
