using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace UnityAssetReferenceFinder.Editor
{
    public static class DependenciesFinderEditor
    {
        [MenuItem("Assets/Find References in Project", true, 30)]
        public static bool FindReferencesInProjectValidate()
            => Selection.objects.Any(x => !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(x)));

        [MenuItem("Assets/Find References in Project", false, 30)]
        static async void FindReferencesInProject()
        {
            using UnityObjectReferenceFinder finder = new UnityObjectReferenceFinder(Selection.objects);

            Dictionary<string, List<string>> results;

            try
            {
                results = await finder.FindReferences();
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Dictionary<Object, Object[]> objectResults = new ();
            foreach (KeyValuePair<string, List<string>> kvp in results)
            {
                objectResults.Add(
                    AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(kvp.Key)),
                    kvp.Value.Select(AssetDatabase.LoadAssetAtPath<Object>).ToArray()
                );
            }

            ResultsPopup window = EditorWindow.GetWindow<ResultsPopup>();
            window.Setup(objectResults);
            window.ShowPopup();
        }
    }
}
