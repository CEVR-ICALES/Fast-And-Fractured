#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
 
namespace Utilities
{
    [CustomEditor(typeof(TimerSystem))]
    public class TimerSystemEditor : UnityEditor.Editor
    {
        private bool _showTimers = true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); 
            TimerSystem timerSystem = (TimerSystem)target;
            _showTimers = EditorGUILayout.Foldout(_showTimers, "Active Timers (Editor Only)");  

            if (_showTimers)
            {
                System.Reflection.FieldInfo timersField = typeof(TimerSystem).GetField("_timers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); // UseReflection , the _timer keep  on the code Private
                if (timersField != null)
                {
                    if (timersField.GetValue(timerSystem) is Dictionary<string, ITimer> timers && timers.Count > 0)     
                    {
                        EditorGUILayout.BeginVertical(GUI.skin.box);   
                        foreach (var timerEntry in timers)   
                        {
                            ITimer timer = timerEntry.Value;      
							if(timer!=null) {	

							    TimerData data = timer.GetData();  
								EditorGUILayout.LabelField($"ID: {data.ID}, Duration: {data.Duration:F2}, Current: {data.CurrentTime:F2}, Is Running : {data.IsRunning} , Is Paused: {data.IsPaused}  , Normalized: {data.NormalizedProgress:P2}, Direction: {data.Direction}"); 

							}      
                            
                           
                        }
                        EditorGUILayout.EndVertical();   
                    }
                    else  
                    {
                        EditorGUILayout.LabelField("No active timers."); 
                    }
                }
				else  
                {   
					EditorGUILayout.LabelField("Could not retrieve timer information.");  
                }
            }


            if (GUI.changed)    
            {
				EditorUtility.SetDirty(target); 
            }
        }
    }
}
#endif