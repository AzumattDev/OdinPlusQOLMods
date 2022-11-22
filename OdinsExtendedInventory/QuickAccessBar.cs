using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OdinsExtendedInventory
{
    [HarmonyPatch(typeof(HotkeyBar), nameof(HotkeyBar.UpdateIcons))]
    internal static class QuickAccessBar
    {
        [HarmonyPriority(Priority.Last)]
        private static bool Prefix(HotkeyBar __instance, Player player)
        {
            if (__instance.name != "QuickAccessBar")
            {
                return true;
            }
            
            if (!player || player.IsDead())
            {
                foreach (HotkeyBar.ElementData element in __instance.m_elements)
                    Object.Destroy(element.m_go);
                __instance.m_elements.Clear();
            }
            else
            {
                try
                {
                    __instance.m_items.Clear();
                    Inventory inventory = player.GetInventory();
                    if (inventory.GetItemAt(5, inventory.GetHeight() - 1) != null)
                        __instance.m_items.Add(inventory.GetItemAt(5, inventory.GetHeight() - 1));
                    if (inventory.GetItemAt(6, inventory.GetHeight() - 1) != null)
                        __instance.m_items.Add(inventory.GetItemAt(6, inventory.GetHeight() - 1));
                    if (inventory.GetItemAt(7, inventory.GetHeight() - 1) != null)
                        __instance.m_items.Add(inventory.GetItemAt(7, inventory.GetHeight() - 1));
                    __instance.m_items.Sort((Comparison<ItemDrop.ItemData>)((x, y) => x.m_gridPos.x.CompareTo(y.m_gridPos.x)));
                    int num = 0;
                    foreach (ItemDrop.ItemData itemData in __instance.m_items)
                        if (itemData.m_gridPos.x - 4 > num)
                            num = itemData.m_gridPos.x - 4;
                    if (__instance.m_elements.Count != num)
                    {
                        foreach (HotkeyBar.ElementData element in __instance.m_elements)
                            Object.Destroy(element.m_go);
                        __instance.m_elements.Clear();
                        for (int index = 0; index < num; ++index)
                        {
                            HotkeyBar.ElementData elementData = new()
                            {
                                m_go = Object.Instantiate(__instance.m_elementPrefab, __instance.transform)
                            };
                            elementData.m_go.transform.localPosition =
                                new Vector3(index * __instance.m_elementSpace, 0.0f, 0.0f);
                            elementData.m_go.transform.Find("binding").GetComponent<Text>().text =
                                OdinsExtendedInventoryPlugin.hotkeyTexts[index].Value.IsNullOrWhiteSpace()
                                    ? OdinsExtendedInventoryPlugin.hotkeys[index].Value.ToString()
                                    : OdinsExtendedInventoryPlugin.hotkeyTexts[index].Value;
                            elementData.m_go.transform.Find("binding").GetComponent<Text>().horizontalOverflow =
                                HorizontalWrapMode.Overflow;
                            elementData.m_go.transform.Find("binding").GetComponent<Text>().verticalOverflow =
                                VerticalWrapMode.Overflow;
                            
                            elementData.m_icon =
                                elementData.m_go.transform.transform.Find("icon").GetComponent<Image>();
                            elementData.m_durability =
                                elementData.m_go.transform.Find("durability").GetComponent<GuiBar>();
                            elementData.m_amount = elementData.m_go.transform.Find("amount").GetComponent<Text>();
                            elementData.m_equiped = elementData.m_go.transform.Find("equiped").gameObject;
                            elementData.m_queued = elementData.m_go.transform.Find("queued").gameObject;
                            elementData.m_selection = elementData.m_go.transform.Find("selected").gameObject;
                            __instance.m_elements.Add(elementData);
                        }
                    }

                    foreach (HotkeyBar.ElementData element in __instance.m_elements)
                        element.m_used = false;
                    bool flag = ZInput.IsGamepadActive();
                    foreach (ItemDrop.ItemData itemData in __instance.m_items)
                    {
                        HotkeyBar.ElementData element = __instance.m_elements[itemData.m_gridPos.x - 5];
                        element.m_used = true;
                        element.m_icon.gameObject.SetActive(true);
                        element.m_icon.sprite = itemData.GetIcon();
                        element.m_durability.gameObject.SetActive(itemData.m_shared.m_useDurability);
                        if (itemData.m_shared.m_useDurability)
                        {
                            if (itemData.m_durability <= 0.0)
                            {
                                element.m_durability.SetValue(1f);
                                element.m_durability.SetColor(Mathf.Sin(Time.time * 10f) > 0.0
                                    ? Color.red
                                    : new Color(0.0f, 0.0f, 0.0f, 0.0f));
                            }
                            else
                            {
                                element.m_durability.SetValue(itemData.GetDurabilityPercentage());
                                element.m_durability.ResetColor();
                            }
                        }

                        element.m_equiped.SetActive(itemData.m_equiped);
                        element.m_queued.SetActive(player.IsEquipActionQueued(itemData));
                        if (itemData.m_shared.m_maxStackSize > 1)
                        {
                            element.m_amount.gameObject.SetActive(true);
                            element.m_amount.text = itemData.m_stack + "/" + itemData.m_shared.m_maxStackSize;
                        }
                        else
                        {
                            element.m_amount.gameObject.SetActive(false);
                        }
                    }

                    for (int index = 0; index < __instance.m_elements.Count; ++index)
                    {
                        HotkeyBar.ElementData element = __instance.m_elements[index];
                        element.m_selection.SetActive(flag && index == __instance.m_selected);
                        if (!element.m_used)
                        {
                            element.m_icon.gameObject.SetActive(false);
                            element.m_durability.gameObject.SetActive(false);
                            element.m_equiped.SetActive(false);
                            element.m_queued.SetActive(false);
                            element.m_amount.gameObject.SetActive(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    OdinsExtendedInventoryPlugin.OdinsExtendedInventoryLogger.LogDebug($"There was an error in your shit: {ex}");
                }
            }

            return false;
        }
    }
}