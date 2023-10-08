using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;
using Ljbw_common;
using static Ljbw_common.Class;
using System.Threading;
using System.Collections;

namespace fivem_mod.Client
{
    public class ClientMain : BaseScript
    {
        bool ljbwCity = true;

        bool autoReset, pedsRiot;

        // true at spawn. If they are true at declaration they each have to be declared individually.
        bool playerInvincible = true;
        bool playerCannnotBeWanted = true;
        bool timePaused = true;
        bool vehicleInvincible = true;
        bool vehicleLocked = true;
        bool waterTeleport = true;

        // Storing state of certain features on the previous frame
        bool prevPlayerInvincible;

        int h = 12; 
        int m, sec;
        bool playerIsInVehicle = false;
        bool prevPlayerIsInVehicle = false;

        uint current_vehicle_hash;

        const int pos_buffer_length = 1800;
        Vector3[] pos_buffer = new Vector3[pos_buffer_length];
        int pos_index = 0;

        Vector3 starting_position;

        bool log_bot_msgs = false;

        public ClientMain()
        {
            Debug.WriteLine("ljbw_mod.Client started");

            EventHandlers["onClientResourceStart"] += new Action<string>(OnClientResourceStart);

            EventHandlers["ljbw:weatherChangeRequest"] += new Action<string>(OnWeatherChangeRequest);
            EventHandlers["ljbw:timeChangeRequest"] += new Action<int, bool>(OnTimeChangeRequest);
            //EventHandlers["ljbw:modelOutput"] += new Action<float, float, float, float>(OnModelOutput);
            EventHandlers["ljbw:chatCommand"] += new Action<byte, byte, byte, byte>(OnBotMessage);
            EventHandlers["ljbw:byteArray"] += new Action<byte[]>(OnByteArray);
            EventHandlers["ljbw:player"] += new Action(OnPlayer);
            EventHandlers["ljbw:setVehicleRequest"] += new Action<string>(SetVehicle);

            EventHandlers["playerSpawned"] += new Action<object>(OnPlayerSpawned);

            AddTextEntry("w0", "~1~"); // string has to contain '1', or perhaps any digit
            AddTextEntry("w1", "~1~");
            AddTextEntry("w2", "~1~");
            AddTextEntry("w3", "~1~");

            for (int i = 0; i < GetNumResources(); i++)
            {
                if (GetResourceByFindIndex(i) == "qb-core") { ljbwCity = false; timePaused = false; break; }
            }
        }

        void ChatMessage(string sender, string message, int[] colour = null)
        {
            if (colour == null)
            {
                colour = new[] { 255, 0, 0 }; // arrays can't be default parameter values in C#
            }
            TriggerEvent("chat:addMessage", new
            {
                color = colour,
                args = new[] { sender, message }
            });
        }

        void teleport(Vector3 coords, float heading = -1)
        {
            int entity = PlayerPedId();
            
            if (IsPedInAnyVehicle(entity, false))
            {
                entity = GetVehiclePedIsUsing(entity);
            }

            SetEntityCoordsNoOffset(entity, coords.X, coords.Y, coords.Z, false, false, true);

            if (heading != -1)
            {
                SetEntityHeading(entity, heading);
                SetGameplayCamRelativeHeading(0);
            }
        }

        void SetVehicleInvincible(int vehicle, bool setting)
        {
            if (setting)
            {
                SetVehicleFixed(GetVehiclePedIsIn(PlayerPedId(), !setting));
            }

            SetVehicleCanBeVisiblyDamaged(vehicle, !setting);
            SetEntityProofs(vehicle, setting, setting, setting, setting, setting, setting, setting, setting);
            SetVehicleTyresCanBurst(vehicle, !setting);
            SetVehicleWheelsCanBreak(vehicle, !setting);
        }

        void teleport_to_nearest_major_road(Vector3 entity_coords)
        {
            Vector3 node_position;
            node_position.X = 0;
            node_position.Y = 0;
            node_position.Z = 0;

            float heading = 0;

            int node_id = GetNthClosestVehicleNodeId(entity_coords.X, entity_coords.Y, entity_coords.Z, 1, 4, 0, 0);

            GetVehicleNodePosition(node_id, ref node_position);
            GetClosestVehicleNodeWithHeading(node_position.X, node_position.Y, node_position.Z, ref node_position, ref heading, 1, 3, 0);

            teleport(node_position, heading);
        }

        float vector3_distance(Vector3 a, Vector3 b)
        {
            return Sqrt(Pow(a.X - b.X, 2) + Pow((a.Y - b.Y), 2) + Pow((a.Z - b.Z), 2));
        }

