using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts.Meshing;

/// <summary>
/// Plain triangle-list data produced on a background thread.
/// Applied to the scene on the main thread.
/// </summary>
public class ChunkMeshData
{
    public static readonly ChunkMeshData Empty = new() { Vertices = [], Normals = [] };

    public Vector3[] Vertices;
    public Vector3[] Normals;

    public bool IsEmpty => Vertices == null || Vertices.Length == 0;
}
