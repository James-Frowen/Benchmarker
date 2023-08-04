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

namespace JamesFrowen.Benchmarker
{
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
    [Serializable]
    public class BenchmarkDetails
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
        public readonly BenchmarkDetails benchmark;
        /// <summary>
        /// Ticks
        /// </summary>
        public readonly Frame[] frames;
        public readonly bool Failed;

        // in order to get averages we need to add (time/Count)  count times to list
        // this will give weighted average for average time per frame
        public IEnumerable<double> ElapsedPerMethod => frames.Where(x => x.count != 0).SelectMany(x => Enumerable.Repeat(frameTimeToSeconds(x), x.count));

        private double frameTimeToSeconds(Frame x) => (double)x.time / x.count / Stopwatch.Frequency;
        public IEnumerable<double> ElapsedPerFrame => frames.Select(x => (double)x.time / Stopwatch.Frequency);
        public IEnumerable<double> CallCounts => frames.Select(x => (double)x.count);


        public Results(Frame[] frames, BenchmarkDetails benchmark)
        {
            this.frames = frames;
            this.benchmark = benchmark;

            if (this.frames == null) { Failed = true; }
        }
    }
}

