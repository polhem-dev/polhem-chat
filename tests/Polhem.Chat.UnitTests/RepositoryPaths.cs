namespace Polhem.Chat.UnitTests;

internal static class RepositoryPaths
{
    public static string SourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Polhem.Chat.slnx")))
        {
            directory = directory.Parent;
        }
        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("The repository root was not found."), "src");
    }
}
