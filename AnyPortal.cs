using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;


namespace AnyPortal
{
    [BepInPlugin("org.sweetgiorni.plugins.anyportal", "AnyPortal", "1.0.0.0")]
    public class AnyPortal : BaseUnityPlugin
    {
        public Harmony harmony;

        public static GameObject dropdownHolder;
        public static Dropdown dropdown;
        public static Button mapButton;

        public static AssetBundle anyPortalAssetBundle;
        public static TeleportWorld lastPortalInteracted;
        public static ZNetView lastPortalZNetView;
        public static List<ZDO> portalList;

        public const string AssetBundleName = "anyportal";

        private void Awake()
        {
            portalList = new List<ZDO>();
            dropdownHolder = null;
            dropdown = null;
            List<string> assetBundleSearchPaths = new List<string> {
                Path.Combine(Paths.BepInExRootPath, "scripts"),
                Path.Combine(Paths.PluginPath, "AnyPortal"),
                Assembly.GetExecutingAssembly().Location
            };
            string assetBundlePath = "";
            foreach (string searchPath in assetBundleSearchPaths)
            {
                if (searchPath == null || searchPath == "") continue;
                var tmpPath = Path.Combine(searchPath, AssetBundleName);
                Debug.Log($"Checking for file {tmpPath}");
                if (File.Exists(tmpPath))
                {
                    assetBundlePath = tmpPath;
                    break;
                }
            }
            if (assetBundlePath == "")
            {
                Debug.LogError($"Couldn't find an AssetBundle named \"{AssetBundleName}\" in the given search paths. Is it really there?");
                return;
            }
            else
            {
                Debug.Log($"Found AssetBundle at {assetBundlePath}");
            }
            anyPortalAssetBundle = AssetBundle.LoadFromFile(assetBundlePath);
            if (!anyPortalAssetBundle)
            {
                Debug.LogError($"Failed to load assset bundle from file {assetBundlePath}");
                return;
            }

            harmony = new Harmony("org.spub.plugins.anyportal.harmony");
            harmony.PatchAll();
        }

        public static void InitializeDropdownHolder()
        {
            var dropdownTemplate = anyPortalAssetBundle.LoadAsset<GameObject>("assets/anyportal.prefab");
            if (!dropdownTemplate)
            {
                Debug.LogError("Failed to load dropdown asset");
            }
            else
            {
                dropdownHolder = GameObject.Instantiate(dropdownTemplate);
                dropdownHolder.transform.SetParent(null);
                dropdownHolder.name = "AnyPortalControls";
                dropdownHolder.SetActive(false);
                dropdown = dropdownHolder.transform.Find("Dropdown").GetComponent<Dropdown>();
                dropdownHolder.transform.Find("Dropdown").Find("Label").GetComponent<Text>().text = "Choose a destination...";
                mapButton = dropdownHolder.transform.Find("MapButton").GetComponent<Button>();
                mapButton.onClick.AddListener(MapButtonClicked);
            }

            if (dropdownHolder.transform.parent == null)
            {
                var uiRoot = GameObject.Find("IngameGui(Clone)");
                if (!uiRoot)
                {
                    Debug.LogError("Unable to find root UI GameObject!");
                    return;
                }
                dropdownHolder.transform.SetParent(uiRoot.transform);
                dropdownHolder.transform.localScale = new Vector3(1f, 1f, 1f);
                dropdownHolder.transform.localPosition = new Vector3(0, -50, 0);
            }
        }

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
            if (anyPortalAssetBundle)
            {
                anyPortalAssetBundle.Unload(false);
            }
            if (dropdownHolder)
            {
                Destroy(dropdownHolder);
            }
        }

