using EternalDungeon.Core;
using EternalDungeon.Core.Data;
using Godot;

namespace EternalDungeon.Game;

/// <summary>M0 title screen: proves the game can call into Core and read the embedded data.</summary>
public partial class Main : Control
{
    public override void _Ready()
    {
        var label = GetNode<Label>("Title");
        try
        {
            var data = DataLoader.Load(DataSource.FromAssembly(typeof(Main).Assembly));
            label.Text = $"{GameInfo.Title}\nCore {GameInfo.Version} · {data.TagList.Count} tags, {data.StatList.Count} stats loaded";
            GD.Print(label.Text.Replace('\n', ' '));
        }
        catch (DataException e)
        {
            label.Text = $"{GameInfo.Title}\nData error: {e.Message}";
            label.Modulate = Colors.OrangeRed;
            GD.PushError(e.Message);
        }
    }
}
