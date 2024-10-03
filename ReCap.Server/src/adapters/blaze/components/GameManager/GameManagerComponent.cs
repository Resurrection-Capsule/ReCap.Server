namespace ReCap.Server.Adapters.Blaze.Component.GameManager;

using HttpServer;
using BlazeServer;
using ReCap.Server.Adapters.Blaze.Component.Util;
using LoggerUtil;

public class GameManagerComponent : IComponent
{
    public static uint CurrentUnixTime => (uint)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

    public ushort Id => 4;
    public Server? Server { get; set; }
    public IGameHandler? GameHandler { get; set; }

    private GameService gameService;

    public GameManagerComponent(SqliteConfig newSqliteConfig) {
        gameService = new GameService();
    }

    public bool HandlePacket(Client client, Packet packet)
    {
        switch (packet.Command)
        {
            case 0x0F:
                return HandleFinalizeGameCreationPacket(client, packet);

            case 0x19:
                return HandleResetDedicatedServer(client, packet);

            case 0x1D:
                return HandleUpdateMeshConnection(client, packet);

            default:
                Log($"Unknown command: {packet.Command}");
                return false;
        }
    }

    private static bool HandleFinalizeGameCreationPacket(Client client, Packet packet)
    {
        var request = packet.ReadContent<UpdateGameSessionRequest>();

        client.RespondTo(packet);
        return true;
    }

