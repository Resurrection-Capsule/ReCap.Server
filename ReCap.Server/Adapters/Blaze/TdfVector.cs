using System.Collections;
using System.Text;

namespace ReCap.Server.Adapters.Blaze;

public abstract class TdfVectorBase : TdfCollectionBase
{
    public abstract int Size { get; }
    public abstract TdfType Type { get; }

    public abstract void InitVector(int size);
}

public class TdfPrimitiveVector<T> : TdfVectorBase, IList<T>
{
    private List<T> Container { get; } = [];

    public override int Size => Container.Count;

    public override TdfType Type
    {
        get
        {
            return typeof(T).Name switch
            {
                "UInt64" or "Int64" or "UInt32" or "Int32" or "UInt16" or "Int16" or "Byte" or "SByte" or "Boolean" => TdfType.Integer,
                "Single" => TdfType.Float,
                "String" => TdfType.String,
                "TdfBlob" => TdfType.Binary,
                "BlazeObjectId" => TdfType.BlazeObjectId,
                "BlazeObjectType" => TdfType.BlazeObjectType,
                _ => throw new Exception($"Unknown type ({typeof(T).FullName}) in PrimitiveVector!")
            };
        }
    }

    public override void DecodeMembers(TdfDecoder decoder, int size)
    {
        for (var i = 0; i < size; ++i)
        {
            switch (typeof(T).Name)
            {
                case "UInt64":
                    Add((T)Convert.ChangeType(decoder.DecodeUInt64(""), typeof(T)));
                    break;

                case "Int64":
                    Add((T)Convert.ChangeType(decoder.DecodeInt64(""), typeof(T)));
                    break;

                case "UInt32":
                    Add((T)Convert.ChangeType(decoder.DecodeUInt32(""), typeof(T)));
                    break;

                case "Int32":
                    Add((T)Convert.ChangeType(decoder.DecodeInt32(""), typeof(T)));
                    break;

                case "UInt16":
                    Add((T)Convert.ChangeType(decoder.DecodeUInt16(""), typeof(T)));
                    break;

                case "Int16":
                    Add((T)Convert.ChangeType(decoder.DecodeInt16(""), typeof(T)));
                    break;

                case "Byte":
                    Add((T)Convert.ChangeType(decoder.DecodeByte(""), typeof(T)));
                    break;

                case "SByte":
                    Add((T)Convert.ChangeType(decoder.DecodeSByte(""), typeof(T)));
                    break;

                case "Boolean":
                    Add((T)Convert.ChangeType(decoder.DecodeBool(""), typeof(T)));
                    break;

                case "Single":
                    Add((T)Convert.ChangeType(decoder.DecodeFloat(""), typeof(T)));
                    break;

                case "String":
                    Add((T)Convert.ChangeType(decoder.DecodeString(""), typeof(T)));
                    break;

                case "TdfBlob":
                    var blob = new TdfBlob();
                    decoder.DecodeBinary("", blob);
                    Add((T)Convert.ChangeType(blob, typeof(T)));
                    break;

                case "BlazeObjectId":
                    var objectId = new BlazeObjectId();
                    decoder.DecodeBlazeObjectId("", objectId);
                    Add((T)Convert.ChangeType(objectId, typeof(T)));
                    break;

                case "BlazeObjectType":
                    var objectType = new BlazeObjectType();
                    decoder.DecodeBlazeObjectType("", objectType);
                    Add((T)Convert.ChangeType(objectType, typeof(T)));
                    break;

                default:
                    throw new Exception($"Unknown type ({typeof(T).FullName}) in PrimitiveVector!");
            }
        }
    }

    public override void EncodeMembers(TdfEncoder encoder)
    {
        foreach (var elem in Container)
        {
            var objElem = Convert.ChangeType(elem, typeof(T))!;

            switch (typeof(T).Name)
            {
                case "UInt64":
                    encoder.EncodeUInt64("", (ulong)objElem);
                    break;

                case "Int64":
                    encoder.EncodeInt64("", (long)objElem);
                    break;

                case "UInt32":
                    encoder.EncodeUInt32("", (uint)objElem);
                    break;

                case "Int32":
                    encoder.EncodeInt32("", (int)objElem);
                    break;

                case "UInt16":
                    encoder.EncodeUInt16("", (ushort)objElem);
                    break;

                case "Int16":
                    encoder.EncodeInt16("", (short)objElem);
                    break;

                case "Byte":
                    encoder.EncodeByte("", (byte)objElem);
                    break;

                case "SByte":
                    encoder.EncodeSByte("", (sbyte)objElem);
                    break;

                case "Boolean":
                    encoder.EncodeBool("", (bool)objElem);
                    break;

                case "Single":
                    encoder.EncodeFloat("", (float)objElem);
                    break;

                case "String":
                    encoder.EncodeString("", (string)objElem);
                    break;

                case "TdfBlob":
                    encoder.EncodeBinary("", (TdfBlob)objElem);
                    break;

                case "BlazeObjectId":
                    encoder.EncodeBlazeObjectId("", (BlazeObjectId)objElem);
                    break;

                case "BlazeObjectType":
                    encoder.EncodeBlazeObjectType("", (BlazeObjectType)objElem);
                    break;

                default:
                    throw new Exception($"Unknown type ({typeof(T).FullName}) in PrimitiveVector!");
            }
        }
    }

