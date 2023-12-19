using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using Ljbw_common;
using static Ljbw_common.Class;
using System.Net.WebSockets;

namespace Ljbw_bot
{
    public class Ljbw_bot
    {
        // Don't forget about assignment-by-reference!

        static Thread modelScriptRecvThread = new(new ThreadStart(ModelScriptRecv));
        static Thread modelScriptSendThread = new(new ThreadStart(ModelScriptSend));
        static Thread modSendThread = new(new ThreadStart(ModSend));
        static Thread modRecvThread = new(new ThreadStart(ModRecv));
        static Thread channelPointListenThread = new(new ThreadStart(ChannelPointListen));

        static BlockingCollection<byte[]> modelScriptSendQ = new();
        static BlockingCollection<byte[]> modSendQ = new();
        static BlockingCollection<string> twitchSendQ = new();

        public static void Main()
        {
            modelScriptRecvThread.Start();
            modelScriptSendThread.Start();

            modSendThread.Start();
            modRecvThread.Start();

            ConnectToChat();

            channelPointListenThread.Start();
        }

        static Socket? modelScriptSocket;

        static void ModelScriptRecv()
        {
            IPEndPoint modelEndpoint = new(IPAddress.Any, 7001);
            Socket listenSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(modelEndpoint);

            while (true)
            {
                listenSocket.Listen();

                Console.WriteLine($"Listening for connection from model script on {modelEndpoint}");
                modelScriptSocket = listenSocket.Accept();

                Console.WriteLine("Entering model script recv loop");
                while (true)
                {
                    int numBytesReceived = 0;
                    byte[] modelOutputBytes = new byte[17];  // This has to stay 17 bytes long if we're sending 17 byte arrays from the python.
                    // If the array is longer than 17 bytes then we get the first array followed by the first half of the second array, all
                    // in the first bit of data Received by the socket.
                    try
                    {
                        numBytesReceived = modelScriptSocket.Receive(modelOutputBytes);
                        position_requested = modelOutputBytes[0] == (byte)botToModMessage.current_position;

                        if (modelOutputBytes[0] == (byte)botToModMessage.reinforcement_learning_resumed)
                        {
                            reinforcement_learning = true;
                        }
                        else if (modelOutputBytes[0] == (byte)botToModMessage.reinforcement_learning_paused)
                        {
                            reinforcement_learning = false;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Exception in ModelScriptRecv:");
                        Console.WriteLine(e);
                        reinforcement_learning = false;
                        if (numBytesReceived > 0)
                        {
                            byte[] byteArray = new byte[16];
                            byteArray[0] = (byte)botToModMessage.model_paused;
                            modSendQ.Add(byteArray);
                        }
                        break;
                    }

                    if (numBytesReceived == 0)
                    {
                        Console.WriteLine("0 bytes received from model script");
                        reinforcement_learning = false;
                        modelScriptSocket.Close();
                        break;
                    }
                    else
                    {
                        //Console.Write($"from script: ");
                        //foreach (byte b in modelOutputBytes) { Console.Write($"{b} "); }
                        //Console.Write("\n");
                        modSendQ.Add(modelOutputBytes);
                    }
                }
            }
        }

        static void ModelScriptSend()
        {
            while (true) { if (modelScriptSocket == null) { Thread.Sleep(1000); } else { break; } }

            while (true)
            {
                if (!modelScriptSocket.Connected) { Thread.Sleep(1000); continue; }

                byte[]? sendBuffer; // ? gets rid of VS warning
                //Console.WriteLine("Entering ModelScriptSend Dequeue loop");

                sendBuffer = modelScriptSendQ.Take();

                int numBytesSent;
                try
                {
                    //Console.Write("to script: ");
                    //foreach (byte b in sendBuffer) { Console.Write($"{b} "); }
                    //Console.Write("\n");
                    numBytesSent = modelScriptSocket.Send(sendBuffer);
                }
                catch
                {
                    Console.WriteLine("Exception from modelScriptSocket.Send");
                    modelScriptSocket.Close();
                    modelScriptSendQ.Add(sendBuffer); // re-enqueue the thing which couldn't be sent
                    break;
                }

                if (numBytesSent == 0)
                {
                    Console.WriteLine("0 bytes sent to modelScript");
                    modelScriptSocket.Close();
                    break;
                }
            }
        }

        static Socket modSocket;

        static void ModSend()
        {
            IPEndPoint modEndpoint = new(IPAddress.Parse("127.0.0.1"), 7002);
            //IPEndPoint modEndpoint = new(IPAddress.Parse("149.102.153.144"), 7002);
            int numBytesSent;

            byte[] starting_position_request = new byte[script_to_mod_array_length];
            starting_position_request[0] = (byte)botToModMessage.starting_position;
            modSendQ.Add(starting_position_request);
            
            byte[] fromScriptBuffer;

            while (true) // this loop is for auto-reconnecting to mod
            {
                modSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    modSocket.Connect(modEndpoint);
                    Console.WriteLine($"Connected to server on {modEndpoint}");
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception in ModSend:");
                    Console.WriteLine(e);
                    modSocket.Close();
                    Thread.Sleep(1000);
                    Console.WriteLine("Trying to reconnect");
                    continue;
                }

                while (true)
                {
                    fromScriptBuffer = modSendQ.Take();
                    byte[] sendBuffer = fromScriptBuffer[0..script_to_mod_array_length];

                    try
                    {
                        //Console.Write("sending to mod: ");
                        //foreach (byte b in sendBuffer) { Console.Write($"{b} "); }
                        //Console.Write("\n");
                        numBytesSent = modSocket.Send(sendBuffer);
                    }
                    catch
                    {
                        Console.WriteLine("Exception from modSocket.Send");
                        modSocket.Close();
                        modSendQ.Add(sendBuffer); // re-enqueue the thing which couldn't be sent
                        break;
                    }

                    if (numBytesSent == 0)
                    {
                        Console.WriteLine("0 bytes sent to mod");
                        modSocket.Close();
                        break;
                    }
                }
            }
        }