    private bool HandleResetDedicatedServer(Client client, Packet packet)
    {
        var request = packet.ReadContent<CreateGameRequest>();

        var game = GameHandler?.CreateGame();
        if (game is null)
        {
            client.RespondTo(packet, error: 0x6C0004);
            return true;
        }

        game.SetupPlayer(1, 0);
        game.SetupBot(1);
        game.SetupBot(2);
        game.SetupBot(3);
        game.SetupBot(4);
        game.SetupBot(5);
        game.SetupBot(6);
        game.SetupBot(7);
        game.SetupBot(8);
        game.SetupBot(9);

        // TODO: AddPlayerToGame
        //gameService.AddPlayerToGame(game.Id, 1);

        client.RespondTo(packet, new JoinGameResponse() { GameId = game.Id, JoinState = JoinState.JoinedGame });

        var notify = new NotifyGameSetup();

        notify.GameData.AdminPlayerList = request.AdminPlayerList;
        notify.GameData.EntryCriteriaMap = request.EntryCriteriaMap;
        notify.GameData.GameAttribs = request.GameAttribs;
        notify.GameData.GameId = game.Id;
        notify.GameData.GameName = request.GameName;
        notify.GameData.GameProtocolVersionHash = 1;
        notify.GameData.GameReportingId = game.Id;
        notify.GameData.GameSettings = request.GameSettings;
        notify.GameData.GameState = GameState.NewState;
        notify.GameData.GameStatusURL = request.GameStatusURL;
        notify.GameData.GameTypeName = request.GameTypeName;
        //notify.GameData.HostNetworkAddressList = request.HostNetworkAddressList;
        notify.GameData.IgnoreEntryCriteriaWithInvite = request.IgnoreEntryCriteriaWithInvite;
        notify.GameData.MeshAttribs = request.MeshAttribs;
        notify.GameData.ServerNotResetable = request.ServerNotResetable;
        notify.GameData.NetworkTopology = request.NetworkTopology;
        notify.GameData.SlotCapacities = request.SlotCapacities;
        notify.GameData.PlaygroupId = request.PlaygroupId;
        notify.GameData.PlaygroupIdSecret = request.PlaygroupIdSecret;
        notify.GameData.MaxPlayerCapacity = request.MaxPlayerCapacity;
        notify.GameData.PresenceMode = request.PresenceMode;
        notify.GameData.QueueCapacity = request.QueueCapacity;
        notify.GameData.TeamCapacity = request.TeamCapacity;
        notify.GameData.TeamIds = request.TeamIds;
        notify.GameData.VoipNetwork = request.VoipNetwork;
        notify.GameData.VersionString = request.VersionString;
        notify.GameData.PlatformHostInfo.PlayerId = 1;
        notify.GameData.PlatformHostInfo.SlotId = 1;
        notify.GameData.TopologyHostInfo.PlayerId = 1;
        notify.GameData.TopologyHostInfo.SlotId = 0;
        notify.GameData.TopologyHostSessionId = 13666;
        notify.GameData.UUID = "71bc4bdb-82ec-494d-8d75-ca5123b827ac";

        notify.GameData.GameAttribs.Add("ServerBuildVersion", "1.0.903.854");

        if (!notify.GameData.GameAttribs.ContainsKey("GameOwnerId"))
            notify.GameData.GameAttribs.Add("GameOwnerId", "1");

        //if (!notify.GameData.GameAttribs.ContainsKey("GameType"))
        //    notify.GameData.GameAttribs.Add("GameType", "");

        if (!notify.GameData.GameAttribs.ContainsKey("GameOwnerName"))
            notify.GameData.GameAttribs.Add("GameOwnerName", "HelloDarkspore");

        if (!notify.GameData.GameAttribs.ContainsKey("GameFlags"))
            notify.GameData.GameAttribs.Add("GameFlags", "0");

        if (!notify.GameData.GameAttribs.ContainsKey("ExpectedPlayerCount"))
            notify.GameData.GameAttribs.Add("ExpectedPlayerCount", "10");

        if (!notify.GameData.GameAttribs.ContainsKey("PrivateMatch"))
            notify.GameData.GameAttribs.Add("PrivateMatch", "0");

        //if (!notify.GameData.GameAttribs.ContainsKey("LevelId"))
        //    notify.GameData.GameAttribs.Add("LevelId", "0");

        //if (!notify.GameData.GameAttribs.ContainsKey("TeamRostersKey"))
        //    notify.GameData.GameAttribs.Add("TeamRostersKey", "");

        //if (!notify.GameData.GameAttribs.ContainsKey("planet"))
        //    notify.GameData.GameAttribs.Add("planet", "");

        //if (!notify.GameData.GameAttribs.ContainsKey("planetDifficulty"))
        //    notify.GameData.GameAttribs.Add("planetDifficulty", "");

        //if (!notify.GameData.GameAttribs.ContainsKey("RosterLockedKey"))
        //    notify.GameData.GameAttribs.Add("RosterLockedKey", "");

        var addr = new NetworkAddress()
        {
            ActiveMember = NetworkAddressMember.IpPairAddress,
        };

        addr.IpPairAddress.InternalAddress.Ip = 0x7F000001;
        addr.IpPairAddress.InternalAddress.Port = 42000;
        addr.IpPairAddress.ExternalAddress.Ip = 0x7F000001;
        addr.IpPairAddress.ExternalAddress.Port = 42000;

        notify.GameData.HostNetworkAddressList.Add(addr);

        if (!notify.GameData.AdminPlayerList.Contains(1))
            notify.GameData.AdminPlayerList.Add(1);

        var player = new ReplicatedGamePlayer
        {
            SlotId = 0,
            SlotType = SlotType.Public,
            GameId = game.Id,
            AccountLocale = 0x656E5553,
            PlayerName = "HelloDarkspore",
            PlayerId = 1,
            JoinedGameTimestamp = CurrentUnixTime,
            PlayerState = PlayerState.ActiveConnected,
            TeamIndex = 0xFFFF,
            PlayerSessionId = 1,
        };

        notify.GameRoster.Add(player);

        notify.GameSetupReason.ActiveMember = GameSetupReasonMember.DatalessSetupContext;
        notify.GameSetupReason.DatalessSetupContext.SetupContext = DatalessContext.CreateGameSetupContext;

        client.Notify(notify, Id, 0x14);

        client.Notify(new NotifyGameStateChange() { GameId = game.Id, GameState = GameState.Initializing }, Id, 0x64);
        return true;
    }

