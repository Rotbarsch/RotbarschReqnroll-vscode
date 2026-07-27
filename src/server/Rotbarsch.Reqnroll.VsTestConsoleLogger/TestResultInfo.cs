using System;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Rotbarsch.Reqnroll.VsTestConsoleLogger
{
    public class TestResultInfo
    {
        public TestResultInfo()
        {
            
        }

        public TestResultInfo(TestResult e)
        {
            DisplayName = e.TestCase.DisplayName;
            FullyQualifiedName = e.TestCase.FullyQualifiedName;
            DisplayName = e.DisplayName;
            Duration = e.Duration;
            Outcome = e.Outcome;
            ErrorMessage = e.ErrorMessage;
            Messages = string.Join(Environment.NewLine, e.Messages.Select(x => x.Text));

        }

        public string Messages { get; set; }

        public TimeSpan Duration { get; set; }

        public string ErrorMessage { get; set; }

        public TestOutcome Outcome { get; set; }

        public string DisplayName { get; set; }

        public string FullyQualifiedName { get; set; }
    }
}