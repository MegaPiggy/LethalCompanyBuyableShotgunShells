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
        private const string modVersion = "1.0.3";

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
            ShotgunShellClone = MakeNonScrap(ShellPrice);
            AddToShop();
            Logger.LogInfo($"Plugin {modName} is loaded with version {modVersion}!");
        }

        private Item MakeNonScrap(int price)
        {
            Item nonScrap = ScriptableObject.CreateInstance<Item>();
            DontDestroyOnLoad(nonScrap);
            nonScrap.name = "Error";
            nonScrap.itemName = "Error";
            nonScrap.itemId = 6624;
            nonScrap.isScrap = false;
            nonScrap.creditsWorth = price;
            nonScrap.canBeGrabbedBeforeGameStart = true;
            nonScrap.automaticallySetUsingPower = false;
            nonScrap.batteryUsage = 300;
            nonScrap.canBeInspected = false;
            nonScrap.isDefensiveWeapon = true;
            nonScrap.saveItemVariable = true;
            nonScrap.syncGrabFunction = false;
            nonScrap.twoHandedAnimation = true;
            nonScrap.verticalOffset = 0.25f;
            var prefab = LethalLib.Modules.NetworkPrefabs.CreateNetworkPrefab("Cube");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(prefab.transform, false);
            cube.GetComponent<MeshRenderer>().sharedMaterial.shader = Shader.Find("HDRP/Lit");
            prefab.AddComponent<BoxCollider>().size = Vector3.one * 2;
            prefab.AddComponent<AudioSource>();
            var prop = prefab.AddComponent<PhysicsProp>();
            prop.itemProperties = nonScrap;
            prop.grabbable = true;
            nonScrap.spawnPrefab = prefab;
            prefab.tag = "PhysicsProp";
            prefab.layer = LayerMask.NameToLayer("Props");
            cube.layer = LayerMask.NameToLayer("Props");
            GameObject scanNode = GameObject.Instantiate<GameObject>(Items.scanNodePrefab, prefab.transform);
            scanNode.name = "ScanNode";
            scanNode.transform.localPosition = new Vector3(0f, 0f, 0f);
            scanNode.transform.localScale *= 2;
            ScanNodeProperties properties = scanNode.GetComponent<ScanNodeProperties>();
            properties.nodeType = 1;
            properties.headerText = "Error";
            properties.subText = $"A mod is incompatible with {modName}";
            prefab.transform.localScale = Vector3.one / 2;
            return nonScrap;
        }

        private void CloneNonScrap(Item original, Item clone, int price)
        {
            DontDestroyOnLoad(original.spawnPrefab);
            clone.name = "Buyable" + original.name;
            clone.itemName = original.itemName;
            clone.itemId = original.itemId;
            clone.spawnPrefab = original.spawnPrefab;
            clone.creditsWorth = price;
            clone.canBeGrabbedBeforeGameStart = original.canBeGrabbedBeforeGameStart;
            clone.automaticallySetUsingPower = original.automaticallySetUsingPower;
            clone.batteryUsage = original.batteryUsage;
            clone.canBeInspected = original.canBeInspected;
            clone.isDefensiveWeapon = original.isDefensiveWeapon;
            clone.saveItemVariable = original.saveItemVariable;
            clone.syncGrabFunction = original.syncGrabFunction;
            clone.twoHandedAnimation = original.twoHandedAnimation;
            clone.weight = original.weight;
            clone.floorYOffset = original.floorYOffset;
            clone.positionOffset = original.positionOffset;
            clone.rotationOffset = original.rotationOffset;
            clone.restingRotation = original.restingRotation;
            clone.verticalOffset = original.verticalOffset;
        }

        private static Dictionary<string, TerminalNode> infoNodes = new Dictionary<string, TerminalNode>();

        private TerminalNode CreateInfoNode(string name, string description)
        {
            if (infoNodes.ContainsKey(name)) return infoNodes[name];
            TerminalNode node = ScriptableObject.CreateInstance<TerminalNode>();
            node.clearPreviousText = true;
            node.name = name + "InfoNode";
            node.displayText = description + "\n\n";
            infoNodes.Add(name, node);
            return node;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (ShotgunShell == null) return;
            CloneNonScrap(ShotgunShell, ShotgunShellClone, ShellPrice);
            ShotgunShellClone.itemName = "Shells";
        }

        private void AddToShop()
        {
            Items.RegisterShopItem(ShotgunShellClone, price: ShellPrice, itemInfo: CreateInfoNode("ShotgunShell", "Ammo for the Nutcracker's Shotgun."));
            LoggerInstance.LogInfo($"Shotgun Shell added to Shop for {ShellPrice} credits");
        }
    }
}