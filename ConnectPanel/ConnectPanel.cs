using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace Connect_Panel
{
    [BepInPlugin(ModGuid, ModName, Version)]
    public class ConnectionPanelPlugin : BaseUnityPlugin
    {
        public const string Version = "1.0.0";
        public const string ModName = "ConnectPanel";
        internal const string Author = "odinplus";
        private const string ModGuid = Author + "qol." + ModName;
        private static string _configFileName = ModGuid + ".cfg";
        private static string _configFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + _configFileName;

        public static readonly ManualLogSource ConnectPanelLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private readonly Harmony _harmony = new(ModGuid);

        public void Awake()
        {
            /* Connect Panel */
            _serverAdditionToggle = Config.Bind("Connection Panel", "Enable Connection Panel", false,
                "This option, if enabled, will add the servers listed below to the Join Game panel on the main menu.");
            _serverIPs = Config.Bind("Connection Panel", "This is the IP for your server",
                "111.111.111.11,222.222.222.22", "This is the IP for your server. Separate each option by a comma.");
            _serverNames = Config.Bind("Connection Panel", "Name of the server",
                "<color=#6600cc>TEST EXAMPLE</color>, Test Example 2",
                "This is how your server shows in the list, can use colors. Separate each option by a comma.");
            _serverPorts = Config.Bind("Connection Panel",
                "The Port For your Server. Separate each option by a comma.", "28200,28300", "Port For server");
            SetupWatcher();
            _harmony.PatchAll();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, _configFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(_configFileFullPath)) return;
            try
            {
                ConnectPanelLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                ConnectPanelLogger.LogError($"There was an issue loading your {_configFileName}");
                ConnectPanelLogger.LogError("Please check your config entries for spelling and format!");
            }
        }

        [HarmonyPatch(typeof(ZSteamMatchmaking), nameof(ZSteamMatchmaking.GetServers))]
        static class OdinQOL_ZSteamMatchmakingGetServersPatch
        {
            static void Postfix(ZSteamMatchmaking __instance, ref List<ServerData> allServers)
            {
                if (!_serverAdditionToggle.Value) return;
                string[] serversArray = _serverIPs.Value.Trim().Split(',').ToArray();
                string[] serversNamesArray = _serverNames.Value.Trim().Split(',').ToArray();
                string[] serversPortsArray = _serverPorts.Value.Trim().Split(',').ToArray();
                int i = 0;
                if (serversArray.Length == serversNamesArray.Length &&
                    serversArray.Length == serversPortsArray.Length)
                    try
                    {
                        foreach (string serv in serversArray)
                        {
                            ServerData serverData = new()
                            {
                                m_host = serv,
                                m_name = serversNamesArray[i],
                                m_password = false,
                                m_players = 999,
                                m_port = int.Parse(serversPortsArray[i]),
                                m_steamHostID = 0uL,
                                m_steamHostAddr = default
                            };
                            serverData.m_steamHostAddr.ParseString(serverData.m_host + ":" + serverData.m_port);
                            serverData.m_upnp = true;
                            serverData.m_version = "";
                            allServers.Insert(0, serverData);
                            ++i;
                        }
                    }
                    catch (Exception exception)
                    {
                        ConnectPanelLogger.LogError(
                            $"There was an issue adding your server listing to the menu. Please check your [Connection Panel] section in the config file for correct length and format {exception}");
                    }
                else
                    ConnectPanelLogger.LogError(
                        "Server IPs, Ports, or Names are not the same length or in an incorrect format. Please Check your [Connection Panel] section in the config and fix the issue.");
            }
        }

        private static ConfigEntry<bool>? _serverAdditionToggle;
        private static ConfigEntry<string>? _serverIPs;
        private static ConfigEntry<string>? _serverNames;
        private static ConfigEntry<string>? _serverPorts;
    }
}