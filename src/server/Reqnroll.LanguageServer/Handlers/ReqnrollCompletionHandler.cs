using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Reqnroll.LanguageServer.Services;
using System.Text.RegularExpressions;

namespace Reqnroll.LanguageServer.Handlers;

/// <summary>
/// Provides auto-completion suggestions for Reqnroll step definitions.
/// Suggests available step bindings based on the project's step definition methods.
/// </summary>
public class ReqnrollCompletionHandler : CompletionHandlerBase
{
    private readonly ReqnrollBindingStorageService _reqnrollBindingStorageService;
    private readonly DocumentStorageService _documentStorageService;

    public ReqnrollCompletionHandler(
        ReqnrollBindingStorageService reqnrollBindingStorageService,
        DocumentStorageService documentStorageService)
    {
        _reqnrollBindingStorageService = reqnrollBindingStorageService;
        _documentStorageService = documentStorageService;
    }

    protected override CompletionRegistrationOptions CreateRegistrationOptions(
        CompletionCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new CompletionRegistrationOptions
        {
            DocumentSelector = DocumentSelector.ForLanguage("reqnroll-feature"),
            TriggerCharacters = new[] { "G", "W", "T", "A", "B", "F", "S", "E" },
            ResolveProvider = false
        };
    }

    /// <summary>
    /// Handles completion requests by providing step definition suggestions from the binding store.
    /// Filters by context and user input.
    /// </summary>
    public override Task<CompletionList> Handle(
        CompletionParams request,
        CancellationToken cancellationToken)
    {
        // Get the document content
        var documentContent = _documentStorageService.Get(request.TextDocument.Uri);
        if (documentContent == null)
        {
            return Task.FromResult(new CompletionList(isIncomplete: false));
        }

        var lines = documentContent.Split('\n');
        if (request.Position.Line >= lines.Length)
        {
            return Task.FromResult(new CompletionList(isIncomplete: false));
        }

        var currentLine = lines[request.Position.Line];
        var lineBeforeCursor = currentLine.Substring(0, Math.Min((int)request.Position.Character, currentLine.Length));
        var textBeforeCursor = lineBeforeCursor.TrimStart();
        var currentIndent = lineBeforeCursor.Length - textBeforeCursor.Length;
        var isAtLineStart = currentIndent == 0 && string.IsNullOrWhiteSpace(textBeforeCursor);

        // If document is empty or only whitespace, offer Feature keyword
        if (string.IsNullOrWhiteSpace(documentContent))
        {
            return Task.FromResult(new CompletionList(new[] {
                CreateBlockKeywordCompletion("Feature", "Defines a feature containing scenarios", 0)
            }, isIncomplete: false));
        }

        var isInsideBlock = IsInsideScenarioOrBackground(lines, request.Position);

        // If cursor is at position 0 (start of line), only offer block keywords
        if (isAtLineStart)
        {
            var blockCompletions = new List<CompletionItem>();
            var keywords = new[]
            {
                ("Background", "Steps run before each scenario"),
                ("Scenario", "A test scenario"),
                ("Scenario Outline", "A parameterized scenario template"),
                ("Examples", "Data table for Scenario Outline"),
                ("Feature", "Defines a feature containing scenarios")
            };

            foreach (var (keyword, description) in keywords)
            {
                blockCompletions.Add(CreateBlockKeywordCompletion(keyword, description, 0));
            }

            return Task.FromResult(new CompletionList(blockCompletions, isIncomplete: false));
        }

        // If no text typed yet on an indented line
        if (string.IsNullOrWhiteSpace(textBeforeCursor))
        {
            var completionItems = new List<CompletionItem>();

            if (isInsideBlock)
            {
                // Offer step keywords first (with current indent)
                completionItems.AddRange(new[] {
                    CreateStepKeywordCompletion("Given", "Precondition or context setup", currentIndent),
                    CreateStepKeywordCompletion("When", "Action or event", currentIndent),
                    CreateStepKeywordCompletion("Then", "Expected outcome or result", currentIndent),
                    CreateStepKeywordCompletion("And", "Additional step of same type as previous", currentIndent),
                    CreateStepKeywordCompletion("But", "Negative additional step of same type as previous", currentIndent)
                });

                // Then offer block keywords (with indent 0)
                var keywords = new[]
                {
                    ("Background", "Steps run before each scenario"),
                    ("Scenario", "A test scenario"),
                    ("Scenario Outline", "A parameterized scenario template"),
                    ("Examples", "Data table for Scenario Outline")
                };

                foreach (var (keyword, description) in keywords)
                {
                    completionItems.Add(CreateBlockKeywordCompletion(keyword, description, 0));
                }
            }
            else
            {
                // Outside scenario: offer block keywords only
                var keywords = new[]
                {
                    ("Background", "Steps run before each scenario"),
                    ("Scenario", "A test scenario"),
                    ("Scenario Outline", "A parameterized scenario template"),
                    ("Examples", "Data table for Scenario Outline"),
                    ("Feature", "Defines a feature containing scenarios")
                };

                foreach (var (keyword, description) in keywords)
                {
                    completionItems.Add(CreateBlockKeywordCompletion(keyword, description, 0));
                }
            }

            return Task.FromResult(new CompletionList(completionItems, isIncomplete: false));
        }

        // If outside a block, only offer block keyword completions based on what's typed
        if (!isInsideBlock)
        {
            var blockCompletions = new List<CompletionItem>();
            var typed = textBeforeCursor.Trim();
            
            // Check each block keyword to see if it matches what the user typed
            var keywords = new[]
            {
                ("Background", "Steps run before each scenario"),
                ("Scenario", "A test scenario"),
                ("Scenario Outline", "A parameterized scenario template"),
                ("Examples", "Data table for Scenario Outline"),
                ("Feature", "Defines a feature containing scenarios")
            };

            foreach (var (keyword, description) in keywords)
            {
                if (keyword.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                {
                    blockCompletions.Add(CreateBlockKeywordCompletion(keyword, description, 0));
                }
            }

            return Task.FromResult(new CompletionList(blockCompletions, isIncomplete: false));
        }

        // Determine what step type to filter by
        var requestedStepType = DetermineStepType(textBeforeCursor, lines, request.Position);
        if (string.IsNullOrEmpty(requestedStepType))
        {
            return Task.FromResult(new CompletionList(isIncomplete: false));
        }

        // Check if user has already typed the keyword
        var hasKeyword = HasStepKeyword(textBeforeCursor);

        // Get step completions filtered by type
        var stepCompletions = new List<CompletionItem>();
        var bindings = _reqnrollBindingStorageService.GetAllBindings();

        foreach (var binding in bindings.Where(b => b.StepType.Equals(requestedStepType, StringComparison.OrdinalIgnoreCase)))
        {
            // Convert regex pattern to a readable step example
            var stepExample = ConvertRegexToStepExample(binding.Expression);
            
            // Build parameter documentation
            var parametersDoc = string.Empty;
            if (binding.Parameters != null && binding.Parameters.Count > 0)
            {
                parametersDoc = "\n\n**Parameters:**\n" + string.Join("\n", binding.Parameters.Select(p => 
                    $"- `{p.Name}` (*{p.ParameterType}*): {p.Description}"));
            }

            // If keyword already typed, only insert the step text; otherwise include the keyword
            var insertText = hasKeyword ? stepExample : $"{binding.StepType} {stepExample}";
            
            stepCompletions.Add(new CompletionItem
            {
                Label = $"{binding.StepType} {stepExample}",
                Kind = CompletionItemKind.Snippet,
                Detail = binding.Description,
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = $"**{binding.StepType}** step\n\n{binding.Description}{parametersDoc}\n\n*Source:* `{binding.Source.ClassName}.{binding.Source.MethodName}`"
                },
                InsertText = insertText,
                InsertTextFormat = InsertTextFormat.PlainText,
                SortText = stepExample,
                FilterText = $"{binding.StepType} {stepExample}"
            });
        }

        return Task.FromResult(new CompletionList(stepCompletions, isIncomplete: false));
    }

