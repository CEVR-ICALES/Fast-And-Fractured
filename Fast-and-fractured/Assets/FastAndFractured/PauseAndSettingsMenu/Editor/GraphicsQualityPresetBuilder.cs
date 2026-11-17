using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FastAndFractured.EditorTools
{
    public static class GraphicsQualityPresetBuilder
    {
        public const string BASE_ASSET_PATH = "Assets/Settings/HDRPDefaultResources/HDRenderPipelineAsset.asset";
        public const string PRESET_FOLDER = "Assets/Settings/Quality";
        private const string QUALITY_SETTINGS_PATH = "ProjectSettings/QualitySettings.asset";
        private const string SETTINGS_ROOT = "m_RenderPipelineSettings.";
        private const int SCALABLE_LEVELS = 3;
        private const string MENU_PATH = "Tools/Fast And Fractured/Graphics/Rebuild Quality Presets";

        public struct Preset
        {
            public string AssetName;
            public bool Volumetrics, VolumetricClouds, SubsurfaceScattering, Distortion, Ssao, ContactShadows;
            public float FogBudget, FogDepthRatio;
            public int AoSteps, AoDirections, AoRadiusPixels;
            public bool AoFullResolution;
            public int ContactShadowSamples, SssSampleBudget;
            public int PunctualShadowAtlas, AreaShadowAtlas, MaxShadowRequests;
            public int MaxDirectionalShadowResolution, MaxPunctualShadowResolution;
            public int DecalAtlas, CookieAtlas;
            public int BloomResolution, MotionBlurSamples, ChromaticAberrationSamples;
            public bool BloomHighQuality;
            public TerrainQualityOverrides TerrainOverrides;
            public float TerrainDetailDistance, TerrainDetailDensity, TerrainTreeDistance, TerrainPixelError;
        }

        private const int BLOOM_HALF = 2;
        private const int BLOOM_QUARTER = 4;
        private const TerrainQualityOverrides TERRAIN_DISTANCES =
            TerrainQualityOverrides.DetailDistance | TerrainQualityOverrides.DetailDensity | TerrainQualityOverrides.TreeDistance;

        public static readonly Preset[] PRESETS =
        {
            new Preset
            {
                AssetName = "HDRP_VeryLow", Volumetrics = false, VolumetricClouds = false, SubsurfaceScattering = false,
                Distortion = false, Ssao = false, ContactShadows = false, FogBudget = 0.166f, FogDepthRatio = 0.666f,
                AoSteps = 4, AoDirections = 1, AoRadiusPixels = 24, AoFullResolution = false, ContactShadowSamples = 6,
                SssSampleBudget = 10, PunctualShadowAtlas = 1024, AreaShadowAtlas = 512, MaxShadowRequests = 16,
                MaxDirectionalShadowResolution = 512, MaxPunctualShadowResolution = 512, DecalAtlas = 1024, CookieAtlas = 512,
                BloomResolution = BLOOM_QUARTER, BloomHighQuality = false, MotionBlurSamples = 4, ChromaticAberrationSamples = 3,
                TerrainOverrides = TERRAIN_DISTANCES | TerrainQualityOverrides.PixelError, TerrainDetailDistance = 50f,
                TerrainDetailDensity = 0.25f, TerrainTreeDistance = 400f, TerrainPixelError = 15f,
            },
            new Preset
            {
                AssetName = "HDRP_Low", Volumetrics = false, VolumetricClouds = true, SubsurfaceScattering = true,
                Distortion = true, Ssao = true, ContactShadows = false, FogBudget = 0.166f, FogDepthRatio = 0.666f,
                AoSteps = 4, AoDirections = 1, AoRadiusPixels = 24, AoFullResolution = false, ContactShadowSamples = 6,
                SssSampleBudget = 10, PunctualShadowAtlas = 1024, AreaShadowAtlas = 512, MaxShadowRequests = 32,
                MaxDirectionalShadowResolution = 1024, MaxPunctualShadowResolution = 512, DecalAtlas = 1024, CookieAtlas = 512,
                BloomResolution = BLOOM_QUARTER, BloomHighQuality = false, MotionBlurSamples = 4, ChromaticAberrationSamples = 3,
                TerrainOverrides = TERRAIN_DISTANCES | TerrainQualityOverrides.PixelError, TerrainDetailDistance = 80f,
                TerrainDetailDensity = 0.4f, TerrainTreeDistance = 600f, TerrainPixelError = 10f,
            },
            new Preset
            {
                AssetName = "HDRP_Medium", Volumetrics = true, VolumetricClouds = true, SubsurfaceScattering = true,
                Distortion = true, Ssao = true, ContactShadows = false, FogBudget = 0.166f, FogDepthRatio = 0.666f,
                AoSteps = 4, AoDirections = 1, AoRadiusPixels = 32, AoFullResolution = false, ContactShadowSamples = 6,
                SssSampleBudget = 20, PunctualShadowAtlas = 2048, AreaShadowAtlas = 1024, MaxShadowRequests = 48,
                MaxDirectionalShadowResolution = 1024, MaxPunctualShadowResolution = 1024, DecalAtlas = 2048, CookieAtlas = 1024,
                BloomResolution = BLOOM_QUARTER, BloomHighQuality = false, MotionBlurSamples = 4, ChromaticAberrationSamples = 3,
                TerrainOverrides = TERRAIN_DISTANCES, TerrainDetailDistance = 120f, TerrainDetailDensity = 0.6f,
                TerrainTreeDistance = 1000f, TerrainPixelError = 5f,
            },
            new Preset
            {
                AssetName = "HDRP_High", Volumetrics = true, VolumetricClouds = true, SubsurfaceScattering = true,
                Distortion = true, Ssao = true, ContactShadows = true, FogBudget = 0.25f, FogDepthRatio = 0.666f,
                AoSteps = 6, AoDirections = 2, AoRadiusPixels = 32, AoFullResolution = false, ContactShadowSamples = 8,
                SssSampleBudget = 30, PunctualShadowAtlas = 2048, AreaShadowAtlas = 1024, MaxShadowRequests = 64,
                MaxDirectionalShadowResolution = 2048, MaxPunctualShadowResolution = 1024, DecalAtlas = 2048, CookieAtlas = 1024,
                BloomResolution = BLOOM_HALF, BloomHighQuality = false, MotionBlurSamples = 6, ChromaticAberrationSamples = 6,
                TerrainOverrides = TERRAIN_DISTANCES, TerrainDetailDistance = 150f, TerrainDetailDensity = 0.8f,
                TerrainTreeDistance = 1500f, TerrainPixelError = 5f,
            },
            new Preset
            {
                AssetName = "HDRP_VeryHigh", Volumetrics = true, VolumetricClouds = true, SubsurfaceScattering = true,
                Distortion = true, Ssao = true, ContactShadows = true, FogBudget = 0.33f, FogDepthRatio = 0.666f,
                AoSteps = 6, AoDirections = 2, AoRadiusPixels = 40, AoFullResolution = false, ContactShadowSamples = 10,
                SssSampleBudget = 40, PunctualShadowAtlas = 4096, AreaShadowAtlas = 2048, MaxShadowRequests = 96,
                MaxDirectionalShadowResolution = 2048, MaxPunctualShadowResolution = 2048, DecalAtlas = 4096, CookieAtlas = 2048,
                BloomResolution = BLOOM_HALF, BloomHighQuality = true, MotionBlurSamples = 8, ChromaticAberrationSamples = 6,
                TerrainOverrides = TERRAIN_DISTANCES, TerrainDetailDistance = 250f, TerrainDetailDensity = 1f,
                TerrainTreeDistance = 2500f, TerrainPixelError = 5f,
            },
        };

        [MenuItem(MENU_PATH)]
        public static void RebuildFromMenu()
        {
            Rebuild();
            Debug.Log($"[GraphicsQuality] Rebuilt {PRESETS.Length} HDRP presets in {PRESET_FOLDER} and assigned every quality level.");
        }

        public static void Rebuild()
        {
            var baseAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(BASE_ASSET_PATH);
            if (baseAsset == null)
            {
                Debug.LogError($"[GraphicsQuality] Base HDRP asset not found at {BASE_ASSET_PATH}.");
                return;
            }
            if (!AssetDatabase.IsValidFolder(PRESET_FOLDER))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(PRESET_FOLDER).Replace('\\', '/'), Path.GetFileName(PRESET_FOLDER));

            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(QUALITY_SETTINGS_PATH)[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            for (int level = 0; level < levels.arraySize; level++)
            {
                var levelProperty = levels.GetArrayElementAtIndex(level);
                if (level < PRESETS.Length)
                {
                    var preset = PRESETS[level];
                    levelProperty.FindPropertyRelative("customRenderPipeline").objectReferenceValue = BuildPreset(baseAsset, preset);
                    WriteTerrain(levelProperty, preset);
                }
                else
                {
                    levelProperty.FindPropertyRelative("customRenderPipeline").objectReferenceValue = baseAsset;
                    levelProperty.FindPropertyRelative("terrainQualityOverrides").intValue = 0;
                }
            }
            quality.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static RenderPipelineAsset BuildPreset(RenderPipelineAsset baseAsset, Preset preset)
        {
            string path = $"{PRESET_FOLDER}/{preset.AssetName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
            if (asset == null)
            {
                AssetDatabase.CopyAsset(BASE_ASSET_PATH, path);
                asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
            }
            else
            {
                EditorUtility.CopySerialized(baseAsset, asset);
                asset.name = preset.AssetName;
            }

            var so = new SerializedObject(asset);
            SetBool(so, "supportVolumetrics", preset.Volumetrics);
            SetBool(so, "supportVolumetricClouds", preset.VolumetricClouds);
            SetBool(so, "supportSubsurfaceScattering", preset.SubsurfaceScattering);
            SetBool(so, "supportDistortion", preset.Distortion);
            SetBool(so, "supportSSAO", preset.Ssao);
            // Volumes pick Low/Medium/High per component: every tier gets the same cap.
            SetAllTiers(so, "lightSettings.useContactShadow.m_Values", p => p.boolValue = preset.ContactShadows);
            SetAllTiers(so, "lightingQualitySettings.Fog_Budget", p => p.floatValue = preset.FogBudget);
            SetAllTiers(so, "lightingQualitySettings.Fog_DepthRatio", p => p.floatValue = preset.FogDepthRatio);
            SetAllTiers(so, "lightingQualitySettings.AOStepCount", p => p.intValue = preset.AoSteps);
            SetAllTiers(so, "lightingQualitySettings.AODirectionCount", p => p.intValue = preset.AoDirections);
            SetAllTiers(so, "lightingQualitySettings.AOMaximumRadiusPixels", p => p.intValue = preset.AoRadiusPixels);
            SetAllTiers(so, "lightingQualitySettings.AOFullRes", p => p.boolValue = preset.AoFullResolution);
            SetAllTiers(so, "lightingQualitySettings.ContactShadowSampleCount", p => p.intValue = preset.ContactShadowSamples);
            SetAllTiers(so, "sssSampleBudget.m_Values", p => p.intValue = preset.SssSampleBudget);
            SetAllTiers(so, "postProcessQualitySettings.BloomRes", p => p.intValue = preset.BloomResolution);
            SetAllTiers(so, "postProcessQualitySettings.BloomHighQualityFiltering", p => p.boolValue = preset.BloomHighQuality);
            SetAllTiers(so, "postProcessQualitySettings.MotionBlurSampleCount", p => p.intValue = preset.MotionBlurSamples);
            SetAllTiers(so, "postProcessQualitySettings.ChromaticAberrationMaxSamples", p => p.intValue = preset.ChromaticAberrationSamples);
            SetInt(so, "hdShadowInitParams.punctualLightShadowAtlas.shadowAtlasResolution", preset.PunctualShadowAtlas);
            SetInt(so, "hdShadowInitParams.areaLightShadowAtlas.shadowAtlasResolution", preset.AreaShadowAtlas);
            SetInt(so, "hdShadowInitParams.maxShadowRequests", preset.MaxShadowRequests);
            SetInt(so, "hdShadowInitParams.maxDirectionalShadowMapResolution", preset.MaxDirectionalShadowResolution);
            SetInt(so, "hdShadowInitParams.maxPunctualShadowMapResolution", preset.MaxPunctualShadowResolution);
            SetInt(so, "hdShadowInitParams.maxAreaShadowMapResolution", preset.MaxPunctualShadowResolution);
            SetInt(so, "decalSettings.atlasWidth", preset.DecalAtlas);
            SetInt(so, "decalSettings.atlasHeight", preset.DecalAtlas);
            SetInt(so, "lightLoopSettings.cookieAtlasSize", preset.CookieAtlas);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void WriteTerrain(SerializedProperty level, Preset preset)
        {
            level.FindPropertyRelative("terrainQualityOverrides").intValue = (int)preset.TerrainOverrides;
            level.FindPropertyRelative("terrainDetailDistance").floatValue = preset.TerrainDetailDistance;
            level.FindPropertyRelative("terrainDetailDensityScale").floatValue = preset.TerrainDetailDensity;
            level.FindPropertyRelative("terrainTreeDistance").floatValue = preset.TerrainTreeDistance;
            level.FindPropertyRelative("terrainPixelError").floatValue = preset.TerrainPixelError;
        }

        private static SerializedProperty Find(SerializedObject so, string relativePath)
        {
            var property = so.FindProperty(SETTINGS_ROOT + relativePath);
            if (property == null)
                throw new System.InvalidOperationException($"HDRP setting not found: {relativePath}");
            return property;
        }

        private static void SetBool(SerializedObject so, string path, bool value) => Find(so, path).boolValue = value;
        private static void SetInt(SerializedObject so, string path, int value) => Find(so, path).intValue = value;

        private static void SetAllTiers(SerializedObject so, string arrayPath, System.Action<SerializedProperty> write)
        {
            var array = Find(so, arrayPath);
            for (int i = 0; i < Mathf.Min(SCALABLE_LEVELS, array.arraySize); i++)
                write(array.GetArrayElementAtIndex(i));
        }
    }

    [InitializeOnLoad]
    internal static class GraphicsQualityPlayModeGuard
    {
        private const string EDIT_LEVEL_KEY = "FastAndFractured.GraphicsQuality.EditLevel";

        static GraphicsQualityPlayModeGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                SessionState.SetInt(EDIT_LEVEL_KEY, QualitySettings.GetQualityLevel());
                return;
            }
            if (change != PlayModeStateChange.EnteredEditMode)
                return;
            int level = SessionState.GetInt(EDIT_LEVEL_KEY, -1);
            if (level >= 0 && level != QualitySettings.GetQualityLevel())
                QualitySettings.SetQualityLevel(level, true);
        }
    }
}
