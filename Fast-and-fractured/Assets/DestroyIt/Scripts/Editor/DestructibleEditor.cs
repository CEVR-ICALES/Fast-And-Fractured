using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
// ReSharper disable SuggestVarOrType_BuiltInTypes

namespace DestroyIt
{
    [CustomEditor(typeof(Destructible)), CanEditMultipleObjects]
    public class DestructibleEditor : Editor
    {
        private GameObject previousDestroyedPrefab;
        private SerializedProperty fallbackParticle;
        private SerializedProperty fallbackParticleMatOption;
        private SerializedProperty totalHitPoints;
        private SerializedProperty currentHitPoints;
        private SerializedProperty ignoreCollisionsUnder;
        private SerializedProperty canBeDestroyed;
        private SerializedProperty canBeRepaired;
        private SerializedProperty isDebrisChipAway;
        private SerializedProperty chipAwayDebrisMass;
        private SerializedProperty chipAwayDebrisDrag;
        private SerializedProperty chipAwayDebrisAngularDrag;
        private SerializedProperty autoPoolDestroyedPrefab;
        private SerializedProperty useFallbackParticle;
        private SerializedProperty centerPointOverride;
        private SerializedProperty fallbackParticleScale;
        private SerializedProperty fallbackParticleParent;
        private SerializedProperty sinkWhenDestroyed;
	    private SerializedProperty destroyedPrefabParent;
        private SerializedProperty limitDamage;
        private SerializedProperty maxDamage;
        private SerializedProperty minDamage;
        private SerializedProperty minDamageTime;