        static float starting_positionX = 0;
        static float starting_positionY = 0;
        static float starting_positionZ = 0;
        static float current_positionX = 0;
        static float current_positionY = 0;
        static float current_positionZ = 0;
        static float current_heading = 0;

        static bool position_requested = false;
        static bool distance_requested = false;
        static bool game_connected;

        static void ModRecv()
        {
            while (true)
            {
                //if (modSocket == null)  // modSocket no longer starts off as null
                //{
                //    Thread.Sleep(1000);
                //    continue;
                //}

                byte[] modRecvBuffer = new byte[mod_to_script_array_length];

                try
                {
                    int numBytesReceived = modSocket.Receive(modRecvBuffer);

                    if (numBytesReceived == 0)
                    {
                        Console.WriteLine($"Received 0 bytes from the mod");
                        modSocket.Close();
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        //float distance = BitConverter.ToSingle(modRecvBuffer, 0);
                        //twitchSendQ.Enqueue($"{(int)distance}m from position at start of session");
                        //Console.Write($"from mod: ");
                        //foreach (byte b in modRecvBuffer[0..numBytesReceived]) { Console.Write($"{b} "); }
                        //Console.Write("\n");

                        //Console.WriteLine($"modRecvBuffer[0] == {modRecvBuffer[0]}");

                        if (modRecvBuffer[0] == (byte)modToBotMessage.starting_position)
                        {
                            Console.WriteLine("Starting position received");
                            starting_positionX = BitConverter.ToSingle(modRecvBuffer, 1);
                            starting_positionY = BitConverter.ToSingle(modRecvBuffer, 5);
                            starting_positionZ = BitConverter.ToSingle(modRecvBuffer, 9);
                        }
                        else if (modRecvBuffer[0] == (byte)modToBotMessage.current_position)
                        {
                            current_positionX = BitConverter.ToSingle(modRecvBuffer, 1);
                            current_positionY = BitConverter.ToSingle(modRecvBuffer, 5);
                            current_positionZ = BitConverter.ToSingle(modRecvBuffer, 9);
                            current_heading = BitConverter.ToSingle(modRecvBuffer, 13);

                            if (position_requested)
                            {
                                if (chat_requested_position)
                                {
                                    twitchSendQ.Add($"{current_positionX} {current_positionY} {current_positionZ}");
                                    chat_requested_position = false;
                                }
                                else
                                {
                                    //Console.WriteLine("modelScriptSendQ.Enqueue(modRecvBuffer)");
                                    modelScriptSendQ.Add(modRecvBuffer[0..numBytesReceived]);
                                }
                                position_requested = false;
                            }
                            else if (distance_requested)
                            {
                                float distance = (float)Math.Sqrt(
                                    Math.Pow(current_positionX - starting_positionX, 2) +
                                    Math.Pow(current_positionY - starting_positionY, 2) +
                                    Math.Pow(current_positionZ - starting_positionZ, 2));

                                twitchSendQ.Add($"{(int)distance}m from starting position");
                                distance_requested = false;
                            }
                        }
                        else if (modRecvBuffer[0] == (byte)modToBotMessage.current_heading)
                        {
                            current_heading = BitConverter.ToSingle(modRecvBuffer, 1);
                            twitchSendQ.Add($"heading: {(int)current_heading}");
                        }
                        else if (modRecvBuffer[0] == (byte)modToBotMessage.state)
                        {
                            modelScriptSendQ.Add(modRecvBuffer);
                        }
                    }
                }
                catch
                {
                    //Console.WriteLine("modRecv thread sleeping for 1 sec");
                    Thread.Sleep(1000);
                }
            }
        }

        static Socket? twitchSocket = null;
        static byte[] twitchRecvBuff = new byte[256];
        static string streamer_username = "ljbwon";

