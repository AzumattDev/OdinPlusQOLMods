The Inventory Discard feature from OdinQOL, pulled out for your modular pleasure. Discard items in your inventory, optionally gain resources.


`This mod uses ServerSync internally. Settings can change live through the BepInEx Configuration manager or by directly changing the file on the server. Can be installed on both the client and the server to enforce configuration.`


### Request of the community to make it modular, resulted in separation of features.


> ## Configuration Options
`[General]`

* Force Server Config [Synced with Server]
    * Force Server Config
        * Default value: true
* Enabled  [Synced with Server]
    * Enable Inventory Discard (whole mod)
        * Default value: false

 `[Inventory Discard]`
* DiscardHotkey [Not Synced with Server]
    * The hotkey to discard an item.
        * Default value: Delete
* ReturnUnknownResources [Synced with Server]
    * Return resources if recipe is unknown.
        * Default value: false
* ReturnEnchantedResources [Synced with Server]
    * Return resources for Epic Loot enchantments.
        * Default value: false
* ReturnResources [Synced with Server]
    * Fraction of resources to return (0.0 - 1.0).
        * Default value: 1

> ## Installation Instructions
***You must have BepInEx installed correctly! I can not stress this enough.***

#### Windows (Steam)
1. Locate your game folder manually or start Steam client and :
    * Right click the Valheim game in your steam library
    * "Go to Manage" -> "Browse local files"
    * Steam should open your game folder
2. Extract the contents of the archive into the BepInEx\plugins folder.
3. Locate odinplus.qol.OdinsInventoryDiscard.cfg under BepInEx\config and configure the mod to your needs

#### Server

`If installed on both the client and the server syncing to clients should work properly.`
1. Locate your main folder manually and :
   a. Extract the contents of the archive into the BepInEx\plugins folder.
   b. Launch your game at least once to generate the config file needed if you haven't already done so.
   c. Locate odinplus.qol.OdinsInventoryDiscard.cfg under BepInEx\config on your machine and configure the mod to your needs
2. Reboot your server. All clients will now sync to the server's config file even if theirs differs. Config Manager mod changes will only change the client config, not what the server is enforcing.


`Feel free to reach out to me on discord if you need manual download assistance.`


# Author Information

### Azumatt

`DISCORD:` Azumatt#2625

`STEAM:` https://steamcommunity.com/id/azumatt/


For Questions or Comments, find me in the Odin Plus Team Discord:
[![https://i.imgur.com/XXP6HCU.png](https://i.imgur.com/XXP6HCU.png)](https://discord.gg/Pb6bVMnFb2)

***
> # Update Information (Latest listed first)
> ### v1.0.0
> - Initial Release