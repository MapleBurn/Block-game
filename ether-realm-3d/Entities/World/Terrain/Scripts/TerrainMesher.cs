using System.Collections.Generic;
using EtherRealm3D.Entities.World.Terrain.Scripts.Data;
using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts;

public static class TerrainMesher
{
private static readonly Vector3[] CornerOffsets = new Vector3[]  
    {  
        new(0, 0, 0), // 0  
        new(1, 0, 0), // 1  
        new(1, 0, 1), // 2  
        new(0, 0, 1), // 3  
        new(0, 1, 0), // 4  
        new(1, 1, 0), // 5  
        new(1, 1, 1), // 6  
        new(0, 1, 1)  // 7  
    };  
  
    /// <summary>  
    /// Thread-safe. Returns raw mesh data — no Godot objects created here.  
    /// </summary>  
    public static ChunkMeshData GenerateMeshData(  
        float[,,] densities,  
        float isoLevel,  
        float voxelSize)  
    {  
        var vertices = new List<Vector3>();  
        var normals  = new List<Vector3>();  
        var indices  = new List<int>();  
  
        int sizeX = densities.GetLength(0) - 1;  
        int sizeY = densities.GetLength(1) - 1;  
        int sizeZ = densities.GetLength(2) - 1;  
  
        for (int x = 0; x < sizeX; x++)  
        for (int y = 0; y < sizeY; y++)  
        for (int z = 0; z < sizeZ; z++)  
            MarchCube(x, y, z, densities, isoLevel, voxelSize, vertices, normals, indices);  
  
        return new ChunkMeshData  
        {  
            Vertices = vertices.ToArray(),  
            Normals  = normals.ToArray(),  
            Indices  = indices.ToArray()  
        };  
    }  
  
    /// <summary>  
    /// Main thread only. Converts ChunkMeshData into a Godot ArrayMesh.  
    /// </summary>  
    public static ArrayMesh BuildMesh(ChunkMeshData data)  
    {  
        if (data.IsEmpty) return null;  
  
        var arrays = new Godot.Collections.Array();  
        arrays.Resize((int)Mesh.ArrayType.Max);  
        arrays[(int)Mesh.ArrayType.Vertex] = data.Vertices;  
        arrays[(int)Mesh.ArrayType.Normal] = data.Normals;  
        arrays[(int)Mesh.ArrayType.Index]  = data.Indices;  
  
        var mesh = new ArrayMesh();  
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);  
        return mesh;  
    }  
  
    private static void MarchCube(  
        int cx, int cy, int cz,  
        float[,,] densities,  
        float isoLevel, float voxelSize,  
        List<Vector3> vertices, List<Vector3> normals, List<int> indices)  
    {  
        // 1. Sample the 8 corner densities.  
        float[] d = new float[8];  
        for (int i = 0; i < 8; i++)  
        {  
            var o = CornerOffsets[i];  
            d[i] = densities[cx + (int)o.X, cy + (int)o.Y, cz + (int)o.Z];  
        }  
  
        // 2. Build the case index.  
        int cubeIndex = 0;  
        for (int i = 0; i < 8; i++)  
            if (d[i] < isoLevel) cubeIndex |= 1 << i;  
  
        int edgeMask = MarchingCubesTables.EdgeTable[cubeIndex];  
        if (edgeMask == 0) return;  
  
        // 3. Interpolate a vertex on each active edge using CornerPairs.  
        Vector3 cellOrigin = new Vector3(cx, cy, cz) * voxelSize;  
        Vector3[] edgeVertices = new Vector3[12];  
  
        for (int e = 0; e < 12; e++)  
        {  
            if ((edgeMask & (1 << e)) == 0) continue;  
  
            int a = MarchingCubesTables.CornerPairs[e, 0];  
            int b = MarchingCubesTables.CornerPairs[e, 1];  
  
            Vector3 pA = cellOrigin + CornerOffsets[a] * voxelSize;  
            Vector3 pB = cellOrigin + CornerOffsets[b] * voxelSize;  
  
            edgeVertices[e] = Interpolate(pA, pB, d[a], d[b], isoLevel);  
        }  
  
        // 4. Emit triangles using TriangleTable.  
        for (int t = 0; MarchingCubesTables.TriangleTable[cubeIndex, t] != -1; t += 3)  
        {  
            int e0 = MarchingCubesTables.TriangleTable[cubeIndex, t    ];  
            int e1 = MarchingCubesTables.TriangleTable[cubeIndex, t + 1];  
            int e2 = MarchingCubesTables.TriangleTable[cubeIndex, t + 2];  
  
            Vector3 v0 = edgeVertices[e0];  
            Vector3 v1 = edgeVertices[e2]; // swapped for correct winding  
            Vector3 v2 = edgeVertices[e1];  
  
            Vector3 normal = (v2 - v0).Cross(v1 - v0).Normalized();  
  
            int baseIndex = vertices.Count;  
            vertices.Add(v0); normals.Add(normal);  
            vertices.Add(v1); normals.Add(normal);  
            vertices.Add(v2); normals.Add(normal);  
            indices.Add(baseIndex);  
            indices.Add(baseIndex + 1);  
            indices.Add(baseIndex + 2);  
        }  
    }  
  
    private static Vector3 Interpolate(Vector3 pA, Vector3 pB, float dA, float dB, float isoLevel)  
    {  
        float denom = dB - dA;  
        if (Mathf.Abs(denom) < 1e-5f) return (pA + pB) * 0.5f;  
        float t = Mathf.Clamp((isoLevel - dA) / denom, 0f, 1f);  
        return pA.Lerp(pB, t);  
    }
}