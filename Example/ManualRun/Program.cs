using JamesFrowen.Benchmarker;
using JamesFrowen.Benchmarker.Weaver;
using UnityEngine;

namespace MyBenchmarks
{
#if UNITY_2019_3_OR_NEWER
    // put this into the first scene in unity, then it will run benchmarks and quit
    public class Program : MonoBehaviour
    {
        private int waitCount = 10;
        private int currentWait = 0;

        private void Update()
        {
            // wait 10 frames, 
            // then run once
            // then quit
            Debug.Log($"Update {currentWait}");
            currentWait++;
            if (currentWait == waitCount)
            {
                Debug.Log("Start");
                Main(null);
                Debug.Log("Finish");

                Debug.Log("Quit");
                Application.Quit();
            }
        }

#else
    public class Program
    {
#endif
        public static void Main(string[] args)
        {
            MyBenchmark.RegisterBenchmarks();

            const int frameCount = 300;
            const int perFrame = 100;
            BenchmarkHelper.StartRecording(frameCount, false, false);

            var myBenchmark = new MyBenchmark();
            myBenchmark.Setup();

            for (var i = 0; i < frameCount; i++)
            {
                for (var j = 0; j < perFrame; j++)
                {
                    myBenchmark.Reset();
                    myBenchmark.Method1();
                }
                BenchmarkHelper.NextFrame();
            }
            for (var i = 0; i < frameCount; i++)
            {
                for (var j = 0; j < perFrame; j++)
                {
                    myBenchmark.Reset();
                    myBenchmark.Method2();
                }
                BenchmarkHelper.NextFrame();
            }
            for (var i = 0; i < frameCount; i++)
            {
                for (var j = 0; j < perFrame; j++)
                {
                    myBenchmark.Reset();
                    myBenchmark.Method3();
                }
                BenchmarkHelper.NextFrame();
            }


            myBenchmark.Cleanup();
            BenchmarkHelper.EndRecording();

            // best to use a global path here, because exe might be running in different folders
            var basePath = "./Results/";

            // different paths for different build types
            // change this based on what you are testing
#if UNITY_2022_2_OR_NEWER
            string path;
            if (UnityEngine.Debug.isDebugBuild)
                path = "results_unity2022_debug.md";
            else
                path = "results_unity2022_release.md";
#elif UNITY_2020_3_OR_NEWER
            string path;
            if (UnityEngine.Debug.isDebugBuild)
                path = "results_unity2020_debug.md";
            else
                path = "results_unity2020_release.md";
#else
#if DEBUG
            var path = "results_net7_debug.md";
#else
            var path = "results_net7_release.md";
#endif
#endif
            BenchmarkRunner.LogResults(basePath + path);
        }
    }

    public class MyBenchmark
    {
        public static int[] hash;
        public static void RegisterBenchmarks()
        {
            var names = new string[]
            {
                // lists methods to benchmark here
                // make sure to use :: instead of for the method name
                "System.Void MyBenchmarks.MyBenchmark::Method1()",
                "System.Void MyBenchmarks.MyBenchmark::Method2()",
                "System.Void MyBenchmarks.MyBenchmark::Method3()",
            };
            hash = new int[3];
            for (var i = 0; i < 3; i++)
            {
                hash[i] = names[i].GetHashCode();
                BenchmarkHelper.RegisterMethod(names[i]);
            }
        }

        public void Setup()
        {
            // global setup here
        }

        public void Reset()
        {
            // reset before each run
        }

        public void Cleanup()
        {
            // global cleanup here
        }

        public void Method1()
        {
            var start = BenchmarkHelper.GetTimestamp();

            // call code you want to benchmark here

            BenchmarkHelper.EndMethod(hash[0], start);
        }

        public void Method2()
        {
            var start = BenchmarkHelper.GetTimestamp();

            // call code you want to benchmark here

            BenchmarkHelper.EndMethod(hash[1], start);
        }

        public void Method3()
        {
            var start = BenchmarkHelper.GetTimestamp();

            // call code you want to benchmark here

            BenchmarkHelper.EndMethod(hash[2], start);
        }
    }
}