        async void SetVehicle(string vehicleName)
        {
            current_vehicle_hash = (uint)GetHashKey(vehicleName);
            string sender = "[SetVehicle]";
            int current_vehicle;
            Vector3 position; position.X = 0; position.Y = 0; position.Z = 0;
            float speed = 0;
            bool isEngineRunning = false;
            int pri_r = 0; int pri_g = 0; int pri_b = 0; int sec_r = 0; int sec_g = 0; int sec_b = 0;
            int[] occupants = { 0, 0, 0, 0, 0, 0, 0, 0 };
            if (IsPedInAnyVehicle(PlayerPedId(), false))
            {
                current_vehicle = GetVehiclePedIsIn(PlayerPedId(), true);
                speed = GetEntitySpeed(current_vehicle);
                isEngineRunning = GetIsVehicleEngineRunning(current_vehicle);
                GetVehicleCustomPrimaryColour(current_vehicle, ref pri_r, ref pri_g, ref pri_b);
                GetVehicleCustomSecondaryColour(current_vehicle, ref sec_r, ref sec_g, ref sec_b);

                for (int i = -1; i <= 5; i++)
                {
                    occupants[i + 1] = GetPedInVehicleSeat(current_vehicle, i);
                    ChatMessage(sender, $"ped {occupants[i + 1]} is in seat {i}");
                }

                SetEntityAsMissionEntity(current_vehicle, true, true);
                DeleteVehicle(ref current_vehicle);
            }
            ChatMessage(sender, "Creating vehicle");
            var vehicle = await World.CreateVehicle(vehicleName, Game.PlayerPed.Position, Game.PlayerPed.Heading); // spawns vehicle in PLAYER's position not prev veh's
            Game.PlayerPed.SetIntoVehicle(vehicle, VehicleSeat.Driver);

            int new_vehicle = GetVehiclePedIsIn(PlayerPedId(), false);
            ChatMessage(sender, $"playerPedId {PlayerPedId()}; playerId {PlayerId()}");

            //for (int i = -1; i <= 5; i++) // POSSIBLY CAUSING A CRASH WHEN CALLED FROM CHAT
            //{
            //    if (IsVehicleSeatFree(new_vehicle, i))
            //    {
            //        SetPedIntoVehicle(occupants[i + 1], new_vehicle, i);
            //        SendChatMessage(sender, $"Setting {occupants[i + 1]} into seat {i}");
            //    }
            //}

            SetVehicleForwardSpeed(new_vehicle, speed);
            SetVehicleEngineOn(new_vehicle, isEngineRunning, true, false);
            SetVehicleCustomPrimaryColour(new_vehicle, pri_r, pri_g, pri_b);
            SetVehicleCustomSecondaryColour(new_vehicle, sec_r, sec_g, sec_b);

            if (vehicleInvincible) { SetVehicleInvincible(new_vehicle, true); }
            if (vehicleLocked) { SetVehicleDoorsLocked(new_vehicle, 2); }

            ChatMessage(sender, $"^*{vehicleName} spawned");
        }

        void OnPlayer()
        {
            TriggerServerEvent("ljbw:player");
        }

        private void OnPlayerSpawned(object spawnInfo)
        {
            string sender = "[PlayerSpawnHandler]";
            ChatMessage(sender, "Triggering playerSpawned on server");
            TriggerServerEvent("playerSpawned", spawnInfo);
            
            int playerPedId = PlayerPedId();

            if (playerInvincible)
            {
                SetEntityInvincible(playerPedId, true);
                ChatMessage(sender, "Setting player inv on spawn");
            }

            starting_position = GetEntityCoords(playerPedId, false);
            
            byte[] byteArray = new byte[13];
            byteArray[0] = (byte)modToBotMessage.starting_position;
            BitConverter.GetBytes(starting_position.X).CopyTo(byteArray, 1);
            BitConverter.GetBytes(starting_position.Y).CopyTo(byteArray, 5);
            BitConverter.GetBytes(starting_position.Z).CopyTo(byteArray, 9);

            TriggerServerEvent("ljbw:byteArray", byteArray);
        }

        private void OnClientResourceStart(string resourceName)
        {
            if (GetCurrentResourceName() != resourceName) return;

            NetworkSetFriendlyFireOption(true);
            SetCanAttackFriendly(PlayerPedId(), true, false);

            EnableAllControlActions(0);
            EnableAllControlActions(1);
            EnableAllControlActions(2);

            //RegisterCommand("", new Action<int, List<object>, string>((source, args, raw) => { }), false);

            RegisterCommand("test", new Action<int, List<object>, string>((source, args, raw) => 
            {
                ChatMessage("[test]", "w" + 1.ToString());

            }), false);

            RegisterCommand("log", new Action<int, List<object>, string>((source, args, raw) =>
            {
                log_bot_msgs = !log_bot_msgs;
                ChatMessage("[log]", $"log_bot_msgs == {log_bot_msgs}");
            }), false);

            RegisterCommand("model", new Action<int, List<object>, string>((source, args, raw) => 
            {
                ChatMessage("[model]", "Triggering ljbw:modelOutput");
                TriggerServerEvent("ljbw:modelOutput");
                modelOutput = !modelOutput;
                if (modelOutput)
                {
                    autoReset = true;
                    waterTeleport = true;
                }
                else
                {
                    autoReset = false;
                    waterTeleport = false;
                }    
            }), false);

            RegisterCommand("peds-riot", new Action<int, List<object>, string>((source, args, raw) => 
            {
                pedsRiot = !pedsRiot;
                SetRiotModeEnabled(pedsRiot);

            }), false);

            RegisterCommand("player", new Action<int, List<object>, string>((source, args, raw) => 
            {
                string sender = "[PlayerHandler]";
                if (args.Count == 0)
                {
                    ChatMessage(sender, "inv");
                }
                else
                {
                    string arg0 = args[0].ToString();
                    if (arg0 == "inv")
                    {
                        playerInvincible = !playerInvincible;
                        ChatMessage(sender, $"Setting playerInvincible to {playerInvincible}");
                    }
                    else if (arg0 == "wanted")
                    {
                        playerCannnotBeWanted = !playerCannnotBeWanted;
                        ChatMessage(sender, $"Setting playerCannotBeWanted to {playerCannnotBeWanted}");
                    }
                }
            }), false);

            RegisterCommand("teleport", new Action<int, List<object>, string>((source, args, raw) => 
            {
                string sender = "[Teleport]";

                if (args.Count == 0)
                {
                    Vector3 coords;
                    coords.X = 1168;
                    coords.Y = -760;
                    coords.Z = 57;

                    teleport(coords, 270);
                }
                else
                {
                    string arg0 = args[0].ToString();
                    if (arg0 == "road")
                    {
                        Vector3 position = GetEntityCoords(PlayerPedId(), false);
                        teleport_to_nearest_major_road(position);
                    }
                    else if (arg0 == "auto")
                    {
                        autoReset = !autoReset;
                        ChatMessage(sender, $"autoReset == {autoReset}");
                    }
                    else if (arg0 == "water")
                    {
                        waterTeleport = !waterTeleport;
                        ChatMessage(sender, $"waterTeleport == {waterTeleport}");
                    }
                }
            }), false);

            if (ljbwCity)
            {
                RegisterCommand("time", new Action<int, List<object>, string>((source, args, raw) =>
                {
                    string sender = "[Time]";
                    if (args.Count == 0)
                    {
                        ChatMessage(sender, $"{GetClockHours()}:{GetClockMinutes()}:{GetClockSeconds()}");
                    }
                    else if (args[0].ToString() == "pause")
                    {
                        h = GetClockHours();
                        m = 0;
                        sec = 0;
                        TriggerServerEvent("ljbw:timeChangeRequest", h, true);
                    }
                    else if (args[0].ToString() == "resume" || args[0].ToString() == "unpause")
                    {
                        TriggerServerEvent("ljbw:timeChangeRequest", GetClockHours(), false);
                    }
                    else if (int.Parse(args[0].ToString()) < 25) // Change the time by sending a single number to the command
                    {
                        ChatMessage(sender, "Triggering ljbw:timeChangeRequest on server");
                        TriggerServerEvent("ljbw:timeChangeRequest", int.Parse(args[0].ToString()), timePaused);
                    }
                    else
                    {
                        ChatMessage(sender, $"args.Count == {args.Count}");
                    }

                }), false);
            }

            RegisterCommand("veh", new Action<int, List<object>, string>(async (source, args, raw) =>
            {
                string sender = "[VehHandler]";
                if (args.Count == 0)
                {
                    ChatMessage(sender, "inv | fix | lock | <vehicle-name>");
                }
                else
                {
                    string arg0 = args[0].ToString();
                    var hash = (uint)GetHashKey(arg0);

                    if (arg0 == "inv")
                    {
                        vehicleInvincible = !vehicleInvincible;

                        if (IsPedInAnyVehicle(PlayerPedId(), false))
                        {
                            int vehicle = GetVehiclePedIsIn(PlayerPedId(), false);

                            SetVehicleInvincible(vehicle, vehicleInvincible);
                        }

                        ChatMessage(sender, $"Setting vehicleInvincible to {vehicleInvincible}");
                    }
                    else if (arg0 == "fix")
                    {
                        if (IsPedInAnyVehicle(PlayerPedId(), true))
                        {
                            int current_vehicle = GetVehiclePedIsIn(PlayerPedId(), true);
                            SetVehicleFixed(current_vehicle);
                        }
                    }
                    else if (arg0 == "lock")
                    {
                        vehicleLocked = !vehicleLocked;
                        if (IsPedInAnyVehicle(PlayerPedId(), true))
                        {
                            if (vehicleLocked)
                            {
                                SetVehicleDoorsLocked(GetVehiclePedIsIn(PlayerPedId(), true), 2);
                                ChatMessage(sender, "locking veh");
                            }
                            else
                            {
                                SetVehicleDoorsLocked(GetVehiclePedIsIn(PlayerPedId(), true), 1);
                                ChatMessage(sender, "unlocking veh");
                            }
                        }
                    }
                    else if (arg0 == "del")
                    {
                        int vehicle = GetVehiclePedIsIn(PlayerPedId(), true);
                        SetEntityAsMissionEntity(vehicle, true, true);
                        DeleteVehicle(ref vehicle);
                    }
                    else if (IsModelInCdimage(hash) && IsModelAVehicle(hash))
                    {
                        current_vehicle_hash = hash;
                        int current_vehicle = 0;
                        Vector3 position; position.X = 0; position.Y = 0; position.Z = 0;
                        float heading = 0;
                        float speed = 0;
                        bool isEngineRunning = false;
                        int[] occupants = { 0, 0, 0, 0, 0, 0, 0, 0 };
                        if (IsPedInAnyVehicle(PlayerPedId(), false))
                        {
                            current_vehicle = GetVehiclePedIsIn(PlayerPedId(), true);
                            position = GetEntityCoords(current_vehicle, false);
                            heading = GetEntityHeading(current_vehicle);
                            speed = GetEntitySpeed(current_vehicle);
                            isEngineRunning = GetIsVehicleEngineRunning(current_vehicle);
                            ChatMessage(sender, $"isEngineRunning == {isEngineRunning}");

                            for (int i = -1; i <=5; i++)
                            {
                                occupants[i + 1] = GetPedInVehicleSeat(current_vehicle, i);
                                ChatMessage(sender, $"ped {occupants[i + 1]} is in seat {i}");
                            }

                            SetEntityAsMissionEntity(current_vehicle, true, true);
                            DeleteVehicle(ref current_vehicle);
                        }

                        var vehicle = await World.CreateVehicle(arg0, Game.PlayerPed.Position, Game.PlayerPed.Heading); // spawns vehicle in PLAYER's position not prev veh's
                        Game.PlayerPed.SetIntoVehicle(vehicle, VehicleSeat.Driver);

                        int new_vehicle = GetVehiclePedIsIn(PlayerPedId(), false);
                        ChatMessage(sender, $"playerPedId {PlayerPedId()}; playerId {PlayerId()}");

                        for (int i = -1; i <= 5; i++)
                        {
                            if (IsVehicleSeatFree(new_vehicle, i))
                            {
                                SetPedIntoVehicle(occupants[i + 1], new_vehicle, i);
                                ChatMessage(sender, $"Setting {occupants[i + 1]} into seat {i}");
                            }
                        }

                        SetVehicleForwardSpeed(new_vehicle, speed);
                        SetVehicleEngineOn(new_vehicle, isEngineRunning, true, false);

                        if (vehicleInvincible) { SetVehicleInvincible(new_vehicle, true); }
                        if (vehicleLocked) { SetVehicleDoorsLocked(new_vehicle, 2); }

                        ChatMessage(sender, $"^*{arg0} spawned");
                    }
                    else
                    {
                        ChatMessage(sender, $"Hash of {arg0} either not found or not a vehicle");
                        return;
                    }
                }
            }), false);

            string[] weapon_names = { "knife", "nightstick", "hammer", "bat", "golfclub", "crowbar", "pistol", "combatpistol", "appistol", "pistol5", "microsmg",
                "smg", "assaultsmg", "assaultrifle", "carbinerifle", "advancedrifle", "mg", "combatmg", "pumpshotgun", "sawnoffshotgun", "assaultshotgun",
                "bullpupshotgun", "stungun", "sniperrifle", "heavysniper", "grenadelauncher", "grenadelauncher_smoke", "rpg", "minigun", "grenade", "stickybomb",
                "smokegrenade", "bzgas", "molotov", "fireextinguisher", "petrolcan", "snspistol", "specialcarbine", "heavypistol", "bullpuprifle", "hominglauncher",
                "proxmine", "snowball", "vintagepistol", "dagger", "firework", "musket", "marksmanfifle", "heavyshotgun", "gusenberg", "hatchet", "railgun", "combatpdw",
                "knuckle", "marksmanpistol", "switchblade", "revolver" };

            RegisterCommand("weapon", new Action<int, List<object>, string>((source, args, raw) =>
            {
                string sender = "[WeaponHandler]";
                int playerPedId = PlayerPedId();
                if (args.Count == 0)
                {
                    foreach (string name in weapon_names)
                    {
                        string weapon_name = "WEAPON_" + name.ToUpper();
                        uint hash = (uint)GetHashKey(weapon_name);
                        GiveWeaponToPed(playerPedId, hash, 1000, false, false);
                    }
                    ChatMessage(sender, "Gave player all weapons with max ammo");
                }
                else
                {
                    string arg0 = args[0].ToString();
                    arg0 = "WEAPON_" + arg0.ToUpper();
                    //int arg1 = int.Parse(args[1].ToString());
                    uint hash = (uint)GetHashKey(arg0);
                    if (IsWeaponValid(hash))
                    {
                        GiveWeaponToPed(playerPedId, hash, 1000, false, false);
                        ChatMessage(sender, $"Giving player {arg0} with max ammo");
                    }
                    else
                    {
                        ChatMessage(sender, $"Invalid weapon: {arg0}");
                    }
                }
            }), false);

            if (ljbwCity)
            {
                SetWeatherOwnedByNetwork(false);
                RegisterCommand("weather", new Action<int, List<object>, string>((source, args, raw) => 
                {
                    string sender = "[WeatherClient]";

                    if (args.Count > 0)
                    {
                        string arg0 = args[0].ToString();
                        if (arg0 == "server")
                        {
                            SetWeatherOwnedByNetwork(true);
                        }
                        else
                        {
                            ChatMessage(sender, $"Triggering ljbw:weatherChangeRequest with {arg0}");
                            TriggerServerEvent("ljbw:weatherChangeRequest", arg0);
                        }
                    }
                    else
                    {
                        ChatMessage(sender, "blizzard clear clearing clouds extrasunny foggy halloween neutral overcast rain smog snow snowlight thunder xmas");
                    }
                }), false );
            }
        }

