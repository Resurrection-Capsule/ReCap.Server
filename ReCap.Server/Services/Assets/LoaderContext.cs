using AssetData.Parser;

namespace ReCap.Server.Services.Assets;

internal sealed record LoaderContext(DbpfReader Reader, AssetParser Parser, EntryIndex Index)
{
    public AssetData.Parser.Model.AssetValue? Parse(DbpfEntry entry, string rootStruct)
    {
        var data = Reader.ReadEntry(entry);
        if (data is null) return null;

        var fileType = Parser.GetFileType(rootStruct);
        if (fileType is null) return null;

        return Parser.Parse(data, fileType.RootStruct, fileType.HeaderSize);
    }
}
