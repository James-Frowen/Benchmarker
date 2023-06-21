using JamesFrowen.Benchmarker;
using UnityEngine;

public class MyClass : MonoBehaviour
{
    private void none()
    {

    }

    [BenchmarkMethod]
    private void empty()
    {

    }

    [BenchmarkMethod]
    private void withLog()
    {
        Debug.Log("do stuff");
    }

    [BenchmarkMethod]
    private void withEarlyExit(int a)
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
    private int withReturn(int a)
    {
        return a * a;
    }

    [BenchmarkMethod]
    private int withMultipleReturn(int a)
    {
        if (a < 10)
            return a * a;
        else
            return a;
    }
}
