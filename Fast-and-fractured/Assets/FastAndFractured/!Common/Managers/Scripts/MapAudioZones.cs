using System;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    [DisallowMultipleComponent]
    public sealed class MapAudioZones : MonoBehaviour
    {
        private const string ZONE_LAYER_NAME = "Ignore Raycast";
        private AudioZoneSettings _settings;

        private void Start()
        {
            if (!GameplayAudio.CanPlay) return;
            var scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded) return;
            _settings = AudioPresentationProfile.Load().Zones;
            var market = new List<Renderer>();
            var construction = new List<Renderer>();
            var bridges = new HashSet<Transform>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var item in root.GetComponentsInChildren<Transform>(true))
                {
                    string name = item.name.ToLowerInvariant();
                    if (name.Contains("mercadillo")) market.AddRange(item.GetComponentsInChildren<Renderer>());
                    if (scene.name.Contains("Construction") && (name.Contains("constructionmen") || name.Contains("excavator") || name.Contains("bulldozer") || name.Contains("towercrane")))
                        construction.AddRange(item.GetComponentsInChildren<Renderer>());
                    if (name.StartsWith("puente", StringComparison.Ordinal) && (item.parent == null || !item.parent.name.ToLowerInvariant().StartsWith("puente", StringComparison.Ordinal))) bridges.Add(item);
                }
            }
            CreateZone("Market ambience", market, GameAudioCues.AmbienceSoundsMercadilloAmbience, true, _settings.MarketPadding);
            CreateZone("Construction ambience", construction, GameAudioCues.AmbienceSoundsConstructionZoneAmbience, true, _settings.ConstructionPadding);
            foreach (var bridge in bridges)
                CreateZone("Bridge creaks", new List<Renderer>(bridge.GetComponentsInChildren<Renderer>()), GameAudioCues.CommonWoodCreak, false, _settings.BridgePadding);
        }

        private void CreateZone(string zoneName, List<Renderer> renderers, global::Utilities.Audio.AudioCueId cue, bool loop, float padding)
        {
            if (renderers.Count == 0) return;
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            bounds.Expand(_settings.ExpansionFor(padding));
            var zone = new GameObject(zoneName);
            zone.layer = LayerMask.NameToLayer(ZONE_LAYER_NAME);
            zone.transform.SetParent(transform);
            zone.transform.position = bounds.center;
            var volume = zone.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = bounds.size;
            var audio = zone.AddComponent<TriggerAudioController>();
            audio.ConfigureCue(cue, loop, _settings.Volume);
        }
    }
}