    private bool HandleUpdateMeshConnection(Client client, Packet packet)
    {
        var request = packet.ReadContent<UpdateMeshConnectionRequest>();

        Log($"UpdateMeshConnection: {request}");

        return true;
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            0x01 => "createGame",
            0x02 => "destroyGame",
            0x03 => "advanceGameState",
            0x04 => "setGameSettings",
            0x05 => "setPlayerCapacity",
            0x06 => "setPresenceMode",
            0x07 => "setGameAttributes",
            0x08 => "setPlayerAttributes",
            0x09 => "joinGame",
            0x0B => "removePlayer",
            0x0D => "startMatchmaking",
            0x0E => "cancelMatchmaking",
            0x0F => "finalizeGameCreation",
            0x12 => "setPlayerCustomData",
            0x13 => "replayGame",
            0x14 => "returnDedicatedServerToPool",
            0x15 => "joinGameByGroup",
            0x16 => "leaveGameByGroup",
            0x17 => "migrateGame",
            0x18 => "updateGameHostMigrationStatus",
            0x19 => "resetDedicatedServer",
            0x1A => "updateGameSession",
            0x1B => "banPlayer",
            0x1D => "updateMeshConnection",
            0x1F => "removePlayerFromBannedList",
            0x20 => "clearBannedList",
            0x21 => "getBannedList",
            0x26 => "addQueuedPlayerToGame",
            0x27 => "updateGameName",
            0x28 => "ejectHost",
            0x64 => "getGameListSnapshot",
            0x65 => "getGameListSubscription",
            0x66 => "destroyGameList",
            0x67 => "getFullGameData",
            0x68 => "getMatchmakingConfig",
            0x69 => "getGameDataFromId",
            0x6A => "addAdminPlayer",
            0x6B => "removeAdminPlayer",
            0x6C => "setPlayerTeam",
            0x6D => "changeGameTeamId",
            0x6E => "migrateAdminPlayer",
            0x6F => "getUserSetGameListSubscription",
            0x70 => "swapPlayersTeam",
            0x96 => "registerDynamicDedicatedServerCreator",
            0x97 => "unregisterDynamicDedicatedServerCreator",
            0x98 => "getGameListSnapshotSync",
            _ => "<unknown>"
        };
    }

    public string GetNotificationName(ushort id)
    {
        return id switch
        {
            0x0A => "NotifyMatchmakingFailed",
            0x0C => "NotifyMatchmakingAsyncStatus",
            0x0F => "NotifyGameCreated",
            0x10 => "NotifyGameRemoved",
            0x14 => "NotifyGameSetup",
            0x15 => "NotifyPlayerJoining",
            0x16 => "NotifyJoiningPlayerInitiateConnections",
            0x17 => "NotifyPlayerJoiningQueue",
            0x18 => "NotifyPlayerPromotedFromQueue",
            0x19 => "NotifyPlayerClaimingReservation",
            0x1E => "NotifyPlayerJoinCompleted",
            0x28 => "NotifyPlayerRemoved",
            0x3C => "NotifyHostMigrationFinished",
            0x46 => "NotifyHostMigrationStart",
            0x47 => "NotifyPlatformHostInitialized",
            0x50 => "NotifyGameAttribChange",
            0x5A => "NotifyPlayerAttribChange",
            0x5F => "NotifyPlayerCustomDataChange",
            0x64 => "NotifyGameStateChange",
            0x6E => "NotifyGameSettingsChange",
            0x6F => "NotifyGameCapacityChange",
            0x70 => "NotifyGameReset",
            0x71 => "NotifyGameReportingIdChange",
            0x73 => "NotifyGameSessionUpdated",
            0x74 => "NotifyGamePlayerStateChange",
            0x75 => "NotifyGamePlayerTeamChange",
            0x76 => "NotifyGameTeamIdChange",
            0x77 => "NotifyProcessQueue",
            0x78 => "NotifyPresenceModeChanged",
            0x79 => "NotifyQueueChanged",
            0x7A => "NotifyGameRecreateRequested",
            0xC9 => "NotifyGameListUpdate",
            0xCA => "NotifyAdminListChange",
            0xDC => "NotifyCreateDynamicDedicatedServerGame",
            0xE6 => "NotifyGameNameChange",
            _ => "<unknown>"
        };
    }

    private static void Log(string message) => Logger.debug($"[Game Manager component]: {message}");
}

public enum GameEntryType
{
    Direct = 0,
    MakeReservation = 1,
    ClaimReservation = 2
}

public enum GameNetworkTopology
{
    ClientServerPeerHosted = 0,
    ClientServerDedicated = 1,
    PeerToPeerFullMesh = 0x82,
    PeerToPeerPartialMesh = 0x83,
    PeerToPeerDirtycastFailover = 0x84
}

public enum PresenceMode
{
    None = 0,
    Standard = 1,
    Private = 2
}

public enum SlotType
{
    Public = 0,
    Private = 1
}

public enum VoipTopology
{
    Disabled = 0,
    DedicatedServer = 1,
    PeerToPeer = 2
}

public class CreateGameRequest : Tdf
{
    [TdfField("ADMN")]
    public TdfPrimitiveVector<ulong> AdminPlayerList { get; } = [];

    [TdfField("ATTR")]
    public TdfPrimitiveMap<string, string> GameAttribs { get; } = [];

    [TdfField("BTPL")]
    public BlazeObjectId BTPL { get; } = new();

