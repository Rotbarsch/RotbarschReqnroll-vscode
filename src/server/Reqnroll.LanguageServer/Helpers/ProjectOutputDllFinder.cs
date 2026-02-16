using System.Xml.Linq;
using System.Xml.XPath;

namespace Reqnroll.LanguageServer.Helpers;

public static class ProjectOutputDllFinder
{
    public static string? GetOutputDllPath(string csProjFilePath)
    {
        if (string.IsNullOrWhiteSpace(csProjFilePath) || !File.Exists(csProjFilePath))
        {
            return null;
        }

        try
        {
            var projectDir = Path.GetDirectoryName(Path.GetFullPath(csProjFilePath)) ?? string.Empty;
            var projectName = Path.GetFileNameWithoutExtension(csProjFilePath);

            var doc = XDocument.Load(csProjFilePath);

            // Find the assembly name (fallback to project file name)
            var assemblyName = doc.XPathSelectElement("//*[local-name()='AssemblyName']")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                assemblyName = projectName;
            }

            // Resolve the resulting file name (TargetFileName or default assemblyName.dll)
            var targetFileName = doc.XPathSelectElement("//*[local-name()='TargetFileName']")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(targetFileName))
            {
                targetFileName = $"{assemblyName}.dll";
            }
            else if (!Path.HasExtension(targetFileName))
            {
                targetFileName += ".dll";
            }

            // Choose configuration (Condition-specific OutputPath is matched against this)
            var configuration = doc.XPathSelectElement("//*[local-name()='PropertyGroup']/*[local-name()='Configuration']")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(configuration))
            {
                configuration = "Debug";
            }

            // Determine target framework (first of TargetFrameworks if plural)
            var targetFramework = doc.XPathSelectElement("//*[local-name()='TargetFramework']")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(targetFramework))
            {
                var targetFrameworks = doc.XPathSelectElement("//*[local-name()='TargetFrameworks']")?.Value;
                targetFramework = targetFrameworks?
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault();
            }

            // Decide whether to append TFM to the output path
            bool appendTargetFramework = true;
            var appendTargetFrameworkElement = doc.XPathSelectElement("//*[local-name()='AppendTargetFrameworkToOutputPath']")?.Value?.Trim();
            if (!string.IsNullOrEmpty(appendTargetFrameworkElement) && bool.TryParse(appendTargetFrameworkElement, out var appendTargetFrameworkValue))
            {
                appendTargetFramework = appendTargetFrameworkValue;
            }

            // Gather candidate OutputPath entries (honoring Condition when present)
            var outputPaths = doc.XPathSelectElements("//*[local-name()='PropertyGroup']/*[local-name()='OutputPath']")
                .Select(x => new
                {
                    Path = x.Value.Trim(),
                    Condition = x.Parent?.Attribute("Condition")?.Value
                })
                .ToList();

            string? selectedOutputPath = null;
            if (outputPaths.Count > 0)
            {
                selectedOutputPath = outputPaths
                    .Where(p => !string.IsNullOrWhiteSpace(p.Condition) && p.Condition.Contains(configuration, StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.Path)
                    .FirstOrDefault();

                selectedOutputPath ??= outputPaths
                    .Where(p => string.IsNullOrWhiteSpace(p.Condition))
                    .Select(p => p.Path)
                    .FirstOrDefault();

                selectedOutputPath ??= outputPaths.First().Path;
            }

            // Default to bin/<Configuration> if no OutputPath found
            selectedOutputPath ??= Path.Combine("bin", configuration);

            // Append TFM if requested and not already part of the path
            if (appendTargetFramework && !string.IsNullOrWhiteSpace(targetFramework))
            {
                var lastSegment = Path.GetFileName(Path.TrimEndingDirectorySeparator(selectedOutputPath));
                if (!string.Equals(lastSegment, targetFramework, StringComparison.OrdinalIgnoreCase))
                {
                    selectedOutputPath = Path.Combine(selectedOutputPath, targetFramework);
                }
            }

            var fullOutputPath = Path.IsPathRooted(selectedOutputPath)
                ? selectedOutputPath
                : Path.GetFullPath(Path.Combine(projectDir, selectedOutputPath));

            return Path.Combine(fullOutputPath, targetFileName);
        }
        catch
        {
            return null;
        }
    }
}
