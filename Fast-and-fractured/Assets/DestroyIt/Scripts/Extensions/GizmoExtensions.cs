using System.Collections.Generic;
using UnityEngine;

namespace DestroyIt
{
    public static class GizmoExtensions
    {
        /// <summary>Creates an editor-visible Gizmo for each damage effect, showing where they will play.</summary>
        public static void DrawGizmos(this List<DamageEffect> damageEffects, Transform transform)
        {
            if (damageEffects == null) return;

            foreach (DamageEffect effect in damageEffects)
            {
                if (effect == null) continue;
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(transform.TransformPoint(effect.Offset), new Vector3(0.1f, 0.1f, 0.1f));
                Quaternion rotatedVector = transform.rotation * Quaternion.Euler(effect.Rotation);
                Gizmos.DrawRay(transform.TransformPoint(effect.Offset), rotatedVector * Vector3.forward * .5f);
            }
        }

        /// <summary>Creates an editor-visible Gizmo for each CenterPointOverride for a Fallback Particle Effect.</summary>
        public static void DrawGizmos(this Vector3 centerPointOverride, Transform transform)
        {
            if (centerPointOverride == Vector3.zero) return;
            
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.TransformPoint(centerPointOverride), 0.1f);
        }
    }
}
