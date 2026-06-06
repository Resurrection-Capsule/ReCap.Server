using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AssetData.Parser;
using ReCap.Server.Util.Logging;

namespace ReCap.Server.Adapters.Persistence;

/// <summary>
/// Serves the client's <c>game://</c> packaged web UI (the in-game account/social hub:
/// <c>UI/XHTML/Labs/Setup/mainwebview.html</c> + its iframes, css and images) straight out of
/// the retail <c>Web.package</c> (DBPF), on demand. The webview shim rewrites
/// <c>game:///X</c> → <c>http://localhost/game/X</c> (ReCapMiniBlink.mapGameUrl); this resolves
/// <c>/game/&lt;path&gt;</c> back to the packaged asset.
///
/// Path → key: the DBPF indexes by FNV(filename)+FNV(extension) (group/path ignored, matching
/// <see cref="DbpfReader.Resolve"/>), so only the final <c>name.ext</c> segment is used — which
/// is also how the page's relative <c>images/foo.png</c> references resolve.
/// </summary>
public static class GameStorageAdapter
{
    private const string URI_PREFIX = "/game/";

    public static bool Handles(string uri) => uri.StartsWith(URI_PREFIX, StringComparison.OrdinalIgnoreCase);

    public static byte[] GetFile(string uri)
    {
        // /game/UI/XHTML/Labs/Setup/mainwebview.html -> "mainwebview.html"
        string path = uri.Substring(URI_PREFIX.Length).Split('?')[0].TrimEnd('/');
        string leaf = path.Length == 0 ? "" : path.Substring(path.LastIndexOf('/') + 1);
        if (leaf.Length == 0 || !leaf.Contains('.'))
            throw new FileNotFoundException(uri);

        // Synthetic locale bootstrap script (not in the package) — see LocaleStore.
        if (LocaleStore.IsScript(leaf))
            return Encoding.UTF8.GetBytes(LocaleStore.ScriptJs());

        DbpfReader reader = PackageMounts.Default?.Get(WellKnownPackage.Web) ?? throw new FileNotFoundException(uri);
        byte[]? bytes = reader.GetAsset(leaf);
        if (bytes == null)
        {
            Log.Rest.Warn($"[GameStorageAdapter] {uri} -> '{leaf}' not in Web.package");
            throw new FileNotFoundException(uri);
        }

        bytes = ModernizeHtml(leaf, bytes);

        Log.Rest.Debug($"[GameStorageAdapter] {uri} -> '{leaf}' ({bytes.Length} bytes)");
        return bytes;
    }

    private static readonly Regex _iframeWithId = new(
        @"<iframe\b(?<attrs>[^>]*?)\bid\s*=\s*[""'](?<id>[^""']+)[""'](?<rest>[^>]*)>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Modern-engine compat shim for the retail EA HTML. The hub addresses its iframes as
    /// <c>window.frames["FRAME3"].showScreen(...)</c> — which in WebKit-2008 (the original
    /// EAWebKit) resolved by the iframe's <c>id</c> to its contentWindow, but in Chromium
    /// (the new engine) <c>window.frames["X"]</c> resolves by the <c>name</c> attribute; with
    /// only an <c>id</c>, named access returns the iframe ELEMENT (no <c>showScreen</c>) →
    /// "is not a function" → blank. Mirror each iframe's <c>id</c> into a <c>name</c> so frame
    /// access works on the modern engine. (Targeted, logged, content-preserving.)
    /// </summary>
    private static byte[] ModernizeHtml(string leaf, byte[] bytes)
    {
        if (!leaf.EndsWith(".html", StringComparison.OrdinalIgnoreCase) &&
            !leaf.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
            return bytes;

        string html = Encoding.UTF8.GetString(bytes);
        int added = 0;
        string patched = _iframeWithId.Replace(html, m =>
        {
            string whole = m.Value;
            if (Regex.IsMatch(whole, @"\bname\s*=", RegexOptions.IgnoreCase))
                return whole;   // already has a name
            added++;
            string id = m.Groups["id"].Value;
            return $"<iframe{m.Groups["attrs"].Value} id=\"{id}\" name=\"{id}\"{m.Groups["rest"].Value}>";
        });

        // Inject the locale bootstrap as the first <head> script so retrieveLocaleString is
        // synchronous before any page script runs. Idempotent (skip if already present).
        bool injectedLocale = false;
        if (!patched.Contains(LocaleStore.ScriptName))
        {
            var headMatch = Regex.Match(patched, @"<head\b[^>]*>", RegexOptions.IgnoreCase);
            if (headMatch.Success)
            {
                int at = headMatch.Index + headMatch.Length;
                patched = patched.Insert(at, "\n" + LocaleStore.ScriptTag);
                injectedLocale = true;
            }
        }

        if (added == 0 && !injectedLocale) return bytes;
        Log.Rest.Debug($"[GameStorageAdapter] {leaf}: name= on {added} iframe(s), locale-script={injectedLocale}");
        return Encoding.UTF8.GetBytes(patched);
    }

    public static string ContentType(string uri)
    {
        int dot = uri.LastIndexOf('.');
        string ext = dot >= 0 ? uri.Substring(dot + 1).Split('?')[0].ToLowerInvariant() : "";
        return ext switch
        {
            "html" or "htm" => "text/html",
            "css" => "text/css",
            "js" => "application/javascript",
            "xml" => "text/xml",
            "json" => "application/json",
            "png" => "image/png",
            "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "svg" => "image/svg+xml",
            _ => "application/octet-stream",
        };
    }
}