        public void OnEnable()
        {
            fallbackParticle = serializedObject.FindProperty("fallbackParticle");
            fallbackParticleMatOption = serializedObject.FindProperty("fallbackParticleMatOption");
            totalHitPoints = serializedObject.FindProperty("_totalHitPoints");
            currentHitPoints = serializedObject.FindProperty("_currentHitPoints");
            ignoreCollisionsUnder = serializedObject.FindProperty("ignoreCollisionsUnder");
            canBeDestroyed = serializedObject.FindProperty("canBeDestroyed");
            canBeRepaired = serializedObject.FindProperty("canBeRepaired");
            isDebrisChipAway = serializedObject.FindProperty("isDebrisChipAway");
            chipAwayDebrisMass = serializedObject.FindProperty("chipAwayDebrisMass");
            chipAwayDebrisDrag = serializedObject.FindProperty("chipAwayDebrisDrag");
            chipAwayDebrisAngularDrag = serializedObject.FindProperty("chipAwayDebrisAngularDrag");
            autoPoolDestroyedPrefab = serializedObject.FindProperty("autoPoolDestroyedPrefab");
            useFallbackParticle = serializedObject.FindProperty("useFallbackParticle");
            centerPointOverride = serializedObject.FindProperty("centerPointOverride");
            fallbackParticleScale = serializedObject.FindProperty("fallbackParticleScale");
            sinkWhenDestroyed = serializedObject.FindProperty("sinkWhenDestroyed");
	        destroyedPrefabParent = serializedObject.FindProperty("destroyedPrefabParent");
            fallbackParticleParent = serializedObject.FindProperty("fallbackParticleParent");
            limitDamage = serializedObject.FindProperty("limitDamage");
            minDamage = serializedObject.FindProperty("minDamage");
            maxDamage = serializedObject.FindProperty("maxDamage");
            minDamageTime = serializedObject.FindProperty("minDamageTime");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            Destructible destructible = target as Destructible;
            bool hasRigidbody = destructible.GetComponent<Rigidbody>() != null;
            List<string> materialOptions = new List<string>();
            List<string> particleMatOptions = new List<string>();
            List<string> debrisOptions = new List<string>();
            List<string> destructibleChildrenOptions = new List<string>();

            #region BASIC DESTRUCTIBLE ATTRIBUTES
            EditorGUILayout.Separator();
            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("Basic Attributes", EditorStyles.boldLabel);
            // TOTAL HIT POINTS
            EditorGUILayout.PropertyField(totalHitPoints, new GUIContent("Total Hit Points", "The maximum, or starting hit points, of the object."));
            if (totalHitPoints.floatValue < 0)
                totalHitPoints.floatValue = 0;
            if (Math.Abs(totalHitPoints.floatValue - destructible.TotalHitPoints) > 0.01f) // value has changed, so change the current hit points to match.
                currentHitPoints.floatValue = totalHitPoints.floatValue;
            destructible.TotalHitPoints = totalHitPoints.floatValue;

            // CURRENT HIT POINTS
            EditorGUILayout.PropertyField(currentHitPoints, new GUIContent("Current Hit Points", "The current hit points of the object."));
            if (currentHitPoints.floatValue > destructible.TotalHitPoints)
                currentHitPoints.floatValue = destructible.TotalHitPoints;
            if (currentHitPoints.floatValue < 0)
                currentHitPoints.floatValue = 0;
            destructible.CurrentHitPoints = currentHitPoints.floatValue;

            // CAN BE DESTROYED
            canBeDestroyed.boolValue = EditorGUILayout.Toggle(new GUIContent("Can Be Destroyed", "If checked, the object can be damaged until it is destroyed. If unchecked, the object can still be damaged and will display any progressive damage and damage effects, but cannot be destroyed."), canBeDestroyed.boolValue);
            destructible.canBeDestroyed = canBeDestroyed.boolValue;
            
            // CAN BE REPAIRED
            canBeRepaired.boolValue = EditorGUILayout.Toggle(new GUIContent("Can Be Repaired", "If checked, this object is capable of being repaired. If unchecked, this object will ignore any attempt to repair it."), canBeRepaired.boolValue);
            destructible.canBeRepaired = canBeRepaired.boolValue;

            // SINK INTO GROUND INSTEAD OF DESTROYING INTO DEBRIS
            if (hasRigidbody && destructible.canBeDestroyed)
            {
                GUILayout.BeginHorizontal();
                sinkWhenDestroyed.boolValue = EditorGUILayout.Toggle(new GUIContent("Sink on Destroy", "If checked, this object will sink into the ground when destroyed, instead of emitting a particle effect or swapping to a destroyed prefab. Requires a rigidbody on the object."), sinkWhenDestroyed.boolValue);
                GUILayout.EndHorizontal();
                destructible.sinkWhenDestroyed = sinkWhenDestroyed.boolValue;
            }
            else
                destructible.sinkWhenDestroyed = false;

            // VELOCITY REDUCTION
            destructible.velocityReduction = EditorGUILayout.Slider(new GUIContent("Velocity Reduction", "How much this object reduces the velocity of fast-moving objects that impact and destroy it."), destructible.velocityReduction, 0f, 1f);

            // IGNORE COLLISIONS UNDER
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Ignore Collisions Under", "Collisions with this object will be ignored if they are under this magnitude. Useful for things like tires, which constantly have small-to-medium collisions with the terrain."), GUILayout.Width(140));
            GUILayout.FlexibleSpace();
            EditorGUILayout.PropertyField(ignoreCollisionsUnder, GUIContent.none, true, GUILayout.MinWidth(10));
            GUILayout.FlexibleSpace();
            GUILayout.Label("Magnitude", GUILayout.Width(65));
            GUILayout.EndHorizontal();
            destructible.ignoreCollisionsUnder = ignoreCollisionsUnder.floatValue;

            // MIN AND MAX DAMAGE
            limitDamage.boolValue = EditorGUILayout.Toggle(new GUIContent("Limit Damage", "Provides options for the minimum and maximum damage the object will take per hit, as well as the amount of time that must pass before taking more damage."), limitDamage.boolValue);
            destructible.limitDamage = limitDamage.boolValue;
            if (destructible.limitDamage)
            {
                EditorGUILayout.BeginHorizontal();
                if (destructible.maxDamage < 0f)
                    GUI.color = Color.red;
                EditorGUILayout.LabelField("", GUILayout.Width(15));
                destructible.maxDamage = EditorGUILayout.FloatField(new GUIContent("Max Damage", "The maximum amount of damage the object can receive per hit. Must be greater than or equal to zero. If zero, the object will not take any damage."), maxDamage.floatValue);
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
            
                EditorGUILayout.BeginHorizontal();
                if (destructible.minDamage < 0f || destructible.minDamage > destructible.maxDamage)
                    GUI.color = Color.red;
                EditorGUILayout.LabelField("", GUILayout.Width(15));
                destructible.minDamage = EditorGUILayout.FloatField(new GUIContent("Min Damage", "The minimum amount of damage the object will receive per hit. Must be greater than or equal to zero. Must be less than or equal to the Max Damage limit."), minDamage.floatValue);
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (destructible.minDamageTime < 0f)
                    GUI.color = Color.red;
                EditorGUILayout.LabelField("", GUILayout.Width(15));
                destructible.minDamageTime = EditorGUILayout.FloatField(new GUIContent("Min Time Between", "After being damaged, the minimum amount of time (in seconds) that must pass before the object can be damaged again. Must be greater than or equal to zero."), minDamageTime.floatValue);
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Separator();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Separator();
            #endregion

            #region AUDIO
            EditorGUILayout.Separator();
            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("Audio", EditorStyles.boldLabel);

            destructible.destroyedSound = (AudioClip)EditorGUILayout.ObjectField(new GUIContent("Destroyed Sound", "The audio clip that will play when this destructible object is destroyed."), destructible.destroyedSound, typeof(AudioClip), false);
            destructible.damagedSound = (AudioClip)EditorGUILayout.ObjectField(new GUIContent("Damaged Sound", "The audio clip that will play with this destructible object is damaged."), destructible.damagedSound, typeof(AudioClip), false);
            destructible.repairedSound = (AudioClip)EditorGUILayout.ObjectField(new GUIContent("Repaired Sound", "The audio clip that will play when this destructible object is repaired."), destructible.repairedSound, typeof(AudioClip), false);

            EditorGUILayout.Separator();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Separator();
            #endregion

            #region DAMAGE LEVELS
            // Initialize and default the damage levels if needed.
            if (destructible.damageLevels == null || destructible.damageLevels.Count == 0)
                destructible.damageLevels = DestructibleHelper.DefaultDamageLevels();
            
            EditorGUILayout.BeginVertical("Box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Damage Levels", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("HP %", GUILayout.Width(40));
            EditorGUILayout.LabelField("HP Range", GUILayout.Width(66));

            // Find out if there are any materials on the destructible object that are capable of showing visual damage levels.
            Renderer[] renderers = destructible.GetComponentsInChildren<Renderer>();
            destructible.UseProgressiveDamage = false;
            foreach (Renderer rend in renderers)
            { 
                foreach (Material mat in rend.sharedMaterials)
                {
                    if (!mat.IsProgressiveDamageCapable()) continue;
                    destructible.UseProgressiveDamage = true;
                    break;
                }
            }

            GUIContent visibleDamageContent = new GUIContent("Visible Damage", "Visible Damage Levels require using the Standard shader for your materials. You also need to use the Detail Mask to control visibility, and both Secondary Maps to add damage.");
            EditorGUILayout.LabelField( visibleDamageContent, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            int[] popupValues = {0, 1, 2, 3, 4, 5, 6, 7, 8, 9};
            string[] popupDisplay = {"0 Undamaged","1 Light","2 Light","3 Light","4 Medium","5 Medium","6 Medium","7 Heavy","8 Heavy","9 Heavy"};
            int lowestHealthPercent = 100;
            for (int i=0; i<destructible.damageLevels.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                if (i > 0)
                {
                    if (destructible.damageLevels[i].hasError)
                        GUI.color = Color.red;
                    destructible.damageLevels[i].healthPercent = EditorGUILayout.IntField(destructible.damageLevels[i].healthPercent, GUILayout.Width(40));
                    GUI.color = Color.white;
                }
                else
                {
                    EditorGUI.BeginDisabledGroup(true);
                    destructible.damageLevels[i].healthPercent = 100;
                    EditorGUILayout.IntField(destructible.damageLevels[i].healthPercent, GUILayout.Width(40));
                    EditorGUI.EndDisabledGroup();
                }
                EditorGUILayout.LabelField(destructible.damageLevels[i].maxHitPoints + "-" + destructible.damageLevels[i].minHitPoints, GUILayout.Width(66));
                if (!destructible.UseProgressiveDamage)
                    GUI.enabled = false;
                destructible.damageLevels[i].visibleDamageLevel = EditorGUILayout.IntPopup(destructible.damageLevels[i].visibleDamageLevel, popupDisplay, popupValues);
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                lowestHealthPercent = destructible.damageLevels[i].healthPercent;
            }
            // Add/Remove buttons
            EditorGUILayout.Space();
            bool showAddDamageLevel = destructible.damageLevels.Count <= 10;
            bool showRemoveDamageLevel = destructible.damageLevels.Count > 1;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUILayout.Width(15));

            if (showAddDamageLevel && GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(30)))
                destructible.damageLevels.Add(new DamageLevel {healthPercent = Mathf.RoundToInt(lowestHealthPercent/2f)});

            if (showRemoveDamageLevel && GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(30)))
                destructible.damageLevels.RemoveAt(destructible.damageLevels.Count - 1);

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset", EditorStyles.toolbarButton, GUILayout.Width(50)))
                destructible.damageLevels = DestructibleHelper.DefaultDamageLevels();
            EditorGUILayout.LabelField("", GUILayout.Width(15));

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Separator();

            // DAMAGE EFFECTS
            if (destructible.damageEffects == null)
                destructible.damageEffects = new List<DamageEffect>();

            string[] dropDownValues = new string[destructible.damageLevels.Count+1];
            for (int i=0; i<destructible.damageLevels.Count; i++)
                dropDownValues[i] = destructible.damageLevels[i].healthPercent + "%";
            dropDownValues[destructible.damageLevels.Count] = "Destroyed";

            EditorGUILayout.LabelField("Damage Effects", EditorStyles.boldLabel);
            foreach (DamageEffect damageParticle in destructible.damageEffects)
            {
                EditorGUILayout.BeginVertical("Box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Effect", "The effect that will play at the specified damage level."), GUILayout.Width(60));
                damageParticle.Prefab = EditorGUILayout.ObjectField(damageParticle.Prefab, typeof(GameObject), false) as GameObject;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Play at HP %", "When the destructible object is damaged to this point, this effect will play."), GUILayout.Width(100));
                // Get the selected index of the dropdown, or default to the first one.
                if (damageParticle.TriggeredAt < 0 || damageParticle.TriggeredAt > destructible.damageLevels.Count)
                    damageParticle.TriggeredAt = destructible.damageLevels.Count;
                damageParticle.TriggeredAt = EditorGUILayout.Popup(damageParticle.TriggeredAt, dropDownValues);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Offset", "Here you can specify how much to move the damage effect in relation to the destructible object, so it plays in the right spot."), GUILayout.Width(40));
                damageParticle.Offset = EditorGUILayout.Vector3Field("", damageParticle.Offset, GUILayout.Width(142), GUILayout.Height(18));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Rotate", "Here you can rotate the damage effect independent from the destructible object, so it plays facing the right direction."), GUILayout.Width(40));
                damageParticle.Rotation = EditorGUILayout.Vector3Field("", damageParticle.Rotation, GUILayout.Width(142), GUILayout.Height(18));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Scale", "Here you can override the scale of the damage effect independent from the destructible object, so it plays at the right size."), GUILayout.Width(40));
                damageParticle.Scale = EditorGUILayout.Vector3Field("", damageParticle.Scale, GUILayout.Width(142), GUILayout.Height(18));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                damageParticle.HasTagDependency = EditorGUILayout.Toggle(damageParticle.HasTagDependency, GUILayout.Width(15));
                EditorGUILayout.LabelField(new GUIContent("Only If Tagged", "If checked, this effect will only play if the destructible object has the specified Tag."), GUILayout.Width(95));

                damageParticle.TagDependency = (Tag)EditorGUILayout.EnumPopup(damageParticle.TagDependency);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                damageParticle.UnparentOnDestroy = EditorGUILayout.Toggle(damageParticle.UnparentOnDestroy, GUILayout.Width(15));
                EditorGUILayout.LabelField(new GUIContent("Unparent when destroyed", "If checked, effect will be unparented when the object is destroyed. If unchecked, effect will be destroyed immediately with the object, which will stop any emissions abruptly."), GUILayout.Width(180));
                EditorGUILayout.EndHorizontal();

                if (damageParticle.UnparentOnDestroy)
                { 
                    EditorGUILayout.BeginHorizontal();
                    damageParticle.StopEmittingOnDestroy = EditorGUILayout.Toggle(damageParticle.StopEmittingOnDestroy, GUILayout.Width(15));
                    EditorGUILayout.LabelField(new GUIContent("Stop emitting when destroyed", "If checked, effect will stop emitting particles and be disabled when object is destroyed, allowing particles to disperse over time."), GUILayout.Width(180));
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space();
                EditorGUILayout.EndVertical();
            }
            // Add/Remove buttons
            EditorGUILayout.Space();
            bool showRemoveEffectButton = destructible.damageEffects.Count > 0;
            CreateButtons(destructible.damageEffects, true, showRemoveEffectButton);

            EditorGUILayout.Separator();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Separator();
            #endregion

            #region DESTROYED PREFAB
            // DESTROYED PREFAB
            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("Destroyed Prefab", EditorStyles.boldLabel);
            destructible.destroyedPrefab = EditorGUILayout.ObjectField(destructible.destroyedPrefab, typeof(GameObject), false) as GameObject;

            if (previousDestroyedPrefab == null)
                previousDestroyedPrefab = destructible.destroyedPrefab;

            if (destructible.destroyedPrefab != null)
            {
                #region REPLACE MATERIALS ON DESTROYED PREFAB
                // REPLACE MATERIALS ON DESTROYED PREFAB
                //Get string array of material names on destroyed prefab
                List<Renderer> destroyedMeshes = destructible.destroyedPrefab.GetComponentsInChildren<Renderer>(true).ToList();
                destroyedMeshes.RemoveAll(x => x.GetType() != typeof(MeshRenderer) && x.GetType() != typeof(SkinnedMeshRenderer));
                List<Material> destroyedMaterials = new List<Material>();
                foreach (Renderer mesh in destroyedMeshes)
                {
                    foreach (Material mat in mesh.sharedMaterials)
                    {
                        if (mat != null && !destroyedMaterials.Contains(mat))
                        {
                            destroyedMaterials.Add(mat);
                            materialOptions.Add(mat.name);
                        }
                    }
                }

                // Initialize replaceMaterials if null
                if (destructible.replaceMaterials == null)
                    destructible.replaceMaterials = new List<MaterialMapping>();

                // Clean up replaceMaterials
                if (destructible.replaceMaterials.Count > 0)
                    destructible.replaceMaterials.RemoveAll(x => x.SourceMaterial != null && !materialOptions.Contains(x.SourceMaterial.name));

                EditorGUILayout.Separator();
                EditorGUILayout.LabelField("Material Replacement", EditorStyles.boldLabel);
                if (destroyedMaterials.Count > 0)
                {
                    destroyedMaterials = destroyedMaterials.OrderBy(x => x.name).ToList();
                    materialOptions.Sort();
                    foreach (MaterialMapping mapping in destructible.replaceMaterials)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("", GUILayout.Width(15));
                        EditorGUILayout.LabelField("Replace", GUILayout.Width(50));
                        int selectedMaterialIndex = mapping.SourceMaterial == null ? 0 : destroyedMaterials.IndexOf(mapping.SourceMaterial);

                        selectedMaterialIndex = EditorGUILayout.Popup(selectedMaterialIndex, materialOptions.ToArray());
                        if (selectedMaterialIndex >= 0 && selectedMaterialIndex < materialOptions.Count)
                        {
                            string materialName = materialOptions[selectedMaterialIndex];
                            mapping.SourceMaterial = destroyedMaterials.First(x => x.name == materialName);
                        }
                        else
                            mapping.SourceMaterial = null;

                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("", GUILayout.Width(15));
                        EditorGUILayout.LabelField("With", GUILayout.Width(50));
                        mapping.ReplacementMaterial = EditorGUILayout.ObjectField(mapping.ReplacementMaterial, typeof(Material), false) as Material;
                        EditorGUILayout.EndHorizontal();
                    }
                    // Add/Remove buttons
                    bool showAddButton = destructible.replaceMaterials.Count < materialOptions.Count;
                    bool showRemoveButton = destructible.replaceMaterials.Count > 0;
                    CreateButtons(destructible.replaceMaterials, showAddButton, showRemoveButton);
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("", GUILayout.Width(15));
                    EditorGUILayout.LabelField("No materials found on prefab!", GUILayout.Width(200), GUILayout.Height(16));
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.Separator();
                #endregion
	            
	            EditorGUILayout.BeginHorizontal();
	            EditorGUILayout.PropertyField(destroyedPrefabParent, new GUIContent("Assigned Parent:", "When destroyed, the destroyed prefab will spawn in as a child of this game object. If blank, the destroyed prefab will have no parent."));
	            destructible.destroyedPrefabParent = destroyedPrefabParent.objectReferenceValue as GameObject;
	            EditorGUILayout.EndHorizontal();
	            
                #region DEBRIS TO RE-PARENT
                // DEBRIS TO RE-ATTACH TO PARENT
                List<Transform> debrisObjects = destructible.destroyedPrefab.GetComponentsInChildrenOnly<Transform>(true);
                if (debrisObjects.Count > 0)
                {
                    EditorGUILayout.LabelField(new GUIContent("Debris to Re-Parent:", "Here you can assign debris pieces that will be re-parented to this game object's parent when destroyed. For example, if a character has a sword that gets destroyed, you might want to re-parent the broken handle back to the character's hand bone."));
	                
	                debrisOptions.Add("ALL DEBRIS");
                    foreach (Transform trans in debrisObjects)
                        debrisOptions.Add(trans.name);

                    debrisOptions = debrisOptions.Distinct().ToList();

                    // Initialize DebrisToReparentByName
                    if (destructible.debrisToReParentByName == null)
                        destructible.debrisToReParentByName = new List<string>();

                    if (destructible.debrisToReParentByName.Count > 0)
                    {
                        debrisOptions.Sort();
                        for (int i = 0; i < destructible.debrisToReParentByName.Count; i++)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField("", GUILayout.Width(15));
                            EditorGUILayout.LabelField("Name", GUILayout.Width(50));
                            int currentSelectedIndex = debrisOptions.FindIndex(m => m == destructible.debrisToReParentByName[i]);
                            int selectedDebrisIndex = EditorGUILayout.Popup(currentSelectedIndex, debrisOptions.ToArray());
                            if (selectedDebrisIndex >= 0 && selectedDebrisIndex < debrisOptions.Count)
                                destructible.debrisToReParentByName[i] = debrisOptions[selectedDebrisIndex];
                            else
                                destructible.debrisToReParentByName[i] = debrisOptions[0];

                            EditorGUILayout.EndHorizontal();
                        }
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("", GUILayout.Width(15));
                        destructible.debrisToReParentIsKinematic = EditorGUILayout.Toggle(new GUIContent("Make Kinematic", "When FALSE, debris pieces that are reparented under this game object's parent will be affected by gravity/forces once the object is destroyed. Set to TRUE for things like a sword handle reparented to a rigged hand bone."), destructible.debrisToReParentIsKinematic);
                        EditorGUILayout.EndHorizontal();
                    }
	                
                    // Add/Remove buttons
	                bool showReparentAddButton = destructible.debrisToReParentByName.Count < debrisOptions.Count;
                    bool showReparentRemoveButton = destructible.debrisToReParentByName.Count > 0;
                    CreateButtons(destructible.debrisToReParentByName, showReparentAddButton, showReparentRemoveButton);
                }
                #endregion

                #region RE-PARENT CHILDREN TO DESTROYED PREFAB
                //RE-PARENT CHILDREN TO DESTROYED PREFAB
                List<Transform> destructibleChildren = destructible.gameObject.GetComponentsInChildrenOnly<Transform>(true);
                if (destructibleChildren.Count > 0)
                {
                    EditorGUILayout.LabelField(new GUIContent("Re-Parent to Destroyed Prefab:", "These child objects will be re-parented under the destroyed prefab at the same location. For example, consider a wooden door that has a small window in it. When destroyed, the door breaks into a few large pieces. The window could be destroyed separately, but if it is not, we want the window to be re-parented to the broken door."));

                    foreach (Transform trans in destructibleChildren)
                        destructibleChildrenOptions.Add(trans.name);

                    destructibleChildrenOptions = destructibleChildrenOptions.Distinct().ToList();

                    // Initialize ChildrenToReparentByName
                    if (destructible.childrenToReParentByName == null)
                        destructible.childrenToReParentByName = new List<string>();

                    if (destructible.childrenToReParentByName.Count > 0)
                    {
                        destructibleChildrenOptions.Sort();
                        for (int i = 0; i < destructible.childrenToReParentByName.Count; i++)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField("", GUILayout.Width(15));
                            EditorGUILayout.LabelField("Name", GUILayout.Width(50));
                            int currentSelectedIndex = destructibleChildrenOptions.FindIndex(m => m == destructible.childrenToReParentByName[i]);
                            int selectedChildIndex = EditorGUILayout.Popup(currentSelectedIndex, destructibleChildrenOptions.ToArray());
                            if (selectedChildIndex >= 0 && selectedChildIndex < destructibleChildrenOptions.Count)
                                destructible.childrenToReParentByName[i] = destructibleChildrenOptions[selectedChildIndex];
                            else
                                destructible.childrenToReParentByName[i] = destructibleChildrenOptions[0];

                            EditorGUILayout.EndHorizontal();
                        }
                    }

                    // Add/Remove buttons
                    bool showChildReparentAddButton = destructible.childrenToReParentByName.Count < destructibleChildrenOptions.Count;
                    bool showChildReparentRemoveButton = destructible.childrenToReParentByName.Count > 0;
                    CreateButtons(destructible.childrenToReParentByName, showChildReparentAddButton, showChildReparentRemoveButton);
                }
                #endregion