    [TdfField("CRIT")]
    public TdfPrimitiveMap<string, string> EntryCriteriaMap { get; } = [];

    [TdfField("GCTR", "")]
    public string GCTR { get; set; } = string.Empty;

    [TdfField("GENT", GameEntryType.Direct)]
    public GameEntryType GameEntryType { get; set; } = GameEntryType.Direct;

    [TdfField("GNAM", "")]
    public string GameName { get; set; } = string.Empty;

    [TdfField("GSET", 0)]
    public uint GameSettings { get; set; }

    [TdfField("GTYP", "")]
    public string GameTypeName { get; set; } = string.Empty;

    [TdfField("GURL", "")]
    public string GameStatusURL { get; set; } = string.Empty;

    [TdfField("HNET")]
    public TdfStructVector<NetworkAddress> HostNetworkAddressList { get; } = [];

    [TdfField("IGNO", false)]
    public bool IgnoreEntryCriteriaWithInvite { get; set; }

    [TdfField("MATR")]
    public TdfPrimitiveMap<string, string> MeshAttribs { get; } = [];

    [TdfField("NRES", false)]
    public bool ServerNotResetable { get; set; }

    [TdfField("NTOP", GameNetworkTopology.ClientServerPeerHosted)]
    public GameNetworkTopology NetworkTopology { get; set; } = GameNetworkTopology.ClientServerPeerHosted;

    [TdfField("PATT")]
    public TdfPrimitiveMap<string, string> HostPlayerAttribs { get; } = [];

    [TdfField("PCAP")]
    public TdfPrimitiveVector<ushort> SlotCapacities { get; } = [];

    [TdfField("PGID", "")]
    public string PlaygroupId { get; set; } = string.Empty;

    [TdfField("PGSC")]
    public TdfBlob PlaygroupIdSecret { get; } = new();

    [TdfField("PMAX", 0)]
    public ushort MaxPlayerCapacity { get; set; }

    [TdfField("PRES", PresenceMode.Standard)]
    public PresenceMode PresenceMode { get; set; } = PresenceMode.Standard;

    [TdfField("QCAP", 0)]
    public ushort QueueCapacity { get; set; }

    [TdfField("RGID", 0)]
    public ulong ReservedDynamicDSGameId { get; set; }

    [TdfField("SEAT")]
    public TdfPrimitiveVector<ulong> ReservedPlayerSeats { get; } = [];

    [TdfField("SIDL")]
    public TdfPrimitiveVector<ulong> SessionIdList { get; } = [];

    [TdfField("SLOT", SlotType.Public)]
    public SlotType JoiningSlotType { get; set; } = SlotType.Public;

    [TdfField("TCAP", 0)]
    public ushort TeamCapacity { get; set; }

    [TdfField("TIDS")]
    public TdfPrimitiveVector<ushort> TeamIds { get; } = [];

    [TdfField("TIDX", 0)]
    public ushort JoiningTeamIndex { get; set; }

    [TdfField("VOIP", VoipTopology.Disabled)]
    public VoipTopology VoipNetwork { get; set; } = VoipTopology.Disabled;

    [TdfField("VSTR", "")]
    public string VersionString { get; set; } = string.Empty;
}

public enum JoinState
{
    JoinedGame = 0,
    InQueue = 1,
    GroupPartiallyJoined = 2
}

public class JoinGameResponse : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("JGS", JoinState.JoinedGame)]
    public JoinState JoinState { get; set; } = JoinState.JoinedGame;
}

public class NotifyGameSetup : Tdf
{
    [TdfField("GAME")]
    public ReplicatedGameData GameData { get; } = new();

    [TdfField("PROS")]
    public TdfStructVector<ReplicatedGamePlayer> GameRoster { get; } = [];

    [TdfField("QUEU")]
    public TdfStructVector<ReplicatedGamePlayer> GameQueue { get; } = [];

    [TdfField("REAS")]
    public GameSetupReason GameSetupReason { get; } = new();
}

public enum GameState
{
    NewState = 0,
    Initializing = 1,
    InactiveVirtual = 2,
    PreGame = 0x82,
    InGame = 0x83,
    PostGame = 4,
    Migrating = 5,
    Destructing = 6,
    Resetable = 7,
    ReplaySetup = 8
}

public class ReplicatedGameData : Tdf
{
    [TdfField("ADMN")]
    public TdfPrimitiveVector<ulong> AdminPlayerList { get; set; } = [];

