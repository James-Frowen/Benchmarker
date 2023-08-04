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
using Debug = UnityEngine.Debug;

namespace JamesFrowen.Benchmarker
{
    public abstract class BenchmarkPrinter
    {
        protected readonly string path;

        protected BenchmarkPrinter(string path)
        {
            this.path = path;
            CheckDirectory(path);
        }

        private void CheckDirectory(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        public abstract void Print(BenchmarkAnalyser.CategoryGroup[] categories, params string[] metaData);
    }
    public class MarkDownBenchmarkPrinter : BenchmarkPrinter
    {
        public MarkDownBenchmarkPrinter(string path) : base(path)
        {
        }

        public override void Print(BenchmarkAnalyser.CategoryGroup[] categories, params string[] metaData)
        {
            if (categories.Length == 0)
            {
                Debug.LogWarning("No Categories to print");
            }
            var rows = new List<Row>();

            //title
            rows.AddRange(Row.GetTitle());
            rows.Add(Row.Empty);

            for (var i = 0; i < categories.Length; i++)
            {
                if (i > 0)
                {
                    // spacer
                    rows.Add(Row.Empty);
                }

                var category = categories[i];
                foreach (var result in category.processedResults)
                {
                    rows.Add(new DataRow(result, category.name));
                }
            }

            // todo do we only want to set units per catetgory?
            SetUnits(rows);

            var paddingLength = GetPaddingLength(rows);

#if UNITY_2019_3_OR_NEWER
            Debug.Log($"Saving benchmark results to {path}");
#else
            Console.WriteLine($"Saving benchmark results to {path}");
#endif
            using (var writer = new StreamWriter(path))
            {
#if UNITY_SERVER
                bool isServer = true;
#else
                var isServer = false;
#endif
                writer.WriteLine("**Application**");
#if UNITY_2019_3_OR_NEWER
                writer.WriteLine($"- UnityVersion:{Application.unityVersion}");
#else
                writer.WriteLine($"- UnityVersion: Not unity");
#endif
                writer.WriteLine($"- Platform:{Application.platform}");
                writer.WriteLine($"- IsEditor:{Application.isEditor}");
#if UNITY_2019_3_OR_NEWER
                writer.WriteLine($"- IsDebug:{Debug.isDebugBuild}");
#else
#if DEBUG
                writer.WriteLine($"- IsDebug:true");
#else
                writer.WriteLine($"- IsDebug:false");
#endif
#endif
                writer.WriteLine($"- IsServer:{isServer}");

                if (metaData != null && metaData.Length > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine();
                    foreach (var header in metaData)
                    {
                        writer.WriteLine($"- {header}");
                    }
                }


                writer.WriteLine();
                writer.WriteLine();
                writer.WriteLine("**Results**");
                writer.WriteLine();

                foreach (var row in rows)
                {
                    writer.WriteLine(row.CreatePaddedString(paddingLength));
                }
            }
        }


        private static int[] GetPaddingLength(List<Row> rows)
        {
            var max = new int[Row.ColumnCount];
            for (var i = 0; i < Row.ColumnCount; i++)
            {
                max[i] = rows.Max(x =>
                {
                    var value = x.GetValue(i);
                    // just incase value is null
                    return (value ?? "").Length;
                });
            }
            return max;
        }


        private static void SetUnits(List<Row> rows)
        {
            var dataRows = rows.Select(x => x as DataRow).Where(x => x != null);

            SetTimeUnits(dataRows.Select(x => x.methodTime));
            SetTimeUnits(dataRows.Select(x => x.frameTime));

            foreach (var row in dataRows.Select(x => x.count))
            {
                row.SetStringWithUnits("", 1);
            }
        }


        private static void SetTimeUnits(IEnumerable<DataRow.DataGroup> dataGroups)
        {
            var means = dataGroups
                .Select(x => x.mean.raw)
                .Where(x => x.HasValue)
                .Select(x => x.Value)
                .ToArray();

            if (means.Length == 0)
            {
                return;
            }

            var min = means.Min();

            (var suffix, var divider) = UnitHelper.GetTimeSuffix(min);

            foreach (var row in dataGroups)
            {
                row.SetStringWithUnits(suffix, divider);
            }
        }

        private abstract class Row
        {
            // text+6*data
            public const int ColumnCount = 3 + (6 * 3);

            public static readonly Row Empty = new EmptyRow();

            public static IEnumerable<Row> GetTitle()
            {
                var dataHeaders = new string[] { "Mean", "Ratio", "StdDev", "StdError", "min", "max" };

                IEnumerable<string> first = new string[] { "Name", "Description", "Category" };
                var second = Enumerable.Repeat("", 3);


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

            public string GetValue(int column)
            {
                // null check just incase value was not set, better than having NullRef throw later
                return _GetValue(column) ?? "<NULL>";
            }

            protected abstract string _GetValue(int column);

            public string GetPaddedValue(int column, int width, char padding)
            {
                var value = GetValue(column);
                if (value == null)
                    value = "";

                var rightPad = isRightPad(column);

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
                var cols = new string[Row.ColumnCount];

                for (var i = 0; i < Row.ColumnCount; i++)
                {
                    // 1 padding either side
                    var inner = GetPaddedValue(i, paddingLength[i], padding);
                    cols[i] = padding + inner + padding;
                }
                var joined = string.Join("|", cols);
                return $"|{joined}|";
            }
        }

        private class StringRow : Row
        {
            private string[] values;
            public StringRow(params string[] values)
            {
                this.values = values;
            }

            protected override string _GetValue(int column)
            {
                return values[column];
            }
        }

        private class EmptyRow : Row
        {
            public EmptyRow()
            {
                padding = '-';
            }
            protected override string _GetValue(int column)
            {
                return string.Empty;
            }
        }

        private class DataRow : Row
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

            protected override string _GetValue(int column)
            {
                if (column == 0) return name;
                else if (column == 1) return description;
                else if (column == 2) return category;
                else if (column < 3 + (6 * 1)) return methodTime.GetValue(column - (3 + (6 * 0)));
                else if (column < 3 + (6 * 2)) return count.GetValue(column - (3 + (6 * 1)));
                else if (column < 3 + (6 * 3)) return frameTime.GetValue(column - (3 + (6 * 2)));
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
                        var value = raw.Value / divider;
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

