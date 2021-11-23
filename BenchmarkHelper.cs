/*******************************************************
 * Copyright (C) 2010-2011 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen Benchmarker
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
        static bool s_isRunning = false;
        static bool s_autoEnd = false;

        public static bool IsRunning => s_isRunning;

        public static event Action OnEndRecording;

        // called by IL 
        public static void RegisterMethod(string name)
        {
            s_methodNames.Add(name.GetHashCode(), name);
        }

        // called by IL 
        public static void EndMethod(int nameHash, long start)
        {
            if (!s_isRunning) return;

            Frame[] method = s_methods[nameHash];
            long end = GetTimestamp();
            method[s_frameIndex].time += (end - start);
            method[s_frameIndex].count++;
        }

        // called by IL 
        public static long GetTimestamp()
        {
            return Stopwatch.GetTimestamp();
        }

        public static void StartRecording(int frameCount, bool autoEnd)
        {
            s_isRunning = true;
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
            OnEndRecording?.Invoke();
        }

        public static void NextFrame()
        {
            if (!s_isRunning) return;

            s_frameIndex++;
            if (s_frameIndex >= s_frameCount)
            {
                if (s_autoEnd)
                {
                    EndRecording();
                }
                else
                {
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

                Benchmark benchmark = CreateDetails(fullName);
                results.Add(new Results(frames, benchmark));
            }
            return results;
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
        /// <param name="autoEnd">Should recorind auto stop after <paramref name="frameCount"/> is reached</param>
        public static void StartRecording(int frameCount, bool autoEnd)
        {
            s_previousFrameCount = frameCount;
            CheckUpdater();
            BenchmarkHelper.StartRecording(frameCount, autoEnd);
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

        public IEnumerable<double> Elapsed => frames.Select(x => (double)x.time);
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
                if (result.benchmark.categories.Length == 0)
                {
                    addResultsToCategory(string.Empty, result);
                }
                else
                {
                    foreach (string cat in result.benchmark.categories)
                    {
                        addResultsToCategory(cat, result);
                    }
                }
            }

            ProcessAllResults();
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
            double? baselineMeanTime = null;
            double? baselineMeanCount = null;
            if (category.baseline != null)
            {
                Debug.Log($"baseLine for {category.name}: {category.baseline.benchmark.name}");
                baselineMeanTime = GetMean(category.baseline.Elapsed);
                baselineMeanCount = GetMean(category.baseline.CallCounts);
            }

            category.processedResults = new List<ProcessedResults>();
            foreach (Results result in category.results)
            {
                if (result.Failed) { continue; }
                var processed = new ProcessedResults(result)
                {
                    time = CreateDataGroup(result.Elapsed, baselineMeanTime),
                    count = CreateDataGroup(result.CallCounts, baselineMeanCount)
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
            var time = new DataGroup
            {

                mean = mean,
                ratio = ratio,
                stdDev = stdDev,
                stdError = stdError,
                min = min,
                max = max
            };
            return time;
        }

        private double GetMean(IEnumerable<double> values)
        {
            return values.Average() / Stopwatch.Frequency;
        }
        private double GetMin(IEnumerable<double> values)
        {
            return values.Min() / Stopwatch.Frequency;
        }
        private double GetMax(IEnumerable<double> values)
        {
            return values.Max() / Stopwatch.Frequency;
        }
        private double GetStandardDeviation(IEnumerable<double> values, double mean)
        {
            double sum = values
                // convert to time
                .Select(x => x / Stopwatch.Frequency)
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

            public DataGroup time;
            public DataGroup count;

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
                return x.time.mean.CompareTo(y.time.mean);
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
            rows.AddRange(Row.Title);
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
            IEnumerable<DataRow.DataGroup> timeGroup = rows.Select(x => x as DataRow).Where(x => x != null)
                .Select(x => x.time);
            IEnumerable<DataRow.DataGroup> countGroup = rows.Select(x => x as DataRow).Where(x => x != null)
                .Select(x => x.time);

            SetUnits(timeGroup);
            SetUnits(countGroup);
        }
        private static void SetUnits(IEnumerable<DataRow.DataGroup> dataGroups)
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

            (string suffix, double divider) = GetSuffix(min);

            foreach (DataRow.DataGroup row in dataGroups)
            {
                if (row.mean.raw.HasValue)
                {
                    row.SetStringWithUnits(suffix, divider);
                }
            }
        }

        public static (string suffix, double divider) GetSuffix(double min)
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

        abstract class Row
        {
            public static readonly Row Empty = new EmptyRow();
            public static IEnumerable<Row> Title => new Row[] {

                new StringRow("Name", "Description", "Category", "Time", "", "", "", "", "", "Count", "", "", "", "", ""),
                new StringRow("", "", "", "Mean", "Ratio", "StdDev", "StdError", "min", "max","Mean", "Ratio", "StdDev", "StdError", "min", "max"),
            };


            public char padding = ' ';

            public const int ColumnCount = 15;
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
            public DataGroup time;
            public DataGroup count;

            public DataRow(BenchmarkAnalyser.ProcessedResults result, string category)
            {
                name = result.benchmark.name;
                description = result.benchmark.description;
                this.category = category;

                if (result.Failed)
                {
                    time = DataGroup.NoResults;
                    count = DataGroup.NoResults;
                }
                else
                {
                    time = new DataGroup(result.time);
                    count = new DataGroup(result.count);
                }
            }
            public DataRow(string name, string description, string category, DataGroup time, DataGroup count)
            {
                this.name = name;
                this.description = description;
                this.category = category;
                this.time = time;
                this.count = count;
            }

            public override string GetValue(int column)
            {
                switch (column)
                {
                    case 0: return name;
                    case 1: return description;
                    case 2: return category;
                    case 3: return time.mean.ToString();
                    case 4: return time.ratio;
                    case 5: return time.stdDev.ToString();
                    case 6: return time.stdError.ToString();
                    case 7: return time.min.ToString();
                    case 8: return time.max.ToString();

                    case 9: return count.mean.ToString();
                    case 10: return count.ratio;
                    case 11: return count.stdDev.ToString();
                    case 12: return count.stdError.ToString();
                    case 13: return count.min.ToString();
                    case 14: return count.max.ToString();
                }
                throw new IndexOutOfRangeException();
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
}
