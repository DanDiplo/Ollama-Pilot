namespace OllamaPilot.Core.Models;

[Flags]
public enum AssistantActionCapabilities
{
    None = 0,
    UseAsDraft = 1 << 0,
    CopyCode = 1 << 1,
    PreviewDiff = 1 << 2,
    InsertIntoEditor = 1 << 3,
    ReplaceSelection = 1 << 4,
    ReplaceFile = 1 << 5,
    CreateSiblingFile = 1 << 6,
    Discussion = UseAsDraft,
    FileGeneration = UseAsDraft | CopyCode | InsertIntoEditor | CreateSiblingFile,
    FileRewrite = UseAsDraft | CopyCode | PreviewDiff | InsertIntoEditor | ReplaceFile,
    SelectionEdit = UseAsDraft | CopyCode | PreviewDiff | InsertIntoEditor | ReplaceSelection | ReplaceFile | CreateSiblingFile
}
