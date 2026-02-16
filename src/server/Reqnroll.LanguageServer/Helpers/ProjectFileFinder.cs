namespace Reqnroll.LanguageServer.Helpers;

public static class ProjectFileFinder
{
    public static string? GetProjectFileOfFeatureFile(string featureFilePath)
    {
        if (string.IsNullOrWhiteSpace(featureFilePath))
        {
            return null;
        }

        var currentDirectory = Path.GetDirectoryName(Path.GetFullPath(featureFilePath));

        for (var i = 0; i < 15 && !string.IsNullOrEmpty(currentDirectory); i++)
        {
            var projectFiles = Directory.GetFiles(currentDirectory, "*.csproj", SearchOption.TopDirectoryOnly);
            if (projectFiles.Length > 0)
            {
                return projectFiles[0];
            }

            currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
        }

        return null;
    }
}