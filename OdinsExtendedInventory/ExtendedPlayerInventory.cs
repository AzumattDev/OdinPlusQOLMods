using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static OdinsExtendedInventory.OdinsExtendedInventoryPlugin;
using Object = UnityEngine.Object;

namespace OdinsExtendedInventory
{
    internal class ExtendedPlayerInventory
    {
        private static GameObject elementPrefab;

        private static readonly ItemDrop.ItemData.ItemType[] typeEnums =
        {
            ItemDrop.ItemData.ItemType.Helmet,
            ItemDrop.ItemData.ItemType.Chest,
            ItemDrop.ItemData.ItemType.Legs,
            ItemDrop.ItemData.ItemType.Shoulder,
            ItemDrop.ItemData.ItemType.Utility
        };

        private static ItemDrop.ItemData[] equipItems = new ItemDrop.ItemData[5];

        private static Vector3 lastMousePos;
        private static string currentlyDragging;

        public static void SetSlotText(string value, Transform transform, bool center = true)
        {
            Transform transform1 = transform.Find("binding");
            if (!transform1)
                transform1 = Object.Instantiate(elementPrefab.transform.Find("binding"), transform);
            transform1.GetComponent<Text>().enabled = true;
            transform1.GetComponent<Text>().text = value;
            if (!center)
                return;
            transform1.GetComponent<RectTransform>().sizeDelta = new Vector2(80f, 17f);
            transform1.GetComponent<RectTransform>().anchoredPosition = new Vector2(30f, -10f);
        }

        private static bool IsEquipmentSlotFree(
            Inventory inventory,
            ItemDrop.ItemData item,
            out int which)
        {
            which = Array.IndexOf(typeEnums, item.m_shared.m_itemType);
            return which >= 0 && inventory.GetItemAt(which, inventory.GetHeight() - 1) == null;
        }

        private static bool IsAtEquipmentSlot(
            Inventory inventory,
            ItemDrop.ItemData item,
            out int which)
        {
            if (!addEquipmentRow.Value || item.m_gridPos.x > 4 || item.m_gridPos.y < inventory.GetHeight() - 1)
            {
                which = -1;
                return false;
            }

            which = item.m_gridPos.x;
            return true;
        }

        private static void SetElementPositions()
        {
            Transform transform = Hud.instance.transform.Find("hudroot");
            if (!(transform.Find("QuickAccessBar")?.GetComponent<RectTransform>() != null))
                return;
            if (quickAccessX.Value == 9999.0)
                quickAccessX.Value = transform.Find("healthpanel").GetComponent<RectTransform>().anchoredPosition.x -
                                     32f;
            if (quickAccessY.Value == 9999.0)
                quickAccessY.Value = transform.Find("healthpanel").GetComponent<RectTransform>().anchoredPosition.y -
                                     870f;
            transform.Find("QuickAccessBar").GetComponent<RectTransform>().anchoredPosition =
                new Vector2(quickAccessX.Value, quickAccessY.Value);
            transform.Find("QuickAccessBar").GetComponent<RectTransform>().localScale =
                new Vector3(quickAccessScale.Value, quickAccessScale.Value, 1f);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Awake))]
        private static class Player_Awake_Patch
        {
            private static void Prefix(Player __instance, Inventory ___m_inventory)
            {
                if (!modEnabled.Value)
                    return;
                OdinsExtendedInventoryLogger.LogDebug("Player_Awake");

                int height = extraRows.Value + (addEquipmentRow.Value ? 5 : 4);
                __instance.m_inventory.m_height = height;
                __instance.m_tombstone.GetComponent<Container>().m_height = height;
            }
        }

        [HarmonyPatch(typeof(TombStone), nameof(TombStone.Awake))]
        private static class TombStone_Awake_Patch
        {
            private static void Prefix(TombStone __instance)
            {
                if (!modEnabled.Value)
                    return;
                OdinsExtendedInventoryLogger.LogDebug("TombStone_Awake");

                int height = extraRows.Value + (addEquipmentRow.Value ? 5 : 4);

                __instance.GetComponent<Container>().m_height = height;
            }
        }

        [HarmonyPatch(typeof(TombStone), nameof(TombStone.Interact))]
        private static class TombStone_Interact_Patch
        {
            private static void Prefix(TombStone __instance, Container ___m_container)
            {
                if (!modEnabled.Value)
                    return;
                OdinsExtendedInventoryLogger.LogDebug("TombStone_Interact");
                int num = extraRows.Value + (addEquipmentRow.Value ? 5 : 4);
                __instance.GetComponent<Container>().m_height = num;
                Traverse traverse = Traverse.Create(___m_container);
                string base64String = traverse.Field("m_nview").GetValue<ZNetView>().GetZDO().GetString("items");
                if (string.IsNullOrEmpty(base64String))
                    return;
                ZPackage pkg = new(base64String);
                ___m_container.m_loading = true;
                ___m_container.m_inventory.Load(pkg);
                ___m_container.m_loading = false;
                ___m_container.m_lastRevision = ___m_container.m_nview.GetZDO().m_dataRevision;
                ___m_container.m_lastDataString = base64String;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveInventoryToGrave))]
        private static class MoveInventoryToGrave_Patch
        {
            private static void Postfix(Inventory __instance, Inventory original)
            {
                if (!modEnabled.Value)
                    return;
                OdinsExtendedInventoryLogger.LogDebug("MoveInventoryToGrave");

                OdinsExtendedInventoryLogger.LogDebug($"inv: {__instance.GetHeight()} orig: {original.GetHeight()}");
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Update))]
        private static class Player_Update_Patch
        {
            private static void Postfix(Player __instance, Inventory ___m_inventory)
            {
                if (!modEnabled.Value)
                    return;
                int height = extraRows.Value + (addEquipmentRow.Value ? 5 : 4);
                ___m_inventory.m_height = height;
                __instance.m_tombstone.GetComponent<Container>().m_height = height;
                if (Util.IgnoreKeyPresses(true) || !addEquipmentRow.Value)
                    return;
                int num2;
                if (Util.CheckKeyDownKeycode(hotKey1.Value))
                {
                    num2 = 1;
                }
                else if (Util.CheckKeyDownKeycode(hotKey2.Value))
                {
                    num2 = 2;
                }
                else
                {
                    if (!Util.CheckKeyDownKeycode(hotKey3.Value))
                        return;
                    num2 = 3;
                }

                ItemDrop.ItemData itemAt = ___m_inventory.GetItemAt(num2 + 4, ___m_inventory.GetHeight() - 1);
                if (itemAt == null)
                    return;
                __instance.UseItem(null, itemAt, false);
            }

            private static void CreateTombStone()
            {
                OdinsExtendedInventoryLogger.LogDebug(
                    $"height {Player.m_localPlayer.m_tombstone.GetComponent<Container>().m_height}");
                GameObject gameObject = Object.Instantiate(Player.m_localPlayer.m_tombstone,
                    Player.m_localPlayer.GetCenterPoint(), Player.m_localPlayer.transform.rotation);
                TombStone component = gameObject.GetComponent<TombStone>();
                OdinsExtendedInventoryLogger.LogDebug($"height {gameObject.GetComponent<Container>().m_height}");
                OdinsExtendedInventoryLogger.LogDebug(
                    $"inv height {gameObject.GetComponent<Container>().GetInventory().GetHeight()}");
                OdinsExtendedInventoryLogger.LogDebug(
                    $"inv slots {gameObject.GetComponent<Container>().GetInventory().GetEmptySlots()}");
                for (int index = 0;
                     index < gameObject.GetComponent<Container>().GetInventory().GetEmptySlots();
                     ++index)
                    gameObject.GetComponent<Container>().GetInventory().AddItem("SwordBronze", 1, 1, 0, 0L, "");
                OdinsExtendedInventoryLogger.LogDebug(
                    $"no items: {gameObject.GetComponent<Container>().GetInventory().NrOfItems()}");
                PlayerProfile playerProfile = Game.instance.GetPlayerProfile();
                component.Setup(playerProfile.GetName(), playerProfile.GetPlayerID());
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Update))]
        private static class InventoryGui_Update_Patch
        {
            private static void Postfix(
                InventoryGui __instance,
                InventoryGrid ___m_playerGrid,
                Animator ___m_animator)
            {
                if (!modEnabled.Value || !Player.m_localPlayer)
                    return;
                if (addEquipmentRow.Value)
                {
                    Inventory inventory = Player.m_localPlayer.GetInventory();
                    List<ItemDrop.ItemData> allItems = inventory.GetAllItems();
                    var player = Player.m_localPlayer;
                    ItemDrop.ItemData itemData1 = player.m_helmetItem;
                    ItemDrop.ItemData itemData2 = player.m_chestItem;
                    ItemDrop.ItemData itemData3 = player.m_legItem;
                    ItemDrop.ItemData itemData4 = player.m_shoulderItem;
                    ItemDrop.ItemData itemData5 = player.m_utilityItem;
                    int width = inventory.GetWidth();
                    int num1 = width * (inventory.GetHeight() - 1);
                    if (itemData1 != null)
                        player.m_helmetItem.m_gridPos =
                            new Vector2i(num1 % width, num1 / width);
                    int num2 = num1 + 1;
                    if (itemData2 != null)
                        player.m_chestItem.m_gridPos =
                            new Vector2i(num2 % width, num2 / width);
                    int num3 = num2 + 1;
                    if (itemData3 != null)
                        player.m_legItem.m_gridPos =
                            new Vector2i(num3 % width, num3 / width);
                    int num4 = num3 + 1;
                    if (itemData4 != null)
                        player.m_shoulderItem.m_gridPos =
                            new Vector2i(num4 % width, num4 / width);
                    int num5 = num4 + 1;
                    if (itemData5 != null)
                        player.m_utilityItem.m_gridPos =
                            new Vector2i(num5 % width, num5 / width);
                    foreach (ItemDrop.ItemData t in allItems)
                    {
                        if (IsAtEquipmentSlot(inventory, t, out int which) &&
                            (which != 0 || t != itemData1) &&
                            (which != 1 || t != itemData2) &&
                            (which != 2 || t != itemData3) &&
                            (which != 3 || t != itemData4) &&
                            (which != 4 || t != itemData5) && (which <= -1 ||
                                                               t.m_shared.m_itemType !=
                                                               typeEnums[which] ||
                                                               equipItems[which] == t ||
                                                               !Player.m_localPlayer.EquipItem(
                                                                   t, false)))
                        {
                            Vector2i vector2I = inventory.FindEmptySlot(true);
                            if (vector2I.x < 0 || vector2I.y < 0 || vector2I.y == inventory.GetHeight() - 1)
                            {
                                Player.m_localPlayer.DropItem(inventory, t, t.m_stack);
                            }
                            else
                            {
                                t.m_gridPos = vector2I;
                                ___m_playerGrid.UpdateInventory(inventory, Player.m_localPlayer, null);
                            }
                        }
                    }

                    equipItems = new ItemDrop.ItemData[5]
                    {
                        itemData1,
                        itemData2,
                        itemData3,
                        itemData4,
                        itemData5
                    };
                }

                if (!___m_animator.GetBool("visible"))
                    return;
                __instance.m_player.Find("Bkg").GetComponent<RectTransform>().anchorMin = new Vector2(0.0f,
                    (extraRows.Value + (!addEquipmentRow.Value || displayEquipmentRowSeparate.Value ? 0 : 1)) * -0.25f);
                if (addEquipmentRow.Value)
                {
                    if (displayEquipmentRowSeparate.Value && __instance.m_player.Find("EquipmentBkg") == null)
                    {
                        Transform transform = Object.Instantiate(__instance.m_player.Find("Bkg"), __instance.m_player);
                        transform.SetAsFirstSibling();
                        transform.name = "EquipmentBkg";
                        transform.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 0.0f);
                        transform.GetComponent<RectTransform>().anchorMax = new Vector2(1.5f, 1f);
                    }
                    else if (!displayEquipmentRowSeparate.Value &&
                             __instance.m_player.Find("EquipmentBkg"))
                    {
                        Object.Destroy(__instance.m_player.Find("EquipmentBkg").gameObject);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Update))]
        private static class MoveableChest_InventoryGui_Update_Patch
        {
            private static void Postfix(InventoryGui __instance, Container ___m_currentContainer)
            {
                if (moveableChestInventoryFound) return;
                Vector3 mousePos = Input.mousePosition;
                if (!modEnabled.Value || !___m_currentContainer || !___m_currentContainer.IsOwner())
                {
                    lastMousePos = mousePos;
                    return;
                }


                if (chestInventoryX.Value < 0)
                    chestInventoryX.Value = __instance.m_container.anchorMin.x;
                if (chestInventoryY.Value < 0)
                    chestInventoryY.Value = __instance.m_container.anchorMin.y;

                __instance.m_container.anchorMin = new Vector2(chestInventoryX.Value, chestInventoryY.Value);
                __instance.m_container.anchorMax = new Vector2(chestInventoryX.Value, chestInventoryY.Value);


                if (lastMousePos == Vector3.zero)
                    lastMousePos = mousePos;


                PointerEventData eventData = new(EventSystem.current)
                {
                    position = lastMousePos
                };

                if (Util.CheckKeyHeldKeycode(modKeyOneChestMove.Value) &&
                    Util.CheckKeyHeldKeycode(modKeyTwoChestMove.Value))
                {
                    List<RaycastResult> raycastResults = new();
                    EventSystem.current.RaycastAll(eventData, raycastResults);

                    foreach (RaycastResult rcr in raycastResults)
                        if (rcr.gameObject.layer == LayerMask.NameToLayer("UI") && rcr.gameObject.name == "Bkg" &&
                            rcr.gameObject.transform.parent.name == "Container")
                        {
                            chestInventoryX.Value += (mousePos.x - lastMousePos.x) / Screen.width;
                            chestInventoryY.Value += (mousePos.y - lastMousePos.y) / Screen.height;
                        }
                }

                lastMousePos = mousePos;
            }
        }


        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateInventory))]
        private static class UpdateInventory_Patch
        {
            private static void Postfix(InventoryGrid ___m_playerGrid)
            {
                if (!modEnabled.Value || !addEquipmentRow.Value)
                    return;
                try
                {
                    Inventory inventory = Player.m_localPlayer.GetInventory();
                    int num1 = inventory.GetWidth() * (inventory.GetHeight() - 1);
                    string str1 = helmetText.Value;
                    Transform transform1 = ___m_playerGrid.m_gridRoot.transform;
                    int num2 = num1 + 1;
                    Transform child1 = transform1.GetChild(num1);
                    SetSlotText(str1, child1);
                    string str2 = chestText.Value;
                    Transform transform2 = ___m_playerGrid.m_gridRoot.transform;
                    int num3 = num2 + 1;
                    Transform child2 = transform2.GetChild(num2);
                    SetSlotText(str2, child2);
                    string str3 = legsText.Value;
                    Transform transform3 = ___m_playerGrid.m_gridRoot.transform;
                    int num4 = num3 + 1;
                    Transform child3 = transform3.GetChild(num3);
                    SetSlotText(str3, child3);
                    string str4 = backText.Value;
                    Transform transform4 = ___m_playerGrid.m_gridRoot.transform;
                    int num5 = num4 + 1;
                    Transform child4 = transform4.GetChild(num4);
                    SetSlotText(str4, child4);
                    string str5 = utilityText.Value;
                    Transform transform5 = ___m_playerGrid.m_gridRoot.transform;
                    int num6 = num5 + 1;
                    Transform child5 = transform5.GetChild(num5);
                    SetSlotText(str5, child5);
                    string str6 = hotKey1.Value.ToString();
                    Transform transform6 = ___m_playerGrid.m_gridRoot.transform;
                    int num7 = num6 + 1;
                    Transform child6 = transform6.GetChild(num6);
                    SetSlotText(str6, child6, false);
                    string str7 = hotKey2.Value.ToString();
                    Transform transform7 = ___m_playerGrid.m_gridRoot.transform;
                    int num8 = num7 + 1;
                    Transform child7 = transform7.GetChild(num7);
                    SetSlotText(str7, child7, false);
                    string str8 = hotKey3.Value.ToString();
                    Transform transform8 = ___m_playerGrid.m_gridRoot.transform;
                    int num9 = num8 + 1;
                    Transform child8 = transform8.GetChild(num8);
                    SetSlotText(str8, child8, false);
                    if (!displayEquipmentRowSeparate.Value)
                        return;
                    int num10 = inventory.GetWidth() * (inventory.GetHeight() - 1);
                    Transform transform9 = ___m_playerGrid.m_gridRoot.transform;
                    int num11 = num10 + 1;
                    transform9.GetChild(num10).GetComponent<RectTransform>().anchoredPosition =
                        new Vector2(678f, 0.0f);
                    Transform transform10 = ___m_playerGrid.m_gridRoot.transform;
                    int num12 = num11 + 1;
                    transform10.GetChild(num11).GetComponent<RectTransform>().anchoredPosition =
                        new Vector2(748f, -35f);
                    Transform transform11 = ___m_playerGrid.m_gridRoot.transform;
                    int num13 = num12 + 1;
                    transform11.GetChild(num12).GetComponent<RectTransform>().anchoredPosition =
                        new Vector2(678f, -70f);
                    Transform transform12 = ___m_playerGrid.m_gridRoot.transform;
                    int num14 = num13 + 1;
                    transform12.GetChild(num13).GetComponent<RectTransform>().anchoredPosition =
                        new Vector2(748f, -105f);
                    Transform transform13 = ___m_playerGrid.m_gridRoot.transform;
                    int num15 = num14 + 1;
                    transform13.GetChild(num14).GetComponent<RectTransform>().anchoredPosition =
                        new Vector2(678f, -140f);
                    Transform transform14 = ___m_playerGrid.m_gridRoot.transform;
                    int num16 = num15 + 1;
                    transform14.GetChild(num15).GetComponent<RectTransform>().anchoredPosition =
                        new Vector2(643f, -210f);
                    Transform transform15 = ___m_playerGrid.m_gridRoot.transform;
                    int num17 = num16 + 1;
                    transform15.GetChild(num16).GetComponent<RectTransform>().anchoredPosition =
                        new Vector2(713f, -210f);
                    Transform transform16 = ___m_playerGrid.m_gridRoot.transform;
                    num9 = num17 + 1;
                    transform16.GetChild(num17).GetComponent<RectTransform>().anchoredPosition =
                        new Vector2(783f, -210f);
                }
                catch (Exception ex)
                {
                    OdinsExtendedInventoryLogger.LogDebug($"Exception in EPI Update Inventory: {ex}");
                }
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.FindEmptySlot))]
        private static class FindEmptySlot_Patch
        {
            private static void Prefix(Inventory __instance, ref int ___m_height)
            {
                if (!modEnabled.Value || !addEquipmentRow.Value || !Player.m_localPlayer ||
                    __instance != Player.m_localPlayer.GetInventory())
                    return;
                OdinsExtendedInventoryLogger.LogDebug("FindEmptySlot");
                --___m_height;
            }

            private static void Postfix(Inventory __instance, ref int ___m_height)
            {
                if (!modEnabled.Value || !addEquipmentRow.Value || !Player.m_localPlayer ||
                    __instance != Player.m_localPlayer.GetInventory())
                    return;
                ++___m_height;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetEmptySlots))]
        private static class GetEmptySlots_Patch
        {
            private static bool Prefix(
                Inventory __instance,
                ref int __result,
                List<ItemDrop.ItemData> ___m_inventory,
                int ___m_width,
                int ___m_height)
            {
                if (!modEnabled.Value || !addEquipmentRow.Value || __instance != Player.m_localPlayer.GetInventory())
                    return true;
                OdinsExtendedInventoryLogger.LogDebug("GetEmptySlots");
                int count = ___m_inventory.FindAll((Predicate<ItemDrop.ItemData>)(i => i.m_gridPos.y < ___m_height - 1))
                    .Count;
                __result = (___m_height - 1) * ___m_width - count;
                return false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.HaveEmptySlot))]
        private static class HaveEmptySlot_Patch
        {
            private static bool Prefix(
                Inventory __instance,
                ref bool __result,
                List<ItemDrop.ItemData> ___m_inventory,
                int ___m_width,
                int ___m_height)
            {
                if (!modEnabled.Value || !addEquipmentRow.Value || __instance != Player.m_localPlayer.GetInventory())
                    return true;
                int count = ___m_inventory.FindAll((Predicate<ItemDrop.ItemData>)(i => i.m_gridPos.y < ___m_height - 1))
                    .Count;
                __result = count < ___m_width * (___m_height - 1);
                return false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), typeof(ItemDrop.ItemData))]
        private static class Inventory_AddItem_Patch1
        {
            private static bool Prefix(
                Inventory __instance,
                ref bool __result,
                List<ItemDrop.ItemData> ___m_inventory,
                ItemDrop.ItemData item)
            {
                if (!modEnabled.Value || !addEquipmentRow.Value || !Player.m_localPlayer ||
                    __instance != Player.m_localPlayer.GetInventory())
                    return true;
                OdinsExtendedInventoryLogger.LogDebug("AddItem");
                int which;
                if (!IsEquipmentSlotFree(__instance, item, out which))
                    return true;
                item.m_gridPos = new Vector2i(which, __instance.GetHeight() - 1);
                ___m_inventory.Add(item);
                Player.m_localPlayer.EquipItem(item, false);
                __instance.Changed();
                __result = true;
                return false;
            }
        }


        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), typeof(ItemDrop.ItemData), typeof(int), typeof(int),
            typeof(int))]
        private static class Inventory_AddItem_Patch2
        {
            private static void Prefix(
                Inventory __instance,
                ref int ___m_width,
                ref int ___m_height,
                int x,
                int y)
            {
                if (!modEnabled.Value)
                    return;
                if (x >= ___m_width)
                    ___m_width = x + 1;
                if (y < ___m_height)
                    return;
                ___m_height = y + 1;
            }
        }

        /*[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
        static class InventoryGui_Show_Patch
        {
            private const float indivRow = 70.5f;
            private const float origY = -90.0f;
            private const float containerHeight = -340.0f;
            private static float lastValue = 0;

            static void Postfix(ref InventoryGui __instance)
            {
                if (!modEnabled.Value) return;
                if (!scrollableInventory.Value) return;
                RectTransform container = __instance.m_container;
                RectTransform player = __instance.m_player;
                GameObject playerGrid = InventoryGui.instance.m_playerGrid.gameObject;
                int playerInventoryBackgroundSize = Math.Min(6,
                    Math.Max(4, extraRows.Value));
                float containerNewY = origY - indivRow * playerInventoryBackgroundSize;
                player.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    playerInventoryBackgroundSize * indivRow);
                container.offsetMax = new Vector2(610, containerNewY);
                container.offsetMin = new Vector2(40, containerNewY + containerHeight);
                if (playerGrid.GetComponent<InventoryGrid>().m_scrollbar) return;
                GameObject playerGridScroll = Object.Instantiate(
                    InventoryGui.instance.m_containerGrid.m_scrollbar.gameObject,
                    playerGrid.transform.parent);
                playerGridScroll.name = "ScrollPInv";
                playerGrid.GetComponent<RectMask2D>().enabled = true;
                ScrollRect playerScrollRect = playerGrid.AddComponent<ScrollRect>();
                playerGrid.GetComponent<RectTransform>().offsetMax = new Vector2(800f,
                    playerGrid.GetComponent<RectTransform>().offsetMax.y);
                playerGrid.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 1f);
                playerScrollRect.content = playerGrid.GetComponent<InventoryGrid>().m_gridRoot;
                playerScrollRect.viewport = __instance.m_player.GetComponentInChildren<RectTransform>();
                playerScrollRect.verticalScrollbar = playerGridScroll.GetComponent<Scrollbar>();
                playerGrid.GetComponent<InventoryGrid>().m_scrollbar =
                    playerGridScroll.GetComponent<Scrollbar>();

                playerScrollRect.horizontal = false;
                playerScrollRect.movementType = ScrollRect.MovementType.Clamped;
                playerScrollRect.scrollSensitivity = indivRow;
                playerScrollRect.inertia = false;
                playerScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                Scrollbar playerScrollbar = playerGridScroll.GetComponent<Scrollbar>();
                lastValue = playerScrollbar.value;
            }
        }*/


        [HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
        private static class Hud_Awake_Patch
        {
            private static void Postfix(Hud __instance)
            {
                if (!modEnabled.Value || !addEquipmentRow.Value)
                    return;
                Transform transform = Object.Instantiate(__instance.m_rootObject.transform.Find("HotKeyBar"),
                    __instance.m_rootObject.transform, true);
                transform.name = "QuickAccessBar";
                transform.GetComponent<RectTransform>().localPosition = Vector3.zero;
            }
        }

        [HarmonyPatch(typeof(Hud), nameof(Hud.Update))]
        private static class Hud_Update_Patch
        {
            private static void Postfix(Hud __instance)
            {
                if (!modEnabled.Value || !addEquipmentRow.Value || Player.m_localPlayer == null)
                    return;
                float scaleFactor = GameObject.Find("LoadingGUI").GetComponent<CanvasScaler>().scaleFactor;
                Vector3 mousePosition = Input.mousePosition;
                if (!modEnabled.Value)
                {
                    lastMousePos = mousePosition;
                }
                else
                {
                    SetElementPositions();
                    if (lastMousePos == Vector3.zero)
                        lastMousePos = mousePosition;
                    Transform transform = Hud.instance.transform.Find("hudroot");
                    if (Util.CheckKeyHeldKeycode(modKeyOne.Value) &&
                        Util.CheckKeyHeldKeycode(modKeyTwo.Value))
                    {
                        Rect rect = Rect.zero;
                        if (transform.Find("QuickAccessBar")?.GetComponent<RectTransform>() != null)
                            rect = new Rect(
                                transform.Find("QuickAccessBar").GetComponent<RectTransform>().anchoredPosition.x *
                                scaleFactor,
                                (float)(transform.Find("QuickAccessBar").GetComponent<RectTransform>().anchoredPosition
                                            .y * (double)scaleFactor + Screen.height -
                                        transform.Find("QuickAccessBar").GetComponent<RectTransform>().sizeDelta.y *
                                        (double)scaleFactor * quickAccessScale.Value),
                                (float)(transform.Find("QuickAccessBar").GetComponent<RectTransform>().sizeDelta.x *
                                        (double)scaleFactor * quickAccessScale.Value * 0.375),
                                transform.Find("QuickAccessBar").GetComponent<RectTransform>().sizeDelta.y *
                                scaleFactor * quickAccessScale.Value);
                        if (rect.Contains(lastMousePos) &&
                            currentlyDragging is "" or "QuickAccessBar")
                        {
                            quickAccessX.Value += (mousePosition.x - lastMousePos.x) / scaleFactor;
                            quickAccessY.Value += (mousePosition.y - lastMousePos.y) / scaleFactor;
                            currentlyDragging = "QuickAccessBar";
                        }
                        else
                        {
                            currentlyDragging = "";
                        }
                    }
                    else
                    {
                        currentlyDragging = "";
                    }

                    lastMousePos = mousePosition;
                }
            }
        }
    }
}