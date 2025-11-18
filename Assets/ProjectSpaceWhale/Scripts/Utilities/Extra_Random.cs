using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public static class Extra_Random
{
    public static int GetRandomNonRepeating(int index, out int newIndex, List<int> pool)
    {
        if (index == 0)
            ShuffleList(pool);

        int num = pool[index];
        newIndex = (index + 1) % pool.Count;
        return num;
    }

    // Fisher–Yates shuffle
    public static void ShuffleList(List<int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rand = Random.Range(i, list.Count);
            (list[i], list[rand]) = (list[rand], list[i]);
        }
    }

    public static Vector3 RandomPointInSphere(Vector3 center, float radius)
    {
        return center + Random.insideUnitSphere * radius;
    }

    // Returns a random point inside a capsule defined by two endpoints and radius.
    public static Vector3 RandomPointInCapsule(Vector3 pointA, Vector3 pointB, float radius)
    {
        // Capsule direction
        Vector3 axis = pointB - pointA;
        float height = axis.magnitude;
        Vector3 axisDir = axis.normalized;

        // Pick a random point along the capsule axis
        float randomHeight = Random.Range(0f, height);

        // Create an orthonormal basis to define a circle plane
        Vector3 tangent = Vector3.Cross(axisDir, Vector3.up);

        // If axis is parallel to Vector3.up, pick another vector
        if (tangent.sqrMagnitude < 0.001f)
            tangent = Vector3.Cross(axisDir, Vector3.right);

        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(axisDir, tangent);

        // Pick a random point in a circle (uniform distribution)
        float r = radius * Mathf.Sqrt(Random.value);
        float theta = Random.Range(0f, Mathf.PI * 2f);

        Vector3 radialOffset = tangent * (r * Mathf.Cos(theta)) +
                               bitangent * (r * Mathf.Sin(theta));

        return pointA + axisDir * randomHeight + radialOffset;
    }
}