    [TdfField("ATTR")]
    public TdfPrimitiveMap<string, string> GameAttribs { get; set; } = [];

    [TdfField("CAP")]
    public TdfPrimitiveVector<ushort> SlotCapacities { get; set; } = [];

    [TdfField("CRIT")]
    public TdfPrimitiveMap<string, string> EntryCriteriaMap { get; set; } = [];

    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("GNAM", "")]
    public string GameName { get; set; } = string.Empty;

    [TdfField("GPVH", 0)]
    public ulong GameProtocolVersionHash { get; set; }

    [TdfField("GSET", 0)]
    public uint GameSettings { get; set; }

    [TdfField("GSID", 0)]
    public ulong GameReportingId { get; set; }

    [TdfField("GSTA", GameState.NewState)]
    public GameState GameState { get; set; } = GameState.NewState;

    [TdfField("GTYP", "")]
    public string GameTypeName { get; set; } = string.Empty;

    [TdfField("GURL", "")]
    public string GameStatusURL { get; set; } = string.Empty;

    [TdfField("HNET")]
    public TdfStructVector<NetworkAddress> HostNetworkAddressList { get; set; } = [];

    [TdfField("HSES", 0)]
    public ulong TopologyHostSessionId { get; set; }

    [TdfField("IGNO", false)]
    public bool IgnoreEntryCriteriaWithInvite { get; set; }

    [TdfField("MATR")]
    public TdfPrimitiveMap<string, string> MeshAttribs { get; set; } = [];

    [TdfField("MCAP", 0)]
    public ushort MaxPlayerCapacity { get; set; }

    [TdfField("NQOS")]
    public NetworkQosData NetworkQosData { get; } = new();

    [TdfField("NRES", false)]
    public bool ServerNotResetable { get; set; }

    [TdfField("NTOP", GameNetworkTopology.ClientServerPeerHosted)]
    public GameNetworkTopology NetworkTopology { get; set; } = GameNetworkTopology.ClientServerPeerHosted;

    [TdfField("PGID", "")]
    public string PlaygroupId { get; set; } = string.Empty;

    [TdfField("PGSR")]
    public TdfBlob PlaygroupIdSecret { get; set; } = new();

    [TdfField("PHST")]
    public HostInfo PlatformHostInfo { get; } = new();

    [TdfField("PRES", PresenceMode.None)]
    public PresenceMode PresenceMode { get; set; } = PresenceMode.None;

    [TdfField("PSAS", "")]
    public string PingSiteAlias { get; set; } = string.Empty;

    [TdfField("QCAP", 0)]
    public ushort QueueCapacity { get; set; }

    [TdfField("SEED", 0)]
    public uint SharedSeed { get; set; }

    [TdfField("TCAP", 0)]
    public ushort TeamCapacity { get; set; }

    [TdfField("THST")]
    public HostInfo TopologyHostInfo { get; } = new();

    [TdfField("TIDS")]
    public TdfPrimitiveVector<ushort> TeamIds { get; set; } = [];

    [TdfField("UUID", "")]
    public string UUID { get; set; } = string.Empty;

    [TdfField("VOIP", VoipTopology.Disabled)]
    public VoipTopology VoipNetwork { get; set; } = VoipTopology.Disabled;

    [TdfField("VSTR", "")]
    public string VersionString { get; set; } = string.Empty;

    [TdfField("XNNC")]
    public TdfBlob XnetNonce { get; } = new();

    [TdfField("XSES")]
    public TdfBlob XnetSession { get; } = new();
}

public class HostInfo : Tdf
{
    [TdfField("HPID", 0)]
    public ulong PlayerId { get; set; }

    [TdfField("HSLT", 0)]
    public byte SlotId { get; set; }
}

public enum PlayerState
{
    Reserved = 0,
    Queued = 1,
    ActiveConnecting = 2,
    ActiveMigrating = 3,
    ActiveConnected = 4,
    ActiveKickPending = 5
}

public class ReplicatedGamePlayer : Tdf
{
    [TdfField("BLOB")]
    public TdfBlob CustomData { get; } = new();

