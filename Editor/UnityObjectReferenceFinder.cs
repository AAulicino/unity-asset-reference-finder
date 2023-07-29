using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace UnityAssetReferenceFinder.Editor
{
    public class UnityObjectReferenceFinder : IDisposable
    {
        const string TITLE = "Object Reference Finder"; 
        
        static readonly string[] defaultExtensionsFilter = { ".prefab", ".unity", ".asset", ".mat", ".anim" };

        readonly CancellationTokenSource cancellationTokenSource;
        readonly IProgress<string> progress;
        readonly ParallelOptions parallelOptions;
        readonly Object[] targetObjects;

        List<string> guidsToFind;
        string[] pathsToSearch;
        int evaluatedFileCount;
        double lastProgressBarTickTime;
        string[] extensionsFilter;

        public UnityObjectReferenceFinder (Object[] targetObjects, string[] customExtensionFilter = null)
        {
            this.targetObjects = targetObjects;
            extensionsFilter = customExtensionFilter ?? defaultExtensionsFilter;
            
            progress = new Progress<string>(HandleProgressChange);
            cancellationTokenSource = new CancellationTokenSource();
            parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationTokenSource.Token,
                MaxDegreeOfParallelism = Environment.ProcessorCount,
            };
        }

        public async Task<Dictionary<string, List<string>>> FindReferences()
        {
            evaluatedFileCount = 0;

            DisplayProgress("Gathering object guids", 0);
            guidsToFind = targetObjects.Select(x => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(x)))
                .ToList();

            DisplayProgress("Filtering paths", 0);
            pathsToSearch = await FilterFilesToSearch(AssetDatabase.GetAllAssetPaths());
            
            DisplayProgress("Preparing to search in files", 0);
            Dictionary<string, List<string>> results = guidsToFind.ToDictionary(x => x, _ => new List<string>());
            
            try
            {
                Stopwatch watch = Stopwatch.StartNew();
                
                await Task.Run(() => Parallel.ForEach(
                    pathsToSearch,
                    parallelOptions,
                    () => new Dictionary<string, List<string>>(results.Count),
                    FindInFiles,
                    resultsDict =>
                    {
                        lock (results)
                        {
                            foreach (KeyValuePair<string, List<string>> entry in resultsDict)
                            {
                                results[entry.Key].AddRange(entry.Value);
                            }
                        }
                    }));
                
                watch.Stop();
                Debug.Log(watch.Elapsed);
            }
            finally
            {
                EditorApplication.delayCall += EditorUtility.ClearProgressBar;
            }

            return results;
        }

        Dictionary<string, List<string>> FindInFiles(
            string path,
            ParallelLoopState loopState, 
            Dictionary<string, List<string>> results
        )
        {
            string content = File.ReadAllText(path);

            foreach (string guid in guidsToFind)
            {
                if (content.Contains(guid, StringComparison.OrdinalIgnoreCase))
                {
                    if (!results.TryGetValue(guid, out List<string> refs))
                    {
                        refs = new List<string>();
                        results.Add(guid, refs);
                    }
                    
                    refs.Add(path);
                }
            }

            progress.Report(path);
            return results;
        }

        void HandleProgressChange(string path)
        {
            evaluatedFileCount++;
            
            if (EditorApplication.timeSinceStartup - lastProgressBarTickTime < 0.1)
                return;
            
            lastProgressBarTickTime = EditorApplication.timeSinceStartup;
            DisplayProgress($"Scanning {path}", evaluatedFileCount / (float)pathsToSearch.Length);
        }

        void DisplayProgress(string message, float progress)
        {
            if (EditorUtility.DisplayCancelableProgressBar(TITLE, message, progress))
            {
                cancellationTokenSource.Cancel();
                EditorUtility.ClearProgressBar();
            }
        }
        
        Task<string[]> FilterFilesToSearch (string[] paths)
        {
            const StringComparison comparison = StringComparison.OrdinalIgnoreCase;
            
            ParallelQuery<string> queries = paths.AsParallel()
                .Where(x => extensionsFilter.Any(y => x.EndsWith(y, comparison)));

            return Task.Run(() => queries.ToArray());
        }

        public void Dispose()
        {
            cancellationTokenSource.Dispose();
            EditorUtility.ClearProgressBar();
        }
    }
}
