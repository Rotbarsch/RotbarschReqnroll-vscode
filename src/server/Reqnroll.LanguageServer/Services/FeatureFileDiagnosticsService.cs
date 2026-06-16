using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Text.RegularExpressions;

namespace Reqnroll.LanguageServer.Services;

/// <summary>
/// Analyzes Reqnroll feature files for missing step bindings and publishes diagnostics.
/// Extracted as a standalone service so it can be called both from document sync events
/// and from binding-refresh flows.
/// </summary>
public class FeatureFileDiagnosticsService
{
    private static readonly Regex StepPattern =
        new(@"^\s*(Given|When|Then|And|But)\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly DocumentStorageService _documentStorageService;
    private readonly ReqnrollBindingStorageService _bindingStorageService;
    private readonly LanguageServerProtocolRequestService _lspRequestService;

    public FeatureFileDiagnosticsService(
        DocumentStorageService documentStorageService,
        ReqnrollBindingStorageService bindingStorageService,
        LanguageServerProtocolRequestService lspRequestService)
    {
        _documentStorageService = documentStorageService;
        _bindingStorageService = bindingStorageService;
        _lspRequestService = lspRequestService;
    }

    /// <summary>
    /// Analyzes a single document and publishes its diagnostics to the client.
    /// </summary>
    public void PublishDiagnostics(DocumentUri uri, string content)
    {
        var diagnostics = Analyze(content);
        _lspRequestService.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = diagnostics
        });
    }

    /// <summary>
    /// Re-analyzes every currently open document and republishes diagnostics for each.
    /// Also requests a semantic token refresh so parameter highlighting is updated.
    /// Call this after bindings change.
    /// </summary>
    public void RefreshAllOpenDocuments()
    {
        foreach (var (uriString, content) in _documentStorageService.GetAll())
        {
            var uri = DocumentUri.From(uriString);
            PublishDiagnostics(uri, content);
        }

        _lspRequestService.SendSemanticTokensRefresh();
    }

    private Container<Diagnostic> Analyze(string content)
    {
        var diagnostics = new List<Diagnostic>();

        if (string.IsNullOrEmpty(content))
            return new Container<Diagnostic>(diagnostics);

        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = StepPattern.Match(line);
            if (!match.Success)
                continue;

            var keyword = match.Groups[1].Value;
            var stepText = match.Groups[2].Value.Trim();

            if (_bindingStorageService.HasMatchingBinding(stepText))
                continue;

            diagnostics.Add(new Diagnostic
            {
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                {
                    Start = new Position(i, line.IndexOf(keyword)),
                    End = new Position(i, line.Length)
                },
                Message = $"No binding found for step: {stepText}",
                Severity = DiagnosticSeverity.Warning,
                Source = "Reqnroll"
            });
        }

        return new Container<Diagnostic>(diagnostics);
    }
}