        static void InitialiseTwitchSocket()
        {
            IPHostEntry hostEntry = Dns.GetHostEntry("irc.chat.twitch.tv");
            int port = 6667;

            // Loop through the AddressList to obtain the supported AddressFamily. This is to avoid
            // an exception that occurs when the host IP Address is not compatible with the address family (typical in the IPv6 case).
            foreach (IPAddress address in hostEntry.AddressList)
            {
                IPEndPoint ipe = new(address, port);
                Socket tempSocket = new(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                Console.WriteLine($"Connecting to Twitch on {ipe}");
                tempSocket.Connect(ipe);

                if (tempSocket.Connected)
                {
                    Console.WriteLine("Connected");
                    twitchSocket = tempSocket;
                    break;
                }
            }
        }

        static void AttemptToConnectToTwitch()
        {
            if (twitchSocket == null) { return; }

            string access_token = File.ReadLines(@"C:\Users\ljbw\My Drive\Tokens\ljbw_bot_access_token.txt").First();
            string PASS = "oauth:" + access_token;

            int numBytesSent = twitchSocket.Send(Encoding.ASCII.GetBytes("PASS " + PASS + "\r\n"));
            twitchSocket.Send(Encoding.ASCII.GetBytes("NICK " + "ljbw_bot" + "\r\n"));
            twitchSocket.Send(Encoding.ASCII.GetBytes("JOIN #" + streamer_username + "\r\n"));

            if (numBytesSent == 0)
            {
                Console.WriteLine("0 bytes sent to Twitch");
                return;
            }
        }

        static async Task RefreshAccessToken()
        {
            HttpClient client = new();
            // The Access Token is initially acquired by going to the following link in a browser
            // https://id.twitch.tv/oauth2/authorize?response_type=code&client_id=lv0t0i2l2p5lz8j7cajwh7b2h4hwdv&redirect_uri=https://localhost:7004&scope=chat:edit%20chat:read
            // The client_id is found on dev.twitch.tv/console and the redirect_uri is one of the URIs registered on the same page. The page above redirects you to the
            // redirect URI with the Access Token appended to it. Scopes are found on dev.twitch.tv/docs/authentication/scopes

            string client_secret = File.ReadLines(@"C:\Users\ljbw\My Drive\Tokens\ljbw_bot_client_secret.txt").First();
            string refresh_token = File.ReadLines(@"C:\Users\ljbw\My Drive\Tokens\ljbw_bot_oauth_refresh_token.txt").First();

            string URI = "https://id.twitch.tv/oauth2/token?client_id=lv0t0i2l2p5lz8j7cajwh7b2h4hwdv&client_secret=" + client_secret
                + "&grant_type=refresh_token&refresh_token=" + refresh_token;

            HttpRequestMessage reqMsg = new();
            HttpResponseMessage response = await client.PostAsync(URI, reqMsg.Content);

            response.EnsureSuccessStatusCode(); // Throws exception if response code is not 200

            string responseBody = await response.Content.ReadAsStringAsync();

            JsonDocument jsonResponse = JsonDocument.Parse(responseBody);

            string access_token_string = "oauth:" + jsonResponse.RootElement.GetProperty("access_token").ToString();

            Console.WriteLine("Writing new access token to file");
            File.WriteAllLines(@"C:\Users\ljbw\My Drive\Tokens\ljbw_bot_oauth_access_token.txt", new string[] { access_token_string });
        }

        static void ConnectToChat()
        {
            InitialiseTwitchSocket(); // This needs to be here for AttemptToConnectToTwitch to work

            Thread twitchRecvThread = new(new ThreadStart(TwitchRecv));
            twitchRecvThread.Start();

            AttemptToConnectToTwitch();

            Thread twitchSendThread = new(new ThreadStart(TwitchSend));
            twitchSendThread.Start();
        }

        //static readonly string[] commands = new string[] { "!paint", "!reset", "!new_area", "!fix", "!clean", "!ip", "!lurk", "!discord", "!commands", "w", "a", "s", "d",
        //"e", "!distance", "!position", "!set_control", "!weather", "!veh" };

        enum chatCommand
        {
            minus_one = -1,
            paint,
            reset,
            new_area,
            clean,
            fix,
            ip,
            lurk,
            discord,
            commands,
            w,
            a,
            s,
            d,
            e,
            distance,
            position,
            set_control,
            weather,
            veh,
            teleport,
            model,
            heading
        }

        static readonly string[] commands = new string[Enum.GetValues(typeof(chatCommand)).Length];

        static Ljbw_bot()
        {
            // not possible to define implicit conversion for an enum
            commands[(int)chatCommand.paint] = "!paint";
            commands[(int)chatCommand.reset] = "!reset";
            commands[(int)chatCommand.new_area] = "!new_area";
            commands[(int)chatCommand.fix] = "!fix";
            commands[(int)chatCommand.clean] = "!clean";
            commands[(int)chatCommand.ip] = "!ip";
            commands[(int)chatCommand.lurk] = "!lurk";
            commands[(int)chatCommand.discord] = "!discord";
            commands[(int)chatCommand.commands] = "!commands";
            commands[(int)chatCommand.w] = "w";
            commands[(int)chatCommand.a] = "a";
            commands[(int)chatCommand.s] = "s";
            commands[(int)chatCommand.d] = "d";
            commands[(int)chatCommand.e] = "e";
            commands[(int)chatCommand.distance] = "!distance";
            commands[(int)chatCommand.position] = "!position";
            commands[(int)chatCommand.set_control] = "!set_control";
            commands[(int)chatCommand.weather] = "!weather";
            commands[(int)chatCommand.veh] = "!veh";
            commands[(int)chatCommand.teleport] = "!teleport";
            commands[(int)chatCommand.model] = "!model";
            commands[(int)chatCommand.heading] = "!heading";
        }

        static bool chat_requested_position;
        static bool reinforcement_learning;

        static void TwitchRecv()
        {
            if (twitchSocket == null) { return; } // VS appeasement

            int numBytesReceived;

            Console.WriteLine("Initial twitch recv loop");
            while (true)
            {
                numBytesReceived = twitchSocket.Receive(twitchRecvBuff);

                if (numBytesReceived == 0)
                {
                    Console.WriteLine("0 bytes received from Twitch. Trying to reconnect.");
                    twitchSocket.Close();
                    Thread.Sleep(1000);
                    InitialiseTwitchSocket();
                    AttemptToConnectToTwitch();
                }
                else
                {
                    string twitchRecvString = Encoding.ASCII.GetString(twitchRecvBuff, 0, numBytesReceived);

                    Console.Write(twitchRecvString);

                    if (twitchRecvString.IndexOf("NOTICE * :Login authentication failed") != -1)
                    {
                        RefreshAccessToken().Wait();
                        twitchSocket.Close();
                        InitialiseTwitchSocket();
                        AttemptToConnectToTwitch();
                        break;
                    }
                    else if (twitchRecvString.IndexOf("End of /NAMES list") != -1)
                    {
                        break;
                    }
                }
            }

            Console.WriteLine("Main twitch recv loop");
            while (true)
            {
                //int numBytesReceived = 0;
                try
                {
                    numBytesReceived = twitchSocket.Receive(twitchRecvBuff);

                    if (numBytesReceived == 0)
                    {
                        Console.WriteLine("0 bytes received from Twitch");
                        twitchSocket.Close();
                        return;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    twitchSocket.Close();
                    Thread.Sleep(1000);
                    InitialiseTwitchSocket();
                    AttemptToConnectToTwitch();
                    continue;
                }

                string recvString = Encoding.ASCII.GetString(twitchRecvBuff, 0, numBytesReceived);

                if (recvString.StartsWith("PING"))
                {
                    twitchSendQ.Add(String.Concat("PONG", recvString.AsSpan(4)));
                    continue;
                }

                string username = "";
                string message = "";

                int pos = recvString.IndexOf("!");
                if (pos != -1)
                {
                    username = recvString.Substring(1, pos - 1);
                }

                pos = recvString.IndexOf(" :");
                if (pos != -1)
                {
                    message = recvString.Substring(pos + 2, recvString.Length - (pos + 2) - 2); // pos + 2 disincludes " :" and - 2 disincludes \r\n
                }

                Console.WriteLine(username + ": " + message);

                string[] args = message.Split(" ");
                string arg0 = args[0].ToLower();

                byte[] sendBuffer = new byte[mod_to_script_array_length];

                if (args[0] == "!test")
                {
                    Console.WriteLine("!test command");
                    try
                    {
                        //sendBuffer[0] = (byte)int.Parse(args[1]);
                        //if (args.Length == 3) { BitConverter.GetBytes(float.Parse(args[2])).CopyTo(sendBuffer, 12); }
                        //modSendQ.Enqueue(sendBuffer);
                        modelScriptSendQ.Add(sendBuffer);
                        continue;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        continue;
                    }
                }
                else if (arg0 == "!w" || arg0 == "!a" || arg0 == "!s" || arg0 == "!d")
                {
                    arg0 = arg0[1..];
                }

                for (int i = 0; i <= arg0.Length; i++)
                {
                    if (i == arg0.Length)
                    {
                        foreach (char c in arg0) // send all the wasds
                        {
                            byte[] modSendBytes = new byte[script_to_mod_array_length];
                            modSendBytes[0] = (byte)botToModMessage.chat_key;
                            if (c == 'w')
                            {
                                modSendBytes[13] = 0;
                            }
                            else if (c == 'a')
                            {
                                modSendBytes[13] = 1;
                            }
                            else if (c == 's')
                            {
                                modSendBytes[13] = 2;
                            }
                            else if (c == 'd')
                            {
                                modSendBytes[13] = 3;
                            }
                            else if (c == 'e')
                            {
                                modSendBytes[13] = 4;
                            }
                            else if (c == 'p')
                            {
                                modSendBytes[13] = 5;
                            }
                            else if (c == 'm')
                            {
                                modSendBytes[13] = 6;
                            }
                            Console.WriteLine($"sendBuffer[13] == {modSendBytes[13]}");
                            modSendQ.Add(modSendBytes);
                        }
                    }
                    else if (arg0[i] != 'w' && arg0[i] != 'a' && arg0[i] != 's' && arg0[i] != 'd' && arg0[i] != 'e' && arg0[i] != 'p' && arg0[i] != 'm')
                    {
                        break;
                    }
                    else if (reinforcement_learning) { break; }
                }

                switch ((chatCommand)Array.IndexOf(commands, arg0))
                {
                    case chatCommand.minus_one:
                        break;
                    case chatCommand.paint: // !paint
                        if (args.Length == 1)
                        {
                            sendBuffer[0] = (byte)botToModMessage.paint_random;
                            modSendQ.Add(sendBuffer);
                        }
                        else if (args.Length == 2)
                        {
                            sendBuffer[0] = (byte)botToModMessage.paint_colour; // necessary to distinguish "!paint" from "!paint black"

                            if (args[1] == "black")
                            {
                                sendBuffer[13] = 0;
                                sendBuffer[14] = 0;
                                sendBuffer[15] = 0;
                            }
                            else if (args[1] == "white")
                            {
                                sendBuffer[13] = 255;
                                sendBuffer[14] = 255;
                                sendBuffer[15] = 255;
                            }
                            else if (args[1] == "red")
                            {
                                sendBuffer[13] = 255;
                                sendBuffer[14] = 0;
                                sendBuffer[15] = 0;
                            }
                            else if (args[1] == "green")
                            {
                                sendBuffer[13] = 0;
                                sendBuffer[14] = 255;
                                sendBuffer[15] = 0;
                            }
                            else if (args[1] == "blue")
                            {
                                sendBuffer[13] = 0;
                                sendBuffer[14] = 0;
                                sendBuffer[15] = 255;
                            }
                            else if (args[1] == "cyan")
                            {
                                sendBuffer[13] = 0;
                                sendBuffer[14] = 255;
                                sendBuffer[15] = 255;
                            }
                            else if (args[1] == "magenta")
                            {
                                sendBuffer[13] = 255;
                                sendBuffer[14] = 0;
                                sendBuffer[15] = 255;
                            }
                            else if (args[1] == "yellow")
                            {
                                sendBuffer[13] = 255;
                                sendBuffer[14] = 255;
                                sendBuffer[15] = 0;
                            }
                            else if (args[1] == "pink")
                            {
                                sendBuffer[13] = 255;
                                sendBuffer[14] = 105;
                                sendBuffer[15] = 180;
                            }
                            else
                            {
                                twitchSendQ.Add("Unknown colour name");
                                continue;
                            }
                            modSendQ.Add(sendBuffer);
                        }
                        else if (args.Length == 4)
                        {
                            sendBuffer[0] = (byte)botToModMessage.paint_colour;

                            try
                            {
                                sendBuffer[13] = byte.Parse(args[1]);
                                sendBuffer[14] = byte.Parse(args[2]);
                                sendBuffer[15] = byte.Parse(args[3]);
                                modSendQ.Add(sendBuffer);
                            }
                            catch
                            {
                                twitchSendQ.Add("RGB values have to be whole numbers between 0 and 255, inclusive");
                                break;
                            }
                        }
                        break;
                    case chatCommand.reset: // !reset
                        if (reinforcement_learning) { twitchSendQ.Add("Command not allowed during reinforcement learning"); break; }
                        sendBuffer[0] = (byte)botToModMessage.reset;
                        modSendQ.Add(sendBuffer);
                        break;
                    case chatCommand.new_area: // !new_area
                        if (reinforcement_learning) { twitchSendQ.Add("Command not allowed during reinforcement learning"); break; }
                        sendBuffer[0] = (byte)botToModMessage.new_area;
                        modSendQ.Add(sendBuffer);
                        break;
                    case chatCommand.fix:
                        sendBuffer[0] = (byte)botToModMessage.fix; // !fix
                        modSendQ.Add(sendBuffer);
                        break;
                    case chatCommand.clean:
                        sendBuffer[0] = (byte)botToModMessage.clean;  // !clean
                        modSendQ.Add(sendBuffer);
                        break;
                    case chatCommand.ip: // !ip
                        twitchSendQ.Add("Not currently on a public address");
                        break;
                    case chatCommand.lurk: // !lurk
                        twitchSendQ.Add(username + " is lurking in the depths. Thank you for lurking!");
                        break;
                    case chatCommand.discord: // !discord
                        twitchSendQ.Add("https://discord.gg/2bUpN4TnTm");
                        break;
                    case chatCommand.commands: // !commands
                        twitchSendQ.Add(String.Join(" | ", commands));
                        break;
                    //case chatCommand.w: // w
                    //    if (reinforcement_learning) { twitchSendQ.Enqueue("Command not allowed during reinforcement learning"); break; }
                    //    sendBuffer[0] = (byte)botToModMessage.chat_key;
                    //    sendBuffer[13] = 0;
                    //    modSendQ.Enqueue(sendBuffer);
                    //    break;
                    //case chatCommand.a: // a
                    //    if (reinforcement_learning) { twitchSendQ.Enqueue("Command not allowed during reinforcement learning"); break; }
                    //    sendBuffer[0] = (byte)botToModMessage.chat_key;
                    //    sendBuffer[13] = 1;
                    //    modSendQ.Enqueue(sendBuffer);
                    //    break;
                    //case chatCommand.s: // s
                    //    sendBuffer[0] = (byte)botToModMessage.chat_key;
                    //    sendBuffer[13] = 2;
                    //    modSendQ.Enqueue(sendBuffer);
                    //    break;
                    //case chatCommand.d: // d
                    //    sendBuffer[0] = (byte)botToModMessage.chat_key;
                    //    sendBuffer[13] = 3;
                    //    modSendQ.Enqueue(sendBuffer);
                    //    break;
                    //case chatCommand.e: // e
                    //    sendBuffer[0] = (byte)botToModMessage.chat_key;
                    //    sendBuffer[13] = 4;
                    //    modSendQ.Enqueue(sendBuffer);
                    //    break;
                    case chatCommand.distance: // !distance
                        distance_requested = true;
                        sendBuffer[0] = (byte)botToModMessage.current_position;
                        modSendQ.Add(sendBuffer);
                        break;
                    case chatCommand.position: // !position
                        position_requested = true;
                        chat_requested_position = true;
                        sendBuffer[0] = (byte)botToModMessage.current_position;
                        modSendQ.Add(sendBuffer);
                        break;
                    case chatCommand.set_control: // !set_control
                        if (reinforcement_learning) { twitchSendQ.Add("Command not allowed during reinforcement learning"); break; }
                        if (args.Length == 2)
                        {
                            sendBuffer[0] = (byte)botToModMessage.set_control;
                            try
                            {
                                BitConverter.GetBytes(int.Parse(args[1])).CopyTo(sendBuffer, 12);
                            }
                            catch
                            {
                                twitchSendQ.Add("Control needs to be parsable as int");
                            }
                            modSendQ.Add(sendBuffer);
                        }
                        else
                        {
                            twitchSendQ.Add("!setcontrol <control number>");
                        }
                        break;
                    case chatCommand.weather: // !weather
                        if (reinforcement_learning) { twitchSendQ.Add("Command not allowed during reinforcement learning"); break; }
                        if (args.Length == 2)
                        {
                            sendBuffer[0] = (byte)botToModMessage.weather;
                            sendBuffer[13] = (byte)Array.IndexOf(weatherNames, args[1].ToLower());
                            modSendQ.Add(sendBuffer);
                        }
                        else
                        {
                            twitchSendQ.Add(String.Join(" ", weatherNames));
                        }
                        break;
                    case chatCommand.veh: // !veh
                        if (reinforcement_learning) { twitchSendQ.Add("Command not allowed during reinforcement learning"); break; }
                        if (args.Length == 2)
                        {
                            int vehicleNum = Array.IndexOf(vehicleNames, args[1].ToLower());
                            if (vehicleNum == -1)
                            {
                                twitchSendQ.Add(String.Join(" ", vehicleNames));
                            }
                            else
                            {
                                sendBuffer[0] = (byte)botToModMessage.veh;
                                //sendBuffer[13] = (byte)vehicleNum;
                                BitConverter.GetBytes(vehicleNum).CopyTo(sendBuffer, 13);
                                //Console.WriteLine($"modSend: sendBuffer.Length == {sendBuffer.Length}");
                                //foreach (byte b in sendBuffer) { Console.Write($"{b} "); }
                                modSendQ.Add(sendBuffer);
                            }
                        }
                        else
                        {
                            //twitchSendQ.Add(String.Join(" ", vehicleNames));
                        }
                        break;
                    case chatCommand.teleport:
                        if (reinforcement_learning) { twitchSendQ.Add("Command not allowed during reinforcement learning"); break; }
                        sendBuffer[0] = (byte)botToModMessage.teleport;
                        if (args.Length == 1)
                        {
                            modSendQ.Add(sendBuffer);
                        }
                        else if (args.Length == 2)
                        {
                            if (args[1] == "water")
                            {
                                sendBuffer[13] = 1;
                                modSendQ.Add(sendBuffer);
                            }
                        }
                        else if (args.Length == 4)
                        {
                            try
                            {
                                float x = float.Parse(args[1]);
                                float y = float.Parse(args[2]);
                                float z = float.Parse(args[3]);

                                if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z))
                                {
                                    twitchSendQ.Add("Swear on me NaN mate");
                                }
                                else
                                {
                                    BitConverter.GetBytes(x).CopyTo(sendBuffer, 4);
                                    BitConverter.GetBytes(y).CopyTo(sendBuffer, 8);
                                    BitConverter.GetBytes(z).CopyTo(sendBuffer, 12);

                                    modSendQ.Add(sendBuffer);
                                }
                            }
                            catch
                            {
                                twitchSendQ.Add("At least one of the coords could not be parsed as a float");
                            }
                        }
                        break;
                    case chatCommand.model:
                        if (args.Length == 2)
                        {
                            if (args[1] == "pause")
                            {
                                sendBuffer[0] = (byte)botToModMessage.model_paused;
                                modelScriptSendQ.Add(sendBuffer);
                                modSendQ.Add(sendBuffer);

                                reinforcement_learning = false;
                                sendBuffer[0] = (byte)botToModMessage.reinforcement_learning_paused;
                                modelScriptSendQ.Add(sendBuffer);
                                modSendQ.Add(sendBuffer);
                            }
                            else if (args[1] == "unpause" || args[1] == "resume")
                            {
                                sendBuffer[0] = (byte)botToModMessage.supervised_model_resumed;
                                modelScriptSendQ.Add(sendBuffer);
                                modSendQ.Add(sendBuffer);
                            }
                        }
                        break;
                    case chatCommand.heading:
                        if (reinforcement_learning) { twitchSendQ.Add("Command not allowed during reinforcement learning"); break; }
                        if (args.Length == 1)
                        {
                            sendBuffer[0] = (byte)botToModMessage.get_heading;
                            modSendQ.Add(sendBuffer);
                        }
                        else if (args.Length == 2)
                        {
                            sendBuffer[0] = (byte)botToModMessage.set_heading;
                            try
                            {
                                float heading = float.Parse(args[1]) % 360.0f;
                                if (float.IsNaN(heading))
                                {
                                    twitchSendQ.Add("Swear on me NaN mate");
                                }
                                else
                                {
                                    BitConverter.GetBytes(heading).CopyTo(sendBuffer, 12);
                                    modSendQ.Add(sendBuffer);
                                }
                            }
                            catch
                            {
                                twitchSendQ.Add("Couldn't parse as float");
                            }
                        }
                        break;
                }
            }
        }