    public override Task<CompletionItem> Handle(
        CompletionItem request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(request);
    }

    private string ConvertRegexToStepExample(string regexPattern)
    {
        // Remove regex anchors
        var cleaned = regexPattern.Replace("^", "").Replace("$", "");
        
        // Replace common regex patterns with readable placeholders
        cleaned = Regex.Replace(cleaned, @"\(\.\*\)", "...");
        cleaned = Regex.Replace(cleaned, @"\(\.\+\)", "...");
        cleaned = Regex.Replace(cleaned, @"\(\\d\+\)", "123");
        cleaned = Regex.Replace(cleaned, @"\(\\w\+\)", "value");
        cleaned = Regex.Replace(cleaned, @"\(\[\^""\]\+\)", "text");
        cleaned = Regex.Replace(cleaned, @"\(\?\<\w+\>.*?\)", "{parameter}");
        cleaned = Regex.Replace(cleaned, @"\(.*?\)", "{value}");
        cleaned = Regex.Replace(cleaned, @"\\s\+", " ");
        cleaned = Regex.Replace(cleaned, @"\\", "");
        
        return cleaned.Trim();
    }

    /// <summary>
    /// Creates a block keyword completion item (with colon and specified indentation).
    /// </summary>
    private CompletionItem CreateBlockKeywordCompletion(string keyword, string description, int indent)
    {
        var indentString = new string(' ', indent);
        return new CompletionItem
        {
            Label = keyword,
            Kind = CompletionItemKind.Keyword,
            Detail = description,
            InsertText = indentString + keyword + ": ",
            InsertTextFormat = InsertTextFormat.PlainText,
            SortText = "z_" + keyword  // Sort block keywords after step keywords
        };
    }

