using System.Linq;
using C = System.Collections.Generic;
using SK = SteamKit2;

// originally from steamkit but then tweaked
// https://github.com/SteamRE/SteamKit/blob/5c0143306dd4e56b0780cc006f822e12a42883a1/Resources/NetHookAnalyzer2/NetHookAnalyzer2/MessageTypeOverrides.cs#L15
var emsgOverrides = new C.Dictionary<SK.EMsg, System.Type>
{
    { SK.EMsg.ClientLogonGameServer, typeof(SK.Internal.CMsgClientLogon) },
    { SK.EMsg.ClientGamesPlayed, typeof(SK.Internal.CMsgClientGamesPlayed) },
    { SK.EMsg.ClientGamesPlayedNoDataBlob, typeof(SK.Internal.CMsgClientGamesPlayed) },
    { SK.EMsg.ClientGamesPlayedWithDataBlob, typeof(SK.Internal.CMsgClientGamesPlayed) },
    { SK.EMsg.ClientToGC, typeof(SK.Internal.CMsgGCClient) },
    { SK.EMsg.ClientFromGC, typeof(SK.Internal.CMsgGCClient) },
    { SK.EMsg.ClientFriendMsgIncoming, typeof(SK.Internal.CMsgClientFriendMsgIncoming) },
    { SK.EMsg.ClientFriendMsgEchoToSender, typeof(SK.Internal.CMsgClientFriendMsgIncoming) },
    { SK.EMsg.ClientCurrentUIMode, typeof(SK.Internal.CMsgClientUIMode) },
    {
        SK.EMsg.ClientGetNumberOfCurrentPlayersDP,
        typeof(SK.Internal.CMsgDPGetNumberOfCurrentPlayers)
    },
    {
        SK.EMsg.ClientGetNumberOfCurrentPlayersDPResponse,
        typeof(SK.Internal.CMsgDPGetNumberOfCurrentPlayersResponse)
    },
    { SK.EMsg.AMGameServerUpdate, typeof(SK.Internal.CMsgGameServerData) },
    { SK.EMsg.ClientDPUpdateAppJobReport, typeof(SK.WebUI.Internal.CMsgClientUpdateAppJobReport) },
    { SK.EMsg.ClientPlayingSessionState, typeof(SK.Internal.CMsgClientPlayingSessionState) },
    {
        SK.EMsg.ClientNetworkingCertRequestResponse,
        typeof(SK.Internal.CMsgClientNetworkingCertReply)
    },
    {
        SK.EMsg.ClientChatRequestOfflineMessageCount,
        typeof(SK.Internal.CMsgClientRequestOfflineMessageCount)
    },
    {
        SK.EMsg.ClientChatOfflineMessageNotification,
        typeof(SK.Internal.CMsgClientOfflineMessageNotification)
    },
};

{
    var msg = System.Environment.GetEnvironmentVariable("HOTAIR_MSG");
    if (msg != null)
    {
        DumpMsg(msg);
        return;
    }

    if (System.Environment.GetEnvironmentVariable("HOTAIR_DUMP_STDIN") == "1")
    {
        var idx = 0;
        while ((msg = System.Console.ReadLine()) != null)
        {
            var len = System.Convert.FromBase64String(msg).Length;
            System.Console.WriteLine();
            System.Console.WriteLine($"[[ BEGIN MSG {idx} ({len} bytes) ]]");
            System.Console.WriteLine();
            DumpMsg(msg);
            idx += 1;
        }
        return;
    }
}

uint chosenGame = 0;
try
{
    chosenGame = System.UInt32.Parse(System.Environment.GetEnvironmentVariable("HOTAIR_GAME"));
    System.Console.WriteLine($"Hotair: HOTAIR_GAME set, will launch game {chosenGame}");
}
catch
{
    System.Console.WriteLine("Hotair: No value for HOTAIR_GAME, will list games in library");
}

System.Console.WriteLine("Hotair: Initializing...");

var steamClient = new SK.SteamClient();
var steamUser = steamClient.GetHandler<SK.SteamUser>();
var steamApps = steamClient.GetHandler<SK.SteamApps>();
var manager = new SK.CallbackManager(steamClient);

var skCallbackConnected = SubscribeTo<SK.SteamClient.ConnectedCallback>();
var skCallbackDisconnected = SubscribeTo<SK.SteamClient.DisconnectedCallback>();
var skCallbackLoggedOn = SubscribeTo<SK.SteamUser.LoggedOnCallback>();
var skCallbackLoggedOff = SubscribeTo<SK.SteamUser.LoggedOffCallback>();
var skCallbackLicenseList = SubscribeTo<SK.SteamApps.LicenseListCallback>();
var skCallbackEmailAddrInfo = SubscribeTo<SK.SteamUser.EmailAddrInfoCallback>();
var skCallbackAccountInfo = SubscribeTo<SK.SteamUser.AccountInfoCallback>();

System.Console.WriteLine("Hotair: Initializing... done");

System.Console.WriteLine("Hotair: Connecting to Steam...");
steamClient.Connect();
WaitCallback(skCallbackConnected);
System.Console.WriteLine("Hotair: Connecting to Steam... done");

System.Console.WriteLine("Hotair: Checking for session data on disk...");

var sessionFile = System.IO.Path.Join(
    System.Environment.GetEnvironmentVariable("HOME"),
    ".cache/hotair/steam-session.json"
);
SteamSession session;
try
{
    session = ReadJson<SteamSession>(sessionFile);
    System.Console.WriteLine("Hotair: Checking for session data on disk... found");
}
catch (System.Exception)
{
    session = new SteamSession();
    System.Console.WriteLine("Hotair: Checking for session data on disk... not found");
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
        System.Console.WriteLine("Hotair: Logging on to Steam using saved session data... failed");
        session.RefreshToken = null;
        session.AccessToken = null;
    }

    System.Console.WriteLine("Hotair: Logging on to Steam using saved session data... done");
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
    System.Console.WriteLine("Hotair: Performing authentication... done");
    System.Console.WriteLine("Hotair: Writing new session data to disk...");
    WriteJson<SteamSession>(sessionFile, session);
    System.Console.WriteLine("Hotair: Writing new session data to disk... done");

    System.Console.WriteLine("Hotair: Logging on to Steam using new session data...");
    steamUser.LogOn(
        new SK.SteamUser.LogOnDetails
        {
            Username = session.Username,
            AccessToken = session.RefreshToken,
            ShouldRememberPassword = true,
            MachineName = "Hotair",
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

    System.Console.WriteLine("Hotair: Logging on to Steam using new session data... done");
}

System.Console.WriteLine("Hotair: Waiting for account information...");

var accountInfo = WaitCallback(skCallbackAccountInfo);
System.Console.WriteLine(
    $"Hotair: Waiting for account information... got name {accountInfo.PersonaName}..."
);

var emailInfo = WaitCallback(skCallbackEmailAddrInfo);
System.Console.WriteLine(
    $"Hotair: Waiting for account information... got email {emailInfo.EmailAddress}..."
);

var licenseList = WaitCallback(skCallbackLicenseList);
System.Console.WriteLine(
    $"Hotair: Waiting for account information... got {licenseList.LicenseList.Count} licenses, done"
);

var appIDs = new C.List<uint>();
if (chosenGame != 0)
{
    appIDs.Add(chosenGame);
}
else
{
    System.Console.WriteLine($"Hotair: Getting list of available apps...");
    var picsPkgs = new C.List<SK.SteamApps.PICSRequest>();
    foreach (var license in licenseList.LicenseList)
    {
        picsPkgs.Add(
            new SK.SteamApps.PICSRequest(id: license.PackageID, access_token: license.AccessToken)
        );
    }
    var pkgPicsInfo = await steamApps.PICSGetProductInfo(
        new C.List<SK.SteamApps.PICSRequest>(),
        picsPkgs
    );
    if (pkgPicsInfo.Failed)
    {
        throw new System.Exception("Failed PICS request for packages");
    }
    foreach (var page in pkgPicsInfo.Results)
    {
        if (page.UnknownPackages.Count > 0)
        {
            throw new System.Exception("PICS request for packages returned unknown packages");
        }
        foreach (var pkg in page.Packages)
        {
            var apps =
                FormatKeyValue(pkg.Value.KeyValues)["appids"] as C.Dictionary<string, object>;
            foreach (var app in apps)
                appIDs.Add(System.UInt32.Parse(app.Value as string));
        }
    }
    System.Console.WriteLine($"Hotair: Getting list of available apps... found {appIDs.Count}");
}

System.Console.WriteLine($"Hotair: Checking metadata for available apps...");
var picsApps = new C.List<SK.SteamApps.PICSRequest>();
foreach (var appID in appIDs)
{
    picsApps.Add(new SK.SteamApps.PICSRequest(id: appID));
}
var appPicsInfo = await steamApps.PICSGetProductInfo(
    picsApps,
    new C.List<SK.SteamApps.PICSRequest>(),
    true
);
if (appPicsInfo.Failed)
{
    throw new System.Exception("Failed PICS request for apps metadata");
}
var tokenAppIDs = new C.HashSet<uint>();
foreach (var page in appPicsInfo.Results)
{
    if (page.UnknownApps.Count > 0)
    {
        throw new System.Exception("PICS request for apps metadata returned unknown apps");
    }
    foreach (var appID in appIDs)
    {
        if (page.Apps[appID].MissingToken)
            tokenAppIDs.Add(appID);
    }
}
System.Console.WriteLine($"Hotair: Checking metadata for available apps... done");

var appTokens = new C.Dictionary<uint, ulong>();
if (tokenAppIDs.Count > 0)
{
    System.Console.WriteLine($"Hotair: Requesting missing tokens for {tokenAppIDs.Count} apps...");
    var appTokensData = await steamApps.PICSGetAccessTokens(tokenAppIDs, new C.List<uint>());
    appTokens = appTokensData.AppTokens;
    System.Console.WriteLine(
        $"Hotair: Requesting missing tokens for {tokenAppIDs.Count} apps... got {appTokens.Count}"
    );
}

System.Console.WriteLine($"Hotair: Getting details for available apps...");
picsApps = new C.List<SK.SteamApps.PICSRequest>();
foreach (var appID in appIDs)
{
    var req = new SK.SteamApps.PICSRequest(id: appID);
    if (appTokens.ContainsKey(appID))
        req.AccessToken = appTokens[appID];
    picsApps.Add(req);
}
var appsInfo = new C.Dictionary<uint, object>();
appPicsInfo = await steamApps.PICSGetProductInfo(
    picsApps,
    new C.List<SK.SteamApps.PICSRequest>(),
    false
);
if (appPicsInfo.Failed)
{
    throw new System.Exception("Failed PICS request for apps");
}
foreach (var page in appPicsInfo.Results)
{
    if (page.UnknownApps.Count > 0)
    {
        throw new System.Exception("PICS request for apps returned unknown apps");
    }
    foreach (var app in page.Apps)
    {
        var attrs = FormatKeyValue(app.Value.KeyValues);
        if (!attrs.ContainsKey("common"))
            continue;
        var type = LookPath(attrs, "common", "type") as string;
        if (type != "Game")
            continue;
        appsInfo[app.Key] = attrs;
    }
}
System.Console.WriteLine(
    $"Hotair: Getting details for available apps... found {appsInfo.Count} games"
);

if (chosenGame == 0)
{
    var sortedGames = new C.List<object>(appsInfo.Values).OrderBy(o =>
        LookPath(o, "common", "name")
    );
    foreach (var game in sortedGames)
    {
        var name = LookPath(game, "common", "name");
        var id = LookPath(game, "appid");
        System.Console.WriteLine($"- {name} ({id})");
    }
}
else
{
    var game = appsInfo[chosenGame];
    var gid = "";
    foreach (var depot in LookPath(game, "depots") as C.Dictionary<string, object>)
    {
        string oslist = "";
        try
        {
            oslist = LookPath(depot.Value, "config", "oslist") as string;
        }
        catch
        {
            continue;
        }
        if (oslist == "linux")
        {
            gid = LookPath(depot.Value, "manifests", "public", "gid") as string;
            break;
        }
    }
    if (gid == "")
    {
        throw new System.Exception($"Failed to find Linux depot for app {chosenGame}");
    }
}

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

string SerializeJson<T>(T obj)
{
    return System.Text.Json.JsonSerializer.Serialize(
        obj,
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
    );
}

C.Dictionary<string, object> FormatKeyValue(SK.KeyValue kv)
{
    var obj = new C.Dictionary<string, object>();
    foreach (var child in kv.Children)
    {
        if (child.Children.Count > 0)
            obj[child.Name] = FormatKeyValue(child);
        else
            obj[child.Name] = child.Value;
    }
    return obj;
}

C.Dictionary<string, object> FormatACF(string acf)
{
    if (acf[acf.Length - 1] == '\0')
        acf = acf.Remove(acf.Length - 1);
    var replaced = System.Text.RegularExpressions.Regex.Replace(
        acf,
        "(^\t*\"(?:[^\"\\\\]|\\\\.)*\"$)|(^\t*\"(?:[^\"\\\\]|\\\\.)*\")(\t*\"(?:[^\"\\\\]|\\\\.)*\"$)|\\}",
        new System.Text.RegularExpressions.MatchEvaluator(
            (match) =>
            {
                if (match.Groups[1].Success)
                    return match.ToString() + ":";
                if (match.Groups[2].Success)
                    return match.Groups[2].ToString() + ":" + match.Groups[3].ToString() + ",";
                return match.ToString() + ",";
            }
        ),
        System.Text.RegularExpressions.RegexOptions.Multiline
    );
    replaced = "{" + replaced + "}";
    var opts = new System.Text.Json.JsonSerializerOptions();
    opts.AllowTrailingCommas = true;
    return System.Text.Json.JsonSerializer.Deserialize<C.Dictionary<string, object>>(
        replaced,
        opts
    );
}

object LookPath(object o, params string[] path)
{
    foreach (var key in path)
    {
        o = (o as C.Dictionary<string, object>)[key];
    }
    return o;
}

void DumpMsg(string msgBase64)
{
    System.Console.WriteLine($"Dumping {msgBase64}");
    var packet = SK.Internal.CMClient.GetPacketMsg(
        System.Convert.FromBase64String(msgBase64),
        null
    );
    System.Console.WriteLine($"Message {packet.GetType()} (type = {0 + packet.MsgType})");
    SK.ClientMsgProtobuf msg;
    switch (packet.MsgType)
    {
        case SK.EMsg.Multi:
            var msgMulti = new SK.ClientMsgProtobuf<SK.Internal.CMsgMulti>(packet);

            {
                using var payloadStream = new System.IO.MemoryStream(msgMulti.Body.message_body);
                System.IO.Stream stream = payloadStream;

                if (msgMulti.Body.size_unzipped > 0)
                {
                    stream = new System.IO.Compression.GZipStream(
                        payloadStream,
                        System.IO.Compression.CompressionMode.Decompress
                    );
                }

                using (stream)
                {
                    System.Span<byte> length = stackalloc byte[sizeof(int)];

                    while (stream.ReadAtLeast(length, length.Length, false) > 0)
                    {
                        var subSize = System.BitConverter.ToInt32(length);
                        var subData = new byte[subSize];

                        stream.ReadAtLeast(subData, subData.Length, false);

                        DumpMsg(System.Convert.ToBase64String(subData));
                    }
                }
            }
            return;
        case SK.EMsg.ServiceMethod:
        case SK.EMsg.ServiceMethodCallFromClient:
        case SK.EMsg.ServiceMethodCallFromClientNonAuthed:
        case SK.EMsg.ServiceMethodResponse:
        {
            msg = new SK.ClientMsgProtobuf(packet);
            System.Console.WriteLine(SerializeJson(msg.Header));
            var match = System.Text.RegularExpressions.Regex.Match(
                msg.Header.Proto.target_job_name,
                @"^([^.]+)\.([^.#]+)#1$"
            );
            var cls = System.Type.GetType(
                $"SteamKit2.Internal.{match.Groups[1].Value}, SteamKit2",
                false
            );
            if (cls == null)
            {
                cls = System.Type.GetType(
                    $"SteamKit2.WebUI.Internal.{match.Groups[1].Value}, SteamKit2",
                    false
                );
                if (cls == null)
                {
                    System.Console.WriteLine("   (could not find corresponding class, skipping)");
                    break;
                }
            }
            var method = cls.GetMethod(match.Groups[2].Value);
            System.Type protoType;
            if (packet.MsgType == SK.EMsg.ServiceMethodResponse)
            {
                protoType = method.ReturnType.GetGenericArguments()[0].GetGenericArguments()[0];
            }
            else
            {
                protoType = method.GetParameters()[0].ParameterType;
            }
            System.Console.WriteLine($"Parsed target as: {protoType}");
            var protoWrapper = typeof(SK.ClientMsgProtobuf<>)
                .MakeGenericType(protoType)
                .GetConstructor(new System.Type[] { typeof(SK.IPacketMsg) })
                .Invoke(new object[] { packet });
            var body = protoWrapper.GetType().GetProperty("Body").GetValue(protoWrapper);
            System.Console.WriteLine(SerializeJson(body));
            break;
        }
        default:
            if (packet.GetType() != typeof(SK.PacketClientMsgProtobuf))
            {
                System.Console.WriteLine("   (not protobuf, skipping)");
            }
            else
            {
                System.Type cls = null;
                if (emsgOverrides.ContainsKey(packet.MsgType))
                {
                    cls = emsgOverrides[packet.MsgType];
                }
                if (cls == null)
                {
                    cls = System.Type.GetType(
                        $"SteamKit2.Internal.CMsg{packet.MsgType}, SteamKit2",
                        false,
                        true
                    );
                }
                if (cls == null)
                {
                    System.Console.WriteLine("   (could not find corresponding class, skipping)");
                }
                else
                {
                    var protoWrapper = typeof(SK.ClientMsgProtobuf<>)
                        .MakeGenericType(cls)
                        .GetConstructor(new System.Type[] { typeof(SK.IPacketMsg) })
                        .Invoke(new object[] { packet });
                    var body = protoWrapper.GetType().GetProperty("Body").GetValue(protoWrapper);
                    System.Console.WriteLine(SerializeJson(body));
                    if (packet.MsgType == SK.EMsg.ClientPICSProductInfoResponse)
                    {
                        foreach (
                            var pkg in (
                                body as SK.Internal.CMsgClientPICSProductInfoResponse
                            ).packages
                        )
                        {
                            if (pkg.buffer == null)
                                continue;
                            using var ms = new System.IO.MemoryStream(pkg.buffer);
                            using var br = new System.IO.BinaryReader(ms);
                            br.ReadUInt32();
                            var kv = new SK.KeyValue();
                            System.Console.WriteLine($":: Buffer for package {pkg.packageid}");
                            if (!kv.TryReadAsBinary(ms))
                            {
                                System.Console.WriteLine("   (failed to parse, skipping)");
                                continue;
                            }
                            System.Console.WriteLine(SerializeJson(FormatKeyValue(kv)));
                        }
                        foreach (
                            var app in (body as SK.Internal.CMsgClientPICSProductInfoResponse).apps
                        )
                        {
                            if (app.buffer == null)
                                continue;
                            System.Console.WriteLine($":: Buffer for app {app.appid}");
                            System.Console.WriteLine(
                                SerializeJson(
                                    FormatACF(System.Text.Encoding.UTF8.GetString(app.buffer))
                                )
                            );
                        }
                    }
                }
            }
            break;
    }
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
