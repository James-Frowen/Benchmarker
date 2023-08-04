/*
MIT License

Copyright (c) 2022 James Frowen

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JamesFrowen.Benchmarker.Weaver;

#if UNITY_2019_3_OR_NEWER
using UnityEngine;
#endif

namespace JamesFrowen.Benchmarker.Weaver
{
    public static class BenchmarkHelper
    {
        private static Dictionary<int, string> s_methodNames = new Dictionary<int, string>();
        private static Dictionary<int, Frame[]> s_methods;
        private static int s_frameCount = 300;
        private static int s_frameIndex = 0;
        private static bool s_waitForFirstFrame = true;
        private static bool s_isRunning = false;
        private static bool s_autoEnd = false;

        public static bool IsRunning => s_isRunning;

        public static event Action OnEndRecording;

        // called by IL
        public static void RegisterMethod(string name)
        {
            s_methodNames[name.GetHashCode()] = name;
        }
        public static void RegisterMethod(int id, string name)
        {
            s_methodNames[id] = name;
        }

        // called by IL
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndMethod(int nameHash, long start)
        {
            if (!s_isRunning) return;
            if (s_waitForFirstFrame) return;

            var method = s_methods[nameHash];
            var end = GetTimestamp();
            method[s_frameIndex].time += end - start;
            method[s_frameIndex].count++;
        }

        // called by IL 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetTimestamp()
        {
            return Stopwatch.GetTimestamp();
        }

        public static void StartRecording(int frameCount, bool autoEnd, bool waitForFirstFrame)
        {
            s_isRunning = true;
            s_waitForFirstFrame = waitForFirstFrame;
            s_frameCount = frameCount;
            s_autoEnd = autoEnd;
            s_methods = new Dictionary<int, Frame[]>();
            foreach (var key in s_methodNames.Keys)
            {
                s_methods.Add(key, new Frame[frameCount]);
            }
        }

        /// <summary>
        /// Used to pause or resume recording. Can useful to pause durning any warmup loops
        /// </summary>
        /// <param name="pause"></param>
        public static void PauseRecording(bool pause)
        {
            s_isRunning = !pause;
        }

        public static void EndRecording()
        {
            s_isRunning = false;
            s_autoEnd = false;
            s_waitForFirstFrame = false;
            OnEndRecording?.Invoke();
        }

        public static void NextFrame()
        {
            // set to false and then return, results will started being recorded from now
            if (s_waitForFirstFrame)
            {
                s_waitForFirstFrame = false;
                return;
            }

            if (!s_isRunning) return;

            s_frameIndex++;
            if (s_frameIndex >= s_frameCount)
            {
                if (s_autoEnd)
                {
                    EndRecording();
                    return;
                }
                else
                {
                    // todo do we need to clean frame? or we will just start adding to buffer again
                    s_frameIndex = 0;
                }
            }
        }

        public static IReadOnlyList<Results> GetResults()
        {
            var results = new List<Results>(s_methodNames.Count);
            foreach (var key in s_methodNames.Keys)
            {
                var fullName = s_methodNames[key];
                var frames = s_methods[key];

                if (NoResults(frames))
                    continue;

                var benchmark = CreateDetails(fullName);
                results.Add(new Results(frames, benchmark));
            }
            return results;

            bool NoResults(Frame[] frames)
            {
                return frames.All(x => x.count == 0);
            }
        }

        private const BindingFlags ALL_METHODS = (BindingFlags)(-1);

        private static BenchmarkDetails CreateDetails(string fullName)
        {
            var benchmark = new BenchmarkDetails()
            {
                name = fullName,
            };

            var methodInfo = GetMethod(fullName);
            if (methodInfo != null)
            {
                var attr = methodInfo.GetCustomAttribute<BenchmarkMethodAttribute>();
                if (attr != null)
                {
                    if (!string.IsNullOrEmpty(attr.Name))
                        benchmark.name = attr.Name;
                    benchmark.description = attr.Description;
                    benchmark.baseline = attr.Baseline;
                }
                var cats = methodInfo.GetCustomAttribute<BenchmarkCategoryAttribute>();
                if (cats != null)
                {
                    benchmark.categories = cats.categories;
                }
            }

            return benchmark;
        }


        private static MethodInfo GetMethod(string fullName)
        {
            try
            {
                // example full name = `System.Void Mirage.NetworkServer::Update()`
                fullName = fullName.Substring(fullName.IndexOf(" ") + 1);
                var nameIndex = fullName.IndexOf("::");

                var methodName = fullName.Substring(nameIndex + 2);
                var bracketIndex = methodName.IndexOf("(");
                methodName = methodName.Substring(0, bracketIndex);

                var typeName = fullName.Substring(0, nameIndex);

                if (typeName.Length == 0)
                    throw new ArgumentException("typeName cannot be empty.");
                if (typeName.Contains(":"))
                    throw new ArgumentException("typeName cannot contain a colon (:).");

                if (typeName.Contains(" "))
                    throw new ArgumentException("typeName cannot contain a space.");

                if (methodName.Length == 0)
                    throw new ArgumentException("methodName cannot be empty.");

                if (methodName.Contains(":"))
                    throw new ArgumentException("methodName cannot contain a colon (:).");

                if (methodName.Contains(" "))
                    throw new ArgumentException("methodName cannot contain a space.");

                if (methodName.Contains("("))
                    throw new ArgumentException("methodName cannot contain a opening parenthesis (().");

                if (methodName.Contains(")"))
                    throw new ArgumentException("methodName cannot contain a closing parenthesis ())");

                if (methodName.Contains(","))
                    throw new ArgumentException("methodName cannot contain a comma (,).");

                return GetMethod(typeName, methodName);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                return null;
            }
        }

        private static MethodInfo GetMethod(string typeName, string methodName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    var method = CheckType(type);
                    if (method != null) { return method; }
                }
            }
            return null;

            MethodInfo CheckType(Type type)
            {
                if (type.FullName == typeName)
                {
                    foreach (var method in type.GetMethods(ALL_METHODS))
                    {
                        if (method.Name == methodName)
                        {
                            // only matching if has attribute
                            if (method.GetCustomAttribute<BenchmarkMethodAttribute>() != null)
                            {
                                return method;
                            }
                        }
                    }
                }

                foreach (var nested in type.GetNestedTypes())
                {
                    var method = CheckType(nested);
                    if (method != null) { return method; }
                }

                return null;
            }
        }
    }
}

namespace JamesFrowen.Benchmarker
{
    public static class BenchmarkRunner
    {
        public static bool AutoLog = true;
        public static string ResultFolder = ".";
        public static string ResultName = "";
        private static int s_previousFrameCount;

        public static bool IsRecording => BenchmarkHelper.IsRunning;

        public static List<string> MetaData;

        /// <summary>
        /// Starts recording time and call count for methods
        /// </summary>
        /// <param name="frameCount">How many frames to record for. If <paramref name="autoEnd"/> is true will end after this time, else will loop back to start of buffer</param>
        /// <param name="autoEnd">Should auto stop after <paramref name="frameCount"/> is reached</param>
        /// <param name="waitForFirstFrame">Delay recording till NextFrame is called for the first time</param>
        public static void StartRecording(int frameCount, bool autoEnd, bool waitForFirstFrame)
        {
            s_previousFrameCount = frameCount;
#if UNITY_2019_3_OR_NEWER
            CheckUpdater();
#endif
            BenchmarkHelper.StartRecording(frameCount, autoEnd, waitForFirstFrame);
            if (autoEnd)
            {
                BenchmarkHelper.OnEndRecording += AutoEnd;
            }
        }

        private static void AutoEnd()
        {
            BenchmarkHelper.OnEndRecording -= AutoEnd;
            if (AutoLog)
            {
                LogResults();
            }
        }
        public static void EndRecording()
        {
            BenchmarkHelper.EndRecording();
            if (AutoLog)
            {
                LogResults();
            }
        }

        public static void LogResults() => LogResults(GetSavePath());
        public static void LogResults(string path)
        {
            var results = BenchmarkHelper.GetResults();
            var analyser = new BenchmarkAnalyser(results);
            var categories = analyser.GetCategories();

            var meta = new List<string>();
            meta.Add($"FrameCount:{s_previousFrameCount}");
            if (MetaData != null)
                meta.AddRange(MetaData);

            var printer = new MarkDownBenchmarkPrinter(path + ".md");
            printer.Print(categories, meta.ToArray());

            var jsonPrinter = new JsonBenchmarkPrinter(path + ".json");
            jsonPrinter.Print(categories, meta.ToArray());
        }

        private static string GetSavePath()
        {
            string name;
            if (string.IsNullOrEmpty(ResultName))
            {
#if UNITY_EDITOR
                var runtime = "Editor";
#elif UNITY_SERVER
                string runtime = "Server";
#else
                var runtime = "Player";
#endif
                name = $"Results-{runtime}";
            }
            else
            {
                name = ResultName;
            }

            var path = $"{ResultFolder}/{name}_{$"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}"}";
            return path;
        }

#if UNITY_2019_3_OR_NEWER
        private static Updater _updater;

        private static void CheckUpdater()
        {
            if (_updater == null)
            {
                _updater = GameObject.FindObjectOfType<Updater>();
                _updater = new GameObject("BenchmarkRunner").AddComponent<Updater>();
                GameObject.DontDestroyOnLoad(_updater.gameObject);
            }
        }

        [DefaultExecutionOrder(int.MaxValue)]
        private class Updater : MonoBehaviour
        {
            private void LateUpdate()
            {
                BenchmarkHelper.NextFrame();
            }
        }
#endif
    }
}

