using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

namespace BlazeServer;

public abstract class TdfMapBase : TdfCollectionBase
{
    public abstract int Size { get; }
    public abstract TdfType KeyType { get; }
    public abstract TdfType ValueType { get; }

    public abstract void InitMap(int count);
}

public class TdfPrimitiveMap<K, V> : TdfMapBase, IDictionary<K, V> where K : notnull
{
    private Dictionary<K, V> Container { get; } = [];

    public override int Size => Container.Count;

    public override TdfType KeyType
    {
        get
        {
            return typeof(K).Name switch
            {
                "UInt64" or "Int64" or "UInt32" or "Int32" or "UInt16" or "Int16" or "Byte" or "SByte" or "Boolean" => TdfType.Integer,
                "Single" => TdfType.Float,
                "String" => TdfType.String,
                _ => throw new Exception($"Unknown type ({typeof(K).FullName}) in PrimitiveVector!")
            };
        }
    }

    public override TdfType ValueType
    {
        get
        {
            return typeof(V).Name switch
            {
                "UInt64" or "Int64" or "UInt32" or "Int32" or "UInt16" or "Int16" or "Byte" or "SByte" or "Boolean" => TdfType.Integer,
                "Single" => TdfType.Float,
                "String" => TdfType.String,
                _ => throw new Exception($"Unknown type ({typeof(V).FullName}) in PrimitiveVector!")
            };
        }
    }

    public override void DecodeMembers(TdfDecoder decoder, int size)
    {
        for (var i = 0; i < size; ++i)
        {
            object key = typeof(K).Name switch
            {
                "UInt64" => decoder.DecodeUInt64(),
                "Int64" => decoder.DecodeInt64(),
                "UInt32" => decoder.DecodeUInt32(),
                "Int32" => decoder.DecodeInt32(),
                "UInt16" => decoder.DecodeUInt16(),
                "Int16" => decoder.DecodeInt16(),
                "Byte" => decoder.DecodeByte(),
                "SByte" => decoder.DecodeSByte(),
                "Boolean" => decoder.DecodeBool(),
                "Single" => decoder.DecodeFloat(),
                "String" => decoder.DecodeString(),
                _ => throw new Exception($"Unknown key type ({typeof(K).FullName}) in PrimitiveMap!"),
            };

            object value = typeof(V).Name switch
            {
                "UInt64" => decoder.DecodeUInt64(),
                "Int64" => decoder.DecodeInt64(),
                "UInt32" => decoder.DecodeUInt32(),
                "Int32" => decoder.DecodeInt32(),
                "UInt16" => decoder.DecodeUInt16(),
                "Int16" => decoder.DecodeInt16(),
                "Byte" => decoder.DecodeByte(),
                "SByte" => decoder.DecodeSByte(),
                "Boolean" => decoder.DecodeBool(),
                "Single" => decoder.DecodeFloat(),
                "String" => decoder.DecodeString(),
                _ => throw new Exception($"Unknown type ({typeof(V).FullName}) in PrimitiveMap!"),
            };

            Add((K)Convert.ChangeType(key, typeof(K)), (V)Convert.ChangeType(value, typeof(V)));
        }
    }

    public override void EncodeMembers(TdfEncoder encoder)
    {
        foreach (var elem in Container)
        {
            var keyObj = Convert.ChangeType(elem.Key, typeof(K))!;
            var valueObj = Convert.ChangeType(elem.Value, typeof(V))!;

            switch (typeof(K).Name)
            {
                case "UInt64":
                    encoder.EncodeUInt64("", (ulong)keyObj);
                    break;

                case "Int64":
                    encoder.EncodeInt64("", (long)keyObj);
                    break;

                case "UInt32":
                    encoder.EncodeUInt32("", (uint)keyObj);
                    break;

                case "Int32":
                    encoder.EncodeInt32("", (int)keyObj);
                    break;

                case "UInt16":
                    encoder.EncodeUInt16("", (ushort)keyObj);
                    break;

                case "Int16":
                    encoder.EncodeInt16("", (short)keyObj);
                    break;

                case "Byte":
                    encoder.EncodeByte("", (byte)keyObj);
                    break;

                case "SByte":
                    encoder.EncodeSByte("", (sbyte)keyObj);
                    break;

                case "Boolean":
                    encoder.EncodeBool("", (bool)keyObj);
                    break;

                case "Single":
                    encoder.EncodeFloat("", (float)keyObj);
                    break;

                case "String":
                    encoder.EncodeString("", (string)keyObj);
                    break;

                default:
                    throw new Exception($"Unknown key type ({typeof(K).FullName}) in PrimitiveMap!");
            }

            switch (typeof(V).Name)
            {
                case "UInt64":
                    encoder.EncodeUInt64("", (ulong)valueObj);
                    break;

                case "Int64":
                    encoder.EncodeInt64("", (long)valueObj);
                    break;

                case "UInt32":
                    encoder.EncodeUInt32("", (uint)valueObj);
                    break;

                case "Int32":
                    encoder.EncodeInt32("", (int)valueObj);
                    break;

                case "UInt16":
                    encoder.EncodeUInt16("", (ushort)valueObj);
                    break;

                case "Int16":
                    encoder.EncodeInt16("", (short)valueObj);
                    break;

                case "Byte":
                    encoder.EncodeByte("", (byte)valueObj);
                    break;

                case "SByte":
                    encoder.EncodeSByte("", (sbyte)valueObj);
                    break;

                case "Boolean":
                    encoder.EncodeBool("", (bool)valueObj);
                    break;

                case "Single":
                    encoder.EncodeFloat("", (float)valueObj);
                    break;

                case "String":
                    encoder.EncodeString("", (string)valueObj);
                    break;

                default:
                    throw new Exception($"Unknown value type ({typeof(V).FullName}) in PrimitiveMap!");
            }
        }
    }

    public override void InitMap(int count)
    {
        Container.Clear();
        Container.EnsureCapacity(count);
    }

    public override string ToString() => ToString(0);

    public override string ToString(int depth)
    {
        var sb = new StringBuilder();
        sb.Append(GetType().GetRealTypeName());

        if (Container.Count == 0)
            return sb.Append(" {} (empty)").ToString();

        sb.AppendLine().Append(' ', depth).AppendLine("{");

        depth += 2;

        foreach (var elem in Container)
            sb.Append(' ', depth).Append($"{elem.Key} = ").AppendLine(elem.Value?.ToString());

        depth -= 2;

        sb.Append(' ', depth).Append('}');

        return sb.ToString();
    }

    #region Dictionary
    public ICollection<K> Keys => Container.Keys;
    public ICollection<V> Values => Container.Values;
    public int Count => Container.Count;
    public bool IsReadOnly => ((ICollection<KeyValuePair<K, V>>)Container).IsReadOnly;
    public V this[K key] { get => Container[key]; set => Container[key] = value; }

    public void Add(K key, V value) => Container.Add(key, value);
    public bool ContainsKey(K key) => Container.ContainsKey(key);
    public bool Remove(K key) => Container.Remove(key);
    public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value) => Container.TryGetValue(key, out value);
    public void Add(KeyValuePair<K, V> item) => ((ICollection<KeyValuePair<K, V>>)Container).Add(item);
    public void Clear() => Container.Clear();
    public bool Contains(KeyValuePair<K, V> item) => ((ICollection<KeyValuePair<K, V>>)Container).Contains(item);
    public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex) => ((ICollection<KeyValuePair<K, V>>)Container).CopyTo(array, arrayIndex);
    public bool Remove(KeyValuePair<K, V> item) => ((ICollection<KeyValuePair<K, V>>)Container).Remove(item);
    public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => ((IEnumerable<KeyValuePair<K, V>>)Container).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Container).GetEnumerator();
    #endregion
}

public class TdfStructMap<K, V> : TdfMapBase, IDictionary<K, V> where K : notnull where V : Tdf, new()
{
    private Dictionary<K, V> Container { get; } = [];

    public override int Size => Container.Count;

    public override TdfType KeyType
    {
        get
        {
            return typeof(K).Name switch
            {
                "UInt64" or "Int64" or "UInt32" or "Int32" or "UInt16" or "Int16" or "Byte" or "SByte" or "Boolean" => TdfType.Integer,
                "Single" => TdfType.Float,
                "String" => TdfType.String,
                _ => throw new Exception($"Unknown type ({typeof(K).FullName}) in PrimitiveVector!")
            };
        }
    }

    public override TdfType ValueType => TdfType.Struct;

    public override void DecodeMembers(TdfDecoder decoder, int size)
    {
        for (var i = 0; i < size; ++i)
        {
            object key = typeof(K).Name switch
            {
                "UInt64" => decoder.DecodeUInt64(),
                "Int64" => decoder.DecodeInt64(),
                "UInt32" => decoder.DecodeUInt32(),
                "Int32" => decoder.DecodeInt32(),
                "UInt16" => decoder.DecodeUInt16(),
                "Int16" => decoder.DecodeInt16(),
                "Byte" => decoder.DecodeByte(),
                "SByte" => decoder.DecodeSByte(),
                "Boolean" => decoder.DecodeBool(),
                "Single" => decoder.DecodeFloat(),
                "String" => decoder.DecodeString(),
                _ => throw new Exception($"Unknown key type ({typeof(K).FullName}) in StructMap!"),
            };

            var structInst = Activator.CreateInstance(typeof(V)) as V ?? throw new Exception($"Unable to create type: {typeof(V).FullName}!");

            decoder.DecodeStruct("", structInst);

            Add((K)Convert.ChangeType(key, typeof(K)), structInst);
        }
    }

    public override void EncodeMembers(TdfEncoder encoder)
    {
        foreach (var elem in Container)
        {
            var keyObj = Convert.ChangeType(elem.Key, typeof(K))!;

            switch (typeof(K).Name)
            {
                case "UInt64":
                    encoder.EncodeUInt64("", (ulong)keyObj);
                    break;

                case "Int64":
                    encoder.EncodeInt64("", (long)keyObj);
                    break;

                case "UInt32":
                    encoder.EncodeUInt32("", (uint)keyObj);
                    break;

                case "Int32":
                    encoder.EncodeInt32("", (int)keyObj);
                    break;

                case "UInt16":
                    encoder.EncodeUInt16("", (ushort)keyObj);
                    break;

                case "Int16":
                    encoder.EncodeInt16("", (short)keyObj);
                    break;

                case "Byte":
                    encoder.EncodeByte("", (byte)keyObj);
                    break;

                case "SByte":
                    encoder.EncodeSByte("", (sbyte)keyObj);
                    break;

                case "Boolean":
                    encoder.EncodeBool("", (bool)keyObj);
                    break;

                case "Single":
                    encoder.EncodeFloat("", (float)keyObj);
                    break;

                case "String":
                    encoder.EncodeString("", (string)keyObj);
                    break;

                default:
                    throw new Exception($"Unknown key type ({typeof(K).FullName}) in PrimitiveMap!");
            }

            encoder.EncodeStruct("", elem.Value);
        }
    }

    public override void InitMap(int count)
    {
        Container.Clear();
        Container.EnsureCapacity(count);
    }

    public override string ToString() => ToString(0);

    public override string ToString(int depth)
    {
        var sb = new StringBuilder();
        sb.Append(GetType().GetRealTypeName());

        if (Container.Count == 0)
            return sb.Append(" {} (empty)").ToString();

        sb.AppendLine().Append(' ', depth).AppendLine("{");

        depth += 2;

        foreach (var elem in Container)
            sb.Append(' ', depth).Append($"{elem.Key} = ").AppendLine(elem.Value?.ToString(depth));

        depth -= 2;

        sb.Append(' ', depth).Append("}");

        return sb.ToString();
    }

    #region Dictionary
    public ICollection<K> Keys => Container.Keys;
    public ICollection<V> Values => Container.Values;
    public int Count => Container.Count;
    public bool IsReadOnly => ((ICollection<KeyValuePair<K, V>>)Container).IsReadOnly;
    public V this[K key] { get => Container[key]; set => Container[key] = value; }

    public void Add(K key, V value) => Container.Add(key, value);
    public bool ContainsKey(K key) => Container.ContainsKey(key);
    public bool Remove(K key) => Container.Remove(key);
    public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value) => Container.TryGetValue(key, out value);
    public void Add(KeyValuePair<K, V> item) => ((ICollection<KeyValuePair<K, V>>)Container).Add(item);
    public void Clear() => Container.Clear();
    public bool Contains(KeyValuePair<K, V> item) => ((ICollection<KeyValuePair<K, V>>)Container).Contains(item);
    public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex) => ((ICollection<KeyValuePair<K, V>>)Container).CopyTo(array, arrayIndex);
    public bool Remove(KeyValuePair<K, V> item) => ((ICollection<KeyValuePair<K, V>>)Container).Remove(item);
    public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => ((IEnumerable<KeyValuePair<K, V>>)Container).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Container).GetEnumerator();
    #endregion
}
