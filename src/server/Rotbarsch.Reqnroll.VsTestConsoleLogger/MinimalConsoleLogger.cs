using System;
using System.Text.Json;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Rotbarsch.Reqnroll.VsTestConsoleLogger
{
    /// <summary>
    /// A minimal logger to get json formatted test result info.
    /// </summary>
    [FriendlyName("rotbarschconsolelogger")]
    [ExtensionUri("logger://rotbarschconsolelogger")]
    public class MinimalConsoleLogger : ITestLogger
    {
        public static string LoggerName = "rotbarschconsolelogger";

        public void Initialize(TestLoggerEvents events, string testRunDirectory)
        {
            events.TestResult += OnTestResult;
        }

        private void OnTestResult(object sender, TestResultEventArgs e)
        {
            var info = new TestResultInfo(e.Result);
            var json = JsonSerializer.Serialize(info);

            Console.Out.WriteLine($"[{LoggerName}]{json}");
        }
    }
}
