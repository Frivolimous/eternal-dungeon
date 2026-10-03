using System.Reflection;

namespace EternalDungeon.Core.Data;

/// <summary>Where data files come from: a folder on disk (Sim, tests), an in-memory set (tests),
/// or resources embedded in an assembly (the Godot game, so exports carry their data).</summary>
public abstract class DataSource
{
    /// <summary>The text of a data file, such as <c>"tags.json"</c>, or null when it doesn't exist.</summary>
    public abstract string? Read(string fileName);

    public static DataSource FromDirectory(string directory) => new DirectorySource(directory);

    public static DataSource FromFiles(IReadOnlyDictionary<string, string> files) => new FilesSource(files);

    /// <summary>Embedded resources whose logical names are <c>{prefix}{fileName}</c>.</summary>
    public static DataSource FromAssembly(Assembly assembly, string prefix = "data/") => new AssemblySource(assembly, prefix);

    sealed class DirectorySource(string directory) : DataSource
    {
        public override string? Read(string fileName)
        {
            var path = Path.Combine(directory, fileName);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
    }

    sealed class FilesSource(IReadOnlyDictionary<string, string> files) : DataSource
    {
        public override string? Read(string fileName) => files.GetValueOrDefault(fileName);
    }

    sealed class AssemblySource(Assembly assembly, string prefix) : DataSource
    {
        public override string? Read(string fileName)
        {
            using var stream = assembly.GetManifestResourceStream(prefix + fileName);
            if (stream == null) return null;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
