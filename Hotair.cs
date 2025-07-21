using SK = SteamKit2;

var steamClient = new SK.SteamClient();
var steamUser = steamClient.GetHandler<SK.SteamUser>();
var manager = new SK.CallbackManager(steamClient);

var skCallbackConnected = SubscribeTo<SK.SteamClient.ConnectedCallback>();
var skCallbackDisconnected = SubscribeTo<SK.SteamClient.DisconnectedCallback>();
var skCallbackLoggedOn = SubscribeTo<SK.SteamUser.LoggedOnCallback>();
var skCallbackLoggedOff = SubscribeTo<SK.SteamUser.LoggedOffCallback>();

steamClient.Connect();
WaitCallback(skCallbackConnected);

var sessionFile = "~/.cache/hotair/steam-session.json";
SteamSession session;
try
{
    session = ReadJson<SteamSession>(sessionFile);
}
catch (System.Exception)
{
    session = new SteamSession();
}

if (session.RefreshToken == null)
{
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
    WriteJson<SteamSession>(sessionFile, session);
}

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

steamUser.LogOff();
WaitCallback(skCallbackLoggedOff);

steamClient.Disconnect();
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
    using System.IO.FileStream stream = System.IO.File.OpenRead(filePath);
    return System.Text.Json.JsonSerializer.Deserialize<T>(stream);
}

void WriteJson<T>(string filePath, T obj)
{
    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath));
    using System.IO.FileStream stream = System.IO.File.OpenWrite(filePath + ".tmp");
    System.Text.Json.JsonSerializer.Serialize<T>(
        stream,
        obj,
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
    );
    System.IO.File.Move(filePath + ".tmp", filePath);
}

class Box<T>
{
    public T Value;
}

class SteamSession
{
    public string Username;
    public string AccessToken;
    public string RefreshToken;
    public string GuardData;
}