                #region CHIP-AWAY DEBRIS
                //CHIP-AWAY DEBRIS
                isDebrisChipAway.boolValue = EditorGUILayout.Toggle("Chip-Away Debris", isDebrisChipAway.boolValue);
                destructible.isDebrisChipAway = isDebrisChipAway.boolValue;
                if (destructible.isDebrisChipAway)
                {
                    // CHIP-AWAY DEBRIS MASS
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("", GUILayout.Width(15));
                    chipAwayDebrisMass.floatValue = EditorGUILayout.FloatField("Debris Mass", chipAwayDebrisMass.floatValue);
                    destructible.chipAwayDebrisMass = chipAwayDebrisMass.floatValue;
                    EditorGUILayout.EndHorizontal();

                    // CHIP-AWAY DEBRIS DRAG
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("", GUILayout.Width(15));
                    chipAwayDebrisDrag.floatValue = EditorGUILayout.FloatField("Debris Drag", chipAwayDebrisDrag.floatValue);
                    destructible.chipAwayDebrisDrag = chipAwayDebrisDrag.floatValue;
                    EditorGUILayout.EndHorizontal();

                    // CHIP-AWAY DEBRIS ANGULARDRAG
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("", GUILayout.Width(15));
                    chipAwayDebrisAngularDrag.floatValue = EditorGUILayout.FloatField("Debris Angular Drag", chipAwayDebrisAngularDrag.floatValue);
                    destructible.chipAwayDebrisAngularDrag = chipAwayDebrisAngularDrag.floatValue;
                    EditorGUILayout.EndHorizontal();
                }
                #endregion

