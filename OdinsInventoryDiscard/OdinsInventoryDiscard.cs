using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace OdinsInventoryDiscard
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class OdinsInventoryDiscardPlugin : BaseUnityPlugin

    {
        internal const string ModName = "OdinsInventoryDiscard";
        internal const string ModVersion = "1.0.11";
        internal const string Author = "odinplus";
        private const string ModGUID = Author + "qol." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static Assembly epicLootAssembly;
        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource OdinsInventoryDiscardLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new(ModGUID)
            { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        private void Awake()
        {
            _serverConfigLocked = config("General", "Force Server Config", true, "Force Server Config");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            /* Discard Items in Inventory */
            discardInvEnabled =
                config("General", "Enabled", false, "Enable Inventory Discard (whole mod)");
            hotKey = config("Inventory Discard", "DiscardHotkey", new KeyboardShortcut(KeyCode.Delete),
                new ConfigDescription("The hotkey to discard an item", new AcceptableShortcuts()), false);
            returnUnknownResources = config("Inventory Discard", "ReturnUnknownResources", false,
                "Return resources if recipe is unknown");
            returnEnchantedResources = config("Inventory Discard", "ReturnEnchantedResources", false,
                "Return resources for Epic Loot enchantments");
            returnResources = config("Inventory Discard", "ReturnResources", 1f,
                "Fraction of resources to return (0.0 - 1.0)");

            _harmony.PatchAll();
            SetupWatcher();
        }

        private void Start()
        {
            if (!Chainloader.PluginInfos.ContainsKey("randyknapp.mods.epicloot")) return;
            epicLootAssembly = Chainloader.PluginInfos["randyknapp.mods.epicloot"].Instance.GetType().Assembly;
            OdinsInventoryDiscardLogger.LogDebug("Epic Loot found, providing compatibility");
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
                OdinsInventoryDiscardLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                OdinsInventoryDiscardLogger.LogError($"There was an issue loading your {ConfigFileName}");
                OdinsInventoryDiscardLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateItemDrag))]
        private static class UpdateItemDrag_Patch
        {
            private static void Postfix(InventoryGui __instance, ItemDrop.ItemData ___m_dragItem,
                Inventory ___m_dragInventory, int ___m_dragAmount, ref GameObject ___m_dragGo)
            {
                /*if (!discardInvEnabled.Value || !Input.GetKeyDown(hotKey.Value) || ___m_dragItem == null ||
                    !___m_dragInventory.ContainsItem(___m_dragItem))
                    return;*/
                if (!discardInvEnabled.Value || !hotKey.Value.IsDown() || ___m_dragItem == null ||
                    !___m_dragInventory.ContainsItem(___m_dragItem))
                    return;

                OdinsInventoryDiscardLogger.LogDebug(
                    $"Discarding {___m_dragAmount}/{___m_dragItem.m_stack} {___m_dragItem.m_dropPrefab.name}");

                if (returnResources.Value > 0)
                {
                    Recipe recipe = ObjectDB.instance.GetRecipe(___m_dragItem);

                    if (recipe != null && (returnUnknownResources.Value ||
                                           Player.m_localPlayer.IsRecipeKnown(___m_dragItem.m_shared.m_name)))
                    {
                        OdinsInventoryDiscardLogger.LogDebug(
                            $"Recipe stack: {recipe.m_amount} num of stacks: {___m_dragAmount / recipe.m_amount}");


                        List<Piece.Requirement>? reqs = recipe.m_resources.ToList();

                        bool isMagic = false;
                        bool cancel = false;
                        if (epicLootAssembly != null && returnEnchantedResources.Value)
                            isMagic = (bool)epicLootAssembly
                                .GetType("EpicLoot.ItemDataExtensions")
                                .GetMethod("IsMagic", BindingFlags.Public | BindingFlags.Static, null,
                                    new[] { typeof(ItemDrop.ItemData) }, null)?.Invoke(null, new[] { ___m_dragItem });
                        if (isMagic)
                        {
                            int rarity = (int)epicLootAssembly
                                ?.GetType("EpicLoot.ItemDataExtensions")
                                .GetMethod("GetRarity", BindingFlags.Public | BindingFlags.Static)
                                ?.Invoke(null, new[] { ___m_dragItem });
                            List<KeyValuePair<ItemDrop, int>> magicReqs =
                                (List<KeyValuePair<ItemDrop, int>>)epicLootAssembly
                                    ?.GetType("EpicLoot.Crafting.EnchantTabController")
                                    .GetMethod("GetEnchantCosts", BindingFlags.Public | BindingFlags.Static)
                                    ?.Invoke(null, new object[] { ___m_dragItem, rarity });
                            foreach (KeyValuePair<ItemDrop, int> kvp in magicReqs)
                            {
                                if (!returnUnknownResources.Value &&
                                    (ObjectDB.instance.GetRecipe(kvp.Key.m_itemData) &&
                                     !Player.m_localPlayer.IsRecipeKnown(kvp.Key.m_itemData.m_shared.m_name) ||
                                     !Player.m_localPlayer.m_knownMaterial.Contains(kvp.Key.m_itemData.m_shared.m_name)))
                                {
                                    Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                                        "You don't know all the recipes for this item's materials.");
                                    return;
                                }

                                reqs.Add(new Piece.Requirement
                                {
                                    m_amount = kvp.Value,
                                    m_resItem = kvp.Key
                                });
                            }
                        }

                        if (!cancel && ___m_dragAmount / recipe.m_amount > 0)
                            for (int i = 0; i < ___m_dragAmount / recipe.m_amount; i++)
                                foreach (Piece.Requirement req in reqs)
                                {
                                    int quality = ___m_dragItem.m_quality;
                                    for (int j = quality; j > 0; j--)
                                    {
                                        GameObject prefab = ObjectDB.instance.m_items.FirstOrDefault(item =>
                                            item.GetComponent<ItemDrop>().m_itemData.m_shared.m_name ==
                                            req.m_resItem.m_itemData.m_shared.m_name)!;
                                        ItemDrop.ItemData newItem = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
                                        int numToAdd = Mathf.RoundToInt(req.GetAmount(j) * returnResources.Value);
                                        OdinsInventoryDiscardLogger.LogDebug(
                                            ($"Returning {numToAdd}/{req.GetAmount(j)} {prefab.name}"));
                                        while (numToAdd > 0)
                                        {
                                            int stack = Mathf.Min(req.m_resItem.m_itemData.m_shared.m_maxStackSize,
                                                numToAdd);
                                            numToAdd -= stack;

                                            if (Player.m_localPlayer.GetInventory().AddItem(prefab.name, stack,
                                                    req.m_resItem.m_itemData.m_quality,
                                                    req.m_resItem.m_itemData.m_variant,
                                                    0, "") == null)
                                            {
                                                Transform transform1;
                                                ItemDrop component = Instantiate(prefab,
                                                    (transform1 = Player.m_localPlayer.transform).position +
                                                    transform1.forward +
                                                    transform1.up,
                                                    transform1.rotation).GetComponent<ItemDrop>();
                                                component.m_itemData = newItem;
                                                component.m_itemData.m_dropPrefab = prefab;
                                                component.m_itemData.m_stack = stack;
                                                component.Save();
                                            }
                                        }
                                    }
                                }
                    }
                }

                if (___m_dragAmount == ___m_dragItem.m_stack)
                {
                    Player.m_localPlayer.RemoveEquipAction(___m_dragItem);
                    Player.m_localPlayer.UnequipItem(___m_dragItem, false);
                    ___m_dragInventory.RemoveItem(___m_dragItem);
                }
                else
                {
                    ___m_dragInventory.RemoveItem(___m_dragItem, ___m_dragAmount);
                }

                Destroy(___m_dragGo);
                ___m_dragGo = null;
                __instance.UpdateCraftingPanel(false);
            }
        }


        #region ConfigOptions

        private static ConfigEntry<bool>? _serverConfigLocked;
        public static ConfigEntry<KeyboardShortcut> hotKey;
        public static ConfigEntry<bool> discardInvEnabled;
        public static ConfigEntry<bool> returnUnknownResources;
        public static ConfigEntry<bool> returnEnchantedResources;
        public static ConfigEntry<float> returnResources;

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