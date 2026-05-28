using AssetData.Parser.Model;
using ReCap.Server.Util;

namespace ReCap.Server.Services.Assets.Loaders;

internal static class CategoryLoader
{
    public static int Load(
        LoaderContext ctx,
        string typeExtension,
        string rootStruct,
        Dictionary<uint, AssetValue> sink)
    {
        int loaded = 0, failed = 0;
        string? firstError = null;

        foreach (var (_, entry) in ctx.Reader.ListAssetsByType(typeExtension))
        {
            try
            {
                var node = ctx.Parse(entry, rootStruct);
                if (node is null) { failed++; continue; }
                sink[entry.Key.InstanceId] = node;
                loaded++;
            }
            catch (Exception ex)
            {
                failed++;
                firstError ??= $"{ex.GetType().Name}: {ex.Message}";
            }
        }

        if (loaded == 0 && failed == 0)
            Logger.info($"[AssetDatabase] {typeExtension}: 0 entries found in DBPF");
        else if (failed > 0)
            Logger.info($"[AssetDatabase] {typeExtension}: {loaded} ok / {failed} failed (first: {firstError ?? "null parse"})");
        return loaded;
    }
}
