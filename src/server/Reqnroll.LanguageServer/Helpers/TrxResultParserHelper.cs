using System.Xml;
using Reqnroll.LanguageServer.Models.TrxResultParserHelper;

namespace Reqnroll.LanguageServer.Helpers;

public static class TrxResultParserHelper
{
    public static IEnumerable<TrxTestCaseResult>? Parse(string trxFilePath)
    {
        if (string.IsNullOrWhiteSpace(trxFilePath) || !File.Exists(trxFilePath))
        {
            return null;
        }

        var doc = new XmlDocument();
        doc.Load(trxFilePath);

        var nsManager = new XmlNamespaceManager(doc.NameTable);
        var defaultNamespace = doc.DocumentElement?.NamespaceURI ?? string.Empty;
        nsManager.AddNamespace("trx", defaultNamespace);

        var testDefinitions = new Dictionary<string, string>();

        var unitTests = doc.SelectNodes("//trx:TestDefinitions/trx:UnitTest", nsManager);
        if (unitTests != null)
        {
            foreach (XmlNode unitTest in unitTests)
            {
                var id = unitTest.Attributes?["id"]?.Value;
                var testMethod = unitTest.SelectSingleNode("trx:TestMethod", nsManager);
                var className = testMethod?.Attributes?["className"]?.Value ?? string.Empty;
                var methodName = testMethod?.Attributes?["name"]?.Value ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(id))
                {
                    var fullMethodName = string.IsNullOrWhiteSpace(className)
                        ? methodName
                        : $"{className}.{methodName}";
                    testDefinitions[id] = fullMethodName;
                }
            }
        }

        var results = doc.SelectNodes("//trx:UnitTestResult", nsManager);
        var parsedResults = new List<TrxTestCaseResult>();

        if (results != null)
        {
            foreach (XmlNode result in results)
            {
                var testId = result.Attributes?["testId"]?.Value ?? string.Empty;
                var testName = result.Attributes?["testName"]?.Value ?? string.Empty;
                var outcome = result.Attributes?["outcome"]?.Value ?? string.Empty;
                var stdOut = result.SelectSingleNode("trx:Output/trx:StdOut", nsManager)?.InnerText ?? string.Empty;

                testDefinitions.TryGetValue(testId, out var fullMethodName);

                parsedResults.Add(new TrxTestCaseResult(testName, outcome, stdOut, fullMethodName ?? string.Empty));
            }
        }

        return parsedResults;
    }
}