        static void TwitchSend()
        {
            if (twitchSocket == null) { return; } // VS appeasement

            Thread.Sleep(2000);

            Console.WriteLine("Enqueing connected to chat message");
            twitchSendQ.Add("connected");

            while (true)
            {
                string? msgToSend;

                msgToSend = twitchSendQ.Take();

                //if (twitchSendQ.TryDequeue(out msgToSend))
                if (true)
                {
                    try
                    {
                        if (msgToSend.StartsWith("PONG"))
                        {
                            twitchSocket.Send(Encoding.ASCII.GetBytes(msgToSend));
                        }
                        else
                        {
                            twitchSocket.Send(Encoding.ASCII.GetBytes("PRIVMSG #" + streamer_username + " :" + msgToSend + "\r\n"));
                        }
                    }
                    catch
                    {
                        Console.WriteLine("TwitchSend trying again in 1 sec");
                        Thread.Sleep(1000);
                    }
                }
                //else
                //{
                //    Thread.Sleep(10);
                //}
            }
        }

        static async void ChannelPointListen()
        {
            ClientWebSocket websocket = new();
            Uri uri = new("wss://eventsub.wss.twitch.tv/ws");
            await websocket.ConnectAsync(uri, default);

            HttpClient client = new();
            string access_token = File.ReadLines(@"C:\Users\ljbw\My Drive\Tokens\ljbw_bot_access_token.txt").First();
            string client_id = File.ReadLines(@"C:\Users\ljbw\My Drive\Tokens\ljbw_bot_client_id.txt").First();
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + access_token);
            client.DefaultRequestHeaders.Add("Client-Id", client_id);

            const string new_colour_id = "90ad149d-1aad-4536-a0fd-862fb10cdbaa";
            const string new_vehicle_id = "4a7876f0-0588-4e0b-a32f-175445252c3c";
            const string new_area_id = "ae69f182-7832-48ee-b49b-ce7dbe973f4d";

            var received_bytes = new byte[4096];
            var result = await websocket.ReceiveAsync(received_bytes, default);

            string received_string = Encoding.UTF8.GetString(received_bytes, 0, result.Count);
            JsonDocument response_json = JsonDocument.Parse(received_string);
            JsonElement root = response_json.RootElement;
            string session_id = root.GetProperty("payload").GetProperty("session").GetProperty("id").ToString();

            string ljbwon_id = File.ReadLines(@"C:\Users\ljbw\My Drive\Tokens\ljbwon_twitch_user_id.txt").First();

            HttpRequestMessage request = new (HttpMethod.Post, "https://api.twitch.tv/helix/eventsub/subscriptions");

            string request_body = "{\"type\":\"channel.channel_points_custom_reward_redemption.add\",\"version\":\"1\",\"condition\":{\"broadcaster_user_id\":\"" + ljbwon_id + "\"},\"transport\":{\"method\":\"websocket\",\"session_id\":\"" + session_id + "\"}}";

            request.Content = new StringContent(request_body, Encoding.UTF8, "application/json");

            // Subscribe to channel point redemption events
            HttpResponseMessage response = await client.SendAsync(request);
            HttpStatusCode statusCode = response.StatusCode;
            string responseContent = await response.Content.ReadAsStringAsync();

            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                Console.WriteLine(responseContent);
            }

            // Not defined at class level because not thread-safe. Multiple Randoms created in quick succession may output identical sequences. That's fine in this context.
            Random rng = new Random();

            string message_type;
            JsonElement payload;
            string reward_id;
            string redemption_id;
            string update_redemption_status_uri = "https://api.twitch.tv/helix/channel_points/custom_rewards/redemptions?broadcaster_id=" + ljbwon_id;
            StringContent fulfilled_request_body = new ("{\"status\":\"FULFILLED\"}", Encoding.UTF8, "application/json");
            StringContent cancelled_request_body = new ("{\"status\":\"CANCELED\"}", Encoding.UTF8, "application/json"); // Single L in CANCELED is an Americanism
            byte[] sendBuffer;
            bool respond;
            string reconnect_url;

            // {"session":{"id":"AgoQqoglP0K8TI-JE77SIvUt0xIGY2VsbC1j","status":"reconnecting","connected_at":"2023-11-15T14:13:23.09581564Z","keepalive_timeout_seconds":null,
            // "reconnect_url":"wss://cell-c.eventsub.wss.twitch.tv/ws?challenge=8ee710d1-cddd-499e-9565-58c761c2f299\u0026id=AgoQqoglP0K8TI-JE77SIvUt0xIGY2VsbC1j"}}

