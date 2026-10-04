using System.Globalization;
using System.Text;
using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

/// <summary>
/// Totals over many simulated battles of one encounter (the brief's batch summary): win rate, battle length,
/// party Health left, damage per unit, and how often each action and effect fired.
/// </summary>
public sealed class BatchSummary
{
    int battles, wins, losses, stalemates;
    double totalTime;
    readonly List<double> partyHealthLeft = [];
    readonly Dictionary<string, double> damageByUnit = [];
    readonly Dictionary<string, int> actionUses = [];
    readonly Dictionary<string, int> effectsApplied = [];
    readonly Dictionary<string, int> heroDeaths = [];
    int staggerBreaks, interrupts, misses, attempts;

    public int Battles => battles;
    public double WinRate => battles == 0 ? 0 : (double)wins / battles;

    public void Add(Battle battle)
    {
        battles++;
        switch (battle.Winner)
        {
            case Side.Party: wins++; break;
            case Side.Enemy: losses++; break;
            default: stalemates++; break;
        }
        totalTime += battle.Clock.Time;

        var party = battle.Units.Where(u => u.Side == Side.Party).ToList();
        partyHealthLeft.Add((double)party.Sum(u => u.Health) / party.Sum(u => u.MaxHealth));
        foreach (var hero in party.Where(u => !u.Alive))
            Bump(heroDeaths, hero.Def.Name);

        foreach (var r in battle.Results)
        {
            if (r.Action is { } action && (r.Outcomes.FirstOrDefault() is CastStarted || action.CastTime == 0))
                Bump(actionUses, $"{r.Actor.Def.Name}: {action.Name}");
            foreach (var o in r.Outcomes)
            {
                switch (o)
                {
                    case Attempt a:
                        attempts++;
                        if (!a.Roll.Success) misses++;
                        break;
                    case Damaged d:
                        AddDamage(r.Actor.Def.Name, d.Taken);
                        break;
                    case PeriodicDamaged p when battle.Units.FirstOrDefault(u => u.Id == p.Buff.CasterId) is { } caster:
                        AddDamage(caster.Def.Name, p.Taken);
                        break;
                    case BuffApplied b:
                        Bump(effectsApplied, b.Buff.Def.Cc == CcKind.None ? b.Buff.Def.Name : $"{b.Buff.Def.Name} ({b.Buff.Def.Cc})");
                        break;
                    case Staggered { Broke: true }:
                        staggerBreaks++;
                        break;
                    case Interrupted:
                        interrupts++;
                        break;
                }
            }
        }
    }

    void AddDamage(string unit, DamageTaken taken) =>
        damageByUnit[unit] = damageByUnit.GetValueOrDefault(unit) + taken.Absorbed + taken.ToHealth;

    static void Bump(Dictionary<string, int> counts, string key) => counts[key] = counts.GetValueOrDefault(key) + 1;

    public string Report()
    {
        var inv = CultureInfo.InvariantCulture;
        string Pct(double v) => (v * 100).ToString("0.0", inv) + "%";
        string Per(double v) => (v / battles).ToString("0.0", inv);

        var sb = new StringBuilder();
        sb.AppendLine($"Win rate         {Pct(WinRate)}  ({wins} won, {losses} lost, {stalemates} stalemates)");
        sb.AppendLine($"Battle length    {Per(totalTime)} turns on average");
        sb.AppendLine($"Party HP left    {Pct(partyHealthLeft.Average())} on average, {Pct(partyHealthLeft.Min())} at worst");
        sb.AppendLine($"Hit rate         {Pct(attempts == 0 ? 0 : 1 - (double)misses / attempts)} of {attempts} attacks");
        sb.AppendLine();
        sb.AppendLine("Damage dealt per battle");
        foreach (var (unit, dmg) in damageByUnit.OrderByDescending(kv => kv.Value))
            sb.AppendLine($"  {unit,-22} {Per(dmg),8}");
        sb.AppendLine();
        sb.AppendLine("Hero deaths per battle");
        foreach (var (hero, n) in heroDeaths.OrderByDescending(kv => kv.Value))
            sb.AppendLine($"  {hero,-22} {Per(n),8}");
        if (heroDeaths.Count == 0) sb.AppendLine("  none");
        sb.AppendLine();
        sb.AppendLine("Actions used per battle");
        foreach (var (action, n) in actionUses.OrderBy(kv => kv.Key))
            sb.AppendLine($"  {action,-32} {Per(n),8}");
        sb.AppendLine();
        sb.AppendLine("Effects and CC applied per battle");
        foreach (var (effect, n) in effectsApplied.OrderBy(kv => kv.Key))
            sb.AppendLine($"  {effect,-32} {Per(n),8}");
        sb.AppendLine($"  {"Stagger breaks",-32} {Per(staggerBreaks),8}");
        sb.AppendLine($"  {"Casts interrupted",-32} {Per(interrupts),8}");
        sb.AppendLine($"  {"Procs",-32} {"none yet (designed at the end of M1)",8}");
        return sb.ToString();
    }
}
