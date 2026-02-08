using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Reqnroll.LanguageServer.Services;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Reqnroll.LanguageServer.Handlers;

/// <summary>
/// Formats Reqnroll feature files by aligning tables, indenting steps, and normalizing keywords.
/// Applies consistent formatting rules to improve readability and maintain code standards.
/// </summary>
public class ReqnrollDocumentFormattingHandler : DocumentFormattingHandlerBase
{
    private readonly DocumentStorageService _documentStorageService;

    public ReqnrollDocumentFormattingHandler(DocumentStorageService documentStorageService)
    {
        _documentStorageService = documentStorageService;
    }

    protected override DocumentFormattingRegistrationOptions CreateRegistrationOptions(
        DocumentFormattingCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new DocumentFormattingRegistrationOptions
        {
            DocumentSelector = DocumentSelector.ForLanguage("reqnroll-feature")
        };
    }

    /// <summary>
    /// Handles formatting requests by applying table alignment, step indentation, and keyword normalization.
    /// </summary>
    public override Task<TextEditContainer?> Handle(
        DocumentFormattingParams request,
        CancellationToken cancellationToken)
    {
        var documentContent = _documentStorageService.Get(request.TextDocument.Uri);
        
        if (string.IsNullOrEmpty(documentContent))
        {
            return Task.FromResult<TextEditContainer?>(new TextEditContainer());
        }

        var edits = new List<TextEdit>();
        edits.AddRange(FormatBlockKeywords(documentContent));
        edits.AddRange(FormatStepDefinitions(documentContent));
        edits.AddRange(FormatTables(documentContent));
        return Task.FromResult<TextEditContainer?>(new TextEditContainer(edits));
    }

    /// <summary>
    /// Formats block keywords (Feature, Background, Scenario, etc.) to have zero indentation and a space after the colon.
    /// </summary>
    private static List<TextEdit> FormatBlockKeywords(string documentText)
    {
        var edits = new List<TextEdit>();
        var lines = documentText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var blockKeywords = new[] { "Feature:", "Background:", "Scenario:", "Scenario Outline:", "Examples:" };

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            // Check if this line starts with a block keyword
            var matchedKeyword = blockKeywords.FirstOrDefault(keyword => trimmed.StartsWith(keyword));
            if (matchedKeyword != null)
            {
                var restOfLine = trimmed.Substring(matchedKeyword.Length).TrimStart();
                var formattedLine = matchedKeyword + " " + restOfLine;

                // Block keywords should have no indentation and a space after the colon
                if (line != formattedLine)
                {
                    edits.Add(new TextEdit
                    {
                        Range = new Range
                        {
                            Start = new Position(i, 0),
                            End = new Position(i, line.Length)
                        },
                        NewText = formattedLine
                    });
                }
            }
        }

        return edits;
    }

    /// <summary>
    /// Formats all tables in the document by aligning columns with consistent spacing.
    /// </summary>
    public static List<TextEdit> FormatTables(string documentText)
    {
        var edits = new List<TextEdit>();
        var lines = documentText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var tableRows = new List<TableRow>();
        int tableStart = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (trimmed.StartsWith("|") && trimmed.EndsWith("|"))
            {
                // This is a table row
                if (tableStart == -1)
                {
                    tableStart = i;
                }

                // Capture leading whitespace
                var indent = line.Length - line.TrimStart().Length;
                var indentation = line.Substring(0, indent);

                var cells = line.Split('|')
                    .Skip(1)
                    .Take(line.Split('|').Length - 2)
                    .Select(cell => cell.Trim())
                    .ToArray();

                tableRows.Add(new TableRow { LineIndex = i, Cells = cells, Indentation = indentation });
            }
            else
            {
                // Not a table row - process accumulated table if any
                if (tableRows.Count > 0)
                {
                    edits.AddRange(FormatTable(lines, tableRows));
                    tableRows.Clear();
                    tableStart = -1;
                }
            }
        }

        // Process any remaining table at the end
        if (tableRows.Count > 0)
        {
            edits.AddRange(FormatTable(lines, tableRows));
        }

        return edits;
    }

    /// <summary>
    /// Formats step definitions by adding tab indentation and normalizing consecutive keywords to 'And'.
    /// </summary>
    private static List<TextEdit> FormatStepDefinitions(string documentText)
    {
        var edits = new List<TextEdit>();
        var lines = documentText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var stepKeywords = new[] { "Given", "When", "Then", "And", "But" };
        var primaryKeywords = new[] { "Given", "When", "Then" };
        string? lastPrimaryKeyword = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            // Check if this line starts with a step keyword
            var matchedKeyword = stepKeywords.FirstOrDefault(keyword => trimmed.StartsWith(keyword + " "));
            if (matchedKeyword != null)
            {
                string keywordToUse = matchedKeyword;
                var restOfLine = trimmed.Substring(matchedKeyword.Length);

                // Replace consecutive same primary keywords with "And"
                if (primaryKeywords.Contains(matchedKeyword))
                {
                    if (lastPrimaryKeyword == matchedKeyword)
                    {
                        keywordToUse = "And";
                    }
                    else
                    {
                        lastPrimaryKeyword = matchedKeyword;
                    }
                }

                // Step should be indented by one tab
                var formattedLine = "\t" + keywordToUse + restOfLine;

                if (line != formattedLine)
                {
                    edits.Add(new TextEdit
                    {
                        Range = new Range
                        {
                            Start = new Position(i, 0),
                            End = new Position(i, line.Length)
                        },
                        NewText = formattedLine
                    });
                }
            }
            else
            {
                // Reset tracking when we encounter a non-step line
                lastPrimaryKeyword = null;
            }
        }

        return edits;
    }

    /// <summary>
    /// Formats a single table by calculating column widths and applying padding.
    /// </summary>
    private static List<TextEdit> FormatTable(string[] lines, List<TableRow> rows)
    {
        var edits = new List<TextEdit>();

        // Calculate column widths
        var columnWidths = new int[rows.Max(r => r.Cells.Length)];
        foreach (var row in rows)
        {
            for (int i = 0; i < row.Cells.Length; i++)
            {
                columnWidths[i] = Math.Max(columnWidths[i], row.Cells[i].Length);
            }
        }

        // Format each row
        foreach (var row in rows)
        {
            var paddedCells = row.Cells.Select((cell, i) =>
            {
                return " " + cell.PadRight(columnWidths[i]) + " ";
            });

            var formattedLine = row.Indentation + "|" + string.Join("|", paddedCells) + "|";
            var currentLine = lines[row.LineIndex];

            if (currentLine != formattedLine)
            {
                edits.Add(new TextEdit
                {
                    Range = new Range
                    {
                        Start = new Position(row.LineIndex, 0),
                        End = new Position(row.LineIndex, currentLine.Length)
                    },
                    NewText = formattedLine
                });
            }
        }

        return edits;
    }

    private class TableRow
    {
        public int LineIndex { get; set; }
        public string[] Cells { get; set; } = Array.Empty<string>();
        public string Indentation { get; set; } = string.Empty;
    }
}
