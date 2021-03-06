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
using System.Linq;
using AnyPortal;

namespace AnyPortal
{
    [BepInPlugin("org.sweetgiorni.plugins.anyportal", "AnyPortal", "1.0.3")]
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

        private void Awake()
        {
            portalList = new List<ZDO>();
            dropdownHolder = null;
            dropdown = null;

            anyPortalAssetBundle = AssetBundle.LoadFromMemory(Properties.Resources.anyportal_asset);
            if (!anyPortalAssetBundle)
            {
                Debug.LogError($"Failed to read AssetBundle stream");
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

            var uiAtlas = Resources.FindObjectsOfTypeAll<UnityEngine.U2D.SpriteAtlas>().Single(a => a.name == "UIAtlas");
            if (uiAtlas)
            {
                var dropdownImage = dropdown.GetComponent<Image>();
                dropdownImage.sprite = uiAtlas.GetSprite("text_field");
                dropdownImage.color = new Color(255, 255, 255);
                var mapButtonImage = mapButton.GetComponent<Image>();
                mapButtonImage.sprite = uiAtlas.GetSprite("button");
                mapButtonImage.color = new Color(255, 255, 255);

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
            if (change.value == 0) // User selected "None" option
            {
                Debug.Log("Unlinking portal");
                lastPortalZNetView.GetZDO().SetOwner(ZDOMan.instance.GetMyID());
                lastPortalZNetView.GetZDO().Set("target", ZDOID.None);
                ZDOMan.instance.ForceSendZDO(lastPortalZNetView.GetZDO().m_uid);
                return;
            }
            var selectedPortalIdx = change.value - 1; // Need to +1 here because the first option is always "None"
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
                Debug.LogError("Connect portals is running - it's supposed to be disabled!?!?");
            }
        }

        

        [HarmonyPatch(typeof(ZDOMan), "CreateSyncList")]
        static class ZDOmanCreateSyncListPatch
        {
            static void Prefix(List<ZDO> toSync)
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
                // If the portal currently has a target configured, make sure that is the value selected in the dropdown
                // Otherwise, set the dropdown value to 0 (No destination)
                ZDOID targetZDOID = ___m_nview.GetZDO().GetZDOID("target");

                dropdown.options.Add(new Dropdown.OptionData("No destination"));
                ZDOMan.instance.GetAllZDOsWithPrefab(Game.instance.m_portalPrefab.name, portalList);
                int index = 0;
                foreach (ZDO portalZDO in portalList)
                {
                    float distance = Vector3.Distance(__instance.transform.position, portalZDO.GetPosition());

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
                            Debug.Log("HoverText: destPortalZDO is null or invalid");
                            destPortalTag = "None";
                            // Reset the target since it's bad...
                            Debug.Log("HoverText: Clearing out the target");
                            ___m_nview.GetZDO().Set("target", ZDOID.None);
                            ZDOMan.instance.ForceSendZDO(___m_nview.GetZDO().m_uid);
                        }
                        else
                        {
                            destPortalTag = destPortalZDO.GetString("tag", "Empty tag");
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
