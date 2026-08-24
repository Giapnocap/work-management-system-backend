using System.Text.RegularExpressions;

namespace WorkManagementSystem.Tests;

public sealed partial class DocumentationTests
{
    [Fact]
    public void MarkdownLinks_ToRepositoryFiles_AreValid()
    {
        var repositoryRoot = FindRepositoryRoot();
        var documentationDirectory = Path.Combine(repositoryRoot, "docs");
        var markdownFiles = Directory
            .EnumerateFiles(documentationDirectory, "*.md", SearchOption.AllDirectories)
            .Prepend(Path.Combine(repositoryRoot, "README.md"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var failures = new List<string>();

        foreach (var markdownFile in markdownFiles)
        {
            var content = File.ReadAllText(markdownFile);
            foreach (Match match in MarkdownLinkRegex().Matches(content))
            {
                var rawTarget = match.Groups["target"].Value.Trim().Trim('<', '>');
                if (ShouldSkip(rawTarget))
                    continue;

                var targetWithoutFragment = rawTarget.Split('#', 2)[0].Split('?', 2)[0];
                var decodedTarget = Uri.UnescapeDataString(targetWithoutFragment)
                    .Replace('/', Path.DirectorySeparatorChar);

                if (Path.IsPathRooted(decodedTarget))
                {
                    failures.Add($"{GetRelativePath(repositoryRoot, markdownFile)} -> absolute path '{rawTarget}'");
                    continue;
                }

                var sourceDirectory = Path.GetDirectoryName(markdownFile)
                    ?? throw new InvalidOperationException($"Cannot resolve directory for {markdownFile}.");
                var resolvedPath = Path.GetFullPath(Path.Combine(sourceDirectory, decodedTarget));
                var relativeTarget = Path.GetRelativePath(repositoryRoot, resolvedPath);

                if (relativeTarget == ".." || relativeTarget.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    failures.Add($"{GetRelativePath(repositoryRoot, markdownFile)} -> outside repository '{rawTarget}'");
                    continue;
                }

                if (!File.Exists(resolvedPath) && !Directory.Exists(resolvedPath))
                    failures.Add($"{GetRelativePath(repositoryRoot, markdownFile)} -> missing '{rawTarget}'");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Broken local Markdown links:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    private static bool ShouldSkip(string target)
    {
        if (string.IsNullOrWhiteSpace(target) || target.StartsWith('#'))
            return true;

        return Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
               uri.Scheme is "http" or "https" or "mailto";
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WorkManagementSystem.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not find WorkManagementSystem.sln above {AppContext.BaseDirectory}.");
    }

    private static string GetRelativePath(string repositoryRoot, string path)
        => Path.GetRelativePath(repositoryRoot, path).Replace(Path.DirectorySeparatorChar, '/');

    [GeneratedRegex("!?\\[[^\\]]*\\]\\((?<target><[^>]+>|[^\\s\\)]+)(?:\\s+(?:\"[^\"]*\"|'[^']*'))?\\)")]
    private static partial Regex MarkdownLinkRegex();
}
