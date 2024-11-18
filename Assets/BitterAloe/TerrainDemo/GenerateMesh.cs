using System.Collections.Generic;
using UnityEngine;
using ProceduralToolkit;
using System;

[RequireComponent(typeof(MeshFilter))]
public class GenerateMesh : MonoBehaviour
{

    private MeshFilter meshFilter;

    public Vector3 TerrainSize { get; set; }
    public float CellSize { get; set; }
    public float NoiseScale { get; set; }

    //public Gradient Gradient { get; set; }

    public Vector2 NoiseOffset { get; set; }

    private static bool usePerlinNoise = true;
    public static bool UsePerlinNoise { get { return usePerlinNoise; } set { usePerlinNoise = value; } }

    public void Generate()
    {
        meshFilter = GetComponent<MeshFilter>();

        MeshDraft draft = TerrainDraft(TerrainSize, CellSize, NoiseOffset, NoiseScale/*, Gradient*/);
        draft.Move(Vector3.left * TerrainSize.x / 2 + Vector3.back * TerrainSize.z / 2);
        meshFilter.mesh = draft.ToMesh();
        //meshFilter.mesh = WeldVertices(draft.ToMesh());
        meshFilter.mesh.normals = CalculateNormals(meshFilter.mesh);

        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider)
            meshCollider.sharedMesh = meshFilter.mesh;
    }

    private static MeshDraft TerrainDraft(Vector3 terrainSize, float cellSize, Vector2 noiseOffset, float noiseScale/*, Gradient gradient*/)
    {
        int xSegments = Mathf.FloorToInt(terrainSize.x / cellSize);
        int zSegments = Mathf.FloorToInt(terrainSize.z / cellSize);

        float xStep = terrainSize.x / xSegments;
        float zStep = terrainSize.z / zSegments;
        int vertexCount = 6 * xSegments * zSegments;
        MeshDraft draft = new MeshDraft
        {
            name = "Terrain",
            vertices = new List<Vector3>(vertexCount),
            triangles = new List<int>(vertexCount),
            uv = new List<Vector2>(vertexCount),
            normals = new List<Vector3>(vertexCount)//,
            //colors = new List<Color>(vertexCount)
        };

        for (int i = 0; i < vertexCount; i++)
        {
            draft.vertices.Add(Vector3.zero);
            draft.triangles.Add(0);
            draft.uv.Add(Vector2.zero);
            draft.normals.Add(Vector3.zero);
            //draft.colors.Add(Color.black);
        }

        //int i = 0;
        //for (int x = 0; x < xSegments; x++)
        //{
        //    for (int z = 0; z < zSegments; z++)
        //    {
        //        float height = GetHeight(x, z, xSegments, zSegments, noiseOffset, noiseScale);
        //        Vector3 vertex = new Vector3(x * xStep, height * terrainSize.y, z * zStep);
        //        draft.vertices[x * zSegments + z] = vertex;
        //    }
        //}

        for (int x = 0; x < xSegments; x++)
        {
            for (int z = 0; z < zSegments; z++)
            {
                int index0 = 6 * (x + z * xSegments);
                int index1 = index0 + 1;
                int index2 = index0 + 2;
                int index3 = index0 + 3;
                int index4 = index0 + 4;
                int index5 = index0 + 5;

                float height00 = GetHeight(x + 0, z + 0, xSegments, zSegments, noiseOffset, noiseScale);
                float height01 = GetHeight(x + 0, z + 1, xSegments, zSegments, noiseOffset, noiseScale);
                float height10 = GetHeight(x + 1, z + 0, xSegments, zSegments, noiseOffset, noiseScale);
                float height11 = GetHeight(x + 1, z + 1, xSegments, zSegments, noiseOffset, noiseScale);

                Vector3 vertex00 = new Vector3((x + 0) * xStep, height00 * terrainSize.y, (z + 0) * zStep);
                Vector3 vertex01 = new Vector3((x + 0) * xStep, height01 * terrainSize.y, (z + 1) * zStep);
                Vector3 vertex10 = new Vector3((x + 1) * xStep, height10 * terrainSize.y, (z + 0) * zStep);
                Vector3 vertex11 = new Vector3((x + 1) * xStep, height11 * terrainSize.y, (z + 1) * zStep);

                // TODO: Fix UV layout

                draft.vertices[index0] = vertex00;
                //draft.uv[index0] = new Vector2(vertex00.x / xSegments, vertex00.z / zSegments);
                draft.vertices[index1] = vertex01;
                //draft.uv[index1] = new Vector2(vertex01.x / xSegments, vertex01.z / zSegments);
                draft.vertices[index2] = vertex11;
                //draft.uv[index2] = new Vector2(vertex11.x / xSegments, vertex11.z / zSegments);
                draft.vertices[index3] = vertex00;
                //draft.uv[index3] = new Vector2(vertex00.x / xSegments, vertex00.z / zSegments);
                draft.vertices[index4] = vertex11;
                //draft.uv[index4] = new Vector2(vertex11.x / xSegments, vertex11.z / zSegments);
                draft.vertices[index5] = vertex10;
                //draft.uv[index5] = new Vector2(vertex10.x / xSegments, vertex10.z / zSegments);

                //draft.colors[index0] = gradient.Evaluate(height00);
                //draft.colors[index1] = gradient.Evaluate(height01);
                //draft.colors[index2] = gradient.Evaluate(height11);
                //draft.colors[index3] = gradient.Evaluate(height00);
                //draft.colors[index4] = gradient.Evaluate(height11);
                //draft.colors[index5] = gradient.Evaluate(height10);

                Vector3 normal000111 = Vector3.Cross(vertex01 - vertex00, vertex11 - vertex00).normalized;
                Vector3 normal001011 = Vector3.Cross(vertex11 - vertex00, vertex10 - vertex00).normalized;

                draft.normals[index0] = normal000111;
                draft.normals[index1] = normal000111;
                draft.normals[index2] = normal000111;
                draft.normals[index3] = normal001011;
                draft.normals[index4] = normal001011;
                draft.normals[index5] = normal001011;

                draft.triangles[index0] = index0;
                draft.triangles[index1] = index1;
                draft.triangles[index2] = index2;
                draft.triangles[index3] = index3;
                draft.triangles[index4] = index4;
                draft.triangles[index5] = index5;
            }
        }

        return draft;
    }

    //int meshSimplificationIncrement = (levelOfDetail == 0) ? 1 : levelOfDetail * 2;

    //int borderedSize = Mathf.FloorToInt(terrainSize.x / cellSize); ;
    //int meshSize = borderedSize - 2 * meshSimplificationIncrement;
    //int meshSizeUnsimplified = borderedSize - 2;

    //float topLeftX = (meshSizeUnsimplified - 1) / -2f;
    //float topLeftZ = (meshSizeUnsimplified - 1) / 2f;


    //int verticesPerLine = (meshSize - 1) / meshSimplificationIncrement + 1;

    ////MeshData meshData = new MeshData(verticesPerLine);

    //MeshDraft meshData = new MeshDraft
    //{
    //    name = "Terrain",
    //    vertices = new List<Vector3>(verticesPerLine * verticesPerLine),
    //    uv = new List<Vector2>(verticesPerLine * verticesPerLine),
    //    triangles = new List<int>((verticesPerLine - 1) * (verticesPerLine - 1) * 6),
    //    normals = new List<Vector3>(verticesPerLine),

    //    borderVertices = new List<Vector3>(verticesPerLine*4 + 4),
    //    borderTriangles = new List<int>(24 * verticesPerLine)          
    //};

    //int[,] vertexIndicesMap = new int[borderedSize, borderedSize];
    //int meshVertexIndex = 0;
    //int borderVertexIndex = -1;

    //for (int y = 0; y < borderedSize; y += meshSimplificationIncrement)
    //{
    //    for (int x = 0; x < borderedSize; x += meshSimplificationIncrement)
    //    {
    //        bool isBorderVertex = y == 0 || y == borderedSize - 1 || x == 0 || x == borderedSize - 1;

    //        if (isBorderVertex)
    //        {
    //            vertexIndicesMap[x, y] = borderVertexIndex;
    //            borderVertexIndex--;
    //        }
    //        else
    //        {
    //            vertexIndicesMap[x, y] = meshVertexIndex;
    //            meshVertexIndex++;
    //        }
    //    }
    //}

    //for (int y = 0; y < borderedSize; y += meshSimplificationIncrement)
    //{
    //    for (int x = 0; x < borderedSize; x += meshSimplificationIncrement)
    //    {
    //        int vertexIndex = vertexIndicesMap[x, y];
    //        Vector2 percent = new Vector2((x - meshSimplificationIncrement) / (float)meshSize, (y - meshSimplificationIncrement) / (float)meshSize);
    //        //float height = heightCurve.Evaluate(heightMap[x, y]) * heightMultiplier;
    //        float height = GetHeight(x,y,borderedSize,noiseOffset,noiseScale);
    //        Vector3 vertexPosition = new Vector3(topLeftX + percent.x * meshSizeUnsimplified, height, topLeftZ - percent.y * meshSizeUnsimplified);

    //        //meshData.AddVertex(vertexPosition, percent, vertexIndex);
    //        if (vertexIndex < 0)
    //        {
    //            meshData.borderVertices[-vertexIndex - 1] = vertexPosition;
    //        }
    //        else
    //        {
    //            meshData.vertices[vertexIndex] = vertexPosition;
    //            meshData.uv[vertexIndex] = percent;
    //        }


    //        if (x < borderedSize - 1 && y < borderedSize - 1)
    //        {
    //            int a = vertexIndicesMap[x, y];
    //            int b = vertexIndicesMap[x + meshSimplificationIncrement, y];
    //            int c = vertexIndicesMap[x, y + meshSimplificationIncrement];
    //            int d = vertexIndicesMap[x + meshSimplificationIncrement, y + meshSimplificationIncrement];
    //            meshData = AddTriangle(meshData, a, d, c);
    //            meshData = AddTriangle(meshData, d, a, b);
    //        }

    //        vertexIndex++;
    //    }
    //}

    //return meshData;

    // https://discussions.unity.com/t/welding-vertices-at-runtime/191731
    public static Mesh WeldVertices(Mesh aMesh, float aMaxDelta = 0.01f)
    {
        var verts = aMesh.vertices;
        var normals = aMesh.normals;
        var uvs = aMesh.uv;
        Dictionary<Vector3, int> duplicateHashTable = new Dictionary<Vector3, int>();
        List<int> newVerts = new List<int>();
        int[] map = new int[verts.Length];

        //create mapping and find duplicates, dictionaries are like hashtables, mean fast
        for (int i = 0; i < verts.Length; i++)
        {
            if (!duplicateHashTable.ContainsKey(verts[i]))
            {
                duplicateHashTable.Add(verts[i], newVerts.Count);
                map[i] = newVerts.Count;
                newVerts.Add(i);
            }
            else
            {
                map[i] = duplicateHashTable[verts[i]];
            }
        }

        // create new vertices
        var verts2 = new Vector3[newVerts.Count];
        var normals2 = new Vector3[newVerts.Count];
        var uvs2 = new Vector2[newVerts.Count];
        for (int i = 0; i < newVerts.Count; i++)
        {
            int a = newVerts[i];
            verts2[i] = verts[a];
            normals2[i] = normals[a];
            try
            {
                uvs2[i] = uvs[a];
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e.Message);
            }

        }
        // map the triangle to the new vertices
        var tris = aMesh.triangles;
        for (int i = 0; i < tris.Length; i++)
        {
            tris[i] = map[tris[i]];
        }
        aMesh.triangles = tris;
        aMesh.vertices = verts2;
        aMesh.normals = normals2;
        aMesh.uv = uvs2;

        aMesh.RecalculateBounds();

        return aMesh;
    }

    // https://www.youtube.com/watch?v=NpeYTcS7n-M&list=PLFt_AvWsXl0eBW2EiBtl_sxmDtSgZBxB3&index=12
    private Vector3[] CalculateNormals(Mesh mesh)
    {
        Vector3[] vertexNormals = new Vector3[mesh.vertices.Length];
        int triangleCount = mesh.triangles.Length / 3;
        for (int i = 0; i < triangleCount; i++)
        {
            int normalTriangleIndex = i * 3;
            int vertexIndexA = mesh.triangles[normalTriangleIndex];
            int vertexIndexB = mesh.triangles[normalTriangleIndex + 1];
            int vertexIndexC = mesh.triangles[normalTriangleIndex + 2];

            Vector3 triangleNormal = SurfaceNormalFromIndices(mesh, vertexIndexA, vertexIndexB, vertexIndexC);
            vertexNormals[vertexIndexA] += triangleNormal;
            vertexNormals[vertexIndexB] += triangleNormal;
            vertexNormals[vertexIndexC] += triangleNormal;
        }

        for (int i = 0; i < vertexNormals.Length; i++)
        {
            vertexNormals[i].Normalize();
        }

        return vertexNormals;
    }

    Vector3 SurfaceNormalFromIndices(Mesh mesh, int indexA, int indexB, int indexC)
    {
        Vector3 pointA = mesh.vertices[indexA];
        Vector3 pointB = mesh.vertices[indexB];
        Vector3 pointC = mesh.vertices[indexC];

        Vector3 sideAB = pointB - pointA;
        Vector3 sideAC = pointC - pointA;
        return Vector3.Cross(sideAB, sideAC).normalized;
    }


    private static float GetHeight(int x, int z, int xSegments, int zSegments, Vector2 noiseOffset, float noiseScale)
    {
        float noiseX = noiseScale * x / xSegments + noiseOffset.x;
        float noiseZ = noiseScale * z / zSegments + noiseOffset.y;
        if (usePerlinNoise)
            return Mathf.PerlinNoise(noiseX, noiseZ);
        else
            return TerrainController.noisePixels[(int)noiseX % TerrainController.noisePixels.Length][(int)noiseZ % TerrainController.noisePixels[0].Length];
    }

}