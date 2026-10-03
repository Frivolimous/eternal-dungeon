using System.Xml.Linq;

namespace EternalDungeon.Core.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Core_has_no_Godot_references()
    {
        var core = typeof(GameInfo).Assembly;
        Assert.DoesNotContain(core.GetReferencedAssemblies(), a => a.Name!.StartsWith("Godot", StringComparison.OrdinalIgnoreCase));

        var project = XDocument.Load(Path.Combine(TestPaths.RepoRoot, "src", "Core", "EternalDungeon.Core.csproj"));
        var refs = project.Descendants()
            .Where(e => e.Name.LocalName is "PackageReference" or "ProjectReference" or "Reference")
            .Select(e => (string?)e.Attribute("Include") ?? "")
            .Append((string?)project.Root!.Attribute("Sdk") ?? "");
        Assert.DoesNotContain(refs, r => r.Contains("Godot", StringComparison.OrdinalIgnoreCase));
    }
}
