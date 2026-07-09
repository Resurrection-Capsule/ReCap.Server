using System.Text.RegularExpressions;
using ReCap.Server.Config;
using ReCap.Server.Models;
using ReCap.Server.Services;
using ReCap.Server.Util.Logging;

namespace ReCap.Server.Adapters.Persistence;

// Serves the creature/template portrait PNGs the client requests by path:
//   /template_png/{templateId}_thumb.png       — per-template portrait shipped under resources/static
//   /creature_png/{creatureId}_thumb|large.png — per-creature custom PNG uploaded via updateCreature (DB blob)
//   /game/service/png?id=<creatureId>|template_id=<templateId>  — C++ /game/service/png (fallback-to-default)
// Always returns a valid image/png (DB blob -> template thumb on disk -> 1x1 transparent PNG) so the
// client never 404s a portrait, mirroring the C++ handler's serve-default.png-on-miss behavior.
public sealed class PngStorageAdapter
{
    private readonly CreatureService _creatures;

    public PngStorageAdapter(SqliteConfig config) => _creatures = new CreatureService(config);

    // 1x1 transparent PNG — last-resort so the response is always a valid image.
    private static readonly byte[] TransparentPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    public bool Handles(string uri) =>
        uri.StartsWith("/template_png/", StringComparison.OrdinalIgnoreCase) ||
        uri.StartsWith("/creature_png/", StringComparison.OrdinalIgnoreCase) ||
        uri.Equals("/game/service/png", StringComparison.OrdinalIgnoreCase);

    public byte[] GetFile(string uri, Dictionary<string, string> parameters)
    {
        try
        {
            string leaf = uri[(uri.LastIndexOf('/') + 1)..];
            bool large = leaf.Contains("large", StringComparison.OrdinalIgnoreCase);

            if (uri.StartsWith("/creature_png/", StringComparison.OrdinalIgnoreCase))
            {
                var creature = LookupCreature(ParseLeadingId(leaf));
                return CreatureBlob(creature, large) ?? TemplateThumb(creature?.TemplateID) ?? TransparentPng;
            }

            if (uri.StartsWith("/template_png/", StringComparison.OrdinalIgnoreCase))
                return TemplateThumb(ParseLeadingId(leaf)) ?? TransparentPng;

            // /game/service/png — id = creature, template_id = template
            if (parameters.TryGetValue("id", out var cid) && ulong.TryParse(cid, out var creatureId))
            {
                var creature = LookupCreature(creatureId);
                return CreatureBlob(creature, large) ?? TemplateThumb(creature?.TemplateID) ?? TransparentPng;
            }
            if (parameters.TryGetValue("template_id", out var tid) && ulong.TryParse(tid, out var templateId))
                return TemplateThumb(templateId) ?? TransparentPng;
        }
        catch (Exception ex)
        {
            Log.Rest.Warn($"[PngStorageAdapter] {uri}: {ex.Message}");
        }
        return TransparentPng;
    }

    private CreatureModel? LookupCreature(ulong? id) => id is ulong v ? _creatures.getCreatureById(v) : null;

    private static byte[]? CreatureBlob(CreatureModel? creature, bool large)
    {
        var b64 = large ? creature?.LargePngBase64 : creature?.ThumbPngBase64;
        return string.IsNullOrEmpty(b64) ? null : Convert.FromBase64String(b64);
    }

    private static byte[]? TemplateThumb(ulong? templateId)
    {
        if (templateId is not ulong id) return null;
        var path = Path.Combine(ServerConfig.ResourcesDirectory, "static", "template_png", $"{id}_thumb.png");
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    private static ulong? ParseLeadingId(string leaf)
    {
        var match = Regex.Match(leaf, @"^\d+");
        return match.Success && ulong.TryParse(match.Value, out var id) ? id : null;
    }
}
