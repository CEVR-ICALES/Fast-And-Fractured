using UnityEngine;
using System.Collections.Generic;
// ReSharper disable InconsistentNaming

namespace DestroyIt
{
    /// <summary>
    /// TagIt is a system that allows you to mark objects with multiple tags. It is used by DestroyIt to keep track of debris,
    /// destructible groups, cling points, etc.
    /// If you are using TagIt to specify hit effects on a destructible object, you should use the HitEffects script instead.
    /// </summary>
    [DisallowMultipleComponent]
    public class TagIt : MonoBehaviour 
    {
        public List<Tag> tags;

        public void OnEnable()
        {
            if (tags == null)
                tags = new List<Tag>();

            if (tags.Count == 0)
                tags.Add(Tag.Untagged);
        }
    }
}