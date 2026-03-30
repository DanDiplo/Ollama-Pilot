using System;
using System.Collections.Generic;
using System.Text;

namespace OllamaPilot
{
    internal static class DiffPreviewBuilder
    {
        public static string BuildUnifiedDiff(string originalText, string updatedText, string fileName)
        {
            var originalLines = SplitLines(originalText);
            var updatedLines = SplitLines(updatedText);
            var operations = BuildOperations(originalLines, updatedLines);

            var builder = new StringBuilder();
            builder.AppendLine("```diff");
            builder.AppendLine($"--- {fileName}");
            builder.AppendLine($"+++ {fileName}");

            foreach (var operation in operations)
            {
                switch (operation.Kind)
                {
                    case DiffKind.Unchanged:
                        builder.AppendLine($" {operation.Line}");
                        break;
                    case DiffKind.Removed:
                        builder.AppendLine($"-{operation.Line}");
                        break;
                    case DiffKind.Added:
                        builder.AppendLine($"+{operation.Line}");
                        break;
                    default:
                        throw new InvalidOperationException("Unexpected diff operation.");
                }
            }

            builder.AppendLine("```");
            return builder.ToString();
        }

        private static List<string> SplitLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new List<string>();
            }

            var normalized = text.Replace("\r\n", "\n");
            return new List<string>(normalized.Split('\n'));
        }

        private static List<DiffOperation> BuildOperations(IReadOnlyList<string> originalLines, IReadOnlyList<string> updatedLines)
        {
            var lcs = BuildLcsTable(originalLines, updatedLines);
            var operations = new List<DiffOperation>();
            BuildOperationsRecursive(originalLines, updatedLines, lcs, 0, 0, operations);
            return operations;
        }

        // Builds a table containing the lengths of the longest common subsequence
        // between suffixes of the original and updated line lists.
        private static int[,] BuildLcsTable(IReadOnlyList<string> originalLines, IReadOnlyList<string> updatedLines)
        {
            // The table dimensions are one larger than the input lists to handle base cases.
            var table = new int[originalLines.Count + 1, updatedLines.Count + 1];

            // Iterate over the original lines in reverse order.
            for (var i = originalLines.Count - 1; i >= 0; i--)
            {
                // Iterate over the updated lines in reverse order.
                for (var j = updatedLines.Count - 1; j >= 0; j--)
                {
                    // If the current lines match, extend the LCS length from the next diagonal cell.
                    if (string.Equals(originalLines[i], updatedLines[j], StringComparison.Ordinal))
                    {
                        table[i, j] = table[i + 1, j + 1] + 1;
                    }
                    // Otherwise, take the maximum LCS length from the cell below or to the right.
                    else
                    {
                        table[i, j] = Math.Max(table[i + 1, j], table[i, j + 1]);
                    }
                }
            }

            return table;
        }

        private static void BuildOperationsRecursive(
            IReadOnlyList<string> originalLines,
            IReadOnlyList<string> updatedLines,
            int[,] lcs,
            int originalIndex,
            int updatedIndex,
            ICollection<DiffOperation> operations)
        {
            while (originalIndex < originalLines.Count && updatedIndex < updatedLines.Count)
            {
                if (string.Equals(originalLines[originalIndex], updatedLines[updatedIndex], StringComparison.Ordinal))
                {
                    operations.Add(new DiffOperation(DiffKind.Unchanged, originalLines[originalIndex]));
                    originalIndex++;
                    updatedIndex++;
                    continue;
                }

                if (lcs[originalIndex + 1, updatedIndex] >= lcs[originalIndex, updatedIndex + 1])
                {
                    operations.Add(new DiffOperation(DiffKind.Removed, originalLines[originalIndex]));
                    originalIndex++;
                }
                else
                {
                    operations.Add(new DiffOperation(DiffKind.Added, updatedLines[updatedIndex]));
                    updatedIndex++;
                }
            }

            while (originalIndex < originalLines.Count)
            {
                operations.Add(new DiffOperation(DiffKind.Removed, originalLines[originalIndex]));
                originalIndex++;
            }

            while (updatedIndex < updatedLines.Count)
            {
                operations.Add(new DiffOperation(DiffKind.Added, updatedLines[updatedIndex]));
                updatedIndex++;
            }
        }

        private enum DiffKind
        {
            Unchanged,
            Removed,
            Added
        }

        private readonly struct DiffOperation
        {
            public DiffOperation(DiffKind kind, string line)
            {
                Kind = kind;
                Line = line ?? string.Empty;
            }

            public DiffKind Kind { get; }

            public string Line { get; }
        }
    }
}
