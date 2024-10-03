namespace ReCap.RakNet;

using ReCap.RakNet.Packets;

public static class PacketActivator
{
    public static IRakNetPacket? CreateInstance(byte[] data)
    {
        var stream = new MemoryStream(data, false);

        var type = (PacketType)stream.ReadByte();

        IRakNetPacket? packet = null;

        switch (type)
        {
            case PacketType.HelloPlayerRequest:
                packet = new HelloPlayerRequestPacket();
                break;

            case PacketType.HelloPlayer:
                packet = new HelloPlayerPacket();
                break;

            case PacketType.ReconnectPlayer:
                break;

            case PacketType.Connected:
                packet = new ConnectedPacket();
                break;

            case PacketType.Goodbye:
                break;

            case PacketType.PlayerJoined:
                packet = new PlayerJoinedPacket();
                break;

            case PacketType.PartyMergeComplete:
                break;

            case PacketType.PlayerDeparted:
                break;

            case PacketType.VoteKickStarted:
                break;

            case PacketType.PlayerStatusUpdate:
                packet = new PlayerStatusUpdatePacket();
                break;

            case PacketType.GameAborted:
                break;

            case PacketType.GameState:
                break;

            case PacketType.DirectorState:
                break;

            case PacketType.ObjectCreate:
                packet = new ObjectCreatePacket();
                break;

            case PacketType.ObjectUpdate:
                packet = new ObjectUpdatePacket();
                break;

            case PacketType.ObjectDelete:
                break;

            case PacketType.ObjectJump:
                break;

            case PacketType.ObjectTeleport:
                break;

            case PacketType.ObjectPlayerMove:
                break;

            case PacketType.ForcePhysicsUpdate:
                break;

            case PacketType.PhysicsChanged:
                break;

            case PacketType.LocomotionDataUpdate:
                break;

            case PacketType.LocomotionDataUnreliableUpdate:
                break;

            case PacketType.AttributeDataUpdate:
                break;

            case PacketType.CombatantDataUpdate:
                break;

            case PacketType.InteractableDataUpdate:
                break;

            case PacketType.AgentBlackboardUpdate:
                break;

            case PacketType.LootDataUpdate:
                break;

            case PacketType.ServerEvent:
                break;

            case PacketType.ActionCommandMsgs:
                break;

            case PacketType.PlayerDamage:
                break;

            case PacketType.LootSpawned:
                break;

            case PacketType.LootAcquired:
                break;

            case PacketType.LabsPlayerUpdate:
                break;

            case PacketType.ModifierCreated:
                break;

            case PacketType.ModifierUpdated:
                break;

            case PacketType.ModifierDeleted:
                break;

            case PacketType.SetAnimationState:
                break;

            case PacketType.SetObjectGfxState:
                break;

            case PacketType.PlayerCharacterDeploy:
                break;

            case PacketType.ActionCommandResponse:
                break;

            case PacketType.ChainVoteMsgs:
                break;

            case PacketType.ChainLevelResultsMsgs:
                break;

            case PacketType.ChainCashOutMsgs:
                break;

            case PacketType.ChainPlayerMsgs:
                break;

            case PacketType.ChainGameMsgs:
                break;

            case PacketType.ChainGameOverMsgs:
                break;

            case PacketType.QuickGameMsgs:
                break;

            case PacketType.GamePrepareForStart:
                packet = new GamePrepareForStartPacket();
                break;

            case PacketType.GameStart:
                packet = new GameStartPacket();
                break;

            case PacketType.CheatMessageDontUseInReleaseButDontChangeTheIndexOfTheMessagesBelowInCaseWeAreRunningOnADevServer:
                break;

            case PacketType.ArenaPlayerMsgs:
                break;

            case PacketType.ArenaLobbyMsgs:
                break;

            case PacketType.ArenaGameMsgs:
                break;

            case PacketType.ArenaResultsMsgs:
                break;

            case PacketType.ObjectivesInitForLevel:
                break;

            case PacketType.ObjectiveUpdated:
                break;

            case PacketType.ObjectivesComplete:
                break;

            case PacketType.CombatEvent:
                break;

            case PacketType.JuggernautPlayerMsgs:
                break;

            case PacketType.JuggernautLobbyMsgs:
                break;

            case PacketType.JuggernautGameMsgs:
                break;

            case PacketType.JuggernautResultsMsgs:
                break;

            case PacketType.ReloadLevel:
                break;

            case PacketType.GravityForceUpdate:
                break;

            case PacketType.CooldownUpdate:
                break;

            case PacketType.CrystalDragMessage:
                break;

            case PacketType.CrystalMessage:
                break;

            case PacketType.KillRacePlayerMsgs:
                break;

            case PacketType.KillRaceLobbyMsgs:
                break;

            case PacketType.KillRaceGameMsgs:
                break;

            case PacketType.KillRaceResultsMsgs:
                break;

            case PacketType.TutorialGameMsgs:
                break;

            case PacketType.CinematicMsgs:
                break;

            case PacketType.ObjectiveAdd:
                break;

            case PacketType.LootDropMessage:
                break;

            case PacketType.DebugPing:
                break;

            default:
                Console.WriteLine($"PacketActivator: Unhandled packet type: {type}!");
                break;
        }

        if (packet is null)
            Console.WriteLine($"PacketActivator: Packet class has not been setup for type: {type}!");

        packet?.ReadFrom(stream);

        return packet;
    }
}
