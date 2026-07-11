using OmniSharp.Extensions.LanguageServer.Server;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.General;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using Reqnroll.LanguageServer.Handlers;
using Reqnroll.LanguageServer.Models.DotnetBuild;
using Reqnroll.LanguageServer.Services;
using Reqnroll.LanguageServer.Services.TestRunning;
using Reqnroll.LanguageServer.Models.TestDiscovery;
using Reqnroll.LanguageServer.Models.TestRunner;

#if DEBUG
if (args.Contains("--wait-for-debugger"))
{
    Console.Error.WriteLine("Waiting for debugger...");
    Debugger.Launch();
    Console.Error.WriteLine("Debugger connected!");
}
#endif

Console.Error.WriteLine("Starting Rotbarsch.Reqnroll Language Server...");

IServiceProvider? serviceProvider = null;

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
            services.AddSingleton<FeatureFileDiagnosticsService>();
            services.AddSingleton<VsCodeOutputLogger>();
            services.AddSingleton<IDotnetTestRunner, DotnetTestRunner>();
            services.AddSingleton<ReqnrollTestRunnerService>();
            services.AddSingleton<FeatureCsParserService>();
            services.AddSingleton<ReqnrollTestDiscoveryService>();
            services.AddSingleton<DotnetBuildService>();
            services.AddSingleton<DotnetBuildRequestHandler>();
        })
        .WithHandler<ReqnrollTextDocumentSyncHandler>()
        .WithHandler<ReqnrollCompletionHandler>()
        .WithHandler<ReqnrollDocumentFormattingHandler>()
        .WithHandler<ReqnrollHoverHandler>()
        .WithHandler<ReqnrollDocumentSymbolHandler>()
        .WithHandler<ReqnrollSemanticTokensHandler>()
        .OnRequest<RunTestsParams, List<TestResult>>("rotbarsch.reqnroll/runTests", (request, ct) =>
        {
            var runner = serviceProvider?.GetService<ReqnrollTestRunnerService>()!;
            return runner.HandleRunTestsRequestAsync(request, ct);
        })
        .OnRequest<DiscoverTestsParams, List<DiscoveredTest>>("rotbarsch.reqnroll/discoverTests", (request, ct) =>
        {
            var discoveryService = serviceProvider?.GetService<ReqnrollTestDiscoveryService>()!;
            return discoveryService.HandleDiscoverTestsRequestAsync(request, ct);
        })
        .OnRequest<StartBuildParams, BuildResult>("rotbarsch.reqnroll/startBuild", (request, ct) =>
        {
            var buildRequestHandler = serviceProvider?.GetService<DotnetBuildRequestHandler>()!;
            return buildRequestHandler.HandleStartBuildRequestAsync(request, ct);
        })
        .OnRequest<StartBuildParams, BuildResult>("rotbarsch.reqnroll/forceBuild", (request, ct) =>
        {
            var buildRequestHandler = serviceProvider?.GetService<DotnetBuildRequestHandler>()!;
            return buildRequestHandler.HandleForceBuildRequestAsync(request, ct);
        })
        .OnRequest<ForceRefreshBindingsParams, ForceRefreshBindingsResult>("rotbarsch.reqnroll/refreshBindings", async (request, ct) =>
        {
            var bindingStorageService = serviceProvider?.GetService<ReqnrollBindingStorageService>()!;
            var diagnosticsService = serviceProvider?.GetService<FeatureFileDiagnosticsService>()!;

            var count = await bindingStorageService.ForceRefresh();
            diagnosticsService.RefreshAllOpenDocuments();

            return new ForceRefreshBindingsResult { BindingCount = count };
        })
        .OnInitialize((languageServer, request, token) =>
        {
            languageServer.Window.LogInfo("Rotbarsch.Reqnroll LSP initializing..");

            try
            {
                serviceProvider = languageServer.Services;

                var bindingStorageService = languageServer.Services.GetService<ReqnrollBindingStorageService>()!;

                // Store workspace directory
                if (request.RootUri is not null)
                {
                    var workspacePath = request.RootUri.GetFileSystemPath();
                    bindingStorageService.SetWorkspaceDirectory(workspacePath);
                    languageServer.Window.LogInfo($"Workspace directory: {workspacePath}");
                }
                else if (request.RootPath != null)
                {
                    bindingStorageService.SetWorkspaceDirectory(request.RootPath);
                    languageServer.Window.LogInfo($"Workspace directory: {request.RootPath}");
                }

                languageServer.Window.LogInfo("Rotbarsch.Reqnroll LSP initialized");

            }
            catch (Exception e)
            {
                languageServer.Window.LogError(e.Message);
            }
            return Task.CompletedTask;
        })
        .OnStarted((languageServer, token) =>
        {
            languageServer.Window.LogInfo("Rotbarsch.Reqnroll LSP starting...");
            languageServer.Window.LogInfo("Rotbarsch.Reqnroll LSP started.");
            return Task.CompletedTask;
        })
        .OnExit(_ => Task.CompletedTask);
});

await server.WaitForExit;