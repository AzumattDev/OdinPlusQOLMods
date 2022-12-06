using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace OdinsExtendedInventory
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency("aedenthorn.MovableChestInventory", BepInDependency.DependencyFlags.SoftDependency)]
    public class OdinsExtendedInventoryPlugin : BaseUnityPlugin

    {
        internal const string ModName = "OdinsExtendedInventory";
        internal const string ModVersion = "3.0.3";
        internal const string Author = "odinplus";
        private const string ModGUID = Author + "qol." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static bool moveableChestInventoryFound = false;

        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource OdinsExtendedInventoryLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new(ModGUID)
            { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };


        private void Awake()
        {
            _serverConfigLocked = config("General", "Force Server Config", true, "Force Server Config");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);
            ConfigSync.IsLocked = true;

            /* Extended Player Inventory Config options */
            modEnabled = config("General", "Enabled", true, "Enable the entire mod");
            //scrollableInventory = config("Extended Inventory", "Scrollable", true, "When this is on, the player's inventory is scrollable if it passes 6x8 size.");
            extraRows = config("Extended Inventory", "ExtraRows", 0,
                "Number of extra ordinary rows. (This can cause overlap with chest GUI, make sure you hold CTRL (the default key) and drag to desired position)");
            addEquipmentRow = config("Extended Inventory", "AddEquipmentRow", false,
                "Add special row for equipped items and quick slots. (IF YOU ARE USING RANDY KNAPPS EAQs KEEP THIS VALUE OFF)");
            displayEquipmentRowSeparate = config("Extended Inventory", "DisplayEquipmentRowSeparate",
                false,
                "Display equipment and quickslots in their own area. (IF YOU ARE USING RANDY KNAPPS EAQs KEEP THIS VALUE OFF)");

            helmetText = config("Extended Inventory", "HelmetText", "Head",
                "Text to show for helmet slot.", false);
            chestText = config("Extended Inventory", "ChestText", "Chest",
                "Text to show for chest slot.", false);
            legsText = config("Extended Inventory", "LegsText", "Legs",
                "Text to show for legs slot.", false);
            backText = config("Extended Inventory", "BackText", "Back",
                "Text to show for back slot.", false);
            utilityText = config("Extended Inventory", "UtilityText", "Utility",
                "Text to show for utility slot.", false);

            quickAccessScale = config("Extended Inventory", "QuickAccessScale", 1f,
                "Scale of quick access bar. ", false);

            hotKey1 = config("Extended Inventory", "HotKey1", KeyCode.Z,
                "Hotkey 1 - Use https://docs.unity3d.com/Manual/ConventionalGameInput.html", false);
            hotKey2 = config("Extended Inventory", "HotKey2", KeyCode.X,
                "Hotkey 2 - Use https://docs.unity3d.com/Manual/ConventionalGameInput.html", false);
            hotKey3 = config("Extended Inventory", "HotKey3", KeyCode.C,
                "Hotkey 3 - Use https://docs.unity3d.com/Manual/ConventionalGameInput.html", false);
            
            hotKey1Text = config("Extended Inventory", "HotKey1 Text", "",
                "Hotkey 1 Display Text. Leave blank to use the hotkey itself.", false);
            hotKey2Text = config("Extended Inventory", "HotKey2 Text", "",
                "Hotkey 2 Display Text. Leave blank to use the hotkey itself.", false);
            hotKey3Text = config("Extended Inventory", "HotKey3 Text", "",
                "Hotkey 3 Display Text. Leave blank to use the hotkey itself.", false);

            modKeyOne = config("Extended Inventory", "ModKey1", KeyCode.Mouse0,
                "First modifier key to move quick slots. Use https://docs.unity3d.com/Manual/ConventionalGameInput.html format.",
                false);
            modKeyTwo = config("Extended Inventory", "ModKey2", KeyCode.LeftControl,
                "Second modifier key to move quick slots. Use https://docs.unity3d.com/Manual/ConventionalGameInput.html format.",
                false);

            quickAccessX = config("Extended Inventory", "quickAccessX", 9999f,
                "Current X of Quick Slots", false);
            quickAccessY = config("Extended Inventory", "quickAccessY", 9999f,
                "Current Y of Quick Slots", false);

            /* Moveable Chest Inventory */
            chestInventoryX = config("Moveable Chest", "ChestInventoryX", -1f,
                "Current X of chest (Not Synced with server)\nNote, if you have aedenthorn's version of this, the config here will not work. Use her mod's config.",
                false);
            chestInventoryY = config("Moveable Chest", "ChestInventoryY", -1f,
                "Current Y of chest (Not Synced with server)\nNote, if you have aedenthorn's version of this, the config here will not work. Use her mod's config.",
                false);
            modKeyOneChestMove = config("Moveable Chest", "ModifierKeyOne", KeyCode.Mouse0,
                "First modifier key (to move the container). Use https://docs.unity3d.com/Manual/class-InputManager.html format.\nNote, if you have aedenthorn's version of this, the config here will not work. Use her mod's config.",
                false);
            modKeyTwoChestMove = config("Moveable Chest", "ModifierKeyTwo", KeyCode.LeftControl,
                "Second modifier key (to move the container). Use https://docs.unity3d.com/Manual/class-InputManager.html format.\nNote, if you have aedenthorn's version of this, the config here will not work. Use her mod's config.",
                false);


            hotkeys = new[]
            {
                hotKey1,
                hotKey2,
                hotKey3
            };
            
            hotkeyTexts = new[]
            {
                hotKey1Text,
                hotKey2Text,
                hotKey3Text
            };
            _harmony.PatchAll();
            SetupWatcher();
        }

        private void Start()
        {
            if (!Chainloader.PluginInfos.ContainsKey("aedenthorn.MovableChestInventory")) return;
            moveableChestInventoryFound = true;
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                OdinsExtendedInventoryLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                OdinsExtendedInventoryLogger.LogError($"There was an issue loading your {ConfigFileName}");
                OdinsExtendedInventoryLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        private static ConfigEntry<bool>? _serverConfigLocked;
        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> addEquipmentRow;
        public static ConfigEntry<bool> displayEquipmentRowSeparate;

        public static ConfigEntry<int> extraRows;
        //public static ConfigEntry<bool> scrollableInventory;

        public static ConfigEntry<string> helmetText;
        public static ConfigEntry<string> chestText;
        public static ConfigEntry<string> legsText;
        public static ConfigEntry<string> backText;
        public static ConfigEntry<string> utilityText;
        public static ConfigEntry<float> quickAccessScale;

        public static ConfigEntry<KeyCode> hotKey1;
        public static ConfigEntry<KeyCode> hotKey2;
        public static ConfigEntry<KeyCode> hotKey3;
        public static ConfigEntry<string> hotKey1Text;
        public static ConfigEntry<string> hotKey2Text;
        public static ConfigEntry<string> hotKey3Text;
        public static ConfigEntry<KeyCode> modKeyOne;
        public static ConfigEntry<KeyCode> modKeyTwo;

        public static ConfigEntry<KeyCode>[] hotkeys;
        public static ConfigEntry<string>[] hotkeyTexts;

        public static ConfigEntry<float> quickAccessX;
        public static ConfigEntry<float> quickAccessY;

        /* Moveable Chest configs*/
        public static ConfigEntry<float> chestInventoryX;
        public static ConfigEntry<float> chestInventoryY;
        public static ConfigEntry<KeyCode> modKeyOneChestMove;
        public static ConfigEntry<KeyCode> modKeyTwoChestMove;
        private static Vector3 lastMousePos;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            public bool? Browsable = false;
        }

        class AcceptableShortcuts : AcceptableValueBase // Used for KeyboardShortcut Configs 
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() =>
                "# Acceptable values: " + string.Join(", ", KeyboardShortcut.AllKeyCodes);
        }

        #endregion
    }
}