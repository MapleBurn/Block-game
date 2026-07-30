using EtherRealm3D.Entities.World.Terrain.Scripts.Data;
using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts;

public partial class TerrainChunk : MeshInstance3D
{
    public Vector3I ChunkCoordinate { get; private set; }
    private int _cellsPerAxis;
    private float _voxelSize;

    // Indicates if the chunk needs to be saved to disk (has changes)
    public bool IsDirty { get; private set; } = true;
    public void MarkDirty()  => IsDirty = true;
    public void ClearDirty() => IsDirty = false;

    private ChunkMeshData _meshData;
    public ChunkData Data;

    public void Configure(ChunkData data, bool isDirty, int cellsPerAxis, float voxelSize)
    {
        Data = data;
        IsDirty = isDirty;
        ChunkCoordinate = data.ChunkCoordinate;
        _cellsPerAxis = cellsPerAxis;
        _voxelSize = voxelSize;

        float chunkWorldSize = _cellsPerAxis * _voxelSize;
        Position = new Vector3(ChunkCoordinate.X * chunkWorldSize, ChunkCoordinate.Y * chunkWorldSize, ChunkCoordinate.Z * chunkWorldSize);
        Name = $"Chunk_{ChunkCoordinate.X}_{ChunkCoordinate.Y}_{ChunkCoordinate.Z}";
    }

    /// <summary>
    /// Hands over mesh data produced on a background thread by TerrainMesher.
    /// ChunkManager owns that generation; this node only stores the result until
    /// ApplyPendingMesh() uploads it.
    /// </summary>
    public void SetMeshData(ChunkMeshData meshData)
    {
        _meshData = meshData;
    }

    /// <summary>
    /// Main thread only. Builds the Godot mesh and collision from pending data.
    /// </summary>
    public void ApplyPendingMesh()
    {
        if (_meshData == null || _meshData.IsEmpty)
        {
            _meshData = null;
            return;
        }

        ArrayMesh mesh = TerrainMesher.BuildMesh(_meshData);
        _meshData = null;

        if (mesh == null) return;

        Mesh = mesh;
        ApplyMaterial();
        BuildCollision();
    }

    private void ApplyMaterial()
    {
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.5f, 0.2f)
        };
        (Mesh as ArrayMesh)?.SurfaceSetMaterial(0, mat);
    }

    private void BuildCollision()
    {
        foreach (var child in GetChildren())
            if (child is StaticBody3D) child.QueueFree();

        var body = new StaticBody3D();
        var shape = new CollisionShape3D();
        var poly = new ConcavePolygonShape3D();

        poly.SetFaces(Mesh.GetFaces());
        shape.Shape = poly;
        body.AddChild(shape);
        AddChild(body);
    }
}
