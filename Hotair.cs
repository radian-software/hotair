var steamClient = new SteamKit2.SteamClient();
var manager = new SteamKit2.CallbackManager( steamClient );

manager.Subscribe<SteamKit2.SteamClient.ConnectedCallback>( OnConnected );
manager.Subscribe<SteamKit2.SteamClient.DisconnectedCallback>( OnDisconnected );

steamClient.Connect();

var isRunning = true;
while (isRunning) {
    manager.RunWaitCallbacks(System.TimeSpan.FromSeconds(1));
}

void OnConnected(SteamKit2.SteamClient.ConnectedCallback callback) {
    System.Console.WriteLine("Connected");
    steamClient.Disconnect();
}

void OnDisconnected(SteamKit2.SteamClient.DisconnectedCallback callback) {
    System.Console.WriteLine("Disconnected");
    isRunning = false;
}
