using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Reqnroll.LanguageServer.Services;
using System.Text.RegularExpressions;

namespace Reqnroll.LanguageServer.Handlers;

/// <summary>
/// Handles text document synchronization events for Reqnroll feature files.
/// Manages document lifecycle (open, change, close) and publishes diagnostics for undefined steps.
/// </summary>
public class ReqnrollTextDocumentSyncHandler : TextDocumentSyncHandlerBase
{
    private readonly DocumentStorageService _documentStorageService;
    private readonly ReqnrollBindingStorageService _reqnrollBindingStorageService;
    private readonly LanguageServerProtocolRequestService _lspRequestService;
    
    public ReqnrollTextDocumentSyncHandler(
        DocumentStorageService documentStorageService,
        ReqnrollBindingStorageService reqnrollBindingStorageService,
        LanguageServerProtocolRequestService lspRequestService)
    {
        _documentStorageService = documentStorageService;
        _reqnrollBindingStorageService = reqnrollBindingStorageService;
        _lspRequestService = lspRequestService;
    }

    /// <summary>
    /// Gets the text document attributes for the given URI.
    /// </summary>
    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new TextDocumentAttributes(uri, "reqnroll-feature");
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        SynchronizationCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new TextDocumentSyncRegistrationOptions
        {
            DocumentSelector = DocumentSelector.ForLanguage("reqnroll-feature"),
            Change = TextDocumentSyncKind.Full,
            Save = new SaveOptions { IncludeText = true }
        };
    }

    /// <summary>
    /// Handles document open events. Stores document content and publishes diagnostics.
    /// </summary>
    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        _documentStorageService.Set(request.TextDocument.Uri, request.TextDocument.Text);
        PublishDiagnostics(request.TextDocument.Uri, request.TextDocument.Text);
        return Unit.Task;
    }

    /// <summary>
    /// Handles document change events. Updates stored content and republishes diagnostics.
    /// </summary>
    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        if (request.ContentChanges.Any())
        {
            var change = request.ContentChanges.First();
            if (change.Range == null)
            {
                // Full document update
                _documentStorageService.Set(request.TextDocument.Uri, change.Text);
                PublishDiagnostics(request.TextDocument.Uri, change.Text);
            }
        }
        return Unit.Task;
    }

    /// <summary>
    /// Handles document save events. Updates stored content and republishes diagnostics.
    /// </summary>
    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        if (request.Text != null)
        {
            _documentStorageService.Set(request.TextDocument.Uri, request.Text);
            PublishDiagnostics(request.TextDocument.Uri, request.Text);
        }
        return Unit.Task;
    }

    /// <summary>
    /// Handles document close events. Removes content from store.
    /// </summary>
    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        _documentStorageService.Remove(request.TextDocument.Uri);
        return Unit.Task;
    }

    private void PublishDiagnostics(DocumentUri uri, string content)
    {
        var diagnostics = AnalyzeDocument(content);
        
        var publishParams = new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = diagnostics
        };
        
        _lspRequestService.PublishDiagnostics(publishParams);
    }

    private Container<Diagnostic> AnalyzeDocument(string content)
    {
        var diagnostics = new List<Diagnostic>();
        
        if (string.IsNullOrEmpty(content))
        {
            return new Container<Diagnostic>(diagnostics);
        }
        
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var stepPattern = new Regex(@"^\s*(Given|When|Then|And|But)\s+(.+)$", RegexOptions.IgnoreCase);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = stepPattern.Match(line);
            
            if (match.Success)
            {
                var keyword = match.Groups[1].Value;
                var stepText = match.Groups[2].Value.Trim();
                
                if (!_reqnrollBindingStorageService.HasMatchingBinding(stepText))
                {
                    var startCol = line.IndexOf(keyword);
                    var endCol = line.Length;
                    
                    diagnostics.Add(new Diagnostic
                    {
                        Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                        {
                            Start = new Position(i, startCol),
                            End = new Position(i, endCol)
                        },
                        Message = $"No binding found for step: {stepText}",
                        Severity = DiagnosticSeverity.Warning,
                        Source = "Reqnroll"
                    });
                }
            }
        }

        return new Container<Diagnostic>(diagnostics);
    }
}
