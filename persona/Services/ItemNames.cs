using System.Collections.Generic;
using Lumina.Excel.Sheets;

namespace persona.Services;

public static class ItemNames
{
    private static readonly Dictionary<ulong, string> Cache = new();

    public static string Resolve(ulong itemId)
    {
        if (Cache.TryGetValue(itemId, out var cached))
            return cached;

        return Cache[itemId] = ResolveUncached(itemId);
    }

    private static string ResolveUncached(ulong itemId)
    {
        if (itemId == 0)
            return "None";
        if (itemId > uint.MaxValue)
            return $"#{itemId}";
        if (itemId >= 4_000_000_000ul)
            return "None";

        var name = Plugin.DataManager.GetExcelSheet<Item>().GetRowOrDefault((uint)itemId)?.Name.ExtractText();
        return string.IsNullOrEmpty(name) ? $"#{itemId}" : name;
    }
}
