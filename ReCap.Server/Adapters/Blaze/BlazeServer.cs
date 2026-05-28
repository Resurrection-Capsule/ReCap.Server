using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Tls;
using ReCap.Server.Adapters.Blaze.Ssl;
using ReCap.Server.Adapters.Blaze.Component;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Config;
using ReCap.Server.Services;

namespace ReCap.Server.Adapters.Blaze;

public class BlazeServer
{
    private CancellationTokenSource CancellationTokenSource { get; set; }
    private TcpListener Listener { get; }
    private List<Client> Clients { get; } = [];
    private Dictionary<ushort, IComponent> Components { get; } = [];

    public Rc4TlsCrypto? Crypto { get; }
    public AsymmetricKeyParameter? Key { get; }
    public Certificate? Cert { get; }

    [MemberNotNullWhen(true, nameof(Crypto), nameof(Key), nameof(Cert))]
    public bool IsSecure { get; }
    public string Name { get; }
    public string HostName { get; }
    public int Port { get; }
    public bool Running { get; private set; }

    public BlazeServer(SqliteConfig newSqliteConfig, string name, IPAddress hostAddress, int port, bool isSecure, string hostname, GameService? sharedGameService = null)
    {
        Name = name;
        IsSecure = isSecure;
        HostName = hostname;
        Port = port;

        CancellationTokenSource = new();

        Listener = new TcpListener(hostAddress, port);

        if (IsSecure)
        {
            Crypto = new Rc4TlsCrypto();
            (Key, Cert) = PrepareCertificate(HostName);
        }

        if (isSecure) {
            AttachComponent(new RedirectorComponent{
                HostName = HostName,
                Ip = 0,
                Port = 42125
            });
        }
        else {
            var gameService = sharedGameService ?? new GameService();
            var gameManagerComponent = new GameManagerComponent(newSqliteConfig);
            gameManagerComponent.GameHandler = gameService;

            List<IComponent> components = new List<IComponent> {
                new AssociationListsComponent(),
                new AuthenticationComponent(newSqliteConfig),
                gameManagerComponent,
                new MessagingComponent(),
                new PlaygroupsComponent(),
                new RoomsComponent(),
                new UserSessionsComponent(newSqliteConfig),
                new UtilComponent(),
                new GameReportingComponent(),
                new UnknownComponent1()
            };
            foreach (var component in components) {
                AttachComponent(component);
            }
        }
    }

    public void Start()
    {
        Running = true;

        Task.Run(Run);
    }

    public string GetComponentAndCommandName(ushort componentId, ushort commandId, bool isNotification)
    {
        if (!Components.ContainsKey(componentId))
            return $"(0x{componentId:X} -> 0x{commandId:X})";

        var component = Components[componentId];

        return $"({component.GetType().Name} 0x{componentId:X} -> {(isNotification ? component.GetNotificationName(commandId) : component.GetCommandName(commandId))} 0x{commandId:X})";
    }

    public void AttachComponent(IComponent component)
    {
        if (Components.ContainsKey(component.Id))
            throw new Exception($"{Name} blaze server for \"{HostName}\" already has a component for id 0x{component.Id:X}!");

        Components.Add(component.Id, component);

        component.Server = this;
    }

    public T? GetComponent<T>(ushort id) where T : class, IComponent
    {
        if (!Components.ContainsKey(id))
            return null;

        return Components[id] as T;
    }

    public T GetComponentOrThrow<T>(ushort id) where T : class, IComponent
    {
        var component = GetComponent<T>(id);
        if (component is null)
            throw new Exception($"Component 0x{id:X} was not registered to the server!");

        return component;
    }

    public void HandlePacket(Client client, Packet packet)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(packet);

        if (Components.TryGetValue(packet.Component, out var component))
        {
            if (!component.HandlePacket(client, packet))
                Log($"Component {component.GetType().Name} wasn't able to handle command 0x{packet.Command:X}");
        }
        else
            Log($"Unknown component: 0x{packet.Component:X}");
    }

    public Client? FindClientByUserId(ulong userId)
    {
        return Clients.FirstOrDefault(c => c.UserId == userId);
    }

    public void Disconnect(Client client)
    {
        Clients.Remove(client);
    }

    private async void Run()
    {
        Log($"Started listening on 127.0.0.1:{Port}!");

        Listener.Start();

        while (Running)
        {
            try
            {
                var client = await Listener.AcceptTcpClientAsync(CancellationTokenSource.Token);

                Log($"Client {client.Client.RemoteEndPoint} accepted!");

                Clients.Add(new(this, client));
            }
            catch (OperationCanceledException)
            {
                Log("Cancellation handled in Run()!");
            }
        }
    }

    public void Stop()
    {
        Log($"Stopping...");

        Running = false;
        CancellationTokenSource.Cancel();
    }

    private void Log(string message) => ReCap.Server.Util.Logging.Log.Blaze.Info($"[{Name}] {message}");

    // TODO: generate for localhost and override the client's URL to localhost?
    // TODO: disable SSL completely for redirector already?
    private static (AsymmetricKeyParameter, Certificate) PrepareCertificate(string hostName)
    {
        var IssuerDN = "CN=GOS 2011 Certificate Authority, C=US, ST=California, L=Redwood City, O=\"Electronic Arts, Inc.\", OU=Global Online Studio/emailAddress=GOSDirtysockSupport@ea.com";
        var SubjectDN = $"C=US, ST=California, O=\"Electronic Arts, Inc.\", OU=Global Online Studio/emailAddress=GOSDirtysockSupport@ea.com, CN={hostName}";

        return new CertGenerator().GenerateVulnerableCert(IssuerDN, SubjectDN);
    }
}