                // AUTO-POOL DESTROYED PREFAB
                autoPoolDestroyedPrefab.boolValue = EditorGUILayout.Toggle(new GUIContent("Auto Pool", "If checked, the destroyed prefab for this object will be added to the object pool automatically for you at runtime."), autoPoolDestroyedPrefab.boolValue);
                destructible.autoPoolDestroyedPrefab = autoPoolDestroyedPrefab.boolValue;

                previousDestroyedPrefab = destructible.destroyedPrefab;
            }
            EditorGUILayout.Separator();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Separator();
            #endregion

            // MISCELLANEOUS
            EditorGUILayout.BeginVertical("Box");

                #region PARTICLES
                EditorGUILayout.LabelField("Miscellaneous", EditorStyles.boldLabel);

                // FALLBACK PARTICLE
                EditorGUILayout.BeginHorizontal();
                useFallbackParticle.boolValue = EditorGUILayout.Toggle("", useFallbackParticle.boolValue, GUILayout.Width(15));
                GUIContent fallbackParticleContent = new GUIContent("Use Fallback Particle", "The particle effect used when this object is: (1) destroyed when there is no destroyed prefab assigned or (2) destroyed when a destroyed prefab is assigned but the DestructionManager's destroyed prefab limit has been reached. If unchecked, the object will disappear when destroyed. If checked but unassigned, the default particle effect will be used.");
                EditorGUILayout.LabelField(fallbackParticleContent);
                destructible.useFallbackParticle = useFallbackParticle.boolValue;
                EditorGUILayout.EndHorizontal();
                if (destructible.useFallbackParticle)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("", GUILayout.Width(15));
                    EditorGUILayout.PropertyField(fallbackParticle, new GUIContent(""));
                    destructible.fallbackParticle = fallbackParticle.objectReferenceValue as ParticleSystem;
                    if (destructible.fallbackParticle == null)
                        destructible.fallbackParticle = Resources.Load<ParticleSystem>("Default_Particles/DefaultLargeParticle");
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("", GUILayout.Width(15));
                    GUIContent fallbackParticleMatOptionGuiContent = new GUIContent("Replace Materials With:", "Allows you to override mesh renderer materials on the fallback particle effect.\n\nDestroyed Object - Mesh materials on the fallback particle will be replaced with the 0-index material from the destructible object.\n\nDon't Replace - Mesh materials will not be modified on the fallback particle.\n\nCustom - Specify each mesh material that will be replaced on the fallback particle, and by which alternative material.");
                    EditorGUILayout.LabelField(fallbackParticleMatOptionGuiContent);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    int[] popupParticleMatOptionValues = {0, 1, 2};
                    string[] popupParticleMatOptionDisplay = {"Destroyed Object", "Don't Replace", "Custom"};
                    EditorGUILayout.LabelField("", GUILayout.Width(15));
                    destructible.fallbackParticleMatOption = EditorGUILayout.IntPopup(fallbackParticleMatOption.intValue, popupParticleMatOptionDisplay, popupParticleMatOptionValues, GUILayout.Width(200));
                    EditorGUILayout.EndHorizontal();