    /// <summary>
    /// Creates a step keyword completion item (with space and specified indentation).
    /// </summary>
    private CompletionItem CreateStepKeywordCompletion(string keyword, string description, int indent)
    {
        var indentString = new string(' ', indent);
        return new CompletionItem
        {
            Label = keyword,
            Kind = CompletionItemKind.Keyword,
            Detail = description,
            InsertText = indentString + keyword + " ",
            InsertTextFormat = InsertTextFormat.PlainText,
            SortText = keyword
        };
    }

    /// <summary>
    /// Determines the step type based on what the user has typed.
    /// For And/But, looks back to find the last Given/When/Then.
    /// </summary>
    private string? DetermineStepType(string lineBeforeCursor, string[] lines, Position position)
    {
        // Check what keyword the user is typing
        if (lineBeforeCursor.StartsWith("Given", StringComparison.OrdinalIgnoreCase))
            return "Given";
        if (lineBeforeCursor.StartsWith("When", StringComparison.OrdinalIgnoreCase))
            return "When";
        if (lineBeforeCursor.StartsWith("Then", StringComparison.OrdinalIgnoreCase))
            return "Then";

        // For And/But, find the last Given/When/Then
        if (lineBeforeCursor.StartsWith("And", StringComparison.OrdinalIgnoreCase) ||
            lineBeforeCursor.StartsWith("But", StringComparison.OrdinalIgnoreCase))
        {
            return FindLastStepType(lines, position);
        }

        return null;
    }

    /// <summary>
    /// Checks if the line already contains a step keyword.
    /// </summary>
    private bool HasStepKeyword(string lineBeforeCursor)
    {
        return lineBeforeCursor.StartsWith("Given", StringComparison.OrdinalIgnoreCase) ||
               lineBeforeCursor.StartsWith("When", StringComparison.OrdinalIgnoreCase) ||
               lineBeforeCursor.StartsWith("Then", StringComparison.OrdinalIgnoreCase) ||
               lineBeforeCursor.StartsWith("And", StringComparison.OrdinalIgnoreCase) ||
               lineBeforeCursor.StartsWith("But", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Searches backwards to find the last Given/When/Then step type.
    /// </summary>
    private string? FindLastStepType(string[] lines, Position position)
    {
        for (int i = (int)position.Line - 1; i >= 0; i--)
        {
            var line = lines[i].TrimStart();
            
            if (line.StartsWith("Given ", StringComparison.OrdinalIgnoreCase))
                return "Given";
            if (line.StartsWith("When ", StringComparison.OrdinalIgnoreCase))
                return "When";
            if (line.StartsWith("Then ", StringComparison.OrdinalIgnoreCase))
                return "Then";
            
            // Stop if we hit a scenario/background boundary
            if (line.StartsWith("Scenario:") || 
                line.StartsWith("Scenario Outline:") || 
                line.StartsWith("Background:"))
            {
                break;
            }
        }

        // Default to Given if no previous step found
        return "Given";
    }

    private string GetStepTypeSortOrder(string stepType)
    {
        return stepType.ToLower() switch
        {
            "given" => "1",
            "when" => "2",
            "then" => "3",
            "and" => "4",
            "but" => "5",
            _ => "9"
        };
    }

    /// <summary>
    /// Determines if the cursor position is inside a Background or Scenario block.
    /// </summary>
    private bool IsInsideScenarioOrBackground(string[] lines, Position position)
    {
        if (position.Line >= lines.Length)
        {
            return false;
        }

        // Search backwards from the cursor position to find the current block
        bool foundBlock = false;
        for (int i = (int)position.Line; i >= 0; i--)
        {
            var line = lines[i].TrimStart();
            
            // If we hit a Scenario or Background keyword, we're inside a valid block
            if (line.StartsWith("Scenario:") || 
                line.StartsWith("Scenario Outline:") || 
                line.StartsWith("Background:"))
            {
                foundBlock = true;
                break;
            }
            
            // If we hit a Feature keyword, we're not inside a scenario/background
            if (line.StartsWith("Feature:"))
            {
                break;
            }
        }

        return foundBlock;
    }
}
