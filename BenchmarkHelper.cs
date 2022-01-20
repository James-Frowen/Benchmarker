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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JamesFrowen.Benchmarker.Weaver;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace JamesFrowen.Benchmarker.Weaver
{
    public static class BenchmarkHelper
    {
        static Dictionary<int, string> s_methodNames = new Dictionary<int, string>();
        static Dictionary<int, Frame[]> s_methods;

        static int s_frameCount = 300;
        static int s_frameIndex = 0;
        static bool s_waitForFirstFrame = true;
        static bool s_isRunning = false;
        static bool s_autoEnd = false;

        public static bool IsRunning => s_isRunning;

        public static event Action OnEndRecording;

        // called by IL
        public static void RegisterMethod(string name)
        {
            s_methodNames[name.GetHashCode()] = name;
        }

        // called by IL
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndMethod(int nameHash, long start)
        {
            if (!s_isRunning) return;
            if (s_waitForFirstFrame) return;

            Frame[] method = s_methods[nameHash];
            long end = GetTimestamp();
            method[s_frameIndex].time += (end - start);
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
            foreach (int key in s_methodNames.Keys)
            {
                s_methods.Add(key, new Frame[frameCount]);
            }
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
            foreach (int key in s_methodNames.Keys)
            {
                string fullName = s_methodNames[key];
                Frame[] frames = s_methods[key];

                if (NoResults(frames))
                    continue;

                Benchmark benchmark = CreateDetails(fullName);
                results.Add(new Results(frames, benchmark));
            }
            return results;

            bool NoResults(Frame[] frames)
            {
                return frames.All(x => x.count == 0);
            }
        }

        const BindingFlags ALL_METHODS = (BindingFlags)(-1);

        private static Benchmark CreateDetails(string fullName)
        {
            var benchmark = new Benchmark()
            {
                name = fullName,
            };

            MethodInfo methodInfo = GetMethod(fullName);
            if (methodInfo != null)
            {
                BenchmarkMethodAttribute attr = methodInfo.GetCustomAttribute<BenchmarkMethodAttribute>();
                if (attr != null)
                {
                    benchmark.name = attr.Name;
                    benchmark.description = attr.Description;
                    benchmark.baseline = attr.Baseline;
                }
                BenchmarkCategoryAttribute cats = methodInfo.GetCustomAttribute<BenchmarkCategoryAttribute>();
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
                int nameIndex = fullName.IndexOf("::");

                string methodName = fullName.Substring(nameIndex + 2);
                int bracketIndex = methodName.IndexOf("(");
                methodName = methodName.Substring(0, bracketIndex);

                string typeName = fullName.Substring(0, nameIndex);

                Debug.Assert(typeName.Length != 0);
                Debug.Assert(!typeName.Contains(":"));
                Debug.Assert(!typeName.Contains(" "));

                Debug.Assert(methodName.Length != 0);
                Debug.Assert(!methodName.Contains(":"));
                Debug.Assert(!methodName.Contains(" "));
                Debug.Assert(!methodName.Contains("("));
                Debug.Assert(!methodName.Contains(")"));
                Debug.Assert(!methodName.Contains(","));

                return GetMethod(typeName, methodName);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                return null;
            }
        }

        static MethodInfo GetMethod(string typeName, string methodName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes())
                {
                    MethodInfo method = CheckType(type);
                    if (method != null) { return method; }
                }
            }
            return null;

            MethodInfo CheckType(Type type)
            {
                if (type.FullName == typeName)
                {
                    foreach (MethodInfo method in type.GetMethods(ALL_METHODS))
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

                foreach (Type nested in type.GetNestedTypes())
                {
                    MethodInfo method = CheckType(nested);
                    if (method != null) { return method; }
                }

                return null;
            }
        }
    }
}

namespace JamesFrowen.Benchmarker
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class BenchmarkMethodAttribute : Attribute
    {
        /// <summary>
        /// Name to use for method, if null use 
        /// </summary>
        public string Name;

        public string Description;

        public bool Baseline;

        public BenchmarkMethodAttribute() { }
        public BenchmarkMethodAttribute(string name, bool baseline = false, string description = "")
        {
            Name = name;
            Baseline = baseline;
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class BenchmarkCategoryAttribute : Attribute
    {
        public string[] categories;
        public BenchmarkCategoryAttribute(params string[] categories)
        {
            this.categories = categories;
        }
    }
    public static class BenchmarkRunner
    {
        public static bool AutoLog = true;
        public static string ResultFolder = ".";
        private static int s_previousFrameCount;

        public static bool IsRecording => BenchmarkHelper.IsRunning;

        /// <summary>
        /// Starts recording time and call count for methods
        /// </summary>
        /// <param name="frameCount">How many frames to record for. If <paramref name="autoEnd"/> is true will end after this time, else will loop back to start of buffer</param>
        /// <param name="autoEnd">Should auto stop after <paramref name="frameCount"/> is reached</param>
        /// <param name="waitForFirstFrame">Delay recording till NextFrame is called for the first time</param>
        public static void StartRecording(int frameCount, bool autoEnd, bool waitForFirstFrame)
        {
            s_previousFrameCount = frameCount;
            CheckUpdater();
            BenchmarkHelper.StartRecording(frameCount, autoEnd, waitForFirstFrame);
            if (autoEnd)
            {
                BenchmarkHelper.OnEndRecording += AutoEnd;
            }
        }
        static void AutoEnd()
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
            IReadOnlyList<Results> results = BenchmarkHelper.GetResults();
            var analyser = new BenchmarkAnalyser(results);
            BenchmarkAnalyser.CategoryGroup[] categories = analyser.GetCategories();

            var printer = new BenchmarkPrinter(path);
            printer.PrintToMarkDownTable(categories, $"FrameCount:{s_previousFrameCount}");
        }

        private static string GetSavePath()
        {
#if UNITY_EDITOR
            string runtime = "Editor";
#elif UNITY_SERVER
            string runtime = "Server";
#else
            string runtime = "Player";
#endif
            string path = $"{ResultFolder}/Results-{runtime}_{$"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}"}.md";
            return path;
        }

        static Updater _updater;
        static void CheckUpdater()
        {
            if (_updater == null)
            {
                _updater = GameObject.FindObjectOfType<Updater>();
                _updater = new GameObject("BenchmarkRunner").AddComponent<Updater>();
                GameObject.DontDestroyOnLoad(_updater.gameObject);
            }
        }

        [DefaultExecutionOrder(int.MaxValue)]
        class Updater : MonoBehaviour
        {
            private void LateUpdate()
            {
                BenchmarkHelper.NextFrame();
            }
        }
    }

    /// <summary>
    /// Measurements from a single frame
    /// </summary>
    public struct Frame
    {
        /// <summary>
        /// how many times method was called in a frame
        /// </summary>
        public int count;
        /// <summary>
        /// total time inside method this frame
        /// </summary>
        public long time;
    }

    /// <summary>
    /// Details about the benchmark
    /// </summary>
    public class Benchmark
    {
        public string name;
        public string description;
        public string[] categories;
        public bool baseline;
    }

    /// <summary>
    /// Results for a benchmark
    /// </summary>
    public class Results
    {
        /// <summary>
        /// Ticks
        /// </summary>
        public readonly Frame[] frames;
        public readonly bool Failed;

        // in order to get averages we need to add (time/Count)  count times to list
        // this will give weighted average for average time per frame
        public IEnumerable<double> ElapsedPerMethod => frames.Where(x => x.count != 0).SelectMany(x => Enumerable.Repeat(frameTimeToSeconds(x), x.count));
        double frameTimeToSeconds(Frame x) => ((double)x.time / x.count) / Stopwatch.Frequency;
        public IEnumerable<double> ElapsedPerFrame => frames.Select(x => (double)x.time / Stopwatch.Frequency);
        public IEnumerable<double> CallCounts => frames.Select(x => (double)x.count);

        public readonly Benchmark benchmark;

        public Results(Frame[] frames, Benchmark benchmark)
        {
            this.frames = frames;
            this.benchmark = benchmark;

            if (this.frames == null) { Failed = true; }
        }
    }

    public class BenchmarkAnalyser
    {
        Dictionary<string, CategoryGroup> categoryGroup = new Dictionary<string, CategoryGroup>();
        public BenchmarkAnalyser(IEnumerable<Results> results)
        {
            foreach (Results result in results)
            {
                string[] categories = result.benchmark.categories;
                if (categories == null || categories.Length == 0)
                {
                    addResultsToCategory(string.Empty, result);
                }
                else
                {
                    foreach (string cat in categories)
                    {
                        addResultsToCategory(cat, result);
                    }
                }
            }

            ProcessAllResults();
            SortResults();
        }

        private void addResultsToCategory(string key, Results result)
        {
            if (!categoryGroup.TryGetValue(key, out CategoryGroup group))
            {
                group = new CategoryGroup(key);
                categoryGroup.Add(key, group);
            }

            if (result.benchmark.baseline)
                group.baseline = result;

            group.results.Add(result);
        }

        private void ProcessAllResults()
        {
            foreach (CategoryGroup category in categoryGroup.Values)
            {
                ProcessCategory(category);
            }
        }

        private void ProcessCategory(CategoryGroup category)
        {
            double? baselineMeanMethodTime = null;
            double? baselineMeanCount = null;
            double? baselineMeanFrameTime = null;
            if (category.baseline != null)
            {
                Debug.Log($"baseLine for {category.name}: {category.baseline.benchmark.name}");
                baselineMeanMethodTime = GetMean(category.baseline.ElapsedPerMethod);
                baselineMeanCount = GetMean(category.baseline.CallCounts);
                baselineMeanFrameTime = GetMean(category.baseline.ElapsedPerFrame);
            }

            category.processedResults = new List<ProcessedResults>();
            foreach (Results result in category.results)
            {
                if (result.Failed) { continue; }
                var processed = new ProcessedResults(result)
                {
                    methodTime = CreateDataGroup(result.ElapsedPerMethod, baselineMeanMethodTime),
                    count = CreateDataGroup(result.CallCounts, baselineMeanCount),
                    frameTime = CreateDataGroup(result.ElapsedPerFrame, baselineMeanFrameTime),
                };
                category.processedResults.Add(processed);
            }
        }

        private DataGroup CreateDataGroup(IEnumerable<double> values, double? baseLineMean)
        {
            double mean = GetMean(values);
            double stdDev = GetStandardDeviation(values, mean);
            double stdError = GetStandardError(values, stdDev);
            double? ratio = baseLineMean.HasValue ? mean / baseLineMean : null;
            double min = GetMin(values);
            double max = GetMax(values);

            var data = new DataGroup
            {
                mean = mean,
                ratio = ratio,
                stdDev = stdDev,
                stdError = stdError,
                min = min,
                max = max
            };
            return data;
        }

        private double GetMean(IEnumerable<double> values)
        {
            return values.Average();
        }
        private double GetMin(IEnumerable<double> values)
        {
            return values.Min();
        }
        private double GetMax(IEnumerable<double> values)
        {
            return values.Max();
        }
        private double GetStandardDeviation(IEnumerable<double> values, double mean)
        {
            double sum = values
                // convert to time
                .Select(x => x)
                .Select(x => x - mean)
                .Select(x => x * x)
                .Sum();

            return Math.Sqrt(sum / (values.Count() - 1));
        }
        private double GetStandardError(IEnumerable<double> values, double stddev)
        {
            return stddev / Math.Sqrt(values.Count());
        }

        public void SortResults()
        {
            foreach (CategoryGroup category in categoryGroup.Values)
            {
                category.processedResults.Sort(new ProcessedResultsComparer());
            }
        }

        internal CategoryGroup[] GetCategories()
        {
            return categoryGroup.Values.ToArray();
        }

        public class CategoryGroup
        {
            public readonly string name;
            public List<Results> results = new List<Results>();
            public Results baseline;

            public List<ProcessedResults> processedResults = new List<ProcessedResults>();

            public CategoryGroup(string key)
            {
                name = key;
            }
        }
        public class ProcessedResults
        {
            public bool Failed;
            public Benchmark benchmark;

            public DataGroup methodTime;
            public DataGroup count;
            public DataGroup frameTime;

            public ProcessedResults(Results result)
            {
                Failed = result.Failed;
                benchmark = result.benchmark;
            }
        }
        public class DataGroup
        {
            /// <summary>
            /// seconds
            /// </summary>
            public double mean;

            /// <summary>
            /// seconds
            /// </summary>
            public double stdDev;

            /// <summary>
            /// seconds
            /// </summary>
            public double stdError;

            /// <summary>
            /// Value vs baseline
            /// </summary>
            public double? ratio;

            /// <summary>
            /// seconds
            /// </summary>
            public double min;
            /// <summary>
            /// seconds
            /// </summary>
            public double max;
        }
        public struct ProcessedResultsComparer : IComparer<ProcessedResults>
        {
            public int Compare(ProcessedResults x, ProcessedResults y)
            {
                return x.methodTime.mean.CompareTo(y.methodTime.mean);
            }
        }
    }

    public class BenchmarkPrinter
    {
        private readonly string path;

        public BenchmarkPrinter(string path)
        {
            this.path = path;
        }

        public void PrintToMarkDownTable(BenchmarkAnalyser.CategoryGroup[] categories, params string[] headers)
        {
            if (categories.Length == 0)
            {
                Debug.LogWarning("No Categories to print");
            }
            var rows = new List<Row>();

            //title
            rows.AddRange(Row.GetTitle());
            rows.Add(Row.Empty);

            for (int i = 0; i < categories.Length; i++)
            {
                if (i > 0)
                {
                    // spacer
                    rows.Add(Row.Empty);
                }

                BenchmarkAnalyser.CategoryGroup category = categories[i];
                foreach (BenchmarkAnalyser.ProcessedResults result in category.processedResults)
                {
                    rows.Add(new DataRow(result, category.name));
                }
            }

            // todo do we only want to set units per catetgory?
            SetUnits(rows);

            int[] paddingLength = GetPaddingLength(rows);

            Debug.Log($"Saving benchmark results to {path}");
            checkDirectroy(path);
            using (var writer = new StreamWriter(path))
            {
#if UNITY_SERVER
                bool isServer = true;
#else
                bool isServer = false;
#endif
                writer.WriteLine("**Application**");
                writer.WriteLine($"- UnityVersion:{Application.unityVersion}");
                writer.WriteLine($"- Platform:{Application.platform}");
                writer.WriteLine($"- IsEditor:{Application.isEditor}");
                writer.WriteLine($"- IsDebug:{Debug.isDebugBuild}");
                writer.WriteLine($"- IsServer:{isServer}");

                if (headers != null && headers.Length > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine();
                    foreach (string header in headers)
                    {
                        writer.WriteLine($"- {header}");
                    }
                }


                writer.WriteLine();
                writer.WriteLine();
                writer.WriteLine("**Results**");
                writer.WriteLine();

                foreach (Row row in rows)
                {
                    writer.WriteLine(row.CreatePaddedString(paddingLength));
                }
            }
        }

        private void checkDirectroy(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        static int[] GetPaddingLength(List<Row> rows)
        {
            int[] max = new int[Row.ColumnCount];
            for (int i = 0; i < Row.ColumnCount; i++)
            {
                max[i] = rows.Max(x => x.GetValue(i).Length);
            }
            return max;
        }


        private static void SetUnits(List<Row> rows)
        {
            IEnumerable<DataRow> dataRows = rows.Select(x => x as DataRow).Where(x => x != null);

            SetTimeUnits(dataRows.Select(x => x.methodTime));
            SetTimeUnits(dataRows.Select(x => x.frameTime));

            foreach (DataRow.DataGroup row in dataRows.Select(x => x.count))
            {
                row.SetStringWithUnits("", 1);
            }
        }


        private static void SetTimeUnits(IEnumerable<DataRow.DataGroup> dataGroups)
        {
            double[] means = (dataGroups
                .Select(x => x.mean.raw)
                .Where(x => x.HasValue)
                .Select(x => x.Value)
                .ToArray());

            if (means.Length == 0)
            {
                return;
            }

            double min = means.Min();

            (string suffix, double divider) = UnitHelper.GetTimeSuffix(min);

            foreach (DataRow.DataGroup row in dataGroups)
            {
                row.SetStringWithUnits(suffix, divider);
            }
        }


        abstract class Row
        {
            // text+6*data
            public const int ColumnCount = 3 + 6 * 3;

            public static readonly Row Empty = new EmptyRow();

            public static IEnumerable<Row> GetTitle()
            {
                string[] dataHeaders = new string[] { "Mean", "Ratio", "StdDev", "StdError", "min", "max" };

                IEnumerable<string> first = new string[] { "Name", "Description", "Category" };
                IEnumerable<string> second = Enumerable.Repeat("", 3);


                first = first.Append("time").Append("method").Concat(Enumerable.Repeat("", 4));
                second = second.Concat(dataHeaders);

                first = first.Append("count").Concat(Enumerable.Repeat("", 5));
                second = second.Concat(dataHeaders);

                first = first.Append("time").Append("frame").Concat(Enumerable.Repeat("", 4));
                second = second.Concat(dataHeaders);


                return new Row[] {
                new StringRow(first.ToArray()),
                new StringRow(second.ToArray())
            };
            }

            public char padding = ' ';

            public abstract string GetValue(int column);

            public string GetPaddedValue(int column, int width, char padding)
            {
                string value = GetValue(column);
                bool rightPad = isRightPad(column);

                return rightPad
                    ? value.PadRight(width, padding)
                    : value.PadLeft(width, padding);

                // some columns are left padded
                bool isRightPad(int ColumnCount)
                {
                    // first 3 are text columns and will be right padded
                    // rest are numbers and will be left padded
                    return ColumnCount < 3;
                }

            }
            public string CreatePaddedString(int[] paddingLength)
            {
                string[] cols = new string[Row.ColumnCount];

                for (int i = 0; i < Row.ColumnCount; i++)
                {
                    // 1 padding either side
                    string inner = GetPaddedValue(i, paddingLength[i], padding);
                    cols[i] = padding + inner + padding;
                }
                string joined = string.Join("|", cols);
                return $"|{joined}|";
            }
        }
        class StringRow : Row
        {
            string[] values;
            public StringRow(params string[] values)
            {
                this.values = values;
            }

            public override string GetValue(int column)
            {
                return values[column];
            }
        }
        class EmptyRow : Row
        {
            public EmptyRow()
            {
                padding = '-';
            }
            public override string GetValue(int column)
            {
                return string.Empty;
            }
        }
        class DataRow : Row
        {
            public string name;
            public string description;
            public string category;
            public DataGroup methodTime;
            public DataGroup count;
            public DataGroup frameTime;

            public DataRow(BenchmarkAnalyser.ProcessedResults result, string category)
            {
                name = result.benchmark.name;
                description = result.benchmark.description;
                this.category = category;

                if (result.Failed)
                {
                    methodTime = DataGroup.NoResults;
                    count = DataGroup.NoResults;
                    frameTime = DataGroup.NoResults;
                }
                else
                {
                    methodTime = new DataGroup(result.methodTime);
                    count = new DataGroup(result.count);
                    frameTime = new DataGroup(result.frameTime);
                }
            }
            public DataRow(string name, string description, string category, DataGroup time, DataGroup count)
            {
                this.name = name;
                this.description = description;
                this.category = category;
                methodTime = time;
                this.count = count;
            }

            public override string GetValue(int column)
            {
                if (column == 0) return name;
                else if (column == 1) return description;
                else if (column == 2) return category;
                else if (column < 3 + 6 * 1) return methodTime.GetValue(column - (3 + 6 * 0));
                else if (column < 3 + 6 * 2) return count.GetValue(column - (3 + 6 * 1));
                else if (column < 3 + 6 * 3) return frameTime.GetValue(column - (3 + 6 * 2));
                else throw new IndexOutOfRangeException();
            }

            public class DataGroup
            {
                public static DataGroup NoResults => new DataGroup();

                public ValueWithUnit mean = ValueWithUnit.NoResults;
                public string ratio = "NA";
                public ValueWithUnit stdDev = ValueWithUnit.NoResults;
                public ValueWithUnit stdError = ValueWithUnit.NoResults;
                public ValueWithUnit min = ValueWithUnit.NoResults;
                public ValueWithUnit max = ValueWithUnit.NoResults;

                public DataGroup() { }
                public DataGroup(BenchmarkAnalyser.DataGroup data)
                {
                    mean = data.mean;
                    stdDev = data.stdDev;
                    stdError = data.stdError;
                    min = data.min;
                    max = data.max;
                    ratio = data.ratio?.ToString("0.00") ?? string.Empty;
                }

                public void SetStringWithUnits(string suffix, double divider)
                {
                    mean.SetStringWithUnits(suffix, divider);
                    stdDev.SetStringWithUnits(suffix, divider);
                    stdError.SetStringWithUnits(suffix, divider);
                    min.SetStringWithUnits(suffix, divider);
                    max.SetStringWithUnits(suffix, divider);
                }

                internal string GetValue(int column)
                {
                    switch (column)
                    {
                        case 0: return mean.ToString();
                        case 1: return ratio;
                        case 2: return stdDev.ToString();
                        case 3: return stdError.ToString();
                        case 4: return min.ToString();
                        case 5: return max.ToString();
                        default: throw new IndexOutOfRangeException();
                    }
                }
            }
            public struct ValueWithUnit
            {
                public static ValueWithUnit NoResults => new ValueWithUnit { text = "NA" };

                public string text;
                public double? raw;

                public ValueWithUnit(double raw)
                {
                    this.raw = raw;
                    text = null;
                }

                public void SetStringWithUnits(string suffix, double divider)
                {
                    if (raw.HasValue)
                    {
                        double value = raw.Value / divider;
                        text = $"{value:0.000} {suffix}";
                    }
                }

                public override string ToString()
                {
                    return text;
                }

                public static implicit operator ValueWithUnit(double raw)
                {
                    return new ValueWithUnit(raw);
                }
            }
        }
    }
    static class UnitHelper
    {
        public static (string suffix, double divider) GetTimeSuffix(double min)
        {
            const double s = 1;
            const double ms = s / 1_000;
            const double us = ms / 1_000;
            const double ns = us / 1_000;

            if (min > s) return (nameof(s), s);
            else if (min > ms) return (nameof(ms), ms);
            else if (min > us) return (nameof(us), us);
            else if (min > ns) return (nameof(ns), ns);
            else return (nameof(ns), ns);
        }
    }
}

