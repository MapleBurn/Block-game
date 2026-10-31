using EtherRealm3D.Entities.World.Terrain.Scripts.Meshing;
using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts.Chunks;

public partial class TerrainChunk : MeshInstance3D
{
    public Vector3I ChunkCoordinate { get; private set; }
    public ChunkData Data { get; private set; }

    // Indicates if the chunk needs to be saved to disk (has changes)
    public bool IsDirty { get; private set; }
    public void MarkDirty()  => IsDirty = true;
    public void ClearDirty() => IsDirty = false;

    public void Configure(ChunkData data, bool isDirty, float chunkWorldSize)
    {
        Data = data;
        IsDirty = isDirty;
        ChunkCoordinate = data.ChunkCoordinate;

        Position = new Vector3(ChunkCoordinate.X, ChunkCoordinate.Y, ChunkCoordinate.Z) * chunkWorldSize;
        Name = $"Chunk_{ChunkCoordinate.X}_{ChunkCoordinate.Y}_{ChunkCoordinate.Z}";
    }

    /// <summary>
    /// Main thread only. Uploads mesh data produced on a background thread by TerrainMesher
    /// and rebuilds collision from it.
    /// </summary>
    public void ApplyMesh(ChunkMeshData meshData, Material material)
    {
        ArrayMesh mesh = TerrainMesher.BuildMesh(meshData);
        if (mesh == null) return;

        mesh.SurfaceSetMaterial(0, material);
        Mesh = mesh;
        BuildCollision(meshData);
    }

    private void BuildCollision(ChunkMeshData meshData)
    {
        foreach (var child in GetChildren())
            if (child is StaticBody3D) child.QueueFree();

        // Mesh data is a plain triangle list, so the vertices are already the face list.
        var poly = new ConcavePolygonShape3D();
        poly.SetFaces(meshData.Vertices);

        var shape = new CollisionShape3D { Shape = poly };
        var body = new StaticBody3D();
        body.AddChild(shape);
        AddChild(body);
    }
}
