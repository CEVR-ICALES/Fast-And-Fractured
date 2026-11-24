using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace FastAndFractured
{
    public static class DebugVolumes
    {
        public const int NONE = -1;

        public static int Count(global::VolumeManager manager) =>
            manager != null && manager.VolumeList != null ? manager.VolumeList.Count : 0;

        public static int CurrentIndex(global::VolumeManager manager) =>
            Count(manager) > 0 ? manager.VolumeList.IndexOf(manager.CurrentVolumeGameObject) : NONE;

        public static string NameOf(global::VolumeManager manager, int index) =>
            IsValid(manager, index) ? manager.VolumeList[index].name : string.Empty;

        public static Volume[] VolumesOf(global::VolumeManager manager) =>
            manager != null && manager.CurrentVolumeGameObject != null
                ? manager.CurrentVolumeGameObject.GetComponentsInChildren<Volume>()
                : Array.Empty<Volume>();

        public static bool Show(global::VolumeManager manager, int index)
        {
            if (!IsValid(manager, index)) return false;
            manager.ChangeCurrentVolume(index);
            return true;
        }

        public static int Step(int current, int count, int step)
        {
            if (count <= 0) return NONE;
            if (current < 0 || current >= count) return step < 0 ? count - 1 : 0;
            return ((current + step) % count + count) % count;
        }

        private static bool IsValid(global::VolumeManager manager, int index) =>
            index >= 0 && index < Count(manager) && manager.VolumeList[index] != null;
    }
}
