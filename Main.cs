using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LethalLib.Modules;
using UnityEngine.SceneManagement;
using BepInEx.Configuration;
using Unity.Netcode;
using System.Reflection;
using System;
using NetworkPrefabs = LethalLib.Modules.NetworkPrefabs;
using Unity.Collections;
using GameNetcodeStuff;

namespace BuyableShotgunShells
{
    [BepInDependency("evaisa.lethallib", "0.13.2")]
    [BepInPlugin(modGUID, modName, modVersion)]
    public class BuyableShotgunShells : BaseUnityPlugin
    {
        private const string modGUID = "MegaPiggy.BuyableShotgunShells";
        private const string modName = "Buyable Shotgun Shells";
        private const string modVersion = "1.3.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        private static BuyableShotgunShells Instance;

        private static ManualLogSource LoggerInstance => Instance.Logger;

        public static List<Item> AllItems => Resources.FindObjectsOfTypeAll<Item>().Reverse().ToList();
        public static Item ShotgunShell => AllItems.FirstOrDefault(item => item.name.Equals("GunAmmo") && item.spawnPrefab != null);
        public static ClonedItem ShotgunShellClone { get; private set; }


        private static ConfigEntry<int> ShellPriceConfig;
        public static int ShellPriceLocal => ShellPriceConfig.Value;
        internal static int ShellPriceRemote = -1;
        public static int ShellPrice => ShellPriceRemote > -1 ? ShellPriceRemote : ShellPriceLocal;
        private static bool IsHost => NetworkManager.Singleton.IsHost;
        private static ulong LocalClientId => NetworkManager.Singleton.LocalClientId;

        private void Awake()
        {
            if (Instance == null)
            {
                DontDestroyOnLoad(this);
                Instance = this;
            }
            harmony.PatchAll();
            ShellPriceConfig = Config.Bind("Prices", "ShotgunShellPrice", 20, "Credits needed to buy shotgun shells");
            Logger.LogInfo($"Plugin {modName} is loaded with version {modVersion}!");
        }

        public class ClonedItem : Item
        {
            public Item original;
        }

        private static ClonedItem CloneNonScrap(Item original, int price)
        {
            ClonedItem clone = ScriptableObject.CreateInstance<ClonedItem>();
            DontDestroyOnLoad(clone);
            clone.original = original;
            var prefab = NetworkPrefabs.CloneNetworkPrefab(original.spawnPrefab, "Buyable" + original.name);
            prefab.AddComponent<Unflagger>();
            DontDestroyOnLoad(prefab);
            CopyFields(original, clone);
            prefab.GetComponent<GrabbableObject>().itemProperties = clone;
            clone.spawnPrefab = prefab;
            clone.name = "Buyable" + original.name;
            clone.creditsWorth = price;
            clone.isScrap = false;
            return clone;
        }

        public static void CopyFields(Item source, Item destination)
        {
            FieldInfo[] fields = typeof(Item).GetFields();
            foreach (FieldInfo field in fields)
            {
                field.SetValue(destination, field.GetValue(source));
            }
        }

        private static Dictionary<string, TerminalNode> infoNodes = new Dictionary<string, TerminalNode>();

        private static TerminalNode CreateInfoNode(string name, string description)
        {
            if (infoNodes.ContainsKey(name)) return infoNodes[name];
            TerminalNode node = ScriptableObject.CreateInstance<TerminalNode>();
            node.clearPreviousText = true;
            node.name = name + "InfoNode";
            node.displayText = description + "\n\n";
            infoNodes.Add(name, node);
            return node;
        }

        private static void CloneShells()
        {
            if (ShotgunShell == null) return;
            if (ShotgunShellClone != null) return;
            ShotgunShellClone = CloneNonScrap(ShotgunShell, ShellPrice);
            ShotgunShellClone.itemName = "Shells";
            AddToShop();
        }

        private static void AddToShop()
        {
            Items.RegisterShopItem(ShotgunShellClone, price: ShellPrice, itemInfo: CreateInfoNode("ShotgunShell", "Ammo for the Nutcracker's Shotgun."));
            LoggerInstance.LogInfo($"Shotgun Shell added to Shop for {ShellPrice} credits");
        }

        private static void UpdateShopItemPrice()
        {
            ShotgunShellClone.creditsWorth = ShellPrice;
            Items.UpdateShopItemPrice(ShotgunShellClone, price: ShellPrice);
            LoggerInstance.LogInfo($"Shotgun Shell price updated to {ShellPrice} credits");
        }

        public static byte CurrentVersionByte = 1;

        public static void WriteData(FastBufferWriter writer)
        {
            writer.WriteByte(CurrentVersionByte);
            writer.WriteBytes(BitConverter.GetBytes(ShellPriceLocal));
        }

        public static void ReadData(FastBufferReader reader)
        {
            reader.ReadByte(out byte version);
            if (version == CurrentVersionByte)
            {
                var priceBytes = new byte[4];
                reader.ReadBytes(ref priceBytes, 4);
                ShellPriceRemote = BitConverter.ToInt32(priceBytes, 0);
                UpdateShopItemPrice();
                LoggerInstance.LogInfo("Host config set successfully");
                return;
            }
            throw new Exception("Invalid version byte");
        }

        public static void OnRequestSync(ulong clientID, FastBufferReader reader)
        {
            if (IsHost)
            {
                LoggerInstance.LogInfo("Sending config to client " + clientID.ToString());
                FastBufferWriter writer = new FastBufferWriter(5, Allocator.Temp, 5);
                try
                {
                    WriteData(writer);
                    NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("BuyableShotgunShells_OnReceiveConfigSync", clientID, writer, NetworkDelivery.Reliable);
                }
                catch (Exception ex)
                {
                    LoggerInstance.LogError($"Failed to send config: {ex}");
                }
                finally
                {
                    writer.Dispose();
                }
            }
        }

        public static void OnReceiveSync(ulong clientID, FastBufferReader reader)
        {
            LoggerInstance.LogInfo("Received config from host");
            try
            {
                ReadData(reader);
            }
            catch (Exception ex)
            {
                LoggerInstance.LogError($"Failed to receive config: {ex}");
                ShellPriceRemote = -1;
            }
        }

        /// <summary>
        /// For what ever reason the hide flags were set to HideAndDontSave, which caused it to not save obviously.
        /// I'm not sure what sets and I don't want to bother finding out when a fix like this is so easy.
        /// </summary>
        internal class Unflagger : MonoBehaviour
        {
            public void Awake()
            {
                gameObject.hideFlags = HideFlags.None;
            }
        }

        [HarmonyPatch]
        internal static class Patches
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
            public static void ServerConnect()
            {
                if (IsHost)
                {
                    LoggerInstance.LogInfo("Started hosting, using local settings");
                    NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("BuyableShotgunShells_OnRequestConfigSync", OnRequestSync);
                    UpdateShopItemPrice();
                }
                else
                {
                    LoggerInstance.LogInfo("Connected to server, requesting settings");
                    NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("BuyableShotgunShells_OnReceiveConfigSync", OnReceiveSync);
                    NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("BuyableShotgunShells_OnRequestConfigSync", 0, new FastBufferWriter(0, Allocator.Temp), NetworkDelivery.Reliable);
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(GameNetworkManager), "Start")]
            public static void Start()
            {
                LoggerInstance.LogWarning("Game network manager start");
                CloneShells();
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(GameNetworkManager), "StartDisconnect")]
            public static void ServerDisconnect()
            {
                LoggerInstance.LogInfo("Server disconnect");
                ShellPriceRemote = -1;
            }
        }
    }
}