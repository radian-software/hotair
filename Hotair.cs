using SK = SteamKit2;

// originally from steamkit but then tweaked
// https://github.com/SteamRE/SteamKit/blob/5c0143306dd4e56b0780cc006f822e12a42883a1/Resources/NetHookAnalyzer2/NetHookAnalyzer2/MessageTypeOverrides.cs#L15
var emsgOverrides = new System.Collections.Generic.Dictionary<SK.EMsg, System.Type>
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
}

System.Console.WriteLine("Hotair: Waiting for account information...");

var accountInfo = WaitCallback(skCallbackAccountInfo);
System.Console.WriteLine($"Hotair: Account name: {accountInfo.PersonaName}");

var emailInfo = WaitCallback(skCallbackEmailAddrInfo);
System.Console.WriteLine($"Hotair: Account email: {emailInfo.EmailAddress}");

var licenseList = WaitCallback(skCallbackLicenseList);
System.Console.WriteLine($"Hotair: Found {licenseList.LicenseList.Count} licenses");

ulong libraryAccessToken = 0;
foreach (var license in licenseList.LicenseList)
{
    if (license.PackageID == 0)
        libraryAccessToken = license.AccessToken;
}
if (libraryAccessToken == 0)
    throw new System.Exception("Failed to find access token in license for package 0");

System.Console.WriteLine($"Hotair: Getting list of apps in library...");
var pkgPicsInfo = await steamApps.PICSGetProductInfo(
    null,
    new SK.SteamApps.PICSRequest(id: 0, access_token: libraryAccessToken)
);
if (pkgPicsInfo.Failed)
{
    throw new System.Exception("Failed PICS request for packages");
}
var appIDs =
    FormatKeyValue(pkgPicsInfo.Results[0].Packages[0].KeyValues)["appids"]
    as System.Collections.Generic.Dictionary<string, object>;
System.Console.WriteLine($"Hotair: Found {appIDs.Count} apps in library");

System.Console.WriteLine($"Hotair: Getting details for apps in library...");
var picsApps = new System.Collections.Generic.List<SK.SteamApps.PICSRequest>();
foreach (var appID in appIDs)
{
    picsApps.Add(new SK.SteamApps.PICSRequest(id: System.UInt32.Parse(appID.Value as string)));
}
var appPicsInfo = await steamApps.PICSGetProductInfo(
    picsApps,
    new System.Collections.Generic.List<SK.SteamApps.PICSRequest>(),
    false
);
if (appPicsInfo.Failed)
{
    throw new System.Exception("Failed PICS request for apps");
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

System.Collections.Generic.Dictionary<string, object> FormatKeyValue(SK.KeyValue kv)
{
    var obj = new System.Collections.Generic.Dictionary<string, object>();
    foreach (var child in kv.Children)
    {
        if (child.Children.Count > 0)
            obj[child.Name] = FormatKeyValue(child);
        else
            obj[child.Name] = child.Value;
    }
    return obj;
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
                "^([^.]+)\\.([^.#]+)#1$"
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
