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
using System.Collections;
using JamesFrowen.Benchmarker.Weaver;
using UnityEngine;

namespace JamesFrowen.Benchmarker
{
    /// <summary>
    /// Inherit from this to set up and run benchmarks
    /// </summary>
    public abstract class BenchmarkBehaviour : MonoBehaviour
    {
        public int WarmupCount = 30;
        public int RunCount = 300;
        public int Iterations = 1000;

        protected virtual void Setup() { }
        protected virtual void Teardown() { }
        protected abstract void Benchmark();

        private IEnumerator Start()
        {
            Setup();
            yield return null;

            Warmup();
            Measure();

            yield return null;

            Teardown();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;

#elif UNITY_2019_3_OR_NEWER
            Application.Quit();
#else
            Environment.Exit(0);
#endif
        }

        private void Warmup()
        {
            for (var i = 0; i < WarmupCount; i++)
            {
                for (var j = 0; j < Iterations; j++)
                {
                    Benchmark();
                }
            }
        }
        private void Measure()
        {
            Benchmarker.BenchmarkRunner.StartRecording(RunCount, true, false);
            for (var i = 0; i < RunCount; i++)
            {
                for (var j = 0; j < Iterations; j++)
                {
                    Benchmark();
                }

                BenchmarkHelper.NextFrame();
            }
        }
    }
}

