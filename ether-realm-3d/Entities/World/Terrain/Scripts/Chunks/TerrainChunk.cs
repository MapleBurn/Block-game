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

    // Triangle list kept so collision can be built whenever the player comes close.
    private Vector3[] _collisionFaces;
    private StaticBody3D _collisionBody;

    /// <summary>
    /// Main thread only. Uploads mesh data produced on a background thread by TerrainMesher.
    /// Any existing collision is rebuilt from the new mesh.
    /// </summary>
    public void ApplyMesh(ChunkMeshData meshData, Material material)
    {
        ArrayMesh mesh = TerrainMesher.BuildMesh(meshData);
        if (mesh != null)
            mesh.SurfaceSetMaterial(0, material);
        Mesh = mesh;

        // Mesh data is a plain triangle list, so the vertices are already the face list.
        _collisionFaces = meshData.Vertices;
        if (_collisionBody != null)
        {
            SetCollisionEnabled(false);
            SetCollisionEnabled(true);
        }
    }

    /// <summary>
    /// Main thread only. Collision is costly to build, so only chunks near the player have it.
    /// </summary>
    public void SetCollisionEnabled(bool enabled)
    {
        if (!enabled)
        {
            _collisionBody?.QueueFree();
            _collisionBody = null;
            return;
        }

        if (_collisionBody != null || _collisionFaces == null || _collisionFaces.Length == 0) return;

        var poly = new ConcavePolygonShape3D();
        poly.SetFaces(_collisionFaces);

        _collisionBody = new StaticBody3D();
        _collisionBody.AddChild(new CollisionShape3D { Shape = poly });
        AddChild(_collisionBody);
    }
}
