using System.Diagnostics;
using System.Reflection;
using System.Text;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Blaze.Component;

namespace ReCap.Server.Adapters.Blaze;

public enum TdfType : byte
{
    Integer         = 0x0,
    String          = 0x1,
    Binary          = 0x2,
    Struct          = 0x3,
    List            = 0x4,
    Map             = 0x5,
    Union           = 0x6,
    Variable        = 0x7,
    BlazeObjectType = 0x8,
    BlazeObjectId   = 0x9,
    Float           = 0xA,
    TimeValue       = 0xB
}

public enum TdfFieldType
{
    Map,
    List,
    Float,
    Enum,
    String,
    Struct,
    Variable,
    Bitfield,
    Blob,
    Union,
    Class,
    BlazeObjectType,
    BlazeObjectId,
    TimeValue,
    Bool,
    Bool8,
    SByte,
    Byte,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    BlazeId,
    ComponentId,
    EntityType,
    EntityId
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class TdfFieldAttribute : Attribute
{
    public TdfFieldType? Type { get; set; } = null;
    public string Label { get; }
    public object? DefaultValue { get; set; } = null;

    public TdfFieldAttribute(string label, object? defaultValue = null)
    {
        Label = label;
        DefaultValue = defaultValue;
    }
}

public abstract class Tdf
{
    private static readonly NullabilityInfoContext _nullabilityContext = new();

    public virtual int Id { get; } = 0;

    public virtual void Encode(TdfEncoder encoder)
    {
        foreach (var prop in GetType().GetProperties())
        {
            var attr = prop.GetCustomAttribute<TdfFieldAttribute>();
            if (attr is null)
                continue;

            attr.Type ??= DetermineType(prop);

            if (attr.DefaultValue is not null && attr.DefaultValue.GetType().Name == "Int32" && attr.Type != TdfFieldType.Int32)
                attr.DefaultValue = Convert.ChangeType(attr.DefaultValue, prop.PropertyType);

            switch (attr.Type)
            {
                case TdfFieldType.Map:
                    encoder.EncodeMap(attr.Label, (prop.GetValue(this) as TdfMapBase)!);
                    break;

                case TdfFieldType.List:
                    encoder.EncodeVector(attr.Label, (prop.GetValue(this) as TdfVectorBase)!);
                    break;

                case TdfFieldType.Float:
                    encoder.EncodeFloat(attr.Label, (float)prop.GetValue(this)!, attr.DefaultValue as float?);
                    break;

                case TdfFieldType.Enum:
                    encoder.EncodeEnumRaw(attr.Label, prop.GetValue(this)!, attr.DefaultValue);
                    break;

                case TdfFieldType.String:
                    encoder.EncodeString(attr.Label, (prop.GetValue(this) as string)!, attr.DefaultValue as string);
                    break;

                case TdfFieldType.Variable:
                    encoder.EncodeVariable(attr.Label, prop.GetValue(this) as Tdf);
                    break;

                case TdfFieldType.Blob:
                    encoder.EncodeBinary(attr.Label, (prop.GetValue(this) as TdfBlob)!);
                    break;

                case TdfFieldType.Union:
                    encoder.EncodeUnion(attr.Label, (prop.GetValue(this) as TdfUnion)!);
                    break;

                case TdfFieldType.Struct:
                case TdfFieldType.Class:
                    encoder.EncodeStruct(attr.Label, (prop.GetValue(this) as Tdf)!);
                    break;

                case TdfFieldType.BlazeObjectType:
                    encoder.EncodeBlazeObjectType(attr.Label, (prop.GetValue(this) as BlazeObjectType)!);
                    break;

                case TdfFieldType.BlazeObjectId:
                    encoder.EncodeBlazeObjectId(attr.Label, (prop.GetValue(this) as BlazeObjectId)!);
                    break;

                case TdfFieldType.TimeValue:
                    encoder.EncodeTimeValue(attr.Label, (prop.GetValue(this) as TimeValue)!);
                    break;

                case TdfFieldType.Bool:
                case TdfFieldType.Bool8:
                    encoder.EncodeBool(attr.Label, (bool)prop.GetValue(this)!, attr.DefaultValue as bool?);
                    break;

                case TdfFieldType.SByte:
                    encoder.EncodeSByte(attr.Label, (sbyte)prop.GetValue(this)!, attr.DefaultValue as sbyte?);
                    break;

                case TdfFieldType.Byte:
                    encoder.EncodeByte(attr.Label, (byte)prop.GetValue(this)!, attr.DefaultValue as byte?);
                    break;

                case TdfFieldType.Int16:
                    encoder.EncodeInt16(attr.Label, (short)prop.GetValue(this)!, attr.DefaultValue as short?);
                    break;

                case TdfFieldType.UInt16:
                case TdfFieldType.ComponentId:
                case TdfFieldType.EntityType:
                    encoder.EncodeUInt16(attr.Label, (ushort)prop.GetValue(this)!, attr.DefaultValue as ushort?);
                    break;

                case TdfFieldType.Int32:
                    encoder.EncodeInt32(attr.Label, (int)prop.GetValue(this)!, attr.DefaultValue as int?);
                    break;

                case TdfFieldType.UInt32:
                case TdfFieldType.Bitfield:
                    encoder.EncodeUInt32(attr.Label, (uint)prop.GetValue(this)!, attr.DefaultValue as uint?);
                    break;

                case TdfFieldType.Int64:
                case TdfFieldType.BlazeId:
                case TdfFieldType.EntityId:
                    encoder.EncodeInt64(attr.Label, (long)prop.GetValue(this)!, attr.DefaultValue as long?);
                    break;

                case TdfFieldType.UInt64:
                    encoder.EncodeUInt64(attr.Label, (ulong)prop.GetValue(this)!, attr.DefaultValue as ulong?);
                    break;

                default:
                    throw new Exception($"Unhandled TdfFieldType: {attr.Type}!");
            }
        }
    }

    public virtual void Decode(TdfDecoder decoder)
    {
        while (decoder.StructHasNextElement() && decoder.PeekNextTag(out var label))
        {
            var handled = false;

            foreach (var prop in GetType().GetProperties())
            {
                var attr = prop.GetCustomAttribute<TdfFieldAttribute>();
                if (attr is null || attr.Label != label)
                    continue;

                handled = true;

                attr.Type ??= DetermineType(prop);

                switch (attr.Type)
                {
                    case TdfFieldType.Map:
                        decoder.DecodeMap(attr.Label, (prop.GetValue(this) as TdfMapBase)!);
                        break;

                    case TdfFieldType.List:
                        decoder.DecodeVector(attr.Label, (prop.GetValue(this) as TdfVectorBase)!);
                        break;

                    case TdfFieldType.Float:
                        prop.SetValue(this, decoder.DecodeFloat(attr.Label));
                        break;

                    case TdfFieldType.Enum:
                        prop.SetValue(this, decoder.DecodeEnumRaw(attr.Label, prop.PropertyType));
                        break;

                    case TdfFieldType.String:
                        prop.SetValue(this, decoder.DecodeString(attr.Label));
                        break;

                    case TdfFieldType.Variable:
                        prop.SetValue(this, decoder.DecodeVariable(attr.Label));
                        break;

                    case TdfFieldType.Blob:
                        decoder.DecodeBinary(attr.Label, (prop.GetValue(this) as TdfBlob)!);
                        break;

                    case TdfFieldType.Union:
                        decoder.DecodeUnion(attr.Label, (prop.GetValue(this) as TdfUnion)!);
                        break;

                    case TdfFieldType.Struct:
                    case TdfFieldType.Class:
                        decoder.DecodeStruct(attr.Label, (prop.GetValue(this) as Tdf)!);
                        break;

                    case TdfFieldType.BlazeObjectType:
                        decoder.DecodeBlazeObjectType(attr.Label, (prop.GetValue(this) as BlazeObjectType)!);
                        break;

                    case TdfFieldType.BlazeObjectId:
                        decoder.DecodeBlazeObjectId(attr.Label, (prop.GetValue(this) as BlazeObjectId)!);
                        break;

                    case TdfFieldType.TimeValue:
                        decoder.DecodeTimeValue(attr.Label, (prop.GetValue(this) as TimeValue)!);
                        break;

                    case TdfFieldType.Bool:
                    case TdfFieldType.Bool8:
                        prop.SetValue(this, decoder.DecodeBool(attr.Label));
                        break;

                    case TdfFieldType.SByte:
                        prop.SetValue(this, decoder.DecodeSByte(attr.Label));
                        break;

                    case TdfFieldType.Byte:
                        prop.SetValue(this, decoder.DecodeByte(attr.Label));
                        break;

                    case TdfFieldType.Int16:
                        prop.SetValue(this, decoder.DecodeInt16(attr.Label));
                        break;

                    case TdfFieldType.UInt16:
                    case TdfFieldType.ComponentId:
                    case TdfFieldType.EntityType:
                        prop.SetValue(this, decoder.DecodeUInt16(attr.Label));
                        break;

                    case TdfFieldType.Int32:
                        prop.SetValue(this, decoder.DecodeInt32(attr.Label));
                        break;

                    case TdfFieldType.UInt32:
                    case TdfFieldType.Bitfield:
                        prop.SetValue(this, decoder.DecodeUInt32(attr.Label));
                        break;

                    case TdfFieldType.Int64:
                    case TdfFieldType.BlazeId:
                    case TdfFieldType.EntityId:
                        prop.SetValue(this, decoder.DecodeInt64(attr.Label));
                        break;

                    case TdfFieldType.UInt64:
                        prop.SetValue(this, decoder.DecodeUInt64(attr.Label));
                        break;

                    default:
                        throw new Exception($"Unhandled TdfFieldType: {attr.Type}!");
                }

                break;
            }

            if (!handled)
            {
                ReCap.Server.Util.Logging.Log.Blaze.Warn($"Unhandled label {label} in {GetType().Namespace}.{GetType().Name}! Skipping...");

                decoder.SkipNextElement();
            }
        }
    }

    private static TdfFieldType DetermineType(PropertyInfo info)
    {
        var type = info.PropertyType;

        if (type.IsSubclassOf(typeof(TdfMapBase)))
            return TdfFieldType.Map;

        if (type.IsSubclassOf(typeof(TdfVectorBase)))
            return TdfFieldType.List;

        if (type.IsSubclassOf(typeof(TdfUnion)))
            return TdfFieldType.Union;

        if (type.IsSubclassOf(typeof(Tdf)))
            return TdfFieldType.Class;

        if (type == typeof(Tdf))
        {
            var nullableInfo = _nullabilityContext.Create(info);
            if (nullableInfo.WriteState is NullabilityState.Nullable)
                return TdfFieldType.Variable;

            throw new Exception($"Property {info.DeclaringType!.Name}.{info.Name} is not nullable, so it can't be Variable, but the actual Tdf type could not be determined!");
        }

        if (type == typeof(TdfBlob))
            return TdfFieldType.Blob;

        if (type == typeof(BlazeObjectType))
            return TdfFieldType.BlazeObjectType;

        if (type == typeof(BlazeObjectId))
            return TdfFieldType.BlazeObjectId;

        if (type.IsEnum)
            return TdfFieldType.Enum;

        return type.Name switch
        {
            "Single" => TdfFieldType.Float,
            "String" => TdfFieldType.String,
            "Boolean" => TdfFieldType.Bool8,
            "SByte" => TdfFieldType.SByte,
            "Byte" => TdfFieldType.Byte,
            "Int16" => TdfFieldType.Int16,
            "UInt16" => TdfFieldType.UInt16,
            "Int32" => TdfFieldType.Int32,
            "UInt32" => TdfFieldType.UInt32,
            "Int64" => TdfFieldType.Int64,
            "UInt64" => TdfFieldType.UInt64,
            _ => throw new Exception($"Unhandled TdfFieldType: {type.Name}"),
        };
    }

    public static Tdf? Create(uint id)
    {
        switch (id)
        {
            case 0x3C1FCCF0: // Blaze::GameManager::ReplicatedGameData
                return new ReplicatedGameData();

            case 0xAADA08AF: // Blaze::Playgroups::PlaygroupInfo
                return new PlaygroupInfo();

            case 0xC86D3EA7: // Blaze::GameManager::ReplicatedGamePlayer
                return new ReplicatedGamePlayer();

            case 0x055ED39E: // Blaze::GameReporting::SampleBase::GameAttributes
            case 0x0A26A2E9: // Blaze::GameReporting::SampleBase::Report
            case 0x0D8E2225: // Blaze::GameReporting::ArsonCTF_KS_Common::Report
            case 0x16CDF2CA: // Blaze::GameReporting::ArsonCTF_KS_NonDerived::Report
            case 0x17B5ED60: // Blaze::GameReporting::IntegratedSample::GameAttributes
            case 0x1887E99B: // Blaze::GameReporting::GameHistoryBasic::PlayerReport
            case 0x1CAD48C0: // Blaze::Rooms::RoomViewData
            case 0x1CCC9EAB: // Blaze::GameReporting::IntegratedSample::Report
            case 0x1D7C1BD4: // Blaze::Rooms::RoomReplicationContext
            case 0x20D983AC: // Blaze::GameReporting::IntegratedSample::PlayerReport
            case 0x21239231: // Blaze::GameManager::GameManagerCensusData
            case 0x2172A377: // Blaze::GameReporting::ArsonCTF_Custom::PlayerReport
            case 0x2317EFA3: // Blaze::GameReporting::ArsonMultiStatUpdatesKeyscopes::GameAttributes
            case 0x240AF9F7: // Blaze::GameReporting::ArsonCTF_GSA_Derived::PlayerReport
            case 0x2D1E297F: // Blaze::GameReporting::ArsonCTF_GSA_NonDerived::Report
            case 0x2D9E9E89: // Blaze::CensusData::RegionCounts
            case 0x2EBC8AEF: // Blaze::GameReporting::GameHistoryClubs_NonDerived::PlayerReport
            case 0x3085576A: // Blaze::GameReporting::ArsonLeague::Report
            case 0x3335476F: // Blaze::GameReporting::Battlegrounds::PlayerReport
            case 0x33710246: // Blaze::GameReporting::ArsonMultiStatUpdates::OffensiveAthlete
            case 0x354CF0B1: // Blaze::GameReporting::ArsonClub::Report
            case 0x386E6161: // Blaze::GameReporting::ArsonCTF_Derived::PlayerReport
            case 0x3AFF9AE2: // Blaze::GameReporting::ArsonLeagueGameKeyscopes::DefensiveAthlete
            case 0x3DDC3299: // Blaze::GameReporting::ArsonMultiStatUpdates::PlayerReport
            case 0x422A46E1: // Blaze::GameReporting::Shooter::GroupReport
            case 0x42A17226: // Blaze::GameReporting::Shooter::Report
            case 0x461F8DB8: // Blaze::GameReporting::ArsonLeagueGameKeyscopes::OffensiveAthlete
            case 0x496E470B: // Blaze::GameReporting::ArsonLeagueGameKeyscopes::PlayerReport
            case 0x4E26B0E5: // Blaze::GameReporting::ArsonCTF_Common::GameAttributes
            case 0x4EE94B44: // Blaze::GameReporting::ArsonCTF_NonDerived::SkippedPlayerReport
            case 0x5191DE40: // Blaze::GameReporting::Battlegrounds::GameReport
            case 0x52C5A4A2: // Blaze::GameReporting::ArsonLeague::DefensiveStats
            case 0x5335B784: // Blaze::GameReporting::GameHistoryClubs_NonDerived::ClubReport
            case 0x5686466B: // Blaze::GameReporting::ArsonCTF_Custom::GameAttributes
            case 0x59D90C2A: // Blaze::GameReporting::ArsonCTF_Custom::ResultNotification
            case 0x5A3762D9: // Blaze::Rooms::RoomViewReplicationContext
            case 0x5A7243A2: // Blaze::GameReporting::ArsonClubGameKeyscopes_NonDerived::OffensiveAthlete
            case 0x630A47B4: // Blaze::GameReporting::ArsonClubGameKeyscopes_NonDerived::Report
            case 0x64E156DA: // Blaze::GameReporting::ArsonCTF_KS_Common::GameAttributes
            case 0x652C075D: // Blaze::GameReporting::ArsonCTF_MidGame::Report
            case 0x690A93EE: // Blaze::GameReporting::ArsonMultiStatUpdatesKeyscopes::Report
            case 0x6E2754FF: // Blaze::GameReporting::ArsonLeagueGameKeyscopes::GameAttributes
            case 0x6F8E65A9: // Blaze::Playgroups::PlaygroupMemberInfo
            case 0x75C28412: // Blaze::GameReporting::ArsonCTF_MidGame::GameAttributes
            case 0x764B142E: // Blaze::GameReporting::GameHistoryClubs_NonDerived::Report
            case 0x76C407D6: // Blaze::GameReporting::ArsonCTF_NonDerived::PlayerReport
            case 0x793AE475: // Blaze::GameReporting::ArsonMultiKeyscopes::GameAttributes
            case 0x7CEE712B: // Blaze::GameReporting::ArsonLeague::PlayerReport
            case 0x81540F5C: // Blaze::GameReporting::ArsonMultiStatUpdatesKeyscopes::OffensiveAthlete
            case 0x837CA8CC: // Blaze::GameReporting::ArsonCTF_KS_Derived::PlayerReport
            case 0x8602611B: // Blaze::GameReporting::ArsonCTF_GSA_Common::PlayerReport
            case 0x8B387072: // Blaze::Rooms::RoomCategoryReplicationContext
            case 0x8B62E16A: // Blaze::GameReporting::SampleBase::PlayerReport
            case 0x8C47B730: // Blaze::GameReporting::ArsonCTF_Common::Report
            case 0x91213F9C: // Blaze::GameReporting::GameHistoryClubs_NonDerived::OffensiveAthlete
            case 0x95EBFC4D: // Blaze::GameManager::NumOfMatchmakingResponse
            case 0x9D100F0A: // Blaze::GameReporting::ArsonCTF_NonDerived::GameAttributes
            case 0x9E83B80A: // Blaze::GameReporting::ArsonClubGameKeyscopes_NonDerived::ClubReport
            case 0xA1844358: // Blaze::GameReporting::ArsonMultiStatUpdates::Report
            case 0xAB17B4DD: // Blaze::GameReporting::ArsonLeague::AthleteReport
            case 0xB6201EDE: // Blaze::GameReporting::ArsonCTF_MidGame::PlayerReport
            case 0xB8507AC0: // Blaze::GameReporting::ArsonMultiKeyscopes::Report
            case 0xBA4D3AAF: // Blaze::GameReporting::ArsonMultiStatUpdatesKeyscopes::PlayerReport
            case 0xBFED2619: // Blaze::Rooms::RoomCategoryData
            case 0xC0513268: // Blaze::Association::PresenceInfo
            case 0xC0D1E7A8: // Blaze::GameReporting::ArsonMultiKeyscopes::Weapon
            case 0xC245771F: // Blaze::GameReporting::ArsonLeague::GameAttributes
            case 0xC2E503A6: // Blaze::GameReporting::ArsonCTF_KS_Common::PlayerReport
            case 0xC4D25F25: // Blaze::GameReporting::ArsonCTF_EndGame::GameAttributes
            case 0xC5500A55: // Blaze::GameReporting::ArsonCTF_NonDerived::Report
            case 0xC943ECC3: // Blaze::GameManager::NumOfPlayerSessionsResponse
            case 0xC96D048B: // Blaze::GameReporting::ArsonCTF_KS_NonDerived::PlayerReport
            case 0xCD1F9A87: // Blaze::GameReporting::ArsonClub::ClubReport
            case 0xCF8032B6: // Blaze::GameReporting::ArsonCTF_Custom::Report
            case 0xD037FF4E: // Blaze::Rooms::RoomMemberReplicationContext
            case 0xD27984FB: // Blaze::Rooms::RoomData
            case 0xD387FB5A: // Blaze::GameReporting::ArsonCTF_GSA_Common::Report
            case 0xD5B9B932: // Blaze::GameReporting::ArsonClub::PlayerReport
            case 0xD61D2B0F: // Blaze::GameReporting::ArsonCTF_GSA_Common::GameAttributes
            case 0xD781A2B1: // Blaze::GameReporting::ArsonCTF_EndGame::PlayerReport
            case 0xD93F6471: // Blaze::GameReporting::ArsonCTF_Common::PlayerReport
            case 0xDB049F80: // Blaze::GameReporting::ArsonCTF_GSA_NonDerived::PlayerReport
            case 0xDBFBD5BC: // Blaze::Redirector::ServerInfoData
            case 0xDE8C81F5: // Blaze::GameReporting::ArsonClubGameKeyscopes_NonDerived::PlayerReport
            case 0xDD62938F: // Blaze::GameReporting::GameHistoryBasic::GameAttributes
            case 0xE027A5F5: // Blaze::Rooms::RoomMemberData
            case 0xE96CB27F: // Blaze::GameReporting::ArsonCTF_KS_NonDerived::GameAttributes
            case 0xEB344310: // Blaze::Playgroups::PlaygroupCensusData
            case 0xF3896434: // Blaze::GameReporting::ArsonCTF_GSA_NonDerived::GameAttributes
            case 0xF5D054F8: // Blaze::GameReporting::ArsonLeague::OffensiveStats
            case 0xF654DDCF: // Blaze::UserManagerCensusData
            case 0xF793154A: // Blaze::GameReporting::ArsonLeagueGameKeyscopes::Report
            case 0xF8FDE3DA: // Blaze::GameReporting::GameHistoryBasic::Report
            case 0xFEC8950D: // Blaze::GameReporting::ArsonMultiStatUpdates::GameAttributes
            case 0xFFA5A570: // Blaze::GameReporting::ArsonCTF_EndGame::Report
            case 0xFFEB3C01: // Blaze::GameReporting::ArsonMultiKeyscopes::PlayerReport
            case 0xFFF38E69: // Blaze::GameReporting::Shooter::EntityReport
            default:
                ReCap.Server.Util.Logging.Log.Blaze.Warn($"Unhandled id (0x{id:X8}) in Tdf.Create!");
                return null;
        }
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    public static uint LabelToTag(string label)
    {
        var result = 0u;

        for (var i = 0; i < Math.Min(label.Length, 4); ++i)
        {
            if (label[i] < ' ' || label[i] > '_')
                throw new ArgumentOutOfRangeException(nameof(label), "The label can only contains uppercase letters from ' ' to '_'!");

            result |= (uint)((label[i] - 0x20) << (8 + (3 - i) * 6));
        }

        return result;
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    public static string TagToLabel(uint label)
    {
        Span<char> chars = stackalloc char[4];

        chars[0] = (char)(((label >> 26) & 0x3F) + 0x20);
        chars[1] = (char)(((label >> 20) & 0x3F) + 0x20);
        chars[2] = (char)(((label >> 14) & 0x3F) + 0x20);
        chars[3] = (char)(((label >>  8) & 0x3F) + 0x20);

        var spaceIdx = chars.IndexOf(' ');
        if (spaceIdx == -1)
            return new string(chars);

        return new string(chars[..spaceIdx]);
    }

    public override string ToString() => ToString(0);

    public virtual string ToString(int depth)
    {
        var sb = new StringBuilder();
        sb.AppendLine(GetType().GetRealTypeName());
        sb.Append(' ', depth).AppendLine("{");

        depth += 2;

        foreach (var prop in GetType().GetProperties())
        {
            var attr = prop.GetCustomAttribute<TdfFieldAttribute>();
            if (attr is null)
                continue;

            sb.Append(' ', depth).Append($"(0x{LabelToTag(attr.Label):X8} {attr.Label}) = ");

            var val = prop.GetValue(this);
            if (val is Tdf tdf)
                sb.AppendLine(tdf.ToString(depth));
            else if (val is TdfCollectionBase tdfColl)
                sb.AppendLine(tdfColl.ToString(depth));
            else
                sb.AppendLine(val?.ToString() ?? "null");
        }

        depth -= 2;

        sb.Append(' ', depth).Append('}');

        return sb.ToString();
    }
}

public static class TypeExtensions
{
    public static string GetRealTypeName(this Type t)
    {
        if (!t.IsGenericType)
            return t.FullName!;

        if (t.IsNested && t.DeclaringType!.IsGenericType)
            throw new NotImplementedException();

        string txt = string.Concat(t.FullName.AsSpan(0, t.FullName!.IndexOf('`')), "<");
        int cnt = 0;

        foreach (var arg in t.GetGenericArguments())
        {
            if (cnt > 0)
                txt += ", ";

            txt += GetRealTypeName(arg);
            cnt++;
        }

        return txt + ">";
    }
}

public abstract class TdfCollectionBase
{
    public abstract void EncodeMembers(TdfEncoder encoder);
    public abstract void DecodeMembers(TdfDecoder decoder, int size);

    public abstract string ToString(int depth);
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class TdfUnionFieldAttribute : Attribute
{
    public uint Index { get; }

    public TdfUnionFieldAttribute(uint index) => Index = index;
}

public abstract class TdfUnion : Tdf
{
    public virtual uint ActiveMemberIndex { get; set; } = 0x7F;

    public override void Decode(TdfDecoder decoder)
    {
        if (ActiveMemberIndex == 0x7F)
            return;

        foreach (var prop in GetType().GetProperties())
        {
            var attr = prop.GetCustomAttribute<TdfUnionFieldAttribute>();
            if (attr is null || ActiveMemberIndex != attr.Index)
                continue;

            decoder.DecodeStruct("VALU", prop.GetValue(this) as Tdf ?? throw new Exception($"TdfUnion {GetType().GetRealTypeName()} with invalid/null members??"));
            return;
        }

        throw new InvalidOperationException($"TdfUnion {GetType().GetRealTypeName()}: Unable to decode invalid union member type: {ActiveMemberIndex}!");
    }

    public override void Encode(TdfEncoder encoder)
    {
        if (ActiveMemberIndex == 0x7F)
            return;

        foreach (var prop in GetType().GetProperties())
        {
            var attr = prop.GetCustomAttribute<TdfUnionFieldAttribute>();
            if (attr is null || ActiveMemberIndex != attr.Index)
                continue;

            encoder.EncodeStruct("VALU", prop.GetValue(this) as Tdf ?? throw new Exception($"TdfUnion {GetType().GetRealTypeName()} with invalid/null members??"));
            return;
        }

        throw new InvalidOperationException($"TdfUnion {GetType().GetRealTypeName()}: Unable to encode invalid union member type: {ActiveMemberIndex}!");
    }

    public override string ToString() => ToString(0);

    public override string ToString(int depth)
    {
        var sb = new StringBuilder();
        sb.AppendLine(GetType().GetRealTypeName());
        sb.Append(' ', depth).AppendLine("{");

        depth += 2;

        sb.Append(' ', depth).AppendLine($"ActiveMember: {ActiveMemberIndex}");

        if (ActiveMemberIndex != 0x7F)
        {
            sb.Append(' ', depth).Append($"(0x{LabelToTag("VALU"):X8} VALU) = ");

            foreach (var prop in GetType().GetProperties())
            {
                var attr = prop.GetCustomAttribute<TdfUnionFieldAttribute>();
                if (attr is null || attr.Index != ActiveMemberIndex)
                    continue;

                sb.AppendLine((prop.GetValue(this) as Tdf)?.ToString(depth));
            }
        }

        depth -= 2;

        return sb.Append(' ', depth).Append('}').ToString();
    }
}

public abstract class TdfUnionT<E> : TdfUnion where E : Enum
{
    public E ActiveMember 
    {
        get
        {
            return (E)Enum.ToObject(typeof(E), ActiveMemberIndex);
        }
        set
        {
            ActiveMemberIndex = (uint)Convert.ChangeType(value, typeof(E))!;
        }
    }

    public override string ToString(int depth)
    {
        var sb = new StringBuilder();
        sb.AppendLine(GetType().GetRealTypeName());
        sb.Append(' ', depth).AppendLine("{");

        depth += 2;

        sb.Append(' ', depth).AppendLine($"ActiveMemberIndex: {ActiveMemberIndex}");
        sb.Append(' ', depth).AppendLine($"ActiveMember: {ActiveMember}");

        if (ActiveMemberIndex != 0x7F)
        {
            sb.Append(' ', depth).Append($"(0x{LabelToTag("VALU"):X8} VALU) = ");

            foreach (var prop in GetType().GetProperties())
            {
                var attr = prop.GetCustomAttribute<TdfUnionFieldAttribute>();
                if (attr is null || attr.Index != ActiveMemberIndex)
                    continue;

                sb.AppendLine((prop.GetValue(this) as Tdf)?.ToString(depth));
            }
        }

        depth -= 2;

        sb.Append(' ', depth).Append('}');

        return sb.ToString();
    }
}

public class TimeValue
{
    public long Time { get; set; }

    public TimeValue(long time) => Time = time;

    public override string ToString() => $"TimeValue({Time})";
}

public class TdfBlob
{
    public byte[] Data { get; private set; }
    public int Offset { get; private set; }
    public int Length { get; private set; }

    public TdfBlob()
    {
        Data = Array.Empty<byte>();
        Offset = 0;
        Length = 0;
    }

    public TdfBlob(byte[] data)
        : this(data, 0, data.Length)
    {
    }

    public TdfBlob(byte[] data, int offset, int length)
    {
        Data = data;
        Offset = offset;
        Length = length;
    }

    public void Setup(byte[] data) => Setup(data, 0, data.Length);
    public void Setup(byte[] data, int offset, int length)
    {
        Data = data;
        Offset = offset;
        Length = length;
    }

    public override string ToString() => $"TdfBlob(\"{BitConverter.ToString(Data, Offset, Length)}\", {Length})";
}
