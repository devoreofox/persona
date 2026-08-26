using System;
using System.Collections.Generic;
using System.Linq;
using Penumbra.Api.Enums;
using GetAvailableModSettings = Penumbra.Api.IpcSubscribers.GetAvailableModSettings;
using GetCollectionForObject = Penumbra.Api.IpcSubscribers.GetCollectionForObject;
using GetCollectionsByIdentifier = Penumbra.Api.IpcSubscribers.GetCollectionsByIdentifier;
using GetCurrentModSettings = Penumbra.Api.IpcSubscribers.GetCurrentModSettings;
using GetModList = Penumbra.Api.IpcSubscribers.GetModList;
using RemoveAllTemporaryModSettingsPlayer = Penumbra.Api.IpcSubscribers.RemoveAllTemporaryModSettingsPlayer;
using SetTemporaryModSettingsPlayer = Penumbra.Api.IpcSubscribers.SetTemporaryModSettingsPlayer;
using TrySetModSetting = Penumbra.Api.IpcSubscribers.TrySetModSetting;
using TrySetModSettings = Penumbra.Api.IpcSubscribers.TrySetModSettings;

namespace persona.Services;

public enum SettingAction { Enable, Disable, Toggle }

public sealed class ModSettingService
{
    private const int TempKey = 0x50524E41;
    private const string TempSource = "Persona";

    private readonly GetCollectionsByIdentifier getCollectionsByIdentifier;
    private readonly GetCollectionForObject getCollectionForObject;
    private readonly GetModList getModList;
    private readonly GetAvailableModSettings getAvailableModSettings;
    private readonly GetCurrentModSettings getCurrentModSettings;
    private readonly TrySetModSetting trySetModSetting;
    private readonly TrySetModSettings trySetModSettings;
    private readonly SetTemporaryModSettingsPlayer setTemporaryModSettingsPlayer;
    private readonly RemoveAllTemporaryModSettingsPlayer removeAllTemporaryModSettingsPlayer;

    public ModSettingService()
    {
        getCollectionsByIdentifier = new GetCollectionsByIdentifier(Plugin.PluginInterface);
        getCollectionForObject = new GetCollectionForObject(Plugin.PluginInterface);
        getModList = new GetModList(Plugin.PluginInterface);
        getAvailableModSettings = new GetAvailableModSettings(Plugin.PluginInterface);
        getCurrentModSettings = new GetCurrentModSettings(Plugin.PluginInterface);
        trySetModSetting = new TrySetModSetting(Plugin.PluginInterface);
        trySetModSettings = new TrySetModSettings(Plugin.PluginInterface);
        setTemporaryModSettingsPlayer = new SetTemporaryModSettingsPlayer(Plugin.PluginInterface);
        removeAllTemporaryModSettingsPlayer = new RemoveAllTemporaryModSettingsPlayer(Plugin.PluginInterface);
    }

    public (bool Ok, string Message) ResetTemporary()
    {
        var ec = removeAllTemporaryModSettingsPlayer.Invoke(0, TempKey);
        return ec != PenumbraApiEc.Success
            ? (false, $"Couldn't reset temporary settings: {Describe(ec)}.")
            : (true, "Cleared Persona's temporary mod settings.");
    }

    public (bool Ok, string Message) Apply(SettingAction action, string collectionName, string modName, string? groupName, string settingName, bool temporary)
    {
        var mods = getModList.Invoke();
        var mod = mods.FirstOrDefault(kv => string.Equals(kv.Value, modName, StringComparison.OrdinalIgnoreCase));
        if (mod.Key is null)
            return (false, $"No mod named '{modName}'.");

        var available = getAvailableModSettings.Invoke(mod.Key, mod.Value);
        if (available is null)
            return (false, $"Could not read settings for '{mod.Value}'.");

        string? group = null;
        var groupType = GroupType.Single;
        string? option = null;

        if (!string.IsNullOrEmpty(groupName))
        {
            var g = available.FirstOrDefault(kv => string.Equals(kv.Key, groupName, StringComparison.OrdinalIgnoreCase));
            if (g.Key is null)
                return (false, $"No group '{groupName}' in mod '{mod.Value}'.");
            option = g.Value.Item1.FirstOrDefault(o => string.Equals(o, settingName, StringComparison.OrdinalIgnoreCase));
            if (option is null)
                return (false, $"No setting '{settingName}' in group '{g.Key}'.");
            group = g.Key;
            groupType = g.Value.Item2;
        }
        else
        {
            foreach (var kv in available)
            {
                var match = kv.Value.Item1.FirstOrDefault(o => string.Equals(o, settingName, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                    continue;
                group = kv.Key;
                groupType = kv.Value.Item2;
                option = match;
                break;
            }

            if (group is null || option is null)
                return (false, $"No setting '{settingName}' in mod '{mod.Value}'.");
        }

        Guid collection;
        string label;
        if (temporary)
        {
            var obj = getCollectionForObject.Invoke(0);
            if (!obj.Item1)
                return (false, "No player collection found.");
            collection = obj.Item3.Item1;
            label = "temporary";
        }
        else if (!ResolveCollection(collectionName, out collection, out label))
        {
            return (false, $"No collection matching '{collectionName}'.");
        }

        var current = getCurrentModSettings.Invoke(collection, mod.Key, mod.Value);
        var currentSettings = current.Item2?.Item3;
        var selected = currentSettings?.GetValueOrDefault(group) is { } sel ? new List<string>(sel) : new List<string>();
        var enable = action switch
        {
            SettingAction.Enable => true,
            SettingAction.Disable => false,
            _ => !selected.Contains(option),
        };

        List<string> desired;
        if (groupType == GroupType.Multi)
        {
            desired = new List<string>(selected);
            if (enable)
            {
                if (!desired.Contains(option))
                    desired.Add(option);
            }
            else
            {
                desired.Remove(option);
            }
        }
        else if (groupType == GroupType.Single)
        {
            if (!enable)
                return (false, $"'{group}' is single-select; disable a specific option by enabling a different one instead.");
            desired = new List<string> { option };
        }
        else
        {
            return (false, $"'{group}' is a {groupType} group, which Persona can't set yet.");
        }

        var verb = enable ? "Enabled" : "Disabled";

        if (temporary)
        {
            var full = new Dictionary<string, IReadOnlyList<string>>();
            if (currentSettings is not null)
                foreach (var kv in currentSettings)
                    full[kv.Key] = kv.Value;
            full[group] = desired;

            var enabled = current.Item2?.Item1 ?? true;
            var priority = current.Item2?.Item2 ?? 0;
            var tec = setTemporaryModSettingsPlayer.Invoke(0, mod.Key, false, enabled, priority, full, TempSource, TempKey, mod.Value);
            return tec != PenumbraApiEc.Success
                ? (false, RejectMessage(tec, enable, option))
                : (true, $"{verb} '{option}' in '{mod.Value}' (temporary).");
        }

        var ec = groupType == GroupType.Multi
            ? trySetModSettings.Invoke(collection, mod.Key, group, desired, mod.Value)
            : trySetModSetting.Invoke(collection, mod.Key, group, option, mod.Value);
        return ec != PenumbraApiEc.Success
            ? (false, RejectMessage(ec, enable, option))
            : (true, $"{verb} '{option}' in '{mod.Value}' ({label}).");
    }

    private static string RejectMessage(PenumbraApiEc ec, bool enable, string option) =>
        ec == PenumbraApiEc.NothingChanged
            ? $"'{option}' is already {(enable ? "enabled" : "disabled")}."
            : $"Penumbra rejected the change: {Describe(ec)}.";

    private static string Describe(PenumbraApiEc ec) => ec switch
    {
        PenumbraApiEc.NothingChanged => "nothing to change",
        PenumbraApiEc.CollectionMissing => "collection not found",
        PenumbraApiEc.ModMissing => "mod not found",
        PenumbraApiEc.OptionGroupMissing => "option group not found",
        PenumbraApiEc.OptionMissing => "option not found",
        PenumbraApiEc.LowerPriority => "another mod is overriding it at a higher priority",
        PenumbraApiEc.InvalidArgument => "invalid argument",
        PenumbraApiEc.TemporarySettingDisallowed => "temporary settings aren't allowed here",
        PenumbraApiEc.TemporarySettingImpossible => "a temporary setting couldn't be applied",
        PenumbraApiEc.CollectionInactive => "that collection isn't active",
        PenumbraApiEc.SystemDisposed => "Penumbra is shutting down",
        _ => ec.ToString(),
    };

    private bool ResolveCollection(string name, out Guid id, out string label)
    {
        id = Guid.Empty;
        label = string.Empty;

        if (name.Length == 0 || string.Equals(name, "current", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "self", StringComparison.OrdinalIgnoreCase))
        {
            var obj = getCollectionForObject.Invoke(0);
            if (!obj.Item1)
                return false;
            id = obj.Item3.Item1;
            label = obj.Item3.Item2;
            return true;
        }

        var matches = getCollectionsByIdentifier.Invoke(name);
        if (matches.Count == 0)
            return false;

        id = matches[0].Item1;
        label = matches[0].Item2;
        return true;
    }
}
