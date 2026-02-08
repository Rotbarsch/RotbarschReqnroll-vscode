using OmniSharp.Extensions.LanguageServer.Server;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using Reqnroll.LanguageServer.Handlers;
using Reqnroll.LanguageServer.Services;

#if DEBUG
if (args.Contains("--wait-for-debugger"))
{
    Console.Error.WriteLine("Waiting for debugger (a maximum of 10 seconds)...");
    int counter = 0;
    while (!Debugger.IsAttached && counter <= 10)
    {
        Thread.Sleep(1000);
        counter++;
    }
    Console.Error.WriteLine("Debugger connected!");
}
#endif

Console.Error.WriteLine("Starting Rotbarsch.Reqnroll Language Server...");

var server = await LanguageServer.From(options =>
{
    options
        .WithInput(Console.OpenStandardInput())
        .WithOutput(Console.OpenStandardOutput())
        .WithServices(services =>
        {
            services.AddSingleton<DocumentStorageService>();
            services.AddSingleton<ReqnrollBindingStorageService>();
            services.AddSingleton<LanguageServerProtocolRequestService>();
        })
        .WithHandler<ReqnrollTextDocumentSyncHandler>()
        .WithHandler<ReqnrollCompletionHandler>()
        .WithHandler<ReqnrollDocumentFormattingHandler>()
        .WithHandler<ReqnrollHoverHandler>()
        .WithHandler<ReqnrollDocumentSymbolHandler>()
        .OnInitialize((server, request, token) =>
        {
            var bindingStorageService = server.Services.GetService<ReqnrollBindingStorageService>()!;

            server.Window.LogInfo("Rotbarsch.Reqnroll LSP initialized");

            // Store workspace directory
            if (request.RootUri is not null)
            {
                var workspacePath = request.RootUri.GetFileSystemPath();
                bindingStorageService.SetWorkspaceDirectory(workspacePath);
                server.Window.LogInfo($"Workspace directory: {workspacePath}");
            }
            else if (request.RootPath != null)
            {
                bindingStorageService.SetWorkspaceDirectory(request.RootPath);
                server.Window.LogInfo($"Workspace directory: {request.RootPath}");
            }

            return Task.CompletedTask;
        })
        .OnStarted((languageServer, token) =>
        {
            languageServer.Window.LogInfo("Rotbarsch.Reqnroll LSP started");
            return Task.CompletedTask;
        });
});

await server.WaitForExit;