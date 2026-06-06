using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using AssetData.Parser;
using ReCap.Server.Util.Logging;

namespace ReCap.Server.Adapters.Persistence;

/// <summary>
/// Loads the client's localized strings from <c>Locale/&lt;loc&gt;/Text.package</c> so the in-game
/// hub's <c>Client.retrieveLocaleString(key)</c> can resolve synchronously. The new webview engine
/// only has an async JS↔native channel, so instead of a per-call round-trip we pre-load the WHOLE
/// table once and inject it as a JS map; the shim then reads it inline.
///
/// Format (per DBPF entry, after RefPack decompress): a UTF-8 text table, one
/// <c>0xKEY  value</c> per line (plus <c>#</c> comments), exactly like the name registries. The
/// JS key is <c>localeGroupKey + lineKey</c> e.g. <c>"0xc6321815!" + "0x0b565e7b"</c>, where the
/// group key is the entry's InstanceId. So the lookup key = <c>"0x{InstanceId:x8}!{lineKey}"</c>.
/// </summary>
public static class LocaleStore
{
    private static readonly object _lock = new();
    private static string? _js;          // cached "/game/__recaplocale.js" payload
    private static bool _tried;

    public const string ScriptName = "__recaplocale.js";

    /// <summary>The injected &lt;script&gt; tag (idempotent marker via the src name).</summary>
    public const string ScriptTag = "<script src=\"__recaplocale.js\"></script>";

    public static bool IsScript(string leaf) =>
        leaf.Equals(ScriptName, StringComparison.OrdinalIgnoreCase);

    /// <summary>The locale bootstrap JS: defines window.__recapLocale and overrides the async
    /// retrieveLocaleString shim with a synchronous map lookup.</summary>
    public static string ScriptJs()
    {
        if (_tried) return _js ?? "";
        lock (_lock)
        {
            if (_tried) return _js ?? "";
            _tried = true;
            _js = Build();
            return _js ?? "";
        }
    }

    private static string? Build()
    {
        var map = new Dictionary<string, string>();
        try
        {
            var reader = PackageMounts.Default?.Get(WellKnownPackage.LocaleText);
            if (reader == null)
            {
                Log.Rest.Warn("[LocaleStore] Text.package not found; locale strings unavailable");
                return null;
            }
            int tables = 0, strings = 0;
            foreach (var entry in reader.Entries)
            {
                byte[]? data = reader.ReadEntry(entry);
                if (data == null) continue;
                string text = Encoding.UTF8.GetString(data);
                string prefix = $"0x{entry.Key.InstanceId:x8}!";
                tables++;
                foreach (var rawLine in text.Split('\n'))
                {
                    string line = rawLine.Trim('\r', '\n', '﻿', ' ', '\t');
                    if (line.Length == 0 || line[0] == '#') continue;
                    int sp = line.IndexOf(' ');
                    if (sp <= 0) continue;
                    string lineKey = line.Substring(0, sp);
                    string value = line.Substring(sp + 1);
                    map[prefix + lineKey] = value;
                    strings++;
                }
            }
            Log.Rest.Info($"[LocaleStore] loaded {strings} strings from {tables} tables");
        }
        catch (Exception ex)
        {
            Log.Rest.Error($"[LocaleStore] failed: {ex.Message}");
            if (map.Count == 0) return null;
        }

        string json = JsonSerializer.Serialize(map);
        // Override the async shim the DLL injects (window.Client already exists at this point —
        // the engine creates the JS context, the native bridge injects Client, THEN page scripts
        // (this one) run). Iframes call parent.Client.retrieveLocaleString → the parent's override.
        return
            "window.__recapLocale=" + json + ";\n" +
            "(function(){function set(){if(window.Client){window.Client.retrieveLocaleString=" +
            "function(k){var v=window.__recapLocale[k];return v!=null?v:'';};return true;}return false;}" +
            "if(!set()){var n=0,t=setInterval(function(){if(set()||++n>50)clearInterval(t);},0);}})();";
    }
}