    public override void InitVector(int size)
    {
        Container.Clear();
        Container.Capacity = size;
    }

    public override string ToString() => ToString(0);

    public override string ToString(int depth)
    {
        var sb = new StringBuilder();
        sb.Append(GetType().GetRealTypeName());

        if (Container.Count == 0)
            return sb.Append(" {} (empty)").ToString();

        sb.Append(" {");

        foreach (var elem in Container)
            sb.Append(elem?.ToString()).Append(", ");

        if (Container.Count > 0)
            sb.Remove(sb.Length - 2, 2);

        return sb.Append('}').ToString();
    }

    #region IList<T>
    public int Count => Container.Count;

    public bool IsReadOnly => ((ICollection<T>)Container).IsReadOnly;

    public T this[int index] { get => Container[index]; set => Container[index] = value; }

    public int IndexOf(T item) => Container.IndexOf(item);
    public void Insert(int index, T item) => Container.Insert(index, item);
    public void RemoveAt(int index) => Container.RemoveAt(index);
    public void Add(T item) => Container.Add(item);
    public void Clear() => Container.Clear();
    public bool Contains(T item) => Container.Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => Container.CopyTo(array, arrayIndex);
    public bool Remove(T item) => Container.Remove(item);
    public IEnumerator<T> GetEnumerator() => Container.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Container.GetEnumerator();
    #endregion
}

public class TdfStructVector<T> : TdfVectorBase, IList<T> where T : Tdf, new()
{
    private List<T> Container { get; } = [];

    public override int Size => Container.Count;

    public override TdfType Type => TdfType.Struct;

    public override void DecodeMembers(TdfDecoder decoder, int size)
    {
        for (var i = 0; i < size; ++i)
        {
            var elem = (Activator.CreateInstance(typeof(T)) as Tdf)!;

            if (elem.GetType().IsSubclassOf(typeof(TdfUnion)))
                decoder.DecodeUnion(string.Empty, (elem as TdfUnion)!);
            else
                decoder.DecodeStruct(string.Empty, elem);

            Add((elem as T)!);
        }
    }

    public override void EncodeMembers(TdfEncoder encoder)
    {
        foreach (var elem in Container)
        {
            if (elem.GetType().IsSubclassOf(typeof(TdfUnion)))
                encoder.EncodeUnion(string.Empty, (elem as TdfUnion)!);
            else
                encoder.EncodeStruct(string.Empty, elem);
        }
    }

    public override void InitVector(int size)
    {
        Container.Clear();
        Container.Capacity = size;
    }

    public override string ToString() => ToString(0);

    public override string ToString(int depth)
    {
        var sb = new StringBuilder();
        sb.Append(GetType().GetRealTypeName());

        if (Container.Count == 0)
            return sb.Append(" {} (empty)").ToString();

        sb.AppendLine().Append(' ', depth).AppendLine("{ ");

        depth += 2;

        var i = 0;

        foreach (var elem in Container)
            sb.Append(' ', depth).Append($"[{i++}]: ").AppendLine(elem.ToString(depth));

        depth -= 2;

        return sb.Append(' ', depth).Append(" }").ToString();
    }

    #region IList<T>
    public int Count => Container.Count;

    public bool IsReadOnly => ((ICollection<T>)Container).IsReadOnly;

    public T this[int index] { get => Container[index]; set => Container[index] = value; }

    public int IndexOf(T item) => Container.IndexOf(item);
    public void Insert(int index, T item) => Container.Insert(index, item);
    public void RemoveAt(int index) => Container.RemoveAt(index);
    public void Add(T item) => Container.Add(item);
    public void Clear() => Container.Clear();
    public bool Contains(T item) => Container.Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => Container.CopyTo(array, arrayIndex);
    public bool Remove(T item) => Container.Remove(item);
    public IEnumerator<T> GetEnumerator() => Container.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Container.GetEnumerator();
    #endregion
}