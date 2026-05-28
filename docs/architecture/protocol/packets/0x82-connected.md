# 0x82 — Connected

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | 1 B (opcode only) | [05 RakNet Connect](../../flow/phases/05-raknet-connect.md) | ✅ |

Server's first packet to the client immediately after `ID_NEW_INCOMING_CONNECTION`. Empty body — signals "your session is bound, start the gameplay handshake."

---

## Body layout

**No payload.** Just the 1-byte opcode `0x82`.

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:1279-1286`:

```cpp
void Server::SendConnected(const ClientPtr& client) {
    // TODO: verify incoming connection

    BitStream outStream(8);
    outStream.Write(PacketID::Connected);

    Send(outStream, client);
}
```

Called from `Server::OnNewIncomingConnection` (`Server.cpp:593`) after `AddClient` + `client->SetGameState(Spaceship)`.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/ConnectedPacket.cs`:

```csharp
public class ConnectedPacket : IRakNetPacket
{
    public PacketType Type => PacketType.Connected;
    public void ReadFrom(Stream stream) { }
    public void WriteTo(Stream stream) { }
}
```

Sent from `RakNetServer.OnSessionOnNewIncomingConnection` (`RakNetServer.cs:62`):

```csharp
private void OnSessionOnNewIncomingConnection(RakNetSession session)
    => SendPacket(session, new ConnectedPacket());
```

---

## Open audit items

1. **C++ TODO.** "verify incoming connection" — the C++ side flags this as unfinished. Decide whether the C# side should add IP/GUID validation here.
2. **Latency-critical?** This is the first byte the client sees on the gameplay wire. Confirm no measurable round-trip cost from C# vs C++ (likely negligible since payload = 0).

---

## Related

- [Phase 05 RakNet Connect](../../flow/phases/05-raknet-connect.md) — exact call site
