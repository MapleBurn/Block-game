using System.Threading.Tasks;
using Godot;

namespace EtherRealm3D.Scripts.Terrain;

public partial class TerrainChunk : MeshInstance3D
{
public Vector3I ChunkCoordinate { get; private set; }  
  
    private int _cellsPerAxis;  
    private float _voxelSize;  
    private float _isoLevel;  
    private FastNoiseLite _noise;  
    private Vector3 _worldOrigin;
    private float _amplitude;
  
    // Set to true by the background task when data is ready.  
    public bool IsReadyToApply { get; private set; } = false;  
    private ChunkMeshData _pendingData;  
  
    public void Configure(Vector3I coord, int cells, float size, float iso, FastNoiseLite noise, float amplitude)  
    {  
        ChunkCoordinate = coord;  
        _cellsPerAxis = cells;  
        _voxelSize = size;  
        _isoLevel = iso;  
        _amplitude = amplitude;
        _noise = noise;  
  
        float chunkWorldSize = _cellsPerAxis * _voxelSize;  
        Position = new Vector3(coord.X * chunkWorldSize, coord.Y * chunkWorldSize, coord.Z * chunkWorldSize);  
        Name = $"Chunk_{coord.X}_{coord.Y}_{coord.Z}";  
        
        _worldOrigin = new Vector3(  
            coord.X * chunkWorldSize,  
            coord.Y * chunkWorldSize,  
            coord.Z * chunkWorldSize  
        );
    }  
  
    /// <summary>  
    /// Kicks off background generation. Call ApplyPendingMesh() on the  
    /// main thread once IsReadyToApply is true.  
    /// </summary>  
    public Task GenerateAsync()  
    {  
        return Task.Run(() =>  
        {  
            float[,,] densities = SampleDensityField();  
            _pendingData = TerrainMesher.GenerateMeshData(  
                densities,  
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
  
        if (_pendingData == null || _pendingData.IsEmpty)  
        {  
            _pendingData = null;  
            return;  
        }  
  
        ArrayMesh mesh = TerrainMesher.BuildMesh(_pendingData);  
        _pendingData = null;  
  
        if (mesh == null) return;  
  
        Mesh = mesh;  
        ApplyMaterial();  
        BuildCollision();  
    }  
  
    private float[,,] SampleDensityField()  
    {  
        int samples = _cellsPerAxis + 1;  
        float[,,] densities = new float[samples, samples, samples];  
        
        Vector3 origin = _worldOrigin;  
  
        for (int x = 0; x < samples; x++)  
        for (int y = 0; y < samples; y++)  
        for (int z = 0; z < samples; z++)  
        {  
            Vector3 worldPos = origin + new Vector3(  
                x * _voxelSize,  
                y * _voxelSize,  
                z * _voxelSize  
            );  
            densities[x, y, z] = SampleDensity(worldPos);  
        }  
  
        return densities;  
    }  
  
    private float SampleDensity(Vector3 worldPos)  
    {  
        float noiseValue = _noise.GetNoise2D(worldPos.X, worldPos.Z);
        var terrainBaseHeight = 10.0f;
        float height = terrainBaseHeight + noiseValue * _amplitude;
        return worldPos.Y - height;  
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
  
        var body  = new StaticBody3D();  
        var shape = new CollisionShape3D();  
        var poly  = new ConcavePolygonShape3D();  
  
        poly.SetFaces(Mesh.GetFaces());  
        shape.Shape = poly;  
        body.AddChild(shape);  
        AddChild(body);  
    }
}