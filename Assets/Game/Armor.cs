using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Add this to a GameObject with collider component.
public class Armor : MonoBehaviour
{
    // In millimeters. 
    public float Thickness = 1.0f;
    public float Weight;
    public float Health = 1.0f;

    private void OnValidate()
    {
        // Calculate weight
        //Weight = GetWeight();
    }

    float GetWeight()
    {
        float Weight = 0;
        // Add all the areas of the triangles
        var mesh = GetComponent<MeshCollider>().sharedMesh;
        var triangles = mesh.triangles;
        var vertices = mesh.vertices;

        for (int i = 0; i < triangles.Length / 3; i += 3)
        {
            Vector3 A = vertices[triangles[i]];
            Vector3 B = vertices[triangles[i + 1]];
            Vector3 C = vertices[triangles[i + 2]];

            var Area = 0.5f * Vector3.Cross(B - A, C - A).magnitude;

            Weight += Area * Thickness;
        }
        //Debug.Log("Weight: " + Weight);

        return Weight;
    }
}
