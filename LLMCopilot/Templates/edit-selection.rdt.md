# Edit Selection

Generate a careful revision for the currently selected code.

```template-initial-message
## Instructions
You are editing existing {{language}} code from {{location}}.
Apply the user's requested change conservatively.
Preserve behavior unless the request clearly asks for a behavior change.
Keep naming, formatting, and style consistent with the original codebase.
Return exactly one fenced code block containing only the revised code.
Do not include explanations, bullets, or commentary outside the code block.

## Requested Change
{{instructions}}

## Selected Code
```{{language}}
{{selectedText}}
```

## Response
```