        static Random rng = new Random();
        static int releaseWfromChatTime = 0;
        static int releaseAfromChatTime = 0;
        static int releaseSfromChatTime = 0;
        static int releaseDfromChatTime = 0;
        static int releasePfromChatTime = 0;

        static int eLastPressedByChatTime = 0;
        static int eCount = 0;
        static int mLastPressedByChatTime = 0;
        static int mCount = 0;

        static int releaseControlFromChatTime = 0;
        static int chatControl = 0;

        static bool submerged = false;

        private void OnBotMessage(byte commandNumber, byte arg1, byte arg2, byte arg3)
        {
            if (log_bot_msgs)
            {
                ChatMessage("[ChatCommand]", $"chat command: {commandNumber}, arg1: {arg1}");
            }

            if (commandNumber == 0) { return; }

            switch ((botToModMessage)commandNumber)
            {
                case botToModMessage.supervised_model_resumed:
                    ChatMessage("[ChatCommand]", "supervised model resumed");

                    modelOutput = true;
                    autoReset = true;
                    waterTeleport = true;

                    break;
                case botToModMessage.model_paused:
                    ChatMessage("[ChatCommand]", "model paused");
                    SetControlNormal(0, (int)Control.VehicleAccelerate, 0);
                    SetControlNormal(0, (int)Control.VehicleBrake, 0);
                    SetControlNormal(0, (int)Control.VehicleMoveLeftRight, 0);

                    modelOutput = false;
                    autoReset = false;
                    waterTeleport = false;

                    accel = 0;
                    brake = 0;
                    turn = 0;

                    break;
                case botToModMessage.paint_random:
                    if (playerIsInVehicle)
                    {
                        int vehicle = GetVehiclePedIsIn(PlayerPedId(), false);

                        int r = rng.Next(0, 256);
                        int g = rng.Next(0, 256);
                        int b = rng.Next(0, 256);
                        SetVehicleCustomPrimaryColour(vehicle, r, g, b);
                        
                        r = rng.Next(0, 256);
                        g = rng.Next(0, 256);
                        b = rng.Next(0, 256);
                        SetVehicleCustomSecondaryColour(vehicle, r, g, b);
                    }
                    break;
                case botToModMessage.paint_colour:
                    if (playerIsInVehicle)
                    {
                        int vehicle = GetVehiclePedIsIn(PlayerPedId(), false);

                        SetVehicleCustomPrimaryColour(vehicle, arg1, arg2, arg3);
                        SetVehicleCustomSecondaryColour(vehicle, arg1, arg2, arg3);
                    }
                    break;
                case botToModMessage.reset:
                    if (playerIsInVehicle)
                    {
                        int vehicle = GetVehiclePedIsIn(PlayerPedId(), false);

                        teleport_to_nearest_major_road(GetEntityCoords(vehicle, false));
                    }
                    break;
                case botToModMessage.new_area:
                    if (playerIsInVehicle)
                    {
                        Vector3 pos = GetEntityCoords(PlayerPedId(), false);
                        Vector3 randomVehicleNode; randomVehicleNode.X = 0; randomVehicleNode.Y = 0; randomVehicleNode.Z = 0;
                        int nodeID = 0;

                        GetRandomVehicleNode(pos.X, pos.Y, pos.Z, 2000.0f, false, false, false, ref randomVehicleNode, ref nodeID);

                        teleport_to_nearest_major_road(randomVehicleNode);
                    }
                    break;
                case botToModMessage.fix:
                    if (playerIsInVehicle)
                    {
                        SetVehicleFixed(GetVehiclePedIsUsing(PlayerPedId()));
                    }
                    break;
                case botToModMessage.clean:
                    if (playerIsInVehicle)
                    {
                        SetVehicleDirtLevel(GetVehiclePedIsUsing(PlayerPedId()), 0.0f);
                    }
                    break;
                case botToModMessage.chat_key:
                    if (playerIsInVehicle)
                    {
                        int tickCount = Environment.TickCount;
                        switch (arg1)
                        {
                            case 0:
                                if (releaseWfromChatTime < Environment.TickCount) // chat last pressed w more than 1 sec ago
                                {
                                    releaseWfromChatTime = Environment.TickCount + 1000;
                                }
                                else // w is still being held down for chat
                                {
                                    releaseWfromChatTime += 1000;
                                }
                                break;
                            case 1:
                                if (releaseAfromChatTime < Environment.TickCount)
                                {
                                    releaseAfromChatTime = Environment.TickCount + 1000;
                                }
                                else
                                {
                                    releaseAfromChatTime += 1000;
                                }
                                break;
                            case 2:
                                if (releaseSfromChatTime < Environment.TickCount)
                                {
                                    releaseSfromChatTime = Environment.TickCount + 1000;
                                }
                                else
                                {
                                    releaseSfromChatTime += 1000;
                                }
                                break;
                            case 3:
                                if (releaseDfromChatTime < Environment.TickCount)
                                {
                                    releaseDfromChatTime = Environment.TickCount + 1000;
                                }
                                else
                                {
                                    releaseDfromChatTime += 1000;
                                }
                                break;
                            case 4:
                                eCount += 1;
                                break;
                            case 5:
                                releasePfromChatTime = tickCount + 1000; // No need to keep adding time for this one
                                break;
                            case 6:
                                mCount += 1;
                                break;
                        }
                    }
                    break;
                case botToModMessage.starting_position:
                    byte[] byteArray = new byte[13];
                    byteArray[0] = (byte)modToBotMessage.starting_position;

                    if (starting_position.X == 0 && starting_position.Y == 0 && starting_position.Z == 0) // mod has been restarted
                    {
                        starting_position = GetEntityCoords(PlayerPedId(), false);
                    }

                    BitConverter.GetBytes(starting_position.X).CopyTo(byteArray, 1);
                    BitConverter.GetBytes(starting_position.Y).CopyTo(byteArray, 5);
                    BitConverter.GetBytes(starting_position.Z).CopyTo(byteArray, 9);

                    TriggerServerEvent("ljbw:byteArray", byteArray);
                    break;
                case botToModMessage.current_position:
                    byteArray = new byte[17];
                    byteArray[0] = (byte)modToBotMessage.current_position;

                    Vector3 veh_pos = GetEntityCoords(GetVehiclePedIsUsing(PlayerPedId()), true);
                    float send_heading = GetEntityHeading(GetVehiclePedIsUsing(PlayerPedId()));

                    BitConverter.GetBytes(veh_pos.X).CopyTo(byteArray, 1);
                    BitConverter.GetBytes(veh_pos.Y).CopyTo(byteArray, 5);
                    BitConverter.GetBytes(veh_pos.Z).CopyTo(byteArray, 9);
                    BitConverter.GetBytes(send_heading).CopyTo(byteArray, 13);

                    TriggerServerEvent("ljbw:byteArray", byteArray);
                    break;
                case botToModMessage.weather:
                    TriggerServerEvent("ljbw:weatherChangeRequest", weatherNames[arg1]);
                    break;
            }
        }

