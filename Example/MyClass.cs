using JamesFrowen.Benchmarker.Weaver;
using UnityEngine;

public class MyClass : MonoBehaviour
{
    void none()
    {

    }

    [BenchmarkMethod]
    void empty()
    {

    }

    [BenchmarkMethod]
    void withLog()
    {
        Debug.Log("do stuff");
    }

    [BenchmarkMethod]
    void withEarlyExit(int a)
    {
        if (a == 2)
        {
            Debug.Log("do stuff");
            return;
        }

        if (a == 3)
        {
            Debug.Log("other stuff");
        }
    }

    [BenchmarkMethod]
    int withReturn(int a)
    {
        return a * a;
    }

    [BenchmarkMethod]
    int withMultipleReturn(int a)
    {
        if (a < 10)
            return a * a;
        else
            return a;
    }
}
