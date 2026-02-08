using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Reqnroll.LanguageServer.Services;
using System.Text.RegularExpressions;

namespace Reqnroll.LanguageServer.Handlers;

/// <summary>
/// Provides hover documentation for Reqnroll steps.
/// Displays step binding information including description, parameters, and source location.
/// </summary>
public class ReqnrollHoverHandler : HoverHandlerBase
{
    private readonly DocumentStorageService _documentStorageService;
    private readonly ReqnrollBindingStorageService _reqnrollBindingStorageService;

    public ReqnrollHoverHandler(DocumentStorageService documentStorageService, ReqnrollBindingStorageService reqnrollBindingStorageService)
    {
        _documentStorageService = documentStorageService;
        _reqnrollBindingStorageService = reqnrollBindingStorageService;
    }

    protected override HoverRegistrationOptions CreateRegistrationOptions(
        HoverCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new HoverRegistrationOptions
        {
            DocumentSelector = DocumentSelector.ForLanguage("reqnroll-feature")
        };
    }

    /// <summary>
    /// Handles hover requests by finding matching step bindings and displaying their documentation.
    /// </summary>
    public override Task<Hover?> Handle(
        HoverParams request,
        CancellationToken cancellationToken)
    {
        var documentContent = _documentStorageService.Get(request.TextDocument.Uri);
        if (string.IsNullOrEmpty(documentContent))
        {
            return Task.FromResult<Hover?>(null);
        }

        var lines = documentContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var lineIndex = (int)request.Position.Line;
        
        if (lineIndex >= lines.Length)
        {
            return Task.FromResult<Hover?>(null);
        }

        var line = lines[lineIndex];
        var trimmedLine = line.Trim();

        // Check if this is a step line
        var stepKeywords = new[] { "Given", "When", "Then", "And", "But" };
        var matchedKeyword = stepKeywords.FirstOrDefault(k => trimmedLine.StartsWith(k + " "));
        
        if (matchedKeyword == null)
        {
            return Task.FromResult<Hover?>(null);
        }

        // Extract the step text (without the keyword)
        var stepText = trimmedLine.Substring(matchedKeyword.Length).Trim();
        
        // Find matching binding
        var bindings = _reqnrollBindingStorageService.GetAllBindings();
        var matchingBinding = bindings.FirstOrDefault(b =>
        {
            try
            {
                var regex = new Regex(b.Expression, RegexOptions.IgnoreCase);
                return regex.IsMatch(stepText);
            }
            catch
            {
                return false;
            }
        });

        if (matchingBinding == null)
        {
            return Task.FromResult<Hover?>(null);
        }

        // Build parameter documentation
        var parametersDoc = string.Empty;
        if (matchingBinding.Parameters != null && matchingBinding.Parameters.Count > 0)
        {
            parametersDoc = "\n\n**Parameters:**\n" + string.Join("\n", matchingBinding.Parameters.Select(p => 
                $"- `{p.Name}` (*{p.ParameterType}*): {p.Description}"));
        }

        // Create hover content with description
        var hoverContent = new MarkupContent
        {
            Kind = MarkupKind.Markdown,
            Value = $"**{matchingBinding.StepType}** step\n\n{matchingBinding.Description}{parametersDoc}\n\n*Pattern:* `{matchingBinding.Expression}`\n\n*Source:* `{matchingBinding.Source.ClassName}.{matchingBinding.Source.MethodName}`"
        };

        return Task.FromResult<Hover?>(new Hover
        {
            Contents = new MarkedStringsOrMarkupContent(hoverContent),
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
            {
                Start = new Position(lineIndex, 0),
                End = new Position(lineIndex, line.Length)
            }
        });
    }
}
