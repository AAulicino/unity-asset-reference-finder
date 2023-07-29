using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityAssetReferenceFinder.Editor
{
    public class ResultsPopup : EditorWindow
    {
        IReadOnlyDictionary<Object, Object[]> results;
        Dictionary<Object, ResultSettings> _resultSettings;

        public void Setup(IReadOnlyDictionary<Object, Object[]> results)
        {
            this.results = results;
            _resultSettings = results.Keys.ToDictionary(x => x, _ => new ResultSettings());
            if (_resultSettings.Count == 1)
                _resultSettings.Single().Value.Foldout = true;
        }
        
        void Awake()
        {
            titleContent.text = "Results";
        }

        void OnGUI()
        {
            if (results == null)
                return;
            
            foreach (KeyValuePair<Object, Object[]> result in results)
            {
                Object targetObj = result.Key;
                Object[] targetObjReferences = result.Value;

                GUI.enabled = false;
                EditorGUILayout.ObjectField("Objects referencing: ", targetObj, targetObj.GetType(), false);
                GUI.enabled = true;

                var settings = _resultSettings[targetObj];
                
                settings.Foldout  = EditorGUILayout.Foldout(settings.Foldout, targetObj.name);
                
                if (!settings.Foldout)
                    continue;
                
                EditorGUILayout.Separator();

                if (targetObjReferences == null)
                    return;
 
                settings.ScrollPosition = EditorGUILayout.BeginScrollView(settings.ScrollPosition);
                GUI.enabled = false;

                if (targetObjReferences.Length == 0)
                {
                    EditorGUILayout.LabelField("No references found for object");
                }
                else
                {
                    foreach (Object refs in targetObjReferences)
                        EditorGUILayout.ObjectField(refs, refs.GetType(), false);
                }

                GUI.enabled = true;
                EditorGUILayout.EndScrollView();
            }
        }

        class ResultSettings
        {
            public Vector2 ScrollPosition { get; set; }
            public bool Foldout { get; set; }
        }
    }
}