        private void Update()
        {
            if (dropdownHolder != null && dropdownHolder.activeSelf && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.KeypadEnter) || ZInput.GetButtonDown("JoyMenu") || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                dropdownHolder.SetActive(false);
                return;
            }
        }

        public static void MapButtonClicked()
        {
            if (!lastPortalInteracted || !lastPortalZNetView || !lastPortalZNetView.IsValid())
                return;
            // Get selected portal pos
            dropdownHolder.SetActive(false);
            TextInput.instance.Hide();
            var selectedPortalZDOID = lastPortalZNetView.GetZDO().GetZDOID("target");// GetPosition();
            if (selectedPortalZDOID.IsNone()) return;
            var selectedPortalZDO = ZDOMan.instance.GetZDO(selectedPortalZDOID);
            if (!selectedPortalZDO.IsValid()) return;
            Vector3 selectedPortalPos = selectedPortalZDO.GetPosition();
            // Using 0 for targetPeerID should basically be the equivalent of a network loopback (localhost)
            ZRoutedRpc.instance.InvokeRoutedRPC(0, "ChatMessage", new object[] { selectedPortalPos, 3, "AnyPortal", "PortalName" });
            Minimap.instance.ShowPointOnMap(selectedPortalPos);
        }

        public static void DropdownValueChanged(Dropdown change)
        {
            if (!(lastPortalZNetView && lastPortalZNetView.IsValid()))
            {
                Debug.LogError("lastPortalZNetView is not a valid reference");
                return;
            }
            var selectedPortalIdx = change.value;
            if (selectedPortalIdx < 0 || selectedPortalIdx >= portalList.Count)
            {
                Debug.LogError($"{selectedPortalIdx} is not a valid portal index.");
                return;
            }
            var selectedPortal = portalList[selectedPortalIdx];
            lastPortalZNetView.GetZDO().SetOwner(ZDOMan.instance.GetMyID());
            lastPortalZNetView.GetZDO().Set("target", selectedPortal.m_uid);
            ZDOMan.instance.ForceSendZDO(lastPortalZNetView.GetZDO().m_uid);
        }

        [HarmonyPatch(typeof(Game), "Start")]
        static class GameStartPatch
        {
            static void Postfix(Game __instance)
            {
                __instance.StopCoroutine("ConnectPortals");
            }
        }

        [HarmonyPatch(typeof(Game), "ConnectPortals")]
        static class GameConnectPortalsPatch
        {
            static void Postfix(Game __instance)
            {
                Debug.LogWarning("Connect portals is running - it's supposed to be disabled!?!?");
            }
        }

        [HarmonyPatch(typeof(TeleportWorld), "Interact")]
        static class PortalInteractPatch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (CodeInstruction instruction in instructions)
                {
                    if (instruction.opcode == OpCodes.Ldc_I4_S)
                    {
                        instruction.operand = SByte.MaxValue;
                    }
                    yield return instruction;
                }
                yield break;
            }
            static void Postfix(TeleportWorld __instance, ref ZNetView ___m_nview, ref bool __result)
            {
                lastPortalInteracted = __instance;
                lastPortalZNetView = ___m_nview;

                if (!__result)
                {
                    return;
                }

                if (dropdownHolder == null)
                {
                    InitializeDropdownHolder();
                }
                portalList.Clear();
                dropdown.onValueChanged.RemoveAllListeners();
                dropdown.onValueChanged.AddListener(delegate
                {
                    DropdownValueChanged(dropdown);
                });
                dropdown.options.Clear();
                dropdownHolder.SetActive(true);
                ZDOMan.instance.GetAllZDOsWithPrefab(Game.instance.m_portalPrefab.name, portalList);
                foreach (ZDO portalZDO in portalList)
                {
                    float distance = Vector3.Distance(__instance.transform.position, portalZDO.GetPosition());

                    dropdown.options.Add(new Dropdown.OptionData($"Destination portal \"{portalZDO.GetString("tag")}\"  --  Distance: " + (int)distance));
                }
            }
        }

        [HarmonyPatch(typeof(TeleportWorld), "GetHoverText")]
        static class TeleportWorldGetHoverTextPatch
        {
            static bool Prefix(TeleportWorld __instance, ref ZNetView ___m_nview, ref string __result)
            {
                string destPortalTag = "None";
                string tag = __instance.GetText();
                if (___m_nview == null || !___m_nview.IsValid())
                {
                    Debug.LogError("HoverTextPatch: ___m_nview is not valid");
                }
                else
                {
                    ZDOID targetZDOID = ___m_nview.GetZDO().GetZDOID("target");
                    if (!targetZDOID.IsNone())
                    {
                        var destPortalZDO = ZDOMan.instance.GetZDO(targetZDOID);
                        if (destPortalZDO == null || !destPortalZDO.IsValid())
                        {
                            destPortalTag = "None";
                            // Reset the target since it's bad...
                            if (destPortalZDO != null)
                            {
                                destPortalZDO.Set("target", ZDOID.None);
                                ZDOMan.instance.ForceSendZDO(lastPortalZNetView.GetZDO().m_uid);
                            }
                        }
                        else
                        {
                            destPortalTag = destPortalZDO.GetString("tag", "None");
                        }
                    }
                }
                __result = Localization.instance.Localize($"Portal Tag: {tag}\nDestination Portal Tag: {destPortalTag}\n[<color=yellow><b>$KEY_Use</b></color> Configure Portal]");
                return false;
            }
        }


        // We want to hook into TextInput's IsVisible method so our panel gets included in other UI logic
        [HarmonyPatch(typeof(InventoryGui), "IsVisible")]
        static class InventoryGuiIsVisiblePatch
        {
            static void Postfix(ref bool __result)
            {
                if (dropdownHolder != null && dropdownHolder.activeSelf)
                    __result = true;
            }
        }
    }
}
