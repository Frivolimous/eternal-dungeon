using System.Reflection;

namespace EternalDungeon.Core.Data;

/// <summary>Where data files come from: a folder on disk (Sim, tests), an in-memory set (tests),
/// or resources embedded in an assembly (the Godot game, so exports carry their data).</summary>
public abstract class DataSource
{
    /// <summary>The text of a data file, such as <c>"tags.json"</c> or <c>"events/caged_merchant.json"</c>, or null
    /// when it doesn't exist.</summary>
    public abstract string? Read(string fileName);

    /// <summary>The JSON files in <paramref name="folder"/> (such as <c>"events"</c>), as names Read takes, sorted.</summary>
    public abstract IReadOnlyList<string> List(string folder);

    public static DataSource FromDirectory(string directory) => new DirectorySource(directory);

    public static DataSource FromFiles(IReadOnlyDictionary<string, string> files) => new FilesSource(files);

    /// <summary>Embedded resources whose logical names are <c>{prefix}{fileName}</c>.</summary>
    public static DataSource FromAssembly(Assembly assembly, string prefix = "data/") => new AssemblySource(assembly, prefix);

    static IReadOnlyList<string> InFolder(IEnumerable<string> names, string folder) =>
        [.. names.Where(n => n.StartsWith(folder + "/") && n.EndsWith(".json") && n.IndexOf('/', folder.Length + 1) < 0)
            .Order(StringComparer.Ordinal)];

    sealed class DirectorySource(string directory) : DataSource
    {
        public override string? Read(string fileName)
        {
            var path = Path.Combine(directory, fileName);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        public override IReadOnlyList<string> List(string folder)
        {
            var dir = Path.Combine(directory, folder);
            return Directory.Exists(dir)
                ? InFolder(Directory.GetFiles(dir, "*.json").Select(f => $"{folder}/{Path.GetFileName(f)}"), folder)
                : [];
        }
    }

    sealed class FilesSource(IReadOnlyDictionary<string, string> files) : DataSource
    {
        public override string? Read(string fileName) => files.GetValueOrDefault(fileName);

        public override IReadOnlyList<string> List(string folder) => InFolder(files.Keys, folder);
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

        public override IReadOnlyList<string> List(string folder) =>
            InFolder(assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix)).Select(n => n[prefix.Length..]), folder);
    }
}