    [TdfField("EXID", 0)]
    public ulong ExternalId { get; set; }

    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("LOC", 0)]
    public uint AccountLocale { get; set; }

    [TdfField("NAME", "")]
    public string PlayerName { get; set; } = string.Empty;

    [TdfField("PATT")]
    public TdfPrimitiveMap<string, string> PlayerAttribs { get; } = [];

    [TdfField("PID", 0)]
    public ulong PlayerId { get; set; }

    [TdfField("PNET")]
    public NetworkAddress NetworkAddress { get; } = new();

    [TdfField("SID", 0xFF)]
    public byte SlotId { get; set; }

    [TdfField("SLOT", SlotType.Public)]
    public SlotType SlotType { get; set; }

    [TdfField("STAT", PlayerState.Reserved)]
    public PlayerState PlayerState { get; set; } = PlayerState.Reserved;

    [TdfField("TIDX", 0xFFFF)]
    public ushort TeamIndex { get; set; }

    [TdfField("TIME", 0)]
    public long JoinedGameTimestamp { get; set; }

    [TdfField("UGID")]
    public BlazeObjectId UserGroupId { get; } = new();

    [TdfField("UID", 0)]
    public ulong PlayerSessionId { get; set; }
}

public enum GameSetupReasonMember : uint
{
    DatalessSetupContext = 0,
    ResetDedicatedServerSetupContext = 1,
    IndirectJoinGameSetupContext = 2,
    MatchmakingSetupContext = 3,
    IndirectMatchmakingSetupContext = 4
}

public class GameSetupReason : TdfUnionT<GameSetupReasonMember>
{
    [TdfUnionField(0)]
    public DatalessSetupContext DatalessSetupContext { get; } = new();

    [TdfUnionField(1)]
    public ResetDedicatedServerSetupContext ResetDedicatedServerSetupContext { get; } = new();

    [TdfUnionField(2)]
    public IndirectJoinGameSetupContext IndirectJoinGameSetupContext { get; } = new();

    [TdfUnionField(3)]
    public MatchmakingSetupContext MatchmakingSetupContext { get; } = new();

    [TdfUnionField(4)]
    public IndirectMatchmakingSetupContext IndirectMatchmakingSetupContext { get; } = new();
}

public enum DatalessContext
{
    CreateGameSetupContext = 0,
    JoinGameSetupContext = 1,
    IndirectJoinGameFromQueueSetupContext = 2,
    IndirectJoinGameFromReservationContext = 3,
    HostInjectionSetupContext = 4
}

public class DatalessSetupContext : Tdf
{
    [TdfField("DCTX", DatalessContext.CreateGameSetupContext)]
    public DatalessContext SetupContext { get; set; } = DatalessContext.CreateGameSetupContext;
}

public class ResetDedicatedServerSetupContext : Tdf
{
    [TdfField("ERR", 0)]
    public uint JoinErr { get; set; }
}

public class IndirectJoinGameSetupContext : Tdf
{
    [TdfField("GRID")]
    public BlazeObjectId UserGroupId { get; } = new();

    [TdfField("RPVC", false)]
    public bool RequiresClientVersionCheck { get; set; }
}

public enum MatchmakingResult
{
    SuccessCreatedGame = 0,
    SuccessJoinedNewGame = 1,
    SuccessJoinedExistingGame = 2,
    SessionTimedOut = 3,
    SessionCancelled = 4,
    SessionTerminated = 5,
    SessionErrorGameSetupFailed = 6
}

public class MatchmakingSetupContext : Tdf
{
    [TdfField("FIT", 0)]
    public uint FitScore { get; set; }

    [TdfField("MAXF", 0)]
    public uint MaxPossibleFitScore { get; set; }

    [TdfField("MSID", 0)]
    public ulong SessionId { get; set; }

    [TdfField("RSLT", MatchmakingResult.SuccessCreatedGame)]
    public MatchmakingResult MatchmakingResult { get; set; } = MatchmakingResult.SuccessCreatedGame;

    [TdfField("USID", 0)]
    public ulong UserSessionId { get; set; }
}

public class IndirectMatchmakingSetupContext : Tdf
{
    [TdfField("", 0)]
    public uint FitScore { get; set; }

    [TdfField("")]
    public BlazeObjectId UserGroupId { get; } = new();

    [TdfField("", 0)]
    public uint MaxPosibleFitScore { get; set; }

    [TdfField("", 0)]
    public ulong SessionId { get; set; }

    [TdfField("", false)]
    public bool RequiresClientVersionCheck { get; set; }

    [TdfField("", MatchmakingResult.SuccessCreatedGame)]
    public MatchmakingResult MatchmakingResult { get; set; } = MatchmakingResult.SuccessCreatedGame;

    [TdfField("", 0)]
    public ulong UserSessionId { get; set; }
}

