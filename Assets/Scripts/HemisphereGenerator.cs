using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HemisphereGenerator : MonoBehaviour
{
    public float radius = 1.0f; // 1 meter reach
    public int segments = 24;
    public Material mrMaterial; // Use a semi-transparent or wireframe material

    void Start()
    {
        GenerateHemisphere();
        SetupPositioning();
    }

    void GenerateHemisphere()
    {
        Mesh mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        int numVertices = (segments + 1) * (segments / 2 + 1);
        Vector3[] vertices = new Vector3[numVertices];
        int[] triangles = new int[segments * (segments / 2) * 6];

        int v = 0;
        for (int i = 0; i <= segments / 2; i++)
        {
            float phi = Mathf.PI * i / segments;
            for (int j = 0; j <= segments; j++)
            {
                float theta = 2 * Mathf.PI * j / segments;
                // Formula for sphere coordinates
                float x = radius * Mathf.Sin(phi) * Mathf.Cos(theta);
                float y = radius * Mathf.Cos(phi);
                float z = radius * Mathf.Sin(phi) * Mathf.Sin(theta);
                vertices[v++] = new Vector3(x, y, z);
            }
        }

        int t = 0;
        for (int i = 0; i < segments / 2; i++)
        {
            for (int j = 0; j < segments; j++)
            {
                int next = j + 1;
                int row = i * (segments + 1);
                int nextRow = (i + 1) * (segments + 1);

                triangles[t++] = row + j;
                triangles[t++] = nextRow + j;
                triangles[t++] = nextRow + next;

                triangles[t++] = row + j;
                triangles[t++] = nextRow + next;
                triangles[t++] = row + next;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        
        if(mrMaterial != null) GetComponent<MeshRenderer>().material = mrMaterial;
    }

    void SetupPositioning()
    {   
        // Find the CenterEyeAnchor (the headset position)
        GameObject centerEye = GameObject.Find("CenterEyeAnchor");
        if (centerEye != null)
        {
            transform.SetParent(centerEye.transform);
            transform.localPosition = Vector3.zero;
            // Rotate so the hemisphere is in front of the user
            transform.localRotation = Quaternion.Euler(-90, 0, 0); 
        }
    }
}