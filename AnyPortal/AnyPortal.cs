// AnyPortal
// a Valheim mod skeleton using Jötunn
// 
// File:    AnyPortal.cs
// Project: AnyPortal

using BepInEx;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System;

namespace AnyPortal
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class AnyPortal : BaseUnityPlugin
    {
        public const string PluginGUID = "com.sweetgiorni.anyportal";
        public const string PluginName = "AnyPortal";
        public const string PluginVersion = "2.0.0";
        
        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        public Harmony harmony;

        public static GameObject dropdownHolder;
        public static Dropdown dropdown;
        public static Button mapButton;
        public static Button okButton;

        public static AssetBundle anyPortalAssetBundle;
        public static TeleportWorld lastPortalInteracted;
        public static ZNetView lastPortalZNetView;
        public static List<ZDO> portalList;

        private void Awake()
        {
            portalList = new List<ZDO>();
            dropdownHolder = null;
            dropdown = null;
            anyPortalAssetBundle = null;
            if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                anyPortalAssetBundle = AssetBundle.LoadFromMemory(Properties.Resources.anyportalAssetsWin64);
            }
            else if (Application.platform == RuntimePlatform.LinuxPlayer)
            {
                anyPortalAssetBundle = AssetBundle.LoadFromMemory(Properties.Resources.anyportalAssetsLinux64);
            }

            if (!anyPortalAssetBundle)
            {
                Jotunn.Logger.LogError($"Failed to read AssetBundle stream");
                return;
            }
            harmony = new Harmony("org.spub.plugins.anyportal.harmony");
            harmony.PatchAll();
        }

        public static void InitializeDropdownHolder()
        {
            var sprite = TextInput.instance.m_panel.GetComponent<Image>();
            if (anyPortalAssetBundle == null)
            {
                Jotunn.Logger.LogError("AnyPortal: Tried to access the AssetBundle before loading it!");
                return;
            }
            var dropdownTemplate = anyPortalAssetBundle.LoadAsset<GameObject>("assets/anyportal.prefab");
            if (!dropdownTemplate)
            {
                Jotunn.Logger.LogError("Failed to load dropdown asset");
                return;
            }
            dropdownHolder = GameObject.Instantiate(dropdownTemplate);
            dropdownHolder.transform.SetParent(null);
            dropdownHolder.name = "AnyPortalControls";
            dropdownHolder.SetActive(false);
            dropdown = dropdownHolder.transform.Find("Dropdown").GetComponent<Dropdown>();
            dropdownHolder.transform.Find("Dropdown").Find("Label").GetComponent<Text>().text = "Choose a destination...";
            mapButton = dropdownHolder.transform.Find("MapButton").GetComponent<Button>();
            mapButton.onClick.AddListener(MapButtonClicked);
            okButton = dropdownHolder.transform.Find("OKButton").GetComponent<Button>();
            okButton.onClick.AddListener(OKButtonClicked);

            var mapButtonText = mapButton.transform.GetChild(0).GetComponent<UnityEngine.UI.Text>();
            mapButtonText.text = Localization.TryTranslate(mapButtonText.text);
            

            var okButtonText = okButton.transform.GetChild(0).GetComponent<UnityEngine.UI.Text>();
            okButtonText.text = Localization.TryTranslate(okButtonText.text);

            if (dropdownHolder.transform.parent == null)
            {
                var uiRoot = GameObject.Find("IngameGui(Clone)");
                if (!uiRoot)
                {
                    Jotunn.Logger.LogError("Unable to find root UI GameObject!");
                    return;
                }
                dropdownHolder.transform.SetParent(uiRoot.transform);
                dropdownHolder.transform.localScale = new Vector3(1f, 1f, 1f);
                dropdownHolder.transform.localPosition = new Vector3(0, -50, 0);
            }

            var uiAtlas = Resources.FindObjectsOfTypeAll<UnityEngine.U2D.SpriteAtlas>().Single(a => a.name == "UIAtlas");
            if (uiAtlas)
            {
                var dropdownImage = dropdown.GetComponent<Image>();
                dropdownImage.sprite = uiAtlas.GetSprite("text_field");
                dropdownImage.color = new Color(255, 255, 255);
                var mapButtonImage = mapButton.GetComponent<Image>();
                mapButtonImage.sprite = uiAtlas.GetSprite("button");
                mapButtonImage.color = new Color(255, 255, 255);
                var okButtonImage = okButton.GetComponent<Image>();
                okButtonImage.sprite = uiAtlas.GetSprite("button");
                okButtonImage.color = new Color(255, 255, 255);

            }
        }

        private void OnDestroy()
        {
            if (harmony != null)
                harmony.UnpatchSelf();
            if (anyPortalAssetBundle)
                anyPortalAssetBundle.Unload(false);
            if (dropdownHolder != null)
                Destroy(dropdownHolder);
        }

        private void Update()
        {
            if (dropdownHolder != null && dropdownHolder.activeSelf && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.KeypadEnter) || ZInput.GetButtonDown("JoyMenu") || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                dropdownHolder.SetActive(false);
                return;
            }
        }

        public static void OKButtonClicked()
        {
            lastPortalInteracted.SetText(TextInput.instance.m_textField.text);
            TextInput.instance.Hide();
            dropdownHolder.SetActive(false);
            return;
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
                Jotunn.Logger.LogError("lastPortalZNetView is not a valid reference");
                return;
            }
            if (change.value == 0) // User selected "None" option
            {
                Jotunn.Logger.LogDebug("Unlinking portal");
                lastPortalZNetView.GetZDO().SetOwner(ZDOMan.instance.GetMyID());
                lastPortalZNetView.GetZDO().Set("target", ZDOID.None);
                ZDOMan.instance.ForceSendZDO(lastPortalZNetView.GetZDO().m_uid);
                return;
            }
            var selectedPortalIdx = change.value - 1; // Need to add one here because the first option is always "None"
            if (selectedPortalIdx < 0 || selectedPortalIdx >= portalList.Count)
            {
                Jotunn.Logger.LogError($"{selectedPortalIdx} is not a valid portal index.");
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
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                bool shouldNop = false;
                foreach (CodeInstruction instruction in instructions)
                {
                    if (instruction.opcode == OpCodes.Ldstr && ((System.String)instruction.operand) == "ConnectPortals")
                    {
                        shouldNop = true;
                        yield return new CodeInstruction(OpCodes.Nop);
                    }
                    else if (shouldNop)
                    {
                        yield return new CodeInstruction(OpCodes.Nop);
                        shouldNop = false;

                    }
                    else
                        yield return instruction;
                }
                yield break;
            }
        }

        [HarmonyPatch(typeof(Game), "ConnectPortals")]
        static class GameConnectPortalsPatch
        {
            static void Postfix(Game __instance)
            {
                Jotunn.Logger.LogError("Connect portals is running - it's supposed to be disabled!?!?");
            }
        }



        [HarmonyPatch(typeof(ZDOMan), "CreateSyncList")]
        static class ZDOmanCreateSyncListPatch
        {
            static void Postfix(List<ZDO> toSync)
            {
                if (ZNet.instance.IsServer())
                {
                    ZDOMan.instance.GetAllZDOsWithPrefab(Game.instance.m_portalPrefab.name, toSync);
                }
            }
        }

        [HarmonyPatch(typeof(TeleportWorld), "Interact")]
        static class PortalInteractPatch
        {
            // Patch the max string length for the portal's tag
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
                ZDOID thisPortalZDOID = ___m_nview.GetZDO().m_uid;
                // If the portal currently has a target configured, make sure that is the value selected in the dropdown
                // Otherwise, set the dropdown value to 0 (No destination)
                ZDOID targetZDOID = ___m_nview.GetZDO().GetZDOID("target");

                dropdown.options.Add(new Dropdown.OptionData("No destination"));
                var tmpPortalList = new List<ZDO>();
                ZDOMan.instance.GetAllZDOsWithPrefab(Game.instance.m_portalPrefab.name, tmpPortalList);
                // Sort alphabetically by portal tag and exclude self
                portalList = tmpPortalList.OrderBy(zdo => zdo.GetString("tag")).Where(zdo => zdo.m_uid != thisPortalZDOID).ToList();
                int index = 0;
                foreach (ZDO portalZDO in portalList)
                {
                    float distance = Vector3.Distance(__instance.transform.position, portalZDO.GetPosition());
                    if (distance == 0f)
                    {
                        continue;
                    }
                    dropdown.options.Add(new Dropdown.OptionData($"\"{portalZDO.GetString("tag")}\"  --  Distance: " + (int)distance));
                    if (portalZDO.m_uid == targetZDOID)
                        dropdown.value = index + 1;
                    index += 1;
                }
                if (targetZDOID == ZDOID.None)
                    dropdown.value = 0;
                dropdownHolder.SetActive(true);
            }
        }

        [HarmonyPatch(typeof(TeleportWorld), "GetHoverText")]
        static class TeleportWorldGetHoverTextPatch
        {
            static bool Prefix(TeleportWorld __instance, ref ZNetView ___m_nview, ref string __result)
            {
                string destPortalTag = "None";
                string tag = __instance.GetText();
                if (tag == "")
                    tag = "Empty tag";
                if (___m_nview == null || !___m_nview.IsValid())
                {
                    Jotunn.Logger.LogError("HoverTextPatch: ___m_nview is not valid");
                }
                else
                {
                    ZDOID targetZDOID = ___m_nview.GetZDO().GetZDOID("target");
                    if (!targetZDOID.IsNone())
                    {
                        var destPortalZDO = ZDOMan.instance.GetZDO(targetZDOID);
                        if (destPortalZDO == null || !destPortalZDO.IsValid())
                        {
                            Jotunn.Logger.LogDebug("HoverText: destPortalZDO is null or invalid");
                            destPortalTag = "None";
                            // Reset the target since it's bad...
                            Jotunn.Logger.LogDebug("HoverText: Clearing out the target");
                            ___m_nview.GetZDO().Set("target", ZDOID.None);
                            ZDOMan.instance.ForceSendZDO(___m_nview.GetZDO().m_uid);
                        }
                        else
                        {
                            destPortalTag = destPortalZDO.GetString("tag", "Empty tag");
                        }
                    }
                }
                __result = ($"Portal Tag: {tag}\nDestination Portal Tag: {destPortalTag}\n[<color=yellow><b>{Localization.TryTranslate("KEY_Use")}</b></color>] Configure Portal");

                return false;
            }
        }

        [HarmonyPatch(typeof(TextInput), "Show")]
        static class TextInputShowPatch
        {
            static void Postfix(ref TextInput __instance, string topic)
            {
                Jotunn.Logger.LogDebug($"Topic: {topic}");
                Button[] buttonsToHide = __instance.m_panel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                foreach (Button button in buttonsToHide)
                {
                    bool enabled = topic != "$piece_portal_tag";
                    Jotunn.Logger.LogDebug($"Setting button state to {enabled}");
                    button.gameObject.SetActive(enabled);
                }
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

