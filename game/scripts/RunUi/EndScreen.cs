using EternalDungeon.Core.Data;
using EternalDungeon.Core.Exploration;
using Godot;
using static EternalDungeon.Game.Text;

namespace EternalDungeon.Game.RunUi;

/// <summary>
/// The end of Dungeon 0 (M3A brief §10). Completed: the "slice complete" screen with time, battles, potions and Camps
/// used, and each hero's level and skills. Wiped: the defeat screen, and Dungeon 0 starts over.
/// </summary>
public partial class EndScreen(Main main, DungeonRun run) : Control
{
    public override void _Ready()
    {
        AddChild(new ColorRect { Color = Ui.Background, AnchorRight = 1, AnchorBottom = 1 });
        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(720, 0) };
        center.AddChild(panel);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        panel.AddChild(col);

        var won = run.State == RunState.Completed;
        col.AddChild(Ui.Label(T(won ? "ui.slice_complete" : "ui.party_fallen"), 30, won ? Ui.Gold : Ui.Enemy));
        col.AddChild(Ui.Label(run.Dungeon.Name, 18));
        var s = run.Stats;
        var time = TimeSpan.FromSeconds(main.RunSeconds);
        col.AddChild(Ui.Label(F("ui.end_stats", ("time", $"{(int)time.TotalMinutes}:{time.Seconds:00}"), ("battles", s.Battles),
            ("won", s.Victories), ("fled", s.Flees), ("items", s.ItemsUsed), ("camps", s.Camps), ("sanctuaries", s.Sanctuaries),
            ("nodes", s.Steps)), 15));
        foreach (var hero in run.Heroes)
        {
            var skills = string.Join(", ", hero.SkillLevels.Where(kv => kv.Value > 0)
                .Select(kv => $"{main.Data.Skills[kv.Key].Name} {kv.Value}").Concat(hero.Masteries.Select(m => m.Name)));
            var line = Ui.Label(F(hero.Dead ? "ui.end_hero_fallen" : "ui.end_hero", ("hero", hero.Name), ("level", hero.Level),
                ("skills", skills.Length > 0 ? skills : T("ui.none"))), 14, hero.Dead ? Ui.Dim : Ui.Ink);
            line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            col.AddChild(line);
        }
        if (!won) col.AddChild(Ui.Label(T("ui.wipe_restart"), 14, Ui.Dim));
        var again = Ui.Button(T(won ? "ui.new_game" : "ui.restart_dungeon"), () => main.NewRun(dungeon: run.Dungeon.Id));
        col.AddChild(again);
        col.AddChild(Ui.Button(T("ui.back_to_menu"), main.ShowMenu));
        again.GrabFocus();
    }
}