        void OnByteArray(byte[] byteArray)
        {
            string sender = "[OnByteArray]";
            botToModMessage commandNumber = (botToModMessage)byteArray[0];

            if (log_bot_msgs) { ChatMessage(sender, $"commandNumber == {commandNumber}"); }

            byte[] sendArray; // = new byte[script_to_mod_array_length];  // Has to stay 17 I think

            // Beware of this situation with switch cases. Each case is unfortunately not its own scope.
            // A new scope can be created with { ... }.
            //int y = 0;
            //switch(x)
            //{
            //    case 0:
            //        y = 1;  // <- "Can't use y before it's declared" here
            //        break;
            //    case 1:
            //        int y = 2; // Even though the actual problem is that you're redeclaring y here
            //        break;
            //}

            switch (commandNumber)
            {
                case botToModMessage.reinforcement_model_resumed:
                    ChatMessage(sender, "reinforcement model resumed");
                    modelOutput = true;
                    autoReset = false;
                    waterTeleport = true;
                    break;
                case botToModMessage.model_output:
                    {
                        // PROGRAMMING SUCKS
                        for (int i = 0; i < byteArray.Length - 1; i++)
                        {
                            byteArray[i] = byteArray[i + 1];
                        }

                        float w = BitConverter.ToSingle(byteArray, 0);
                        float a = BitConverter.ToSingle(byteArray, 4);
                        float s = BitConverter.ToSingle(byteArray, 8);
                        float d = BitConverter.ToSingle(byteArray, 12);

                        accel = w;
                        brake = s;

                        if (d > a)
                        {
                            turn = d;
                        }
                        else // a > d
                        {
                            turn = -a;
                        }
                        break;
                    }
                case botToModMessage.set_control:
                    chatControl = BitConverter.ToInt32(byteArray, 12);
                    ChatMessage("[OnByteArray]", $"Setting control {chatControl}");
                    if (releaseControlFromChatTime < Environment.TickCount)
                    {
                        releaseControlFromChatTime = Environment.TickCount + 1000;
                    }
                    else
                    {
                        releaseControlFromChatTime += 1000;
                    }
                    break;
                case botToModMessage.veh:
                    ChatMessage("[OnByteArray]", "spawning " + vehicleNames[byteArray[13]]);
                    ChatMessage("[OnByteArray]", $"{byteArray[12]} {byteArray[13]} {byteArray[14]} {byteArray[15]} {byteArray[16]}");
                    ChatMessage(sender, vehicleNames[byteArray[13]]);
                    TriggerEvent("ljbw:setVehicleRequest", vehicleNames[byteArray[13]]);
                    break;
                case botToModMessage.teleport:
                    if (byteArray[4] == 0 && byteArray[5] == 0 && byteArray[6] == 0 && byteArray[7] == 0)
                    {
                        if (byteArray[13] == 0)
                        {
                            Vector3 coords;
                            coords.X = 1168;
                            coords.Y = -760;
                            coords.Z = 57;
                            teleport(coords, 270);
                        }
                        else if (byteArray[13] == 1)
                        {
                            waterTeleport = !waterTeleport;
                            ChatMessage("[ChatCommand]", $"waterTeleport == {waterTeleport}");
                        }
                    }
                    else
                    {
                        Vector3 coords;
                        coords.X = BitConverter.ToSingle(byteArray, 4);
                        coords.Y = BitConverter.ToSingle(byteArray, 8);
                        coords.Z = BitConverter.ToSingle(byteArray, 12);
                        teleport(coords);
                    }
                    break;
                case botToModMessage.get_heading:
                    float sendHeading = GetEntityHeading(GetVehiclePedIsUsing(PlayerPedId()));
                    //Console.WriteLine(sendArray[0]);
                    sendArray = new byte[mod_to_script_array_length];
                    sendArray[0] = (byte)modToBotMessage.current_heading;
                    BitConverter.GetBytes(sendHeading).CopyTo(sendArray, 1);
                    TriggerServerEvent("ljbw:byteArray", sendArray);
                    break;
                case botToModMessage.set_heading:
                    float heading = BitConverter.ToSingle(byteArray, 12);
                    SetEntityHeading(GetVehiclePedIsUsing(PlayerPedId()), heading);
                    SetGameplayCamRelativeHeading(0);
                    break;
                case botToModMessage.reinforcement_learning_resumed:
                    ChatMessage(sender, "reinforcement learning resumed");
                    modelOutput = true;
                    autoReset = false;
                    waterTeleport = true;
                    accel = 0;
                    brake = 0;
                    turn = 0;
                    break;
                case botToModMessage.reinforcement_learning_paused:
                    ChatMessage(sender, "reinforcement learning paused");
                    modelOutput = false;
                    waterTeleport = false;
                    break;
                case botToModMessage.action:
                    {
                        // First byte contains enum value
                        float w = BitConverter.ToSingle(byteArray, 1);
                        float a = BitConverter.ToSingle(byteArray, 5);
                        float s = BitConverter.ToSingle(byteArray, 9);
                        float d = BitConverter.ToSingle(byteArray, 13);

                        accel = w;
                        brake = s;

                        // This happens to also work for discrete wasd values
                        if (d > a)
                        {
                            turn = d;
                        }
                        else // a > d
                        {
                            turn = -a;
                        }

                        // Now send state back to model script
                        sendArray = new byte[mod_to_script_array_length];
                        sendArray[0] = (byte)modToBotMessage.state;

                        int vehicle = GetVehiclePedIsUsing(PlayerPedId());
                        Vector3 rel_vel = GetEntitySpeedVector(vehicle, true); // relative vel seems to be relative to pos & heading on previous game tick or something
                        float forward_speed = rel_vel.Y;

                        byte[] materials = new byte[4];
                        for (int i = 0; i < 4; i++)
                        {
                            int surface_mat = GetVehicleWheelSurfaceMaterial(vehicle, i);
                            try
                            {
                                materials[i] = (byte)surface_mat;
                            }
                            catch
                            {
                                ChatMessage("[action]", "material no. " + surface_mat.ToString());
                            }
                        }

                        BitConverter.GetBytes(forward_speed).CopyTo(sendArray, 1);
                        materials.CopyTo(sendArray, 5);

                        if (submerged)
                        {
                            sendArray[9] = 1;
                            submerged = false;
                        }

                        TriggerServerEvent("ljbw:byteArray", sendArray);
                        break;
                    }
                case botToModMessage.state:
                    {
                        sendArray = new byte[mod_to_script_array_length];
                        sendArray[0] = (byte)modToBotMessage.state;

                        int veh = GetVehiclePedIsUsing(PlayerPedId());

                        float veh_heading = GetEntityHeading(veh);
                        Vector3 veh_pos = GetEntityCoords(veh, true);
                        Vector3 veh_vel = GetEntityVelocity(veh);

                        BitConverter.GetBytes(veh_heading).CopyTo(sendArray, 1);
                        BitConverter.GetBytes(veh_pos.X).CopyTo(sendArray, 5);
                        BitConverter.GetBytes(veh_pos.Y).CopyTo(sendArray, 9);
                        BitConverter.GetBytes(veh_pos.Z).CopyTo(sendArray, 13);
                        BitConverter.GetBytes(veh_vel.X).CopyTo(sendArray, 17);
                        BitConverter.GetBytes(veh_vel.Y).CopyTo(sendArray, 21);
                        BitConverter.GetBytes(veh_vel.Z).CopyTo(sendArray, 25);

                        TriggerServerEvent("ljbw:byteArray", sendArray);
                        break;
                    }
            }
        }

        bool modelOutput = false;
        float accel, brake, turn;

        private void OnModelOutput(float w, float a, float s, float d)
        {
            accel = w;
            brake = s;

            if (d > a)
            {
                turn = d;
            }
            else // a > d
            {
                turn = -a;
            }
        }

        private void OnTimeChangeRequest(int hours, bool paused)
        {
            h = hours;
            timePaused = paused;
            NetworkOverrideClockTime(hours, 0, 0);
        }

        private void OnWeatherChangeRequest(string weatherName)
        {
            string sender = "[WeatherChanger]";
            SetOverrideWeather(weatherName);
            ChatMessage(sender, $"Changing weather to {weatherName}");
        }

        Vector3 pos;
        Vector3 prevTeleport;
        
        [Tick]
        public Task OnTick()
        {
            string sender = "[Tick]";
            int tickCount = Environment.TickCount;
            int playerId = PlayerId();
            //int playerPed = GetPlayerPed(0);
            int playerPedId = PlayerPedId();
            playerIsInVehicle = IsPedInAnyVehicle(playerPedId, false);

            if (playerCannnotBeWanted)
            {
                SetPlayerWantedLevel(playerId, 0, false);
                SetPlayerWantedLevelNow(playerId, false);
            }

            if (!prevPlayerInvincible && playerInvincible)
            {
                SetEntityInvincible(playerPedId, true);
            }
            else if (prevPlayerInvincible && !playerInvincible)
            {
                SetEntityInvincible(playerPedId, false);
            }

            if (timePaused)
            {
                NetworkOverrideClockTime(h, m, sec);
            }

            if (IsPedInAnyVehicle(playerPedId, false))
            {
                int vehicle = GetVehiclePedIsIn(playerPedId, false);

                if (vehicleInvincible)
                {
                    if (!AreAllVehicleWindowsIntact(vehicle))
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            FixVehicleWindow(vehicle, i);
                        }
                    }
                }

                pos = GetEntityCoords(playerPedId, false);
                pos_buffer[pos_index] = pos;
                pos_index++;
                pos_index %= pos_buffer_length;

                if (autoReset)
                {
                    Vector3 recent_pos = pos_buffer[(pos_buffer_length + pos_index - 60 * 5) % pos_buffer_length];
                    Vector3 least_recent_pos = pos_buffer[(pos_index + 1) % pos_buffer_length];

                    if ((vector3_distance(pos, recent_pos) < 1.0) && (GetEntitySpeed(vehicle) < 1.0) && (vector3_distance(pos, prevTeleport) > 1.0))
                    {
                        teleport_to_nearest_major_road(pos);
                        prevTeleport = pos;
                    }
                    else if ((vector3_distance(pos, least_recent_pos) < 1.0) && (GetEntitySpeed(vehicle) < 1.0))
                    {
                        Vector3 randomVehicleNode; randomVehicleNode.X = 0; randomVehicleNode.Y = 0; randomVehicleNode.Z = 0;
                        int nodeID = 0;

                        GetRandomVehicleNode(pos.X, pos.Y, pos.Z, 2000.0f, false, false, false, ref randomVehicleNode, ref nodeID);

                        teleport_to_nearest_major_road(randomVehicleNode); // teleport to new area if we haven't moved in 30s
                    }
                }

                if (GetEntitySubmergedLevel(vehicle) > 0.36)
                {
                    if (waterTeleport)
                    {
                        teleport_to_nearest_major_road(pos);
                        submerged = true;
                    }
                    else
                    {
                        if (GetDisplayNameFromVehicleModel((uint)GetEntityModel(vehicle)) != "APC")
                        {
                            TriggerEvent("ljbw:setVehicleRequest", "apc");
                        }
                    }
                }

                //int angryPed = GetPedUsingVehicleDoor(vehicle, (int)VehicleDoorIndex.FrontLeftDoor);
                //if (angryPed != 0)
                //{
                    //SendChatMessage("[OnTick]", $"angryPed {angryPed}");
                    //SetEntityHasGravity(angryPed, false);
                    //SetEntityDynamic(angryPed, true);
                    //SetPedToRagdoll(angryPed, 1000, 1000, 2, true, true, true);
                    //SetPedRagdollForceFall(angryPed);
                    //SetPedCanRagdoll(angryPed, false);
                    // ActivePhysics from PHYSICS namespace maybe?
                //}

                //int material = GetVehicleWheelSurfaceMaterial(vehicle, 0);
                //BeginTextCommandDisplayText("w0");
                //AddTextComponentInteger(material);
                //EndTextCommandDisplayText(0.5f, 0.5f);

                //int material;
                //for (int i = 0; i < 4; i++)
                //{
                //    material = GetVehicleWheelSurfaceMaterial(vehicle, i);
                //    BeginTextCommandDisplayText("w" + i.ToString());
                //    AddTextComponentInteger(material);
                //    EndTextCommandDisplayText(0.3f + 0.1f * (float)i, 0.5f);
                //}
            }

            if (!prevPlayerIsInVehicle && playerIsInVehicle)
            {
                if (vehicleInvincible)
                {
                    SetVehicleInvincible(GetVehiclePedIsIn(playerPedId, false), true);
                }

                if (vehicleLocked)
                {
                    int prevVehicle = GetVehiclePedIsIn(playerPedId, true);
                    SetVehicleDoorsLocked(prevVehicle, 2);
                    //SendChatMessage(sender, "locking veh on veh entry");
                }
            }
            else if (prevPlayerIsInVehicle && !playerIsInVehicle)
            {
                int prevVehicle = GetVehiclePedIsIn(playerPedId, true);

                if (vehicleLocked)
                {
                    SetVehicleDoorsLocked(prevVehicle, 1);
                    //SendChatMessage(sender, "unlocking veh on veh exit");
                }

                SetVehicleAsNoLongerNeeded(ref prevVehicle);
            }

            if (modelOutput)
            {
                SetControlNormal(0, (int)Control.VehicleAccelerate, accel);
                SetControlNormal(0, (int)Control.VehicleBrake, brake);
                SetControlNormal(0, (int)Control.VehicleMoveLeftRight, turn);
            }

            if (Environment.TickCount < releaseWfromChatTime)
            {
                SetControlNormal(0, (int)Control.VehicleAccelerate, 1.0f);
            }
            if (Environment.TickCount < releaseSfromChatTime)
            {
                SetControlNormal(0, (int)Control.VehicleBrake, 1.0f);
            }
            if (Environment.TickCount < releaseAfromChatTime && Environment.TickCount >= releaseDfromChatTime)
            {
                SetControlNormal(0, (int)Control.VehicleMoveLeftRight, -1.0f);
            }
            else if (Environment.TickCount >= releaseAfromChatTime && Environment.TickCount < releaseDfromChatTime)
            {
                SetControlNormal(0, (int)Control.VehicleMoveLeftRight, 1.0f);
            }
            else if (Environment.TickCount < releasePfromChatTime)
            {
                SetControlNormal(0, (int)Control.VehicleParachute, 1.0f);
            }
            else if (((Environment.TickCount - eLastPressedByChatTime) >= 1000) && eCount > 0)
            {
                eLastPressedByChatTime = Environment.TickCount;
                eCount -= 1;
                SetControlNormal(0, (int)Control.VehicleHorn, 1.0f);
                SetControlNormal(0, (int)Control.VehicleCarJump, 1.0f);
                SetVehicleRocketBoostPercentage(GetVehiclePedIsIn(playerPedId, false), 100);
                SetControlNormal(0, (int)Control.VehicleRocketBoost, 1.0f);
            }
            else if (((tickCount - mLastPressedByChatTime) >= 2000) && mCount > m)
            {
                mLastPressedByChatTime = tickCount;
                mCount -= 1;
                SetControlNormal(0, (int)Control.VehicleAim, 1.0f);
                SetControlNormal(0, (int)Control.VehicleAttack, 1.0f);
            }
            else if (Environment.TickCount < releaseControlFromChatTime)
            {
                SetControlNormal(0, chatControl, 1.0f);
            }

            prevPlayerInvincible = playerInvincible;
            prevPlayerIsInVehicle = playerIsInVehicle;

            return Task.FromResult(0);
        }
    }
}