using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LethalLib.Modules;
using UnityEngine.SceneManagement;
using BepInEx.Configuration;

namespace BuyableShotgunShells
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class BuyableShotgunShells : BaseUnityPlugin
    {
        private const string modGUID = "MegaPiggy.BuyableShotgunShells";
        private const string modName = "Buyable Shotgun Shells";
        private const string modVersion = "1.0.2";

        private readonly Harmony harmony = new Harmony(modGUID);

        private static BuyableShotgunShells Instance;

        private static ManualLogSource LoggerInstance => Instance.Logger;

        public List<Item> AllItems => Resources.FindObjectsOfTypeAll<Item>().Concat(UnityEngine.Object.FindObjectsByType<Item>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID)).ToList();
        public Item ShotgunShell => AllItems.FirstOrDefault(item => item.name == "GunAmmo");
        public Item ShotgunShellClone { get; private set; }


        private ConfigEntry<int> ShellPriceConfig;
        public int ShellPrice => ShellPriceConfig.Value;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            harmony.PatchAll();
            ShellPriceConfig = Config.Bind("Prices", "ShotgunShellPrice", 20, "Credits needed to buy shotgun shells");
            SceneManager.sceneLoaded += OnSceneLoaded;
            Logger.LogInfo($"Plugin {modName} is loaded with version {modVersion}!");
        }

        private Item MakeNonScrap(Item original, int price)
        {
            Item clone = Object.Instantiate<Item>(original);
            clone.name = "Buyable" + original.name;
            clone.isScrap = false;
            clone.creditsWorth = price;
            return clone;
        }

        private TerminalNode CreateInfoNode(string name, string description)
        {
            TerminalNode node = ScriptableObject.CreateInstance<TerminalNode>();
            node.clearPreviousText = true;
            node.name = name + "InfoNode";
            node.displayText = description + "\n\n";
            return node;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (ShotgunShell == null) return;
            ShotgunShell.itemName = "Shells";
            if (ShotgunShellClone == null) ShotgunShellClone = MakeNonScrap(ShotgunShell, ShellPrice);
            Items.RegisterShopItem(ShotgunShell, price: ShellPrice, itemInfo: CreateInfoNode("ShotgunShell", "Ammo for the Nutcracker's Shotgun."));
            LoggerInstance.LogInfo($"Shotgun Shells added to Shop for {ShellPrice} credits");
        }
    }
}