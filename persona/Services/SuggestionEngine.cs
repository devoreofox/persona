using System;
using System.Collections.Generic;
using System.Linq;
using persona.Models;

namespace persona.Services;

public sealed record DesignData(
    IReadOnlyDictionary<Guid, string> Designs,
    IReadOnlyDictionary<Guid, Dictionary<string, List<(string Dir, string Name)>>> EquipMods,
    IReadOnlyDictionary<string, List<(Guid Design, string Dir, string Name)>> ItemSources,
    IReadOnlyDictionary<Guid, HashSet<string>> EnabledDirs,
    IReadOnlyDictionary<Guid, HashSet<string>> DisabledDirs,
    IReadOnlyDictionary<Guid, Dictionary<string, int>> EnabledPriority,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> ChangedItems,
    IReadOnlySet<string> LiveEnabled,
    IReadOnlyDictionary<string, int> LivePriority,
    IReadOnlyDictionary<string, string> ModNames,
    Func<Guid, bool> IsBlacklisted,
    Func<string, string, bool> IsIgnored);

public static class SuggestionEngine
{
    public static List<(string Design, string Items)> Conflicts(DesignData data, Guid id)
    {
        var rows = new List<(string, string)>();
        if (!data.EquipMods.TryGetValue(id, out var mine))
            return rows;

        foreach (var (other, name) in data.Designs)
        {
            if (other == id || data.IsBlacklisted(other) || !data.EquipMods.TryGetValue(other, out var theirs))
                continue;

            var clashes = new List<string>();
            foreach (var (item, myMods) in mine)
            {
                if (myMods.Count == 0 || !theirs.TryGetValue(item, out var theirMods) || theirMods.Count == 0)
                    continue;
                if (!myMods.Select(m => m.Dir).ToHashSet().SetEquals(theirMods.Select(m => m.Dir)))
                    clashes.Add(item);
            }

            if (clashes.Count > 0)
                rows.Add((name, string.Join(", ", clashes)));
        }

        return rows.OrderBy(r => r.Item1, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static List<(string Item, string Sources)> BleedRisks(DesignData data, Guid id)
    {
        var rows = new List<(string, string)>();
        if (!data.EquipMods.TryGetValue(id, out var mine))
            return rows;

        foreach (var (item, mods) in mine)
        {
            if (mods.Count > 0 || !data.ItemSources.TryGetValue(item, out var sources))
                continue;

            var others = sources.Where(s => s.Design != id).ToList();
            if (others.Count == 0)
                continue;

            var summary = string.Join(", ", others
                .Select(s => $"{s.Name} ({data.Designs.GetValueOrDefault(s.Design, "?")})")
                .Distinct());
            rows.Add((item, summary));
        }

        return rows.OrderBy(r => r.Item1, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static (List<Suggestion> ThisDesign, List<(string Design, List<Suggestion> Rows)> Others) Suggestions(DesignData data, Guid id)
    {
        var thisDesign = new List<Suggestion>();
        var others = new List<(string, List<Suggestion>)>();
        if (!data.EquipMods.TryGetValue(id, out var mine))
            return (thisDesign, others);

        var myName = data.Designs.GetValueOrDefault(id, "?");
        var myEnabled = data.EnabledDirs.GetValueOrDefault(id) ?? new HashSet<string>();
        var myDisabled = data.DisabledDirs.GetValueOrDefault(id) ?? new HashSet<string>();

        var wanted = WantedMods(data, id);
        var wantedDirs = wanted.Values.SelectMany(v => v.Select(m => m.Dir)).ToHashSet();

        var add = new Dictionary<string, (string Name, int Priority, SortedSet<string> Items)>();
        foreach (var (item, mods) in wanted)
            foreach (var (dir, name, prio) in mods)
            {
                if (!add.TryGetValue(dir, out var e))
                    add[dir] = e = (name, prio, new SortedSet<string>(StringComparer.OrdinalIgnoreCase));
                e.Items.Add(item);
            }
        foreach (var (dir, e) in add)
            thisDesign.Add(new Suggestion(id, myName, dir, e.Name, e.Items.ToList(), true, e.Priority, myEnabled.Contains(dir)));

        var disable = new Dictionary<string, (string Name, SortedSet<string> Items)>();
        foreach (var item in mine.Keys)
        {
            if (!data.ItemSources.TryGetValue(item, out var sources))
                continue;
            foreach (var (g, dir, name) in sources)
            {
                if (g == id || wantedDirs.Contains(dir))
                    continue;
                if (!disable.TryGetValue(dir, out var e))
                    disable[dir] = e = (name, new SortedSet<string>(StringComparer.OrdinalIgnoreCase));
                e.Items.Add(item);
            }
        }
        foreach (var (dir, e) in disable)
            thisDesign.Add(new Suggestion(id, myName, dir, e.Name, e.Items.ToList(), false, 0, myDisabled.Contains(dir)));

        thisDesign = thisDesign
            .OrderByDescending(s => s.Enable)
            .ThenBy(s => s.ModName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var perDesign = new Dictionary<Guid, Dictionary<string, (string Name, SortedSet<string> Items)>>();
        foreach (var (item, mods) in wanted)
        {
            foreach (var (dir, name, _) in mods)
            {
                foreach (var g in data.Designs.Keys)
                {
                    if (g == id || data.IsBlacklisted(g) || !data.EquipMods.TryGetValue(g, out var gEquip) || !gEquip.ContainsKey(item))
                        continue;
                    if ((data.EnabledDirs.GetValueOrDefault(g) ?? new HashSet<string>()).Contains(dir))
                        continue;

                    if (!perDesign.TryGetValue(g, out var map))
                        perDesign[g] = map = new Dictionary<string, (string, SortedSet<string>)>();
                    if (!map.TryGetValue(dir, out var e))
                        map[dir] = e = (name, new SortedSet<string>(StringComparer.OrdinalIgnoreCase));
                    e.Items.Add(item);
                }
            }
        }
        foreach (var (g, map) in perDesign)
        {
            var gDisabled = data.DisabledDirs.GetValueOrDefault(g) ?? new HashSet<string>();
            var rows = map
                .Select(kv => new Suggestion(g, data.Designs.GetValueOrDefault(g, "?"), kv.Key, kv.Value.Name,
                    kv.Value.Items.ToList(), false, 0, gDisabled.Contains(kv.Key)))
                .OrderBy(s => s.ModName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            others.Add((data.Designs.GetValueOrDefault(g, "?"), rows));
        }

        return (thisDesign, others.OrderBy(o => o.Item1, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static Dictionary<string, List<(string Dir, string Name, int Priority)>> WantedMods(DesignData data, Guid id)
    {
        var result = new Dictionary<string, List<(string, string, int)>>();
        if (!data.EquipMods.TryGetValue(id, out var mine))
            return result;

        var savedPriority = data.EnabledPriority.GetValueOrDefault(id) ?? new Dictionary<string, int>();

        foreach (var (item, saved) in mine)
        {
            if (saved.Count > 0)
            {
                result[item] = saved
                    .Select(m => (m.Dir, m.Name, savedPriority.GetValueOrDefault(m.Dir)))
                    .ToList();
                continue;
            }

            var live = data.LiveEnabled
                .Where(dir => !data.IsIgnored(dir, item) && data.ChangedItems.TryGetValue(dir, out var items) && items.ContainsKey(item))
                .OrderByDescending(dir => data.LivePriority.GetValueOrDefault(dir))
                .Select(dir => (dir, data.ModNames.GetValueOrDefault(dir, dir), data.LivePriority.GetValueOrDefault(dir)))
                .ToList();
            if (live.Count > 0)
                result[item] = live;
        }

        return result;
    }
}
