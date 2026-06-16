using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Reqnroll.LanguageServer.Services;

namespace Reqnroll.LanguageServer.Handlers;

/// <summary>
/// Handles text document synchronization events for Reqnroll feature files.
/// Manages document lifecycle (open, change, close) and publishes diagnostics for undefined steps.
/// </summary>
public class ReqnrollTextDocumentSyncHandler : TextDocumentSyncHandlerBase
{
    private readonly DocumentStorageService _documentStorageService;
    private readonly FeatureFileDiagnosticsService _diagnosticsService;
    
    public ReqnrollTextDocumentSyncHandler(
        DocumentStorageService documentStorageService,
        FeatureFileDiagnosticsService diagnosticsService)
    {
        _documentStorageService = documentStorageService;
        _diagnosticsService = diagnosticsService;
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
        _diagnosticsService.PublishDiagnostics(request.TextDocument.Uri, request.TextDocument.Text);
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
                _documentStorageService.Set(request.TextDocument.Uri, change.Text);
                _diagnosticsService.PublishDiagnostics(request.TextDocument.Uri, change.Text);
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
            _diagnosticsService.PublishDiagnostics(request.TextDocument.Uri, request.Text);
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
}
