using System;
using System.Collections.Generic;
using EtherRealm3D.Entities.World.Terrain.Scripts.Chunks;
using EtherRealm3D.Entities.World.Terrain.Scripts.Generation;
using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts.Meshing;

public static class TerrainMesher
{
    // The terrain surface sits where density crosses this value.
    public const float IsoLevel = 0.0f;

    private static readonly Vector3I[] CornerOffsets =
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
    public static ChunkMeshData GenerateMeshData(ChunkData data, ColumnData columns, SurfaceColoring surface, float voxelSize)
    {
        if (data.Content != ChunkContent.Mixed) return ChunkMeshData.Empty;

        var mesh = new MeshBuffers(columns, surface, voxelSize);

        // Offset of each cube corner in the flat density array, relative to the cube's first corner.
        Span<int> cornerIndexOffsets = stackalloc int[8];
        for (int i = 0; i < 8; i++)
            cornerIndexOffsets[i] = data.Index(CornerOffsets[i].X, CornerOffsets[i].Y, CornerOffsets[i].Z);

        Span<float> cornerDensities = stackalloc float[8];
        Span<Vector3> edgeVertices = stackalloc Vector3[12];

        int cells = data.Size - 1;
        for (int x = 0; x < cells; x++)
        for (int y = 0; y < cells; y++)
        for (int z = 0; z < cells; z++)
            MarchCube(x, y, z, data, voxelSize, cornerIndexOffsets, cornerDensities, edgeVertices, mesh);

        return new ChunkMeshData
        {
            Vertices = mesh.Vertices.ToArray(),
            Normals  = mesh.Normals.ToArray(),
            Colors   = mesh.Colors.ToArray()
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
        arrays[(int)Mesh.ArrayType.Color]  = data.Colors;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private static void MarchCube(
        int cx, int cy, int cz,
        ChunkData data, float voxelSize,
        ReadOnlySpan<int> cornerIndexOffsets, Span<float> d, Span<Vector3> edgeVertices,
        MeshBuffers mesh)
    {
        // 1. Sample the 8 corner densities.
        float[] densities = data.Densities;
        int baseIndex = data.Index(cx, cy, cz);
        for (int i = 0; i < 8; i++)
            d[i] = densities[baseIndex + cornerIndexOffsets[i]];

        // 2. Build the case index.
        int cubeIndex = 0;
        for (int i = 0; i < 8; i++)
            if (d[i] < IsoLevel) cubeIndex |= 1 << i;

        int edgeMask = MarchingCubesTables.EdgeTable[cubeIndex];
        if (edgeMask == 0) return;

        // 3. Interpolate a vertex on each active edge using CornerPairs.
        Vector3 cellOrigin = new Vector3(cx, cy, cz) * voxelSize;

        for (int e = 0; e < 12; e++)
        {
            if ((edgeMask & (1 << e)) == 0) continue;

            int a = MarchingCubesTables.CornerPairs[e, 0];
            int b = MarchingCubesTables.CornerPairs[e, 1];

            Vector3 pA = cellOrigin + (Vector3)CornerOffsets[a] * voxelSize;
            Vector3 pB = cellOrigin + (Vector3)CornerOffsets[b] * voxelSize;

            edgeVertices[e] = Interpolate(pA, pB, d[a], d[b]);
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
            mesh.AddTriangle(v0, v1, v2, normal);
        }
    }

    /// <summary>Growing vertex buffers plus the coloring rules for one chunk.</summary>
    private sealed class MeshBuffers(ColumnData columns, SurfaceColoring surface, float voxelSize)
    {
        public readonly List<Vector3> Vertices = new();
        public readonly List<Vector3> Normals  = new();
        public readonly List<Color>   Colors   = new();

        public void AddTriangle(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 normal)
        {
            Color color = TriangleColor((v0 + v1 + v2) / 3f, normal);

            Vertices.Add(v0); Normals.Add(normal); Colors.Add(color);
            Vertices.Add(v1); Normals.Add(normal); Colors.Add(color);
            Vertices.Add(v2); Normals.Add(normal); Colors.Add(color);
        }

        /// <summary>Biome color of the nearest column, turning to rock on steep slopes.</summary>
        private Color TriangleColor(Vector3 center, Vector3 normal)
        {
            int last = columns.Size - 1;
            int x = Mathf.Clamp(Mathf.RoundToInt(center.X / voxelSize), 0, last);
            int z = Mathf.Clamp(Mathf.RoundToInt(center.Z / voxelSize), 0, last);
            Color biomeColor = columns.SurfaceColors[columns.Index(x, z)];

            float rock = Mathf.Clamp(
                (surface.RockSlopeStart - normal.Y) / (surface.RockSlopeStart - surface.RockSlopeFull), 0f, 1f);
            return biomeColor.Lerp(surface.RockColor, rock);
        }
    }

    private static Vector3 Interpolate(Vector3 pA, Vector3 pB, float dA, float dB)
    {
        float denom = dB - dA;
        if (Mathf.Abs(denom) < 1e-5f) return (pA + pB) * 0.5f;
        float t = Mathf.Clamp((IsoLevel - dA) / denom, 0f, 1f);
        return pA.Lerp(pB, t);
    }
}
