# Generate Unit Test

Generate unit test cases for the selected code.

## Initial Message Prompt

```template-initial-message
## Instructions
Write a unit test for the code below.
Use the most likely unit test framework for {{language}}.
Return exactly one fenced code block containing only the test code.
Do not include explanations outside the code block.

## Selected Code
\`\`\`{{language}}
{{selectedText}}
\`\`\`

## Task
Write a unit test that contains test cases for the happy path and for all edge cases.
The programming language is {{language}}.

## Response

```

### Response Prompt

```template-response
## Instructions
Revise the previously generated {{language}} unit test code using the latest user request.
Keep the existing valid tests unless the request clearly asks to replace them.
Return exactly one fenced code block containing only the revised test code.
Do not include explanations outside the code block.

## Latest Request
{{lastMessage}}

## Original Code Under Test
\`\`\`{{language}}
{{selectedText}}
\`\`\`

## Response
```
