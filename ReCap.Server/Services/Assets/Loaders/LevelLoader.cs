using AssetData.Parser;
using AssetData.Parser.Model;
using ReCap.Server.Util;

namespace ReCap.Server.Services.Assets.Loaders;

internal static class LevelLoader
{
    public static void Load(
        LoaderContext ctx,
        Dictionary<string, AssetValue> levelsByName,
        Dictionary<uint, AssetValue> markerSetsByHash,
        Dictionary<string, uint> aiMarkerSetByLevel)
    {
        int msOk = 0, msFail = 0;
        var failedNames = new List<string>();

        foreach (var (name, entry) in ctx.Reader.ListAssetsByType("Markerset"))
        {
            try
            {
                var node = ctx.Parse(entry, "Markerset");
                if (node is null) { msFail++; continue; }
                markerSetsByHash[entry.Key.InstanceId] = node;
                msOk++;
            }
            catch (Exception ex)
            {
                msFail++;
                failedNames.Add($"{name} (memSize={entry.MemSize}): {ex.Message.Split(':').Last().Trim()}");
                // if (failedNames.Count < 5)
            }
        }
        if (msFail > 0)
            Logger.info($"[AssetDatabase] Markerset: {msOk} ok / {msFail} failed. First 5: \n  " + string.Join("\n  ", failedNames));

        int lvlOk = 0, lvlFail = 0;
        string? firstLvlErr = null;

        foreach (var (fullName, entry) in ctx.Reader.ListAssetsByType("Level"))
        {
            try
            {
                var node = ctx.Parse(entry, "Level");
                if (node is null) { lvlFail++; continue; }

                var bareName = StripExtension(fullName);
                levelsByName[bareName] = node;
                lvlOk++;

                if (node.FindByName("markersets") is ArrayValue msArr)
                {
                    foreach (var msRef in msArr.Items.OfType<StructValue>())
                    {
                        var refPath = (msRef.FindByName("markersetAsset") as StringValue)?.Value;
                        if (string.IsNullOrEmpty(refPath)) continue;
                        if (!refPath.EndsWith("_ai_1", StringComparison.OrdinalIgnoreCase)) continue;

                        aiMarkerSetByLevel[bareName] = DbpfReader.FnvHash(refPath);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                lvlFail++;
                firstLvlErr ??= $"{ex.GetType().Name}: {ex.Message}";
            }
        }
        if (lvlFail > 0)
            Logger.info($"[AssetDatabase] Level: {lvlOk} ok / {lvlFail} failed (first: {firstLvlErr})");
    }

    private static string StripExtension(string fullName)
    {
        var dot = fullName.LastIndexOf('.');
        return dot > 0 ? fullName[..dot] : fullName;
    }
}
