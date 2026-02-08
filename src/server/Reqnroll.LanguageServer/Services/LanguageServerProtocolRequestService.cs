using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;

namespace Reqnroll.LanguageServer.Services;

/// <summary>
/// Abstraction service for sending LSP protocol requests to the client.
/// Wraps ILanguageServerFacade to avoid direct MediatR dependencies in handlers.
/// </summary>
public class LanguageServerProtocolRequestService
{
    private readonly ILanguageServerFacade _server;

    public LanguageServerProtocolRequestService(ILanguageServerFacade server)
    {
        _server = server;
    }

    /// <summary>
    /// Publishes diagnostics (errors/warnings) for a document.
    /// </summary>
    public void PublishDiagnostics(PublishDiagnosticsParams diagnosticsParams)
    {
        _server.TextDocument.PublishDiagnostics(diagnosticsParams);
    }

    /// <summary>
    /// Shows a message notification to the user in the VS Code UI.
    /// </summary>
    public void ShowMessage(ShowMessageParams messageParams)
    {
        _server.Window.ShowMessage(messageParams);
    }

    /// <summary>
    /// Logs a message to the language server output channel.
    /// </summary>
    public void LogMessage(LogMessageParams messageParams)
    {
        _server.Window.LogMessage(messageParams);
    }
}
