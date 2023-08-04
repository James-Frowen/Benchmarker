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
using System.Linq;
using Debug = UnityEngine.Debug;

namespace JamesFrowen.Benchmarker
{
    public class BenchmarkAnalyser
    {
        private Dictionary<string, CategoryGroup> categoryGroup = new Dictionary<string, CategoryGroup>();
        public BenchmarkAnalyser(IEnumerable<Results> results)
        {
            foreach (var result in results)
            {
                var categories = result.benchmark.categories;
                if (categories == null || categories.Length == 0)
                {
                    addResultsToCategory(string.Empty, result);
                }
                else
                {
                    foreach (var cat in categories)
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
            if (!categoryGroup.TryGetValue(key, out var group))
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
            foreach (var category in categoryGroup.Values)
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
            foreach (var result in category.results)
            {
                if (result.Failed) { continue; }
                var processed = new ProcessedResults(result)
                {
                    methodTime = CreateDataGroup(result.ElapsedPerMethod, baselineMeanMethodTime),
                    count = CreateDataGroup(result.CallCounts, baselineMeanCount),
                    frameTime = CreateDataGroup(result.ElapsedPerFrame, baselineMeanFrameTime),
                    BaseLine = result == category.baseline,
                };

                category.processedResults.Add(processed);
            }
        }

        private DataGroup CreateDataGroup(IEnumerable<double> values, double? baseLineMean)
        {
            var mean = GetMean(values);
            var stdDev = GetStandardDeviation(values, mean);
            var stdError = GetStandardError(values, stdDev);
            var ratio = baseLineMean.HasValue ? mean / baseLineMean : null;
            var min = GetMin(values);
            var max = GetMax(values);

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
            var sum = values
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
            foreach (var category in categoryGroup.Values)
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
        [Serializable]
        public class ProcessedResults
        {
            public bool Failed;
            public bool BaseLine;
            public BenchmarkDetails benchmark;

            public DataGroup methodTime;
            public DataGroup count;
            public DataGroup frameTime;

            public ProcessedResults(Results result)
            {
                Failed = result.Failed;
                benchmark = result.benchmark;
            }
        }
        [Serializable]
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
}

