using System.Numerics;
using AssetData.Parser.Model;

namespace ReCap.Server.Services.Assets;

internal static class AssetValueExtensions
{
    public static AssetValue? FindByName(this AssetValue? node, string name)
    {
        if (node is null) return null;
        foreach (var child in node.Children)
            if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                return child;
        return null;
    }

    public static uint AsUInt32(this AssetValue? n) => n is NumberValue v ? (uint)v.Value : 0u;
    public static int AsInt32(this AssetValue? n) => n is NumberValue v ? (int)v.Value : 0;
    public static ulong AsUInt64(this AssetValue? n) => n is NumberValue v ? (ulong)v.Value : 0ul;
    public static float AsFloat(this AssetValue? n) => n is NumberValue v ? (float)v.Value : 0f;
    public static bool AsBool(this AssetValue? n) => n is BoolValue v && v.Value;

    public static string AsString(this AssetValue? n) => n switch
    {
        StringValue s => s.Value,
        LocalizedStringValue l => l.PrimaryValue,
        _ => string.Empty
    };

    public static Vector3 AsVector3(this AssetValue? n) =>
        n is VectorValue v ? new Vector3(v.X, v.Y, v.Z) : Vector3.Zero;

    public static Vector4 AsVector4(this AssetValue? n) =>
        n is VectorValue v ? new Vector4(v.X, v.Y, v.Z, v.W) : Vector4.Zero;

    public static Quaternion AsQuaternion(this AssetValue? n) =>
        n is VectorValue v ? new Quaternion(v.X, v.Y, v.Z, v.W) : Quaternion.Identity;
}
