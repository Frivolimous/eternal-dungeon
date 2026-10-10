using EternalDungeon.Core.Assets;
using EternalDungeon.Core.Data;
using Godot;
using Txt = EternalDungeon.Game.Text;
using Side = EternalDungeon.Core.Combat.Side;

namespace EternalDungeon.Game.BattleUi;

/// <summary>
/// One action in the action bar (M2 brief §5), styled as a small card frame: icon, name, AP cost, Mana cost and cast
/// time, and its number key. Unusable actions are shown disabled; the reason shows in the info line when the button
/// is hovered or focused (and as a tooltip).
/// </summary>
public partial class ActionButton : Button
{
    public ActionDef Action { get; }
    readonly BattleScreen screen;
    readonly int number;
    readonly string? why;
    readonly int mana;

    /// <param name="mana">What the action costs this unit (Mana Conduit lowers it).</param>
    public ActionButton(BattleScreen screen, ActionDef action, int number, string? why, int mana)
    {
        this.mana = mana;
        this.screen = screen;
        Action = action;
        this.number = number;
        this.why = why;
        CustomMinimumSize = new Vector2(112, 86);
        FocusMode = FocusModeEnum.All;
        AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        TooltipText = why is null ? "" : Txt.F("ui.unusable", ("reason", Txt.Reason(why)));
        // Disabled buttons can't take focus, so an unusable action stays enabled and refuses the pick instead.
        Modulate = why is null ? Colors.White : new Color(1, 1, 1, 0.45f);
    }

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        var r = new Rect2(Vector2.Zero, Size);
        var tags = Action.Tags;
        var accent = tags.Contains("spell") ? Ui.Mana : tags.Contains("melee") ? Ui.Enemy.Lightened(0.2f) : tags.Contains("ranged") ? Ui.Health : Ui.Gold;
        DrawRect(r, new Color(0.93f, 0.89f, 0.8f));
        DrawRect(r.Grow(-3), new Color(0.2f, 0.18f, 0.24f));
        DrawRect(r, HasFocus() ? Ui.Gold : accent, false, HasFocus() ? 3 : 2);

        // Icon (or a placeholder: the action's initial on its accent colour).
        var icon = new Rect2((Size.X - ArtCatalog.ActionIconDisplay) / 2, 7, ArtCatalog.ActionIconDisplay, ArtCatalog.ActionIconDisplay);
        if (screen.Main.Art.Get(ArtCatalog.ActionIconId(Action.Id)) is { } tex)
            DrawTextureRect(tex, icon, false);
        else
        {
            DrawRect(icon, accent.Darkened(0.35f));
            DrawString(font, icon.Position + new Vector2(0, 24), Action.Name[..1], HorizontalAlignment.Center, icon.Size.X, 20, Ui.Ink);
        }
        DrawString(font, new Vector2(5, 15), number.ToString(), HorizontalAlignment.Left, -1, 11, Ui.Dim);

        DrawString(font, new Vector2(4, 54), Action.Name, HorizontalAlignment.Center, Size.X - 8, 12, Ui.Ink);
        var costs = new List<string> { Txt.F("ui.ap_short", ("ap", Action.ApCost)) };
        if (mana > 0) costs.Add(Txt.F("ui.mana_short", ("mana", mana)));
        if (Action.CastTime > 0) costs.Add(Txt.F("ui.cast_short", ("cast", Action.CastTime / 100.0)));
        DrawString(font, new Vector2(4, 72), string.Join(" ", costs), HorizontalAlignment.Center, Size.X - 8, 10, Ui.Dim);
        if (why is not null)
            DrawLine(new Vector2(8, Size.Y - 8), new Vector2(Size.X - 8, 8), new Color(Ui.Enemy, 0.8f), 2);
    }

    public override void _Notification(int what)
    {
        if (what is (int)NotificationFocusEnter or (int)NotificationFocusExit or (int)NotificationMouseEnter or (int)NotificationMouseExit)
            QueueRedraw();
    }
}