public class NotifyGameStateChange : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("GSTA", GameState.NewState)]
    public GameState GameState { get; set; } = GameState.NewState;
}

public class UpdateGameSessionRequest : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("XNNC")]
    public TdfBlob XnetNonce { get; } = new();

    [TdfField("XSES")]
    public TdfBlob XnetSession { get; } = new();
}

public class UpdateMeshConnectionRequest : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("TARG")]
    public TdfStructVector<PlayerConnectionStatus> Target { get; } = [];
}

public enum PlayerConnectionState
{
    Disconnected = 0,
    EstablishingConnection = 1,
    Connected = 2
}

public class PlayerConnectionStatus : Tdf
{
    [TdfField("FLGS", 0)]
    public uint Flags { get; set; }

    [TdfField("PID", 0)]
    public ulong PlayerId { get; set; }

    [TdfField("STAT", PlayerConnectionState.Disconnected)]
    public PlayerConnectionState PlayerConnectionState { get; set; } = PlayerConnectionState.Disconnected;
}

public class NotifyMatchmakingFailed : Tdf
{
    [TdfField("MAXF", 0)]
    public uint MaxPossibleFitScore { get; set; }

    [TdfField("MSID", 0)]
    public ulong SessionId { get; set; }

    [TdfField("RSLT", MatchmakingResult.SuccessCreatedGame)]
    public MatchmakingResult MatchmakingResult { get; set; } = MatchmakingResult.SuccessCreatedGame;

    [TdfField("USID", 0)]
    public ulong UserSessionId { get; set; }
}

public class MatchmakingAsyncStatus : Tdf
{
    // TODO when needed
}

public class NotifyMatchmakingAsyncStatus : Tdf
{
    [TdfField("ASIL")]
    public TdfStructVector<MatchmakingAsyncStatus> MatchmakingAsyncStatusList { get; } = [];

    [TdfField("MSID", 0)]
    public ulong SessionId { get; set; }

    [TdfField("USID", 0)]
    public ulong UserSessionId { get; set; }
}

public class NotifyGameCreated : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }
}

public enum GameDestructionReason
{
    SysGameEnding = 0,
    SysCreationFailed = 1,
    SysGameRecreate = 2,
    HostLeaving = 3,
    HostInjection = 4,
    HostEjection = 5,
    LocalPlayerLeaving = 6,
    TitleReasonBaseGameDestructionReason = 7
}

public class NotifyGameRemoved : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("REAS", GameDestructionReason.SysGameEnding)]
    public GameDestructionReason DestructionReason { get; set; } = GameDestructionReason.SysGameEnding;
}

public class NotifyPlayerJoining : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("PDAT")]
    public ReplicatedGamePlayer JoiningPlayer { get; set; } = new();
}

public class NotifyPlayerJoinCompleted : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("PID", 0)]
    public ulong PlayerId { get; set; }
}

public enum PlayerRemovedReason
{
    PlayerJoinTimeout = 0,
    PlayerConnLost = 1,
    BlazeserverConnLost = 2,
    MigrationFailed = 3,
    GameDestroyed = 4,
    GameEnded = 5,
    PlayerLeft = 6,
    GroupLeft = 7,
    PlayerKicked = 8,
    PlayerKickedWithBan = 9,
    PlayerJoinFromQueueFailed = 10,
    PlayerReservationTimeout = 11,
    HostEjected = 12
}

public class NotifyPlayerRemoved : Tdf
{
    [TdfField("CNTX", 0)]
    public ushort TitleContext { get; set; }

    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("PID", 0)]
    public ulong PlayerId { get; set; }

    [TdfField("REAS", PlayerRemovedReason.PlayerJoinTimeout)]
    public PlayerRemovedReason Reason { get; set; } = PlayerRemovedReason.PlayerJoinTimeout;
}

public class NotifyHostMigrationFinished : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }
}

public enum HostMigrationType
{
    TopologyHostMigration = 0,
    PlatformHostMigration = 1,
    TopologyPlatformHostMigration = 2
}

public class NotifyHostMigrationStart : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("HOST", 0)]
    public ulong NewHostId { get; set; }

    [TdfField("PMIG", HostMigrationType.TopologyHostMigration)]
    public HostMigrationType MigrationType { get; set; } = HostMigrationType.TopologyHostMigration;

    [TdfField("SLOT", 0)]
    public byte NewHostSlotId { get; set; }
}

public class NotifyPlatformHostInitialized : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("PHST", 0)]
    public byte PlatformHostSlotId { get; set; }
}

