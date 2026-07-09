using System.IO;
using System.Numerics;
using AssetData.Parser;

namespace ReCap.Server.Adapters.RakNet;

// Emits a wire reflection_serializer<N> message whose field ids/order/types come from the
// AssetData.Parser struct schema (one source of truth, shared with asset-file parsing) instead of a
// hardcoded per-packet field list. The parser's field order is registration order = the wire
// reflection id (verified against Ghidra for cAIDirector). Used by reflection packets (e.g. DirectorState).
public static class AssetReflection
{
    // Embedded schemas only — no game data needed to resolve a struct definition.
    private static readonly AssetParser Parser = new();

    public static IReadOnlyList<FieldDefinition> Schema(string structName) =>
        Parser.Structs.TryGetValue(structName, out var def)
            ? def.Fields
            : throw new InvalidOperationException($"AssetData.Parser has no struct definition '{structName}'");

    // Full reflection snapshot of the named struct: a bitmap over every schema field present in
    // `values`, in schema (registration) order, each value written per its DataType.
    public static void WriteReflection(BinaryWriter writer, string structName, IReadOnlyDictionary<string, object> values)
    {
        var fields = Schema(structName);
        var reflection = new ReflectionSerializer(writer, fields.Count);
        reflection.Begin();
        for (int id = 0; id < fields.Count; id++)
        {
            var field = fields[id];
            if (!values.TryGetValue(field.Name, out var value)) continue;
            reflection.Write((byte)id, () => WriteScalar(writer, field.Type, value));
        }
        reflection.End();
    }

    private static void WriteScalar(BinaryWriter w, DataType type, object v)
    {
        switch (type)
        {
            case DataType.Bool: w.Write((byte)(Convert.ToBoolean(v) ? 1 : 0)); break;
            case DataType.UInt8: case DataType.Char: w.Write(Convert.ToByte(v)); break;
            case DataType.UInt16: w.Write(Convert.ToUInt16(v)); break;
            case DataType.Int: case DataType.Int32: w.Write(Convert.ToInt32(v)); break;
            case DataType.UInt32: case DataType.ObjId: case DataType.HashId: case DataType.Key: w.Write(Convert.ToUInt32(v)); break;
            case DataType.Int64: w.Write(Convert.ToInt64(v)); break;
            case DataType.UInt64: w.Write(Convert.ToUInt64(v)); break;
            case DataType.Float: w.Write(Convert.ToSingle(v)); break;
            case DataType.Vector2: { var vec = (Vector2)v; w.Write(vec.X); w.Write(vec.Y); break; }
            case DataType.Vector3: { var vec = (Vector3)v; w.Write(vec.X); w.Write(vec.Y); w.Write(vec.Z); break; }
            case DataType.Vector4: { var vec = (Vector4)v; w.Write(vec.X); w.Write(vec.Y); w.Write(vec.Z); w.Write(vec.W); break; }
            default: throw new NotSupportedException($"AssetReflection: unsupported DataType {type} for wire reflection");
        }
    }
}
