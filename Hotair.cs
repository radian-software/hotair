using SK = SteamKit2;

System.Console.WriteLine("Hotair: Initializing...");

var steamClient = new SK.SteamClient();
var steamUser = steamClient.GetHandler<SK.SteamUser>();
var manager = new SK.CallbackManager(steamClient);

var skCallbackConnected = SubscribeTo<SK.SteamClient.ConnectedCallback>();
var skCallbackDisconnected = SubscribeTo<SK.SteamClient.DisconnectedCallback>();
var skCallbackLoggedOn = SubscribeTo<SK.SteamUser.LoggedOnCallback>();
var skCallbackLoggedOff = SubscribeTo<SK.SteamUser.LoggedOffCallback>();

System.Console.WriteLine("Hotair: Connecting to Steam...");

steamClient.Connect();
WaitCallback(skCallbackConnected);

System.Console.WriteLine("Hotair: Checking for session data on disk...");

var sessionFile = System.IO.Path.Join(
    System.Environment.GetEnvironmentVariable("HOME"),
    ".cache/hotair/steam-session.json"
);
SteamSession session;
try
{
    session = ReadJson<SteamSession>(sessionFile);
    System.Console.WriteLine("Hotair: Checking for session data on disk...found");
}
catch (System.Exception)
{
    session = new SteamSession();
    System.Console.WriteLine("Hotair: Checking for session data on disk...not found");
}

if (session.RefreshToken != null)
{
    System.Console.WriteLine("Hotair: Logging on to Steam using saved session data...");
    steamUser.LogOn(
        new SK.SteamUser.LogOnDetails
        {
            Username = session.Username,
            AccessToken = session.RefreshToken,
            ShouldRememberPassword = true,
        }
    );

    var logonResult = WaitCallback(skCallbackLoggedOn);
    if (logonResult.Result != SK.EResult.OK)
    {
        System.Console.WriteLine("Hotair: Logging on to Steam using saved session data...failed");
        session.RefreshToken = null;
        session.AccessToken = null;
    }
}

if (session.RefreshToken == null)
{
    System.Console.WriteLine("Hotair: Performing authentication...");
    var username = ReadString("Username: ");
    var password = ReadPassword("Password: ");
    var authSession = await steamClient.Authentication.BeginAuthSessionViaCredentialsAsync(
        new SK.Authentication.AuthSessionDetails
        {
            Username = username,
            Password = password,
            DeviceFriendlyName = "Hotair",
            IsPersistentSession = true,
            GuardData = session.GuardData,
            Authenticator = new SK.Authentication.UserConsoleAuthenticator(),
        }
    );
    var pollResponse = await authSession.PollingWaitForResultAsync();
    session.Username = username;
    session.AccessToken = pollResponse.AccessToken;
    session.RefreshToken = pollResponse.RefreshToken;
    if (pollResponse.NewGuardData != null)
    {
        session.GuardData = pollResponse.NewGuardData;
    }
    System.Console.WriteLine("Hotair: Writing new session data to disk...");
    WriteJson<SteamSession>(sessionFile, session);

    System.Console.WriteLine("Hotair: Logging on to Steam using new session data...");
    steamUser.LogOn(
        new SK.SteamUser.LogOnDetails
        {
            Username = session.Username,
            AccessToken = session.RefreshToken,
            ShouldRememberPassword = true,
        }
    );

    var logonResult = WaitCallback(skCallbackLoggedOn);
    if (logonResult.Result != SK.EResult.OK)
    {
        throw new System.Exception(
            System.String.Format(
                "Unable to logon to Steam: {0} / {1}",
                logonResult.Result,
                logonResult.ExtendedResult
            )
        );
    }
}

System.Console.WriteLine("Hotair: Initializing Steam web API...");
using dynamic playerService = SK.WebAPI.GetInterface("IPlayerService");

System.Console.WriteLine("Hotair: Retrieving list of owned games...");
SK.KeyValue ownedGames = playerService.GetOwnedGames(
    steamid: steamUser.SteamID,
    include_appinfo: true
);

System.Console.WriteLine("Hotair: Found {0} games", ownedGames["game_count"]);

System.Console.WriteLine("Hotair: Logging off from Steam...");
steamUser.LogOff();
WaitCallback(skCallbackDisconnected);

Box<T> SubscribeTo<T>()
    where T : SK.CallbackMsg
{
    var box = new Box<T>();
    manager.Subscribe<T>(
        (val) =>
        {
            box.Value = val;
        }
    );
    return box;
}

T WaitCallback<T>(Box<T> box)
    where T : SK.CallbackMsg
{
    while (box.Value == null)
    {
        manager.RunWaitCallbacks(System.TimeSpan.FromSeconds(1));
    }
    var val = box.Value;
    box.Value = null;
    return val;
}

string ReadString(string prompt)
{
    System.Console.Write(prompt);
    return System.Console.ReadLine();
}

string ReadPassword(string prompt)
{
    System.Console.Write(prompt);
    var password = new System.Text.StringBuilder();
    while (true)
    {
        var key = System.Console.ReadKey(true).KeyChar;
        switch (key)
        {
            case '\b': // DEL
                if (password.Length > 0)
                {
                    System.Console.Write("\b \b");
                    password.Remove(password.Length - 1, 1);
                }
                break;
            case '\r': // RET
                System.Console.WriteLine();
                return password.ToString();
            case '\x15': // C-u
                System.Console.Write(new string('\b', password.Length));
                System.Console.Write(new string(' ', password.Length));
                System.Console.Write(new string('\b', password.Length));
                password.Clear();
                break;
            default:
                if (!System.Char.IsControl(key))
                {
                    System.Console.Write("*");
                    password.Append(key);
                }
                break;
        }
    }
}

T ReadJson<T>(string filePath)
{
    using var stream = System.IO.File.OpenRead(filePath);
    return System.Text.Json.JsonSerializer.Deserialize<T>(stream);
}

void WriteJson<T>(string filePath, T obj)
{
    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath));
    using var stream = System.IO.File.OpenWrite(filePath + ".tmp");
    System.Text.Json.JsonSerializer.Serialize<T>(
        stream,
        obj,
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
    );
    stream.Close();
    System.IO.File.Move(filePath + ".tmp", filePath, true);
}

class Box<T>
{
    public T Value;
}

class SteamSession
{
    public string Username { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public string GuardData { get; set; }
}