            // Start listening for websocket events
            while (true)
            {
                sendBuffer = new byte[mod_to_script_array_length];

                result = await websocket.ReceiveAsync(received_bytes, default);
                received_string = Encoding.UTF8.GetString(received_bytes, 0, result.Count);
                response_json = JsonDocument.Parse(received_string);
                root = response_json.RootElement;

                try
                {
                    message_type = root.GetProperty("metadata").GetProperty("message_type").ToString();
                }
                catch (Exception ex) { Console.WriteLine(ex); Console.WriteLine(root); continue; }


                if (message_type == "session_reconnect")
                {
                    Console.WriteLine("Reconnecting websocket");
                    await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", default);
                    reconnect_url = root.GetProperty("payload").GetProperty("session").GetProperty("reconnect_url").ToString();
                    websocket = new();
                    await websocket.ConnectAsync(new Uri(reconnect_url), default);
                }
                else if (message_type != "session_keepalive")
                {
                    try
                    {
                        payload = root.GetProperty("payload");
                    }
                    catch (Exception ex) { Console.WriteLine(ex); Console.WriteLine(root); continue; }

                    try
                    {
                        reward_id = payload.GetProperty("event").GetProperty("reward").GetProperty("id").ToString();
                    }
                    catch (Exception ex) { Console.WriteLine(ex); Console.WriteLine(payload); continue; }

                    try
                    {
                        redemption_id = payload.GetProperty("event").GetProperty("id").ToString();
                    }
                    catch (Exception ex) { Console.WriteLine(ex); Console.WriteLine(payload); continue; }

                    //Console.WriteLine(payload);

                    switch (reward_id)
                    {
                        case new_colour_id:
                            Console.WriteLine("New Colour was redeemed!");
                            respond = true;
                            sendBuffer[0] = (byte)botToModMessage.paint_random;
                            break;
                        case new_vehicle_id:
                            Console.WriteLine("New Vehicle was redeemed!");
                            respond = true;
                            sendBuffer[0] = (byte)botToModMessage.veh;
                            sendBuffer[13] = (byte)rng.Next(vehicleNames.Length);
                            break;
                        case new_area_id:
                            Console.WriteLine("New Area was redeemed!");
                            respond = true;
                            sendBuffer[0] = (byte)botToModMessage.new_area;
                            break;
                        default:
                            Console.WriteLine(reward_id + " was redeemed");
                            respond = false;
                            break;
                    }
                    
                    if (respond)
                    {
                        request = new (HttpMethod.Patch, update_redemption_status_uri + "&reward_id=" + reward_id + "&id=" + redemption_id);
                        
                        if (modSocket.Connected)
                        {
                            request.Content = fulfilled_request_body;
                            modSendQ.Add(sendBuffer);
                        }
                        else
                        {
                            request.Content = cancelled_request_body;
                            twitchSendQ.Add("Not currently connected to GTA");
                        }
                        
                        response = await client.SendAsync(request);

                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            Console.WriteLine(await response.Content.ReadAsStringAsync());
                        }
                    }
                }
            }
            client.Dispose();
            await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", default);
        }
    }
}