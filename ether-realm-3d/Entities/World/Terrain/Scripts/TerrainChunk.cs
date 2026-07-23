using System.Threading.Tasks;
using EtherRealm3D.Entities.World.Terrain.Scripts.Data;
using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts;

public partial class TerrainChunk : MeshInstance3D
{
    public Vector3I ChunkCoordinate { get; private set; }
    private int _cellsPerAxis;
    private float _voxelSize;
    private float _isoLevel;

    // Set to true by the background task when data is ready.
    public bool IsReadyToApply { get; private set; } = false;
    private ChunkMeshData _meshData;
    private ChunkData _chunkData;

    public void Configure(ChunkData data)
    {
        _chunkData = data;
        ChunkCoordinate = data.ChunkCoordinate;
        _cellsPerAxis = data.CellsPerAxis;
        _voxelSize = data.VoxelSize;
        _isoLevel = data.IsoLevel;

        float chunkWorldSize = _cellsPerAxis * _voxelSize;
        Position = new Vector3(ChunkCoordinate.X * chunkWorldSize, ChunkCoordinate.Y * chunkWorldSize, ChunkCoordinate.Z * chunkWorldSize);
        Name = $"Chunk_{ChunkCoordinate.X}_{ChunkCoordinate.Y}_{ChunkCoordinate.Z}";
    }

    /// <summary>
    /// Kicks off background mesh generation using the pre-sampled ChunkData.
    /// Call ApplyPendingMesh() on the main thread once IsReadyToApply is true.
    /// </summary>
    public Task GenerateAsync()
    {
        return Task.Run(() =>
        {
            _meshData = TerrainMesher.GenerateMeshData(
                _chunkData.Densities,
                _isoLevel,
                _voxelSize
            );
            IsReadyToApply = true;
        });
    }

    /// <summary>
    /// Main thread only. Builds the Godot mesh and collision from pending data.
    /// </summary>
    public void ApplyPendingMesh()
    {
        IsReadyToApply = false;

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
