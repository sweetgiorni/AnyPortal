using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;


namespace AnyPortal
{
    [BepInPlugin("org.spub.plugins.anyportal", "AnyPortal", "1.0.0.0")]
    public class AnyPortal : BaseUnityPlugin
    {
        public Harmony harmony;

        public static GameObject dropdownHolder;
        public static AssetBundle anyPortalAssetBundle;
        public static TeleportWorld lastPortalInteracted;
        public static ZNetView lastPortalZNetView;
        public static List<ZDO> portalList;

        private void Awake()
        {
            portalList = new List<ZDO>();

            string path = Path.Combine(Paths.BepInExRootPath, "scripts\\anyportal");
            Debug.Log(path);
            if (!File.Exists(path))
            {
                Debug.Log($"File {path} does not exist!");
            }
            anyPortalAssetBundle = AssetBundle.LoadFromFile(path);
            if (!anyPortalAssetBundle)
            {
                Debug.LogError($"Failed to load assset bundle from file {path}");
            }
            else
            {
                var dropdownTemplate = anyPortalAssetBundle.LoadAsset<GameObject>("assets/dropdown.prefab");
                if (!dropdownTemplate)
                {
                    Debug.Log("Failed to load dropdown asset");
                }
                else
                {
                    Debug.Log("Loaded dropdown prefab!");
                    var uiRoot = GameObject.Find("IngameGui(Clone)");
                    dropdownHolder = GameObject.Instantiate(dropdownTemplate, uiRoot.transform);
                    dropdownHolder.name = "AnyPortalDropdown";
                    dropdownHolder.SetActive(false);
                }
            }

            harmony = new Harmony("org.spub.plugins.anyportal.harmony");
            harmony.PatchAll();
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
            if (dropdownHolder && dropdownHolder.activeSelf && (Input.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyMenu")))
            {
                dropdownHolder.SetActive(false);
                return;
            }
        }

        public static void DropdownValueChanged(Dropdown change)
        {
            Debug.Log("New Value : " + change.value);
            if (lastPortalZNetView && lastPortalZNetView.IsValid())
            {
                var selectedPortalIdx = change.value;
                if (selectedPortalIdx < 0 || selectedPortalIdx >= portalList.Count)
                {
                    Debug.LogError($"{selectedPortalIdx} is not a valid portal index.");
                    return;
                }
                var selectedPortal = portalList[selectedPortalIdx];
                lastPortalZNetView.GetZDO().Set("target", selectedPortal.m_uid);
            }
        }

        [HarmonyPatch(typeof(TeleportWorld), "Interact")]
        static class PortalInteractPatch
        {
            static bool Prefix(TeleportWorld __instance, ref ZNetView ___m_nview, Humanoid human, bool hold, ref bool __result)
            {
                if (hold)
                {
                    __result = false;
                    return false;
                }

                if (PrivateArea.CheckAccess(__instance.transform.position, 0f, true))
                {
                    if (dropdownHolder)
                    {
                        var dropdown = dropdownHolder.GetComponent<Dropdown>();
                        lastPortalInteracted = __instance;
                        lastPortalZNetView = ___m_nview;
                        dropdown.onValueChanged.AddListener(delegate {
                            DropdownValueChanged(dropdown);
                        });
                        dropdownHolder.SetActive(true);
                        var label = dropdownHolder.GetComponentInChildren<Text>();
                        label.text = "Loading portal list...";
                        portalList.Clear();
                        int index = 0;
                        ZDOMan.instance.GetAllZDOsWithPrefabIterative(Game.instance.m_portalPrefab.name, portalList, ref index);
                        label.text = "Select a portal...";
                        dropdown.options.Clear();
                        foreach (ZDO portalZDO in portalList)
                        {
                            float distance = Vector3.Distance(__instance.transform.position, portalZDO.GetPosition());

                            dropdown.options.Add(new Dropdown.OptionData("Distance: " + (int)distance));
                        }
                    }
                    __result = true;
                    return false;
                }
                human.Message(MessageHud.MessageType.Center, "$piece_noaccess", 0, null);
                __result = true;
                return false;
            }
        }

        // We want to hook into TextInput's IsVisible method so our panel gets included in other UI logic
        [HarmonyPatch(typeof(InventoryGui), "IsVisible")]
        static class InventoryGuiIsVisiblePatch
        {
            static void Postfix(ref bool __result)
            {
                if (dropdownHolder && dropdownHolder.activeSelf)
                    __result = true;
            }
        }
    }
}
