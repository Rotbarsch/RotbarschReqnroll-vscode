using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;

namespace Reqnroll.LanguageServer.Services;

public class VsCodeOutputLogger
{
    private IWindowLanguageServer? ServerWindow => _server?.Window;
    private ILanguageServerFacade _server;

    public VsCodeOutputLogger(ILanguageServerFacade server)
    {
        _server = server;
    }

    public void LogInfo(string message)
    {
        ServerWindow?.LogInfo(message);
    }

    public void LogWarning(string message)
    {
        ServerWindow?.LogWarning(message);
    }

    public void LogError(string message)
    {
        ServerWindow?.LogError(message);
    }
}