using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace JamesFrowen.Benchmarker.Weaver
{
    struct Frame
    {
        public int count;
        public long time;
    }
    public static class BenchmarkHelper
    {
        static Dictionary<int, string> s_methodNames = new Dictionary<int, string>();
        static Dictionary<int, Frame[]> s_methods;

        static int s_frameCount = 300;
        static int s_frameIndex = 0;
        static bool s_isRunning = false;
        static bool s_autoEnd = false;

        // called by IL 
        public static void RegisterMethod(string name)
        {
            s_methodNames.Add(name.GetHashCode(), name);
        }

        // called by IL 
        public static void EndMethod(int nameHash, long start)
        {
            if (s_isRunning) return;

            Frame[] method = s_methods[nameHash];
            method[s_frameCount].count++;
            long end = GetTimestamp();
            method[s_frameCount].time += (end - start);
        }

        public static void StartRecording(int frameCount, bool autoEnd)
        {
            s_isRunning = true;
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
            // todo log/save results

            s_methods = null;
        }
        public static void NextFrame()
        {
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

        public static long GetTimestamp()
        {
            return Stopwatch.GetTimestamp();
        }
    }

    public class BenchmarkMethodAttribute : Attribute
    {

    }
}
