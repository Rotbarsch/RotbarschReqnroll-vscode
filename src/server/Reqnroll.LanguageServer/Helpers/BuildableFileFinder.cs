namespace Reqnroll.LanguageServer.Helpers;

public static class BuildableFileFinder
{
    private const int MaxScanDepth = 15;
    private static readonly string[] BuildableFilePatterns = ["*.sln", "*.slnx", "*.csproj"];

    public static string? GetBuildableFileOfReferenceFile(string featureFilePath)
    {
        if (string.IsNullOrWhiteSpace(featureFilePath))
        {
            return null;
        }

        var currentDirectory = Path.GetDirectoryName(Path.GetFullPath(featureFilePath));

        for (var i = 0; i < MaxScanDepth && !string.IsNullOrEmpty(currentDirectory); i++)
        {
            foreach (var pattern in BuildableFilePatterns)
            {
                var files = Directory.GetFiles(currentDirectory, pattern, SearchOption.TopDirectoryOnly);
                if (files.Length > 0)
                {
                    return files[0];
                }
            }

            currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
        }

        return null;
    }
}