using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Reqnroll.LanguageServer.Services;
using System.Text.RegularExpressions;

namespace Reqnroll.LanguageServer.Handlers;

/// <summary>
/// Provides semantic token highlighting for Reqnroll step parameters.
/// For each step line, finds the matching binding and emits the regex capture groups
/// as <c>parameter</c> semantic tokens so they are highlighted regardless of quoting style.
/// </summary>
public class ReqnrollSemanticTokensHandler : SemanticTokensHandlerBase
{
    private static readonly SemanticTokensLegend Legend = new()
    {
        TokenTypes = new[] { SemanticTokenType.Parameter },
        TokenModifiers = Array.Empty<SemanticTokenModifier>()
    };

    private static readonly Regex StepLinePattern =
        new(@"^(\s*)(Given|When|Then|And|But)\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly DocumentStorageService _documentStorageService;
    private readonly ReqnrollBindingStorageService _reqnrollBindingStorageService;

    public ReqnrollSemanticTokensHandler(
        DocumentStorageService documentStorageService,
        ReqnrollBindingStorageService reqnrollBindingStorageService)
    {
        _documentStorageService = documentStorageService;
        _reqnrollBindingStorageService = reqnrollBindingStorageService;
    }

    protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(
        SemanticTokensCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new SemanticTokensRegistrationOptions
        {
            DocumentSelector = DocumentSelector.ForLanguage("reqnroll-feature"),
            Legend = Legend,
            Full = true,
            Range = false
        };
    }

    protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(
        ITextDocumentIdentifierParams @params,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new SemanticTokensDocument(Legend));
    }

    protected override Task Tokenize(
        SemanticTokensBuilder builder,
        ITextDocumentIdentifierParams identifier,
        CancellationToken cancellationToken)
    {
        var content = _documentStorageService.Get(identifier.TextDocument.Uri);
        if (string.IsNullOrEmpty(content))
            return Task.CompletedTask;

        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var bindings = _reqnrollBindingStorageService.GetAllBindings();

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            var stepMatch = StepLinePattern.Match(line);
            if (!stepMatch.Success)
                continue;

            var leadingWhitespace = stepMatch.Groups[1].Length;
            var keyword = stepMatch.Groups[2].Value;
            var stepText = stepMatch.Groups[3].Value;

            // Column in the full line where the step text (after the keyword) starts
            var stepTextStartCol = leadingWhitespace + keyword.Length + 1;

            foreach (var binding in bindings)
            {
                try
                {
                    var bindingRegex = new Regex(binding.Expression, RegexOptions.IgnoreCase);
                    var bindingMatch = bindingRegex.Match(stepText);
                    if (!bindingMatch.Success)
                        continue;

                    // Emit each named/numbered capture group as a parameter token
                    for (var gi = 1; gi < bindingMatch.Groups.Count; gi++)
                    {
                        var group = bindingMatch.Groups[gi];
                        if (!group.Success || group.Length == 0)
                            continue;

                        builder.Push(
                            lineIndex,
                            stepTextStartCol + group.Index,
                            group.Length,
                            SemanticTokenType.Parameter,
                            Array.Empty<SemanticTokenModifier>());
                    }

                    break; // stop after the first matching binding
                }
                catch (RegexParseException)
                {
                    // skip malformed binding expressions
                }
            }
        }

        return Task.CompletedTask;
    }
}
