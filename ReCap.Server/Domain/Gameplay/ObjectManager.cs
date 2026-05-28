using System.Numerics;
using AssetData.Parser.Model;
using ReCap.Server.Services;
using ReCap.Server.Services.Assets;

namespace ReCap.Server.Domain.Gameplay;

public sealed class GameObject
{
    public required uint ObjectId { get; init; }
    public required uint NounId { get; init; }
    public Vector3 Position { get; set; }
    public float Scale { get; set; } = 1f;
    public byte Team { get; set; }
    public bool PlayerControlled { get; set; }
    public float Health { get; set; }
    public float MaxHealth { get; set; }
    public AssetValue? AIDefinition { get; set; }
}

public sealed class ObjectManager
{
    private readonly AssetDatabase? _db;
    private readonly Dictionary<uint, GameObject> _objects = new();

    public IReadOnlyDictionary<uint, GameObject> Objects => _objects;

    public ObjectManager(AssetDatabase? db)
    {
        _db = db;
    }

    public GameObject Spawn(uint objectId, uint nounId, Vector3 position, float scale, byte team, bool playerControlled)
    {
        float maxHealth = 0f;
        AssetValue? aiDef = null;

        if (_db is not null)
        {
            var attrs = _db.ResolveClassAttributesForCreature(nounId);
            if (attrs is not null)
                maxHealth = attrs.FindByName("maxHealth").AsFloat();

            var noun = _db.GetNoun(nounId);
            if (noun is not null)
            {
                var aiRef = (noun.FindByName("aiDefinition") as StringValue)?.Value;
                if (!string.IsNullOrEmpty(aiRef))
                    aiDef = _db.GetAssetByName(aiRef);
            }
        }

        var obj = new GameObject
        {
            ObjectId = objectId,
            NounId = nounId,
            Position = position,
            Scale = scale,
            Team = team,
            PlayerControlled = playerControlled,
            Health = maxHealth,
            MaxHealth = maxHealth,
            AIDefinition = aiDef
        };
        _objects[objectId] = obj;
        return obj;
    }

    public bool Remove(uint objectId) => _objects.Remove(objectId);

    public void Update(double deltaSeconds)
    {
        if (_objects.Count == 0) return;
        // AI tick plumbing — behaviors land in a later phase.
    }
}
