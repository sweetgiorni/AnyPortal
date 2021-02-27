using System;
using System.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;


namespace AnyPortal
{
    [BepInPlugin("org.spub.plugins.anyportal", "AnyPortal", "1.0.0.0")]
    public class AnyPortal : BaseUnityPlugin
    {
        public Harmony harmony;

        public static GameObject dropdownHolder;

        private void Awake()
        {
            var uiRoot = GameObject.Find("IngameGui(Clone)");
            dropdownHolder = new GameObject("DropdownHolder");
            dropdownHolder.transform.SetParent(uiRoot.transform);
            dropdownHolder.AddComponent<CanvasRenderer>();
            var rectTransform = dropdownHolder.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(540f, 200f);
            rectTransform.offsetMin = new Vector2(-50f, 50f);
            rectTransform.offsetMax = new Vector2(50f, 50f);
            rectTransform.localScale = new Vector3(.5f, .5f, .5f);
            var dropdown = dropdownHolder.AddComponent<UnityEngine.UI.Dropdown>();
            var image = dropdownHolder.AddComponent<UnityEngine.UI.Image>();
            String spritePath = "Assets/Texture2D/sactx-2048x2048-Uncompressed-UIAtlas-97597a23_0.png";
            var sprite = Resources.Load<Sprite>(spritePath);
            if (sprite == null)
            {
                Debug.LogError("Failed to load sprite from resources: " + spritePath);
            }
            else
            {
                image.sprite = sprite;
            }
            GameObject labelGO = new GameObject();
            labelGO.transform.SetParent(dropdownHolder.transform);
            var captionText = labelGO.AddComponent<UnityEngine.UI.Text>();
            labelGO.AddComponent<CanvasRenderer>();
            labelGO.AddComponent<RectTransform>();
            captionText.text = "Select a portal";
            dropdown.captionText = captionText;

            var options = new List<UnityEngine.UI.Dropdown.OptionData>();
            var optionData1 = new UnityEngine.UI.Dropdown.OptionData();
            optionData1.text = "Portal1";
            options.Add(optionData1);
            dropdown.options = options;
            dropdownHolder.layer = 5;

            var templateGO = new GameObject();
            templateGO.transform.SetParent(dropdownHolder.transform);
            templateGO.AddComponent<CanvasRenderer>();
            templateGO.AddComponent<RectTransform>();
            var templateImage = templateGO.AddComponent<UnityEngine.UI.Image>();
            templateImage.sprite = Resources.Load<Sprite>("unity_builtin_extra/UISprite");
            templateImage.material = null;
            var templateScrollRect = templateGO.AddComponent<UnityEngine.UI.ScrollRect>();
            //templateScrollRect.content =  TODO

            var viewportGO = new GameObject();
            viewportGO.transform.SetParent(dropdownHolder.transform);
            viewportGO.AddComponent<CanvasRenderer>();
            viewportGO.AddComponent<RectTransform>();
            viewportGO.AddComponent<UnityEngine.UI.Image>();



            harmony = new Harmony("org.spub.plugins.anyportal.harmony");
            harmony.PatchAll();
        }

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
            if (dropdownHolder)
            {
                Destroy(dropdownHolder);
            }
        }


        [HarmonyPatch(typeof(TeleportWorld), "Interact")]
        static class ShipAwake_Patch
        {
            static bool Prefix(TeleportWorld __instance, Humanoid human, bool hold, ref bool __result)
            {
                if (hold)
                {
                    __result = false;
                    return false;
                }
                if (PrivateArea.CheckAccess(__instance.transform.position, 0f, true))
                {
                    //TextInput.instance.RequestText(__instance, "$piece_portal_tag", 10);
                    //UnityEngine.UI.Dropdown dropdown = new UnityEngine.UI.Dropdown();
                    dropdownHolder.SetActive(true);
                    __result = true;
                    return false;
                }
                human.Message(MessageHud.MessageType.Center, "$piece_noaccess", 0, null);
                __result = true;
                return false;
            }
        }
    }
}
