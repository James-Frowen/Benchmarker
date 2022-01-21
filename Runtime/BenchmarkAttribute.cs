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
}