                    // FALLBACK PARTICLE MATERIAL REPLACEMENT OPTIONS
                    if (destructible.fallbackParticleMatOption == 2)
                    {
                        List<ParticleSystemRenderer> psrs = destructible.fallbackParticle.GetComponentsInChildren<ParticleSystemRenderer>(true).ToList();
                        List<Material> particleMaterials = new List<Material>();
                        foreach (ParticleSystemRenderer psr in psrs)
                        {
                            if (psr.renderMode != ParticleSystemRenderMode.Mesh) continue;
                            foreach (Material mat in psr.sharedMaterials)
                            {
                                if (mat != null && !particleMaterials.Contains(mat))
                                {
                                    particleMaterials.Add(mat);
                                    particleMatOptions.Add(mat.name);
                                }
                            }
                        }

                        // Initialize replaceMaterials if null
                        if (destructible.replaceParticleMats == null)
                            destructible.replaceParticleMats = new List<MaterialMapping>();

                        // Clean up replaceMaterials
                        if (destructible.replaceParticleMats.Count > 0)
                            destructible.replaceParticleMats.RemoveAll(x => x.SourceMaterial != null && !particleMatOptions.Contains(x.SourceMaterial.name));

                        EditorGUILayout.Separator();
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("", GUILayout.Width(15));
                        EditorGUILayout.LabelField("Material Replacement", EditorStyles.boldLabel);
                        EditorGUILayout.EndHorizontal();
                        if (particleMaterials.Count > 0)
                        {
                            particleMaterials = particleMaterials.OrderBy(x => x.name).ToList();
                            particleMatOptions.Sort();
                            foreach (MaterialMapping mapping in destructible.replaceParticleMats)
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("", GUILayout.Width(15));
                                EditorGUILayout.LabelField("Replace", GUILayout.Width(50));
                                int selectedMaterialIndex = mapping.SourceMaterial == null ? 0 : particleMaterials.IndexOf(mapping.SourceMaterial);

                                selectedMaterialIndex = EditorGUILayout.Popup(selectedMaterialIndex, particleMatOptions.ToArray());
                                if (selectedMaterialIndex >= 0 && selectedMaterialIndex < particleMatOptions.Count)
                                {
                                    string materialName = particleMatOptions[selectedMaterialIndex];
                                    mapping.SourceMaterial = particleMaterials.First(x => x.name == materialName);
                                }
                                else
                                    mapping.SourceMaterial = null;

                                EditorGUILayout.EndHorizontal();
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("", GUILayout.Width(15));
                                EditorGUILayout.LabelField("With", GUILayout.Width(50));
                                mapping.ReplacementMaterial = EditorGUILayout.ObjectField(mapping.ReplacementMaterial, typeof(Material), false) as Material;
                                EditorGUILayout.EndHorizontal();
                            }

                            // Add/Remove buttons
                            bool showAddButton = destructible.replaceParticleMats.Count < particleMatOptions.Count;
                            bool showRemoveButton = destructible.replaceParticleMats.Count > 0;
                            CreateButtons(destructible.replaceParticleMats, showAddButton, showRemoveButton);
                        }
                        else
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField("", GUILayout.Width(15));
                            EditorGUILayout.LabelField("No materials found on particle!", GUILayout.Width(200), GUILayout.Height(16));
                            EditorGUILayout.EndHorizontal();
                        }

                        EditorGUILayout.Separator();
                    }

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("", GUILayout.Width(15));
                    destructible.centerPointOverride = EditorGUILayout.Vector3Field(new GUIContent("Position Override", "Here you can override the position the particle effect will spawn. This is particularly useful for Static objects, since DestroyIt uses the transform's position as the particle spawn point, which may not always be the center of the mesh."), centerPointOverride.vector3Value);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("", GUILayout.Width(15));
                    destructible.fallbackParticleScale = EditorGUILayout.Vector3Field(new GUIContent("Scale Override", "Here you can override the scale of the particle effect when it spawns."), fallbackParticleScale.vector3Value);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("", GUILayout.Width(15));
                    EditorGUILayout.PropertyField(fallbackParticleParent, new GUIContent("Parent Under", "Here you can specify a transform to parent the fallback particle effect under when it spawns.\n\nThis is helpful, for instance, if your destructible object is on a moving platform and you want the particle effect to move with the platform.\n\nNote that changing the parent transform will not change where the particle effect will spawn - it will still spawn at the destroyed object's position."));
                    destructible.fallbackParticleParent = fallbackParticleParent.objectReferenceValue as Transform;
                    EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Separator();
            #endregion

            #region UNPARENT ON DESTROY
            // UNPARENT ON DESTROY
            List<Transform> children = destructible.gameObject.GetComponentsInChildrenOnly<Transform>();
            List<string> childrenOptions = children.Select(x => $"{x.name} [{x.GetInstanceID()}]").ToList();
            childrenOptions.Insert(0, "--Select--");

            if (children.Count > 0)
            {
                bool rigidbodyFound = false;
                EditorGUILayout.Separator();
                EditorGUILayout.LabelField(new GUIContent("Un-Parent Children When Destroyed:", "These child objects will be un-parented from the destructible object when it is destroyed."));
                List<GameObject> tempList = new List<GameObject>();
                if (destructible.unparentOnDestroy == null)
                    destructible.unparentOnDestroy = new List<GameObject>();

                foreach (GameObject go in destructible.unparentOnDestroy)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("", GUILayout.Width(15));
                    EditorGUILayout.LabelField("Child", GUILayout.Width(50));

                    int selectedchildIndex = go == null ? 0 : children.IndexOf(go.transform) + 1;
                    selectedchildIndex = EditorGUILayout.Popup(selectedchildIndex, childrenOptions.ToArray());
                    GameObject selectedChild = null;
                    if (selectedchildIndex > 0 && selectedchildIndex < childrenOptions.Count)
                    {
                        string childName = childrenOptions[selectedchildIndex];
                        const string pattern = @"\[-*[0-9]+\]";
                        string match = Regex.Match(childName, pattern).Value;
                        int instanceId = Convert.ToInt32(match.Trim('[', ']'));
                        selectedChild = children.First(x => x.GetInstanceID() == instanceId).gameObject;
                        Rigidbody rb = selectedChild.GetComponent<Rigidbody>();
                        if (rb != null) rigidbodyFound = true;
                    }
                    
                    EditorGUILayout.EndHorizontal();
                    tempList.Add(selectedChild);
                }

                if (rigidbodyFound)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("", GUILayout.Width(15));
                    destructible.disableKinematicOnUparentedChildren = EditorGUILayout.Toggle(new GUIContent("Turn off Kinematic", "When TRUE, kinematic rigidbodies on unparented children will be affected by gravity/forces once the object is destroyed."), destructible.disableKinematicOnUparentedChildren);
                    EditorGUILayout.EndHorizontal();
                }
                else
                    destructible.disableKinematicOnUparentedChildren = true; // default to TRUE when future children are added to unparent.

                destructible.unparentOnDestroy = tempList;

                // Add/Remove buttons
                bool showAddButton = destructible.unparentOnDestroy.Count < children.Count;
                bool showRemoveButton = destructible.unparentOnDestroy.Count > 0;
                CreateButtons(destructible.unparentOnDestroy, showAddButton, showRemoveButton);
                EditorGUILayout.Separator();
            }
            #endregion

            EditorGUILayout.EndVertical();
            EditorGUILayout.Separator();

            if (GUI.changed && !Application.isPlaying)
            {
                EditorUtility.SetDirty(destructible);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            serializedObject.ApplyModifiedProperties();
        }

        private void CreateButtons<T>(List<T> itemList, bool showAddButton, bool showRemoveButton)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUILayout.Width(15));

            if (showAddButton && GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(30)))
                itemList.Add(default);

            if (showRemoveButton && GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(30)))
                itemList.RemoveAt(itemList.Count - 1);

            EditorGUILayout.EndHorizontal();
        }
    }
}