public class NotifyGameAttribChange : Tdf
{
    [TdfField("ATTR")]
    public TdfPrimitiveMap<string, string> GameAttribs { get; } = [];

    [TdfField("GID", 0)]
    public ulong GameId { get; set; }
}

public class NotifyPlayerAttribChange : Tdf
{
    [TdfField("ATTR")]
    public TdfPrimitiveMap<string, string> PlayerAttribs { get; } = [];

    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("PID", 0)]
    public ulong PlayerId { get; set; }
}

public class NotifyPlayerCustomDataChang : Tdf
{
    [TdfField("CDAT")]
    public TdfBlob CustomData { get; } = new();

    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("PID", 0)]
    public ulong PlayerId { get; set; }
}

public class NotifyGameSettingsChange : Tdf
{
    [TdfField("ATTR", 0)]
    public uint GameSettings { get; set; }

    [TdfField("GID", 0)]
    public ulong GameId { get; set; }
}

public class NotifyGameCapacityChange : Tdf
{
    [TdfField("CAP")]
    public TdfPrimitiveVector<ushort> SlotCapacities { get; set; } = [];

    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("TCAP", 0)]
    public ushort TeamCapacity { get; set; }
}

public class NotifyGameReset : Tdf
{
    [TdfField("DATA")]
    public ReplicatedGameData GameData { get; set; } = new();
}

public class NotifyGameReportingIdChange : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("GRID", 0)]
    public ulong GameReportingId { get; set; }
}

public class GameSessionUpdatedNotification : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("XNNC")]
    public TdfBlob XnetNonce { get; } = new();

    [TdfField("XSES")]
    public TdfBlob XnetSession { get; } = new();
}

public class NotifyGamePlayerStateChange : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("PID", 0)]
    public ulong PlayerId { get; set; }

    [TdfField("STAT", PlayerState.Reserved)]
    public PlayerState PlayerState { get; set; } = PlayerState.Reserved;
}

public class NotifyGamePlayerTeamChange : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("PID", 0)]
    public ulong PlayerId { get; set; }

    [TdfField("TIDX", 0)]
    public ushort TeamIndex { get; set; }
}

public class NotifyGameTeamIdChange : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("NTID", 0)]
    public ushort NewTeamId { get; set; }

    [TdfField("OTID", 0)]
    public ushort OldTeamId { get; set; }

    [TdfField("TIDX", 0)]
    public ushort TeamIndex { get; set; }
}

public class NotifyProcessQueue : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }
}

public class NotifyPresenceModeChanged : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("PRES", PresenceMode.None)]
    public PresenceMode PresenceMode { get; set; } = PresenceMode.None;
}

public class NotifyQueueChanged : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("PIDL")]
    public TdfPrimitiveVector<long> PlayerIdList { get; set; } = [];
}

public class NotifyGameRecreateRequested : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }
}

public class GameBrowserGameData : Tdf
{
    // TODO when needed
}

public class NotifyGameListUpdate : Tdf
{
    [TdfField("DONE", 0)]
    public byte IsFinalUpdate { get; set; }

    [TdfField("GLID", 0)]
    public ulong ListId { get; set; }

    [TdfField("REMV")]
    public TdfPrimitiveVector<uint> RemovedGameList { get; set; } = [];

    [TdfField("UPDT")]
    public TdfStructVector<GameBrowserGameData> UpdatedGames { get; set; } = [];
}

public enum UpdateAdminListOperation
{
    Added = 0,
    Removed = 1,
    Migrated = 2
}

public class NotifyAdminListChange : Tdf
{
    [TdfField("ALST", 0)]
    public ulong AdminPlayerId { get; set; }

    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("OPER", UpdateAdminListOperation.Added)]
    public UpdateAdminListOperation Operation { get; set; } = UpdateAdminListOperation.Added;

    [TdfField("UID", 0)]
    public ulong UpdaterPlayerId { get; set; }
}

public class NotifyCreateDynamicDedicatedServerGame : Tdf
{
    [TdfField("MID", "")]
    public string MachineId { get; set; } = string.Empty;

    [TdfField("GREQ")]
    public CreateGameRequest CreateGameRequest { get; set; } = new();
}

public class NotifyGameNameChange : Tdf
{
    [TdfField("GID", 0)]
    public ulong GameId { get; set; }

    [TdfField("GNAM", "")]
    public string GameName { get; set; } = string.Empty;
}
