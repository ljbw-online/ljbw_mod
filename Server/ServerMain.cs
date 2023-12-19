using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;
using Ljbw_common;
using static Ljbw_common.Class;

namespace fivem_mod.Server
{
    public class ServerMain : BaseScript
    {
        //bool ljbwCity = true;

        public ServerMain()
        {
            EventHandlers["onServerResourceStart"] += new Action<string>(OnServerResourceStart);

            EventHandlers["ljbw:weatherChangeRequest"] += new Action<string>(OnWeatherChangeRequest);
            EventHandlers["ljbw:timeChangeRequest"] += new Action<int, bool>(OnTimeChangeRequest);
            //EventHandlers["ljbw:modelOutput"] += new Action<Player>(OnModelOutputRequest);
            EventHandlers["ljbw:byteArray"] += new Action<byte[]>(OnByteArray);
            EventHandlers["ljbw:player"] += new Action<Player>(OnPlayer);

            EventHandlers["playerSpawned"] += new Action<Player, object>(OnPlayerSpawned);

            Thread thread = new Thread(new ThreadStart(SocketListen));
            thread.Start();
        }

        [Command("hello_server")]
        public void HelloServer()
        {
            Debug.WriteLine("Sure, hello.");
        }

        Socket chatBotSocket;
        Player ljbwPlayer = null;

        void SocketListen()
        {
            Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ipe = new IPEndPoint(IPAddress.Any, 7002);
            Debug.WriteLine("Binding socket");
            listenSocket.Bind(ipe);
            Debug.WriteLine("Listening to socket");
            listenSocket.Listen(1024);

            Byte[] bytesReceived = new Byte[script_to_mod_array_length];
            int numBytesReceived;

            while (true)
            {
                Debug.WriteLine($"Listening for connections on {ipe}");
                chatBotSocket = listenSocket.Accept();

                Debug.WriteLine("Accepted connection, entering Receive loop");
                while (true)
                {
                    try
                    {
                        numBytesReceived = chatBotSocket.Receive(bytesReceived);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.ToString());
                        break;
                    }

                    if (numBytesReceived == 0)
                    {
                        Debug.WriteLine("Connection closed by client");
                        listenSocket.Close();

                        listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        listenSocket.Bind(ipe);
                        listenSocket.Listen(1024);

                        Debug.WriteLine($"Waiting for a new connection on {ipe}");
                        chatBotSocket = listenSocket.Accept();
                    }
                    else //if (numBytesReceived == 16)
                    {
                        if (true) // (bytesReceived[1] == 0 && bytesReceived[2] == 0 && bytesReceived[3] == 0)
                        {
                            if (ljbwPlayer == null)
                            {
                                TriggerClientEvent("ljbw:player");
                                Thread.Sleep(250);
                            }

                            if (ljbwPlayer != null)
                            {
                                ljbwPlayer.TriggerEvent("ljbw:chatCommand", bytesReceived[0], bytesReceived[13], bytesReceived[14], bytesReceived[15]);
                                ljbwPlayer.TriggerEvent("ljbw:byteArray", bytesReceived);
                            }
                            else
                            {
                                SendChatMessageToAllClients("[SocketListener]", "ljbwPlayer == null");
                            }
                        }
                        //else
                        //{
                        //    float w = BitConverter.ToSingle(bytesReceived, 0);
                        //    float a = BitConverter.ToSingle(bytesReceived, 4);
                        //    float s = BitConverter.ToSingle(bytesReceived, 8);
                        //    float d = BitConverter.ToSingle(bytesReceived, 12);

                        //    if (ljbwPlayer != null) // && sendModelOutput)
                        //    {
                        //        //Debug.WriteLine($"sending to client: {w} {a} {s} {d}");
                        //        ljbwPlayer.TriggerEvent("ljbw:modelOutput", w, a, s, d);
                        //    }
                        //}
                    }
                }
            }
        }

        void SendChatMessageToAllClients(string sender, string message, int[] colour = null)
        {
            if (colour == null)
            {
                colour = new[] { 255, 0, 0 }; // arrays can't be default parameter values in C#
            }
            TriggerClientEvent("chat:addMessage", new
            {
                color = colour,
                args = new[] { sender, message }
            });
        }

        void SendChatMessageToSingleClient(Player player, string sender, string message, int[] colour = null)
        {
            if (colour == null)
            {
                colour = new[] { 255, 0, 0 };
            }
            player.TriggerEvent("chat:addMessage", new
            {
                color = colour,
                args = new[] { sender, message }
            });
        }

        void OnServerResourceStart(string resourceName)
        {

        }

        void OnPlayer([FromSource] Player source)
        {
            if (source.Name == "ljbw") { ljbwPlayer = source; }
        }

        void OnByteArray(byte[] byteArray)
        {
            try
            {
                int bytesSent = chatBotSocket.Send(byteArray);
                //Debug.WriteLine($"{bytesSent} bytes, starting with {byteArray[0]}, sent to bot");
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception in OnByteArray:");
                Debug.WriteLine(e.ToString());
            }
        }

        bool sendModelOutput = false;

        void OnModelOutputRequest([FromSource] Player source)
        {
            if (source.Name == "ljbw")
            {
                if (ljbwPlayer == null) { ljbwPlayer = source; }

                sendModelOutput = !sendModelOutput;
                SendChatMessageToSingleClient(source, "[ModelRequest]", $"sendModelOutput == {sendModelOutput}");
            }
            else
            {
                SendChatMessageToSingleClient(source, "[ModelRequest]", $"Only ljbw is allowed to request the model output. You are {source.Name}.");
            }
        }

        void OnPlayerSpawned([FromSource] Player source, object spawnInfo)
        {
            string sender = "[PlayerSpawnServer]";
            SendChatMessageToAllClients(sender, $"{source.Name} has spawned");

            if (source.Name == "ljbw")
            {
                ljbwPlayer = source;
            }
            
            if (timeHasBeenOverridden)
            {
                if (currentTimePausedState)
                {
                    TriggerClientEvent("ljbw:timeChangeRequest", currentTimeHours, currentTimePausedState);
                }
                else
                {
                    // GET THE CORRECT TIME FROM AN ALREADY-CONNECTED CLIENT
                    source.TriggerEvent("chat:addMessage", new
                    {
                        color = new[] {255, 0, 0 },
                        args = new[] { sender, "Time has been paused and then unpaused by already-connected clients, so you do not have the same time of day as them" }
                    });
                }
            }

            if (weatherHasBeenOverridden)
            {
                SendChatMessageToAllClients(sender, $"Changing {source.Name}'s weather to {currentWeather}");
                source.TriggerEvent("ljbw:weatherChangeRequest", currentWeather);
            }
        }

        int currentTimeHours;
        bool currentTimePausedState;
        bool timeHasBeenOverridden;
        void OnTimeChangeRequest(int hours, bool paused)
        {
            SendChatMessageToAllClients("[OnTimeChangeRequest]", "Triggering ljbw:timeChangeRequest on all clients");
            TriggerClientEvent("ljbw:timeChangeRequest", hours, paused);
            currentTimeHours = hours;
            currentTimePausedState = paused;
            timeHasBeenOverridden = true;
        }

        string currentWeather;
        bool weatherHasBeenOverridden;

        void OnWeatherChangeRequest(string weatherName)
        {
            SendChatMessageToAllClients("[WeatherChangeRequest]", $"Triggering ljbw:weatherChangeRequest with {weatherName}");
            TriggerClientEvent("ljbw:weatherChangeRequest", weatherName);
            currentWeather = weatherName;
            weatherHasBeenOverridden = true;
        }
    }
}