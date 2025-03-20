using System.Collections.Generic;
using UnityEngine;
using ProceduralToolkit;
using System;
using System.Linq;

[RequireComponent(typeof(MeshFilter))]
public class GenerateMesh : MonoBehaviour
{

    private MeshFilter meshFilter;
    private MeshFilter childMeshFilter;

    // physical size of terrain
    public Vector3 TerrainSize { get; set; }
    // physical size of the terrain mesh's quads
    public float CellSize { get; set; }
    public float NoiseScale { get; set; }

    public Vector2 NoiseOffset { get; set; }

    public Vector2 TileIndex { get; set; }

    private static bool usePerlinNoise = true;
    public static bool UsePerlinNoise { get { return usePerlinNoise; } set { usePerlinNoise = value; } }

    public void Generate()
    {
        meshFilter = GetComponent<MeshFilter>();
        childMeshFilter = transform.GetChild(0).GetComponent<MeshFilter>();

        MeshDraft draft = SmoothTerrainDraft(TerrainSize, CellSize, NoiseOffset, NoiseScale);
        draft.Move(Vector3.left * TerrainSize.x / 2 + Vector3.back * TerrainSize.z / 2);
        meshFilter.mesh = draft.ToMesh();
        //meshFilter.mesh = WeldVertices(draft.ToMesh());
        //meshFilter.mesh.normals = CalculateNormals(meshFilter.mesh);

        childMeshFilter.mesh = meshFilter.mesh;

        MeshCollider meshCollider = GetComponent<MeshCollider>();
        MeshCollider childMeshCollider = transform.GetChild(0).GetComponent<MeshCollider>();

        if (meshCollider)
            meshCollider.sharedMesh = meshFilter.mesh;
        if (childMeshCollider)
            childMeshCollider.sharedMesh = meshFilter.mesh;
    }

    private MeshDraft SmoothTerrainDraft(Vector3 terrainSize, float cellSize, Vector2 noiseOffset, float noiseScale)
    {
        // how many quads fit into a terrain tile on one axis
        int meshQuads = Mathf.FloorToInt(terrainSize.x / cellSize);
        int borderedQuads = meshQuads + 2;
        // count of vertices across tile edge
        int meshSize = meshQuads + 1;
        int borderedSize = borderedQuads + 1;

        // physical distance between vertices
        float xStep = terrainSize.x / meshQuads;
        float zStep = terrainSize.z / meshQuads;

        int vertexCount = meshSize * meshSize;
        int triangleCount = 6 * meshQuads * meshQuads;

        int vertexCountBordered = borderedSize * borderedSize;
        int triangleCountBordered = 6 * borderedQuads * borderedQuads;

        MeshDraft draft = new MeshDraft
        {
            name = "Terrain",
            vertices = new List<Vector3>(vertexCountBordered),
            triangles = new List<int>(triangleCountBordered),
            normals = new List<Vector3>(vertexCountBordered),
            uv = new List<Vector2>(vertexCount)
        };

        for (int i = 0; i < vertexCountBordered; i++)
        {
            draft.vertices.Add(Vector3.zero);
            draft.normals.Add(Vector3.zero);
        }
        for (int i = 0; i < triangleCountBordered; i++)
            draft.triangles.Add(0);
        for (int i = 0; i < vertexCount; i++)
            draft.uv.Add(Vector2.zero);

        // vertex index map
        int[,] vertexIndexMap = new int[borderedSize, borderedSize];

        // height map
        float[,] heightMap = new float[borderedSize, borderedSize];


        // populates height map and index map
        for (int z = 0, meshVertexIndex = 0, borderVertexIndex = -1; z < borderedSize; z++)
        {
            for (int x = 0; x < borderedSize; x++) 
            {
                heightMap[x, z] = GetHeight(x, z, meshQuads, meshQuads, noiseOffset, noiseScale) * terrainSize.y;
                if (z == 0 || z == borderedSize - 1 || x == 0 || x == borderedSize - 1)
                {
                    vertexIndexMap[x, z] = borderVertexIndex;
                    borderVertexIndex--;
                }
                else
                {
                    vertexIndexMap[x, z] = meshVertexIndex;
                    meshVertexIndex++;
                }
            }
        }

        // adds vertices to draft using height map and index map
        for (int z = 0; z < borderedSize; z++)
        {
            for (int x = 0; x < borderedSize; x++)
            {
                Vector3 vertex = new Vector3((x - 1) * xStep, heightMap[x, z], (z - 1) * zStep);
                int index = vertexIndexMap[x, z];

                if (index >= 0)
                    draft.vertices[index] = vertex;
                else
                    draft.vertices[draft.vertices.Count + index] = vertex;
            }
        }

        // triangles
        for (int z = 0, meshQuadIndex = 0, borderQuadIndex = -6; z < borderedQuads; z++)
        {
            for (int x = 0; x < borderedQuads; x++)
            {
                int vertexIndex00 = vertexIndexMap[x + 0, z + 0];
                int vertexIndex01 = vertexIndexMap[x + 0, z + 1];
                int vertexIndex10 = vertexIndexMap[x + 1, z + 0];
                int vertexIndex11 = vertexIndexMap[x + 1, z + 1];

                // if border quad
                if (z == 0 || z == borderedQuads - 1 || x == 0 || x == borderedQuads - 1)
                {
                    // adjust negative indexes to correctly pull from the end of the vertex list
                    //Debug.Log($"vertex indices for quads: 00 = {vertexIndex00}, 01 = {vertexIndex01}, 10 = {vertexIndex10}, 11 = {vertexIndex11}");
                    if (vertexIndex00 < 0)
                        vertexIndex00 += draft.vertices.Count;
                    if (vertexIndex01 < 0)
                        vertexIndex01 += draft.vertices.Count;
                    if (vertexIndex10 < 0)
                        vertexIndex10 += draft.vertices.Count;
                    if (vertexIndex11 < 0)
                        vertexIndex11 += draft.vertices.Count;

                    draft.triangles[draft.triangles.Count + borderQuadIndex + 0] = vertexIndex00;
                    draft.triangles[draft.triangles.Count + borderQuadIndex + 1] = vertexIndex01;
                    draft.triangles[draft.triangles.Count + borderQuadIndex + 2] = vertexIndex10;
                    draft.triangles[draft.triangles.Count + borderQuadIndex + 3] = vertexIndex10;
                    draft.triangles[draft.triangles.Count + borderQuadIndex + 4] = vertexIndex01;
                    draft.triangles[draft.triangles.Count + borderQuadIndex + 5] = vertexIndex11;

                    borderQuadIndex -= 6;
                }
                else
                {
                    draft.triangles[meshQuadIndex + 0] = vertexIndex00;
                    draft.triangles[meshQuadIndex + 1] = vertexIndex01;
                    draft.triangles[meshQuadIndex + 2] = vertexIndex10;
                    draft.triangles[meshQuadIndex + 3] = vertexIndex10;
                    draft.triangles[meshQuadIndex + 4] = vertexIndex01;
                    draft.triangles[meshQuadIndex + 5] = vertexIndex11;

                    meshQuadIndex += 6;
                }
            }
        }


        List<Vector3> vertexNormals = new List<Vector3>(draft.normals.Count);
        for (int z = 0, meshQuadIndex = 0, borderQuadIndex = -6; z < borderedQuads; z++)
        {
            for (int x = 0; x < borderedQuads; x++)
            {
                int vertexIndex00 = vertexIndexMap[x + 0, z + 0];
                int vertexIndex01 = vertexIndexMap[x + 0, z + 1];
                int vertexIndex10 = vertexIndexMap[x + 1, z + 0];
                int vertexIndex11 = vertexIndexMap[x + 1, z + 1];
                 
                if (vertexIndex00 < 0)
                    vertexIndex00 += draft.vertices.Count;
                if (vertexIndex01 < 0)
                    vertexIndex01 += draft.vertices.Count;
                if (vertexIndex10 < 0)
                    vertexIndex10 += draft.vertices.Count;
                if (vertexIndex11 < 0)
                    vertexIndex11 += draft.vertices.Count;

                Vector3 triangleNormal = SurfaceNormalFromIndices(draft, vertexIndex00, vertexIndex01, vertexIndex10);
                draft.normals[vertexIndex00] += triangleNormal;
                draft.normals[vertexIndex01] += triangleNormal;
                draft.normals[vertexIndex10] += triangleNormal;

                triangleNormal = SurfaceNormalFromIndices(draft, vertexIndex10, vertexIndex01, vertexIndex11);
                draft.normals[vertexIndex10] += triangleNormal;
                draft.normals[vertexIndex01] += triangleNormal;
                draft.normals[vertexIndex11] += triangleNormal;

            }
        }

        for (int i = 0; i < vertexNormals.Count; i++)
        {
            draft.normals[i].Normalize();
        }

        
        draft.vertices.RemoveRange(vertexCount, vertexCountBordered - vertexCount);
        draft.triangles.RemoveRange(triangleCount, triangleCountBordered - triangleCount); 
        draft.normals.RemoveRange(vertexCount, vertexCountBordered - vertexCount);
        


        /*
        // vertices
        List<Vector3> verticesBordered = new List<Vector3>(vertexCountBordered);
        for (int i = 0; i < vertexCountBordered; i++)
        {
            verticesBordered.Add(Vector3.zero);
        }
        // primary vertices
        for (int i = 0, z = 0; z <= meshQuads; z++)
        {
            for (int x = 0; x <= meshQuads; x++)
            {
                float y = GetHeight(x, z, meshQuads, meshQuads, noiseOffset, noiseScale);
                Vector3 vertex = new Vector3(x * xStep, y * terrainSize.y, z * zStep);

                draft.vertices[i] = vertex;
                i++;
            }
        }
        // extra vertices only used for normal calculation
        for (int z = -1; z <= meshQuads + 1; z++) if (z < 0 || z > meshQuads)
                for (int x = -1; x <= meshQuads + 1; x++) if (x < 0 || x > meshQuads)
                    {
                        float y = GetHeight(x, z, meshQuads, meshQuads, noiseOffset, noiseScale);
                        Vector3 vertex = new Vector3(x * xStep, y * terrainSize.y, z * zStep);

                        draft.vertices.Add(vertex);
                    }

        // triangles
        List<int> trianglesBordered = new List<int>(triangleCountBordered);
        for (int i = 0; i < triangleCountBordered; i++)
        {
            trianglesBordered.Add(0);
        }
        // primary triangles
        for (int vert = 0, tris = 0, z = 0; z < meshQuads; z++)
        {
            for (int x = 0; x < meshQuads; x++)
            {
                draft.triangles[tris + 0] = vert + 0;
                draft.triangles[tris + 1] = vert + meshQuads + 1;
                draft.triangles[tris + 2] = vert + 1;
                draft.triangles[tris + 3] = vert + 1;
                draft.triangles[tris + 4] = vert + meshQuads + 1;
                draft.triangles[tris + 5] = vert + meshQuads + 2;
                vert++;
                tris += 6;
            }
            vert++;
        }
        //  extra triangles only used for normal calculation
        for (int vert = 0, tris = 0, z = 0; z < meshQuads; z++)
        {
            for (int x = 0; x < meshQuads; x++)
            {
                draft.triangles[tris + 0] = vert + 0;
                draft.triangles[tris + 1] = vert + meshQuads + 1;
                draft.triangles[tris + 2] = vert + 1;
                draft.triangles[tris + 3] = vert + 1;
                draft.triangles[tris + 4] = vert + meshQuads + 1;
                draft.triangles[tris + 5] = vert + meshQuads + 2;
                vert++;
                tris += 6;
            }
            vert++;
        }
        */


        /*
        // vertices
        List<Vector3> verticesBordered = new List<Vector3>(vertexCountBordered);
        for (int i = 0; i < vertexCountBordered; i++)
        {
            verticesBordered.Add(Vector3.zero);
        }
        for (int i = 0, j = 0, z = -1; z <= xSegments + 1; z++)
        {
            for (int x = -1; x <= zSegments + 1; x++)
            {
                float y = GetHeight(x, z, xSegments, zSegments, noiseOffset, noiseScale);
                Vector3 vertex = new Vector3(x * xStep, y * terrainSize.y, z * zStep);
                verticesBordered[j] = vertex;
                j++;

                if ((x >= 0 && x <= xSegments) && (z >= 0 && z <= zSegments))
                {
                    draft.vertices[i] = vertex;
                    i++;
                }
            }
        }
        */
        /*
        // triangles
        List<int> trianglesBordered = new List<int>(triangleCountBordered);
        for (int i = 0; i < triangleCountBordered; i++)
        {
            trianglesBordered.Add(0);
        }

        for (int vert = 0, vertBordered = 0, tris = 0, trisBordered = 0, z = -1; z < zSegments + 1; z++)
        {
            for (int x = -1; x < xSegments + 1; x++)
            {
                trianglesBordered[tris + 0] = vert + 0;
                trianglesBordered[tris + 1] = vert + xSegments + 1;
                trianglesBordered[tris + 2] = vert + 1;
                trianglesBordered[tris + 3] = vert + 1;
                trianglesBordered[tris + 4] = vert + xSegments + 1;
                trianglesBordered[tris + 5] = vert + xSegments + 2;
                vertBordered++;
                trisBordered += 6;

                if ((x >= 0 && x < xSegments) && (z >= 0 && z < zSegments))
                {
                    draft.triangles[tris + 0] = vert + 0;
                    draft.triangles[tris + 1] = vert + xSegments + 1;
                    draft.triangles[tris + 2] = vert + 1;
                    draft.triangles[tris + 3] = vert + 1;
                    draft.triangles[tris + 4] = vert + xSegments + 1;
                    draft.triangles[tris + 5] = vert + xSegments + 2;
                    vert++;
                    tris += 6;
                }
            }
            vertBordered++;

            if (z >= 0 && z < zSegments)
                vert++;
        }
        */


        /*
        // normals first pass
        List<Vector3> normalsBordered = new List<Vector3>(vertexCountBordered);
        for (int i = 0; i < vertexCountBordered; i++)
        {
            normalsBordered.Add(Vector3.zero);
        }

        for (int vertBordered = 0, trisBordered = 0, z = -1; z < zSegments + 1; z++)
        {
            for (int x = -1; x < xSegments + 1; x++)
            {
                int vertex00 = vertBordered + 0;
                int vertex01 = vertBordered + xSegments + 1;
                int vertex10 = vertBordered + 1;
                int vertex11 = vertBordered + xSegments + 2;

                Vector3 normal000111 = Vector3.Cross(
                    verticesBordered[vertex01] - verticesBordered[vertex00],
                    verticesBordered[vertex11] - verticesBordered[vertex00]
                ).normalized;
                Vector3 normal001011 = Vector3.Cross(
                    verticesBordered[vertex11] - verticesBordered[vertex00],
                    verticesBordered[vertex10] - verticesBordered[vertex00]
                ).normalized;

                normalsBordered[vertBordered + 0] += normal000111;
                normalsBordered[vertBordered + 1] += normal000111;
                normalsBordered[vertBordered + 2] += normal000111;
                normalsBordered[vertBordered + 3] += normal001011;
                normalsBordered[vertBordered + 4] += normal001011;
                normalsBordered[vertBordered + 5] += normal001011;

                vertBordered++;
                trisBordered += 6;
            }
            vertBordered++;
        }
        
        // normals second pass
        for (int vert = 0, vertBordered = 0, tris = 0, trisBordered = 0, z = -1; z < zSegments + 1; z++)
        {
            for (int x = -1; x < xSegments + 1; x++)
            {
                normalsBordered[vertBordered + 0] = normalsBordered[vertBordered + 0].normalized;
                normalsBordered[vertBordered + 1] = normalsBordered[vertBordered + 1].normalized;
                normalsBordered[vertBordered + 2] = normalsBordered[vertBordered + 2].normalized;
                normalsBordered[vertBordered + 3] = normalsBordered[vertBordered + 3].normalized;
                normalsBordered[vertBordered + 4] = normalsBordered[vertBordered + 4].normalized;
                normalsBordered[vertBordered + 5] = normalsBordered[vertBordered + 5].normalized;

                if ((x >= 0 && x < xSegments) && (z >= 0 && z < zSegments))
                {
                    draft.normals[vert + 0] = normalsBordered[vertBordered + 0];
                    draft.normals[vert + 1] = normalsBordered[vertBordered + 1];
                    draft.normals[vert + 2] = normalsBordered[vertBordered + 2];
                    draft.normals[vert + 3] = normalsBordered[vertBordered + 3];
                    draft.normals[vert + 4] = normalsBordered[vertBordered + 4];
                    draft.normals[vert + 5] = normalsBordered[vertBordered + 5];
                    vert++;
                    tris += 6;
                }
                vertBordered++;
                trisBordered += 6;
            }
            if (z >= 0 && z < zSegments)
                vert++;

            vertBordered++;
        }
        */

        /*  
            Use a single mesh*, and add all the STANDARD vertices for the non-bordered version
            Add the "extra" vertices & triangles to the END of the standard lists
            Calculate normals (should work with the exact same method)
            Remove the "extra" vertices & triangles & normals before returning the mesh
 
            *single mesh meaning you get to discard trianglesBordered and verticesBordered and heavily simplify the code
        */




        /*

        var bNormals = GetNormals(trianglesBordered, verticesBordered);
        var dNormals = new Vector3[draft.vertices.Count];
        for (int i = 0; i < bNormals.Count; i++)
        {
            var bVert = verticesBordered[i];
            if (!draft.vertices.Contains(bVert)) { continue; }

            var dIdx = draft.vertices.IndexOf(bVert);
            dNormals[dIdx] = normalsBordered[i];
        }

        draft.normals = dNormals.ToList();
        */

        // uvs
        for (int i = 0, z = 0; z <= meshQuads; z++)
        {
            for (int x = 0; x <= meshQuads; x++)
            {
                draft.uv[i] = new Vector2((float)x / meshQuads, (float)z / meshQuads);
                i++;
            }
        }

        return draft;
    }

    List<Vector3> GetNormals(List<int> triangles, List<Vector3> vertices)
    {
        Vector3[] normals = new Vector3[vertices.Count];

        for (int i = 0; i < triangles.Count; i += 3)
        {
            var (i0, i1, i2) = (triangles[i + 0], triangles[i + 1], triangles[i + 2]);
            var (v0, v1, v2) = (vertices[i0], vertices[i1], vertices[i2]);
            var (e1, e2) = (v1 - v0, v2 - v0);

            var normal = Vector3.Cross(e1, e2).normalized;
            normals[i0] += normal;
            normals[i1] += normal;
            normals[i2] += normal;
        }

        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = normals[i].normalized;
        }

        return normals.ToList();
    }

    private MeshDraft TerrainDraft(Vector3 terrainSize, float cellSize, Vector2 noiseOffset, float noiseScale)
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
            normals = new List<Vector3>(vertexCount)
        };

        for (int i = 0; i < vertexCount; i++)
        {
            draft.vertices.Add(Vector3.zero);
            draft.triangles.Add(0);
            draft.uv.Add(Vector2.zero);
            draft.normals.Add(Vector3.zero);
        }

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
    private List<Vector3> CalculateNormals(MeshDraft draft)
    {
        List<Vector3> vertexNormals = new List<Vector3>(draft.normals.Count);
        int triangles = draft.triangles.Count / 3;
        for (int i = 0; i < triangles; i++)
        { 
            int normalTriangleIndex = i * 3;
            int vertexIndexA = draft.triangles[normalTriangleIndex];
            int vertexIndexB = draft.triangles[normalTriangleIndex + 1];
            int vertexIndexC = draft.triangles[normalTriangleIndex + 2];

            Vector3 triangleNormal = SurfaceNormalFromIndices(draft, vertexIndexA, vertexIndexB, vertexIndexC);
            vertexNormals[vertexIndexA] += triangleNormal;
            vertexNormals[vertexIndexB] += triangleNormal;
            vertexNormals[vertexIndexC] += triangleNormal;
        }

        for (int i = 0; i < vertexNormals.Count; i++)
        {
            vertexNormals[i].Normalize();
        }

        return vertexNormals;
    }

    Vector3 SurfaceNormalFromIndices(MeshDraft draft, int indexA, int indexB, int indexC) 
    {
        Vector3 pointA = draft.vertices[indexA];
        Vector3 pointB = draft.vertices[indexB];
        Vector3 pointC = draft.vertices[indexC];

        Vector3 sideAB = pointB - pointA;
        Vector3 sideAC = pointC - pointA;
        return Vector3.Cross(sideAB, sideAC).normalized;
    }


    private float GetHeight(int x, int z, int xSegments, int zSegments, Vector2 noiseOffset, float noiseScale)
    {
        float noiseX = noiseScale * x / xSegments + noiseOffset.x;
        float noiseZ = noiseScale * z / zSegments + noiseOffset.y;
        if (usePerlinNoise)
            return Mathf.PerlinNoise(noiseX, noiseZ);
        else
            return TerrainController.noisePixels[(int)noiseX % TerrainController.noisePixels.Length][(int)noiseZ % TerrainController.noisePixels[0].Length];
    }

}