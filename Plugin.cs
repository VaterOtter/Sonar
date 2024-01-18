using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using TinyResort;
using UnityEngine;

namespace Sonar
{
    [BepInDependency("dev.TinyResort.TRTools", "0.8.5")]
    [BepInPlugin("Sonar", "Sonar", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private AssetBundle LoadAssetBundle(string Name)
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            string name = executingAssembly.GetManifestResourceNames().ToList<string>().Find((string CurrentResource) => CurrentResource.Contains(Name));
            return AssetBundle.LoadFromStream(executingAssembly.GetManifestResourceStream(name));
        }

        private void StoreModId()
        {
            ConfigEntry<int> configEntry = base.Config.Bind<int>("Developer", "NexusID", ModId, "nexus mod id -- automatically generated -- cannot be changed");
            if (configEntry.Value != ModId)
            {
                configEntry.Value = ModId;
                base.Config.Save();
            }
        }

        private void Awake()
        {
            HideInsteadOfSpin = base.Config.Bind<bool>("General", "HideInsteadOfSpin", false, "'true' to hide arrow when no treasure found - 'false' to spin arrow when no treasure found").Value;
            RequireLicenses = base.Config.Bind<bool>("General", "RequireLicenses", true, "'true' to require licenses - 'false' to not require them").Value;
            SearchInWater = base.Config.Bind<bool>("General", "SearchInWater", false, "'true' to also search water for treasure - 'false' to search only land for treasure").Value;
            EnableMod = base.Config.Bind<bool>("General", "EnableMod", true, "'true' to enable the mod - 'false' to disable the mod").Value;
            License1Cost = Mathf.Clamp(base.Config.Bind<int>("General", "License1Cost", 2000, "cost of license level 1 - minimum 1").Value, 1, int.MaxValue);
            License2Cost = Mathf.Clamp(base.Config.Bind<int>("General", "License2Cost", 5000, "cost of license level 2 - minimum 1").Value, 1, int.MaxValue);
            License3Cost = Mathf.Clamp(base.Config.Bind<int>("General", "License3Cost", 9000, "cost of license level 3 - minimum 1").Value, 1, int.MaxValue);
            this.StoreModId();
            ArrowPrefab = this.LoadAssetBundle("advancedmetaldetector").LoadAsset<GameObject>("MetalDetectorArrow");
            FrameworkInstance = TRTools.Initialize(this, ModId, "");
            base.Config.Remove(new ConfigDefinition("Developer", "DebugMode"));
            base.Config.Save();
            License = Plugin.FrameworkInstance.AddLicence(0, "Advanced Metal Detection", 3);
            License.SetLevelInfo(1, "Auto detect within normal range.", Plugin.License1Cost);
            License.SetLevelInfo(2, "Increase range to visible area of world.", Plugin.License2Cost);
            License.SetLevelInfo(3, "Increase range to entire island.", Plugin.License3Cost);
            License.AddPrerequisite(LicenceManager.LicenceTypes.MetalDetecting, 2);
        }

        private static void FindArrowTarget(Vector3 CharacterPosition)
        {
            WorldManager manageWorld = WorldManager.Instance;
            bool[,] waterMap = manageWorld.waterMap;
            BuriedManager manage = BuriedManager.manage;
            TreasureFound = false;
            float num = 0f;
            float y = CharacterPosition.y;
            float num2 = float.MaxValue;
            if (RequireLicenses)
            {
                if (License.level == 1)
                {
                    num2 = new Vector2(2f, 2f).sqrMagnitude;
                }
                else if (License.level == 2)
                {
                    num2 = new Vector2((float)(NewChunkLoader.loader.getChunkDistance() * 10), (float)(NewChunkLoader.loader.getChunkDistance() * 10)).sqrMagnitude;
                }
            }
            for (int i = 0; i < 1000; i++)
            {
                for (int j = 0; j < 1000; j++)
                {
                    if (manageWorld.onTileMap[i, j] == 30 && (SearchInWater || !waterMap[i, j]) && manage.checkIfBuriedItem(i, j) == null)
                    {
                        Vector3 vector = new Vector3((float)(i * 2), y, (float)(j * 2));
                        float sqrMagnitude = (vector - CharacterPosition).sqrMagnitude;
                        if (sqrMagnitude < num2)
                        {
                            if (TreasureFound)
                            {
                                if (sqrMagnitude < num)
                                {
                                    num = sqrMagnitude;
                                    ArrowTargetPosition = vector;
                                }
                            }
                            else
                            {
                                TreasureFound = true;
                                num = sqrMagnitude;
                                ArrowTargetPosition = vector;
                            }
                        }
                    }
                }
            }
        }

        private void Update()
        {
            if (NetworkMapSharer.Instance.localChar == null)
            {
                if (Ready)
                {
                    ArrowObject = null;
                    ArrowTransform = null;
                    Ready = false;
                    return;
                }
            }
            else if (EnableMod)
            {
                if (!Ready)
                {
                    ArrowObject = UnityEngine.Object.Instantiate<GameObject>(ArrowPrefab);
                    ArrowObject.SetActive(false);
                    ArrowTransform = ArrowObject.transform;
                    Ready = true;
                }
                Vector3 position = NetworkMapSharer.Instance.localChar.transform.position;
                float y = position.y;
                bool flag = y >= -5f && y <= 12f;
                bool flag2 = !RequireLicenses || License.level > 0;
                if (flag && flag2 && Inventory.Instance.invSlots[Inventory.Instance.selectedSlot].itemNo == 103)
                {
                    FindArrowTarget(position);
                    ArrowTransform.position = position;
                    if (TreasureFound)
                    {
                        ArrowObject.SetActive(true);
                        ArrowTransform.LookAt(ArrowTargetPosition);
                        ArrowTransform.eulerAngles = new Vector3(0f, ArrowTransform.eulerAngles.y, 0f);
                        return;
                    }
                    CurrentRotationForTreasureNotFound = (CurrentRotationForTreasureNotFound + Time.deltaTime * 360f) % 360f;
                    ArrowTransform.eulerAngles = new Vector3(0f, CurrentRotationForTreasureNotFound, 0f);
                    ArrowObject.SetActive(! HideInsteadOfSpin);
                    return;
                }
                else
                {
                    Plugin.ArrowObject.SetActive(false);
                }
            }
        }

        private const int ModId = 328;

        private const int MapSize = 1000;

        private const int ChunkSize = 10;

        private const int MetalDetectorItemId = 103;

        private const int BuriedItemTileId = 30;

        private const int MinimumY = -5;

        private const int MaximumY = 12;

        private static bool Ready;

        private static bool TreasureFound;

        private static bool HideInsteadOfSpin;

        private static bool RequireLicenses;

        private static bool SearchInWater;

        private static bool EnableMod;

        private static int License1Cost;

        private static int License2Cost;

        private static int License3Cost;

        private static GameObject ArrowPrefab;

        private static GameObject ArrowObject;

        private static Transform ArrowTransform;

        private static Vector3 ArrowTargetPosition;

        private static float CurrentRotationForTreasureNotFound;

        private static TRPlugin FrameworkInstance;

        private static TRCustomLicence License;
    }
}
