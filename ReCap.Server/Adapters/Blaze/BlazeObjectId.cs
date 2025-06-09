namespace ReCap.Server.Adapters.Blaze;

public class BlazeObjectType
{
    public ushort Component { get; set; }
    public ushort Type { get; set; }

    public BlazeObjectType()
    {
    }

    public BlazeObjectType(ushort component, ushort type)
    {
        Component = component;
        Type = type;
    }

    public override string ToString() => $"BlazeObjectType({Component}, {Type})";
}

public class BlazeObjectId
{
    public long Id { get; set; }
    public BlazeObjectType Type { get; set; }

    public BlazeObjectId()
    {
        Type = new();
    }

    public BlazeObjectId(long id, BlazeObjectType type)
    {
        Id = id;
        Type = type;
    }

    public override string ToString() => $"BlazeObjectId({Id}, Type: {Type})";
}
