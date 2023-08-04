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
using System.IO;
using System.Linq;
using UnityEngine;
using static JamesFrowen.Benchmarker.BenchmarkAnalyser;

namespace JamesFrowen.Benchmarker
{
    public class JsonBenchmarkPrinter : BenchmarkPrinter
    {
        public JsonBenchmarkPrinter(string path) : base(path)
        {
        }

        public override void Print(BenchmarkAnalyser.CategoryGroup[] categories, params string[] metaData)
        {
            var reuslts = new BenchmarkResults
            {
                ApplicationSettings = CreateApplicationSettings(),
                MetaData = metaData,
                Categories = CreateCategories(categories),
            };
            var str = JsonUtility.ToJson(reuslts, prettyPrint: true);
            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine(str);
            }
        }

        private static ApplicationSettings CreateApplicationSettings()
        {
            var applicationSettings = new ApplicationSettings()
            {
#if UNITY_2019_3_OR_NEWER
                UnityVersion = Application.unityVersion,
#else
                UnityVersion= "Not unity",
#endif
                Platform = Application.platform.ToString(),
                IsEditor = Application.isEditor,
#if UNITY_2019_3_OR_NEWER
                IsDebug = Debug.isDebugBuild,
#else
#if DEBUG
                IsDebug = true,
#else
                IsDebug = false,
#endif
#endif
#if UNITY_SERVER
                IsServer = true,
#else
                IsServer = false,
#endif
            };
            return applicationSettings;
        }

        private Category[] CreateCategories(BenchmarkAnalyser.CategoryGroup[] categories)
        {
            return categories.Select(category => new Category
            {
                Name = category.name,
                ProcessedResults = category.processedResults.ToArray(),
            }).ToArray();
        }

        [Serializable]
        public struct BenchmarkResults
        {
            public ApplicationSettings ApplicationSettings;
            public string[] MetaData;
            public Category[] Categories;
        }

        [Serializable]
        public struct ApplicationSettings
        {
            public string UnityVersion;
            public string Platform;
            public bool IsEditor;
            public bool IsDebug;
            public bool IsServer;
        }

        [Serializable]
        public struct Category
        {
            public string Name;
            public ProcessedResults[] ProcessedResults;
        }
    }
}

