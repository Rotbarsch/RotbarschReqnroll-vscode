using OmniSharp.Extensions.LanguageServer.Server;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.General;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using Reqnroll.LanguageServer.Handlers;
using Reqnroll.LanguageServer.Models.DotnetBuild;
using Reqnroll.LanguageServer.Services;
using Reqnroll.LanguageServer.Models.TestDiscovery;
using Reqnroll.LanguageServer.Models.TestRunner;

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
            services.AddSingleton<VsCodeOutputLogger>();
            services.AddSingleton<ReqnrollTestRunnerService>();
            services.AddSingleton<FeatureCsParserService>();
            services.AddSingleton<ReqnrollTestDiscoveryService>();
            services.AddSingleton<DotnetBuildService>();
            services.AddSingleton<DotnetBuildRequestHandler>();
            services.AddSingleton<DotnetTestService>();
        })
        .WithHandler<ReqnrollTextDocumentSyncHandler>()
        .WithHandler<ReqnrollCompletionHandler>()
        .WithHandler<ReqnrollDocumentFormattingHandler>()
        .WithHandler<ReqnrollHoverHandler>()
        .WithHandler<ReqnrollDocumentSymbolHandler>()
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
        .OnInitialize((server, request, token) =>
        {
            serviceProvider = server.Services;

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
        .OnStarted(async (languageServer, token) =>
        {
            languageServer.Window.LogInfo("Rotbarsch.Reqnroll LSP started");

            var testService = serviceProvider?.GetService<DotnetTestService>()!;

            try
            {
                var parallelLimit = languageServer.Configuration.GetValue<int>("reqnroll:test:parallelExecutionLimit");
                testService.SetParallelExecutionLimit(parallelLimit);
            }
            catch (Exception ex)
            {
                languageServer.Window.LogWarning($"Failed to read parallel execution limit configuration: {ex.Message}");
            }

        })
        .OnExit(_ =>
        {
            // Kill all running test processes
            var testService = serviceProvider?.GetService<DotnetTestService>();
            testService?.KillAllRunningProcesses();

            // Clean up test results directory
            var testResultsPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "rotbarsch.reqnroll", "test_results");
            if (Directory.Exists(testResultsPath))
            {
                Directory.Delete(testResultsPath, true);
            }
        });
});

await server.WaitForExit;