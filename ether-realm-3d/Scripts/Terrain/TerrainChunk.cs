using EtherRealm3D.Scripts.Terrain;
using Godot;

namespace EtherRealm3D.Scripts.Terrain;

public partial class TerrainChunk : MeshInstance3D
{
    public int CellsPerAxis { get; private set; }
    public float VoxelSize { get; private set; }
    public float IsoLevel { get; private set; }
    public Vector3I ChunkCoordinate { get; private set; }
    
    // Procedural-coordinate origin of this chunk, measured in world units.
    public Vector3 WorldOrigin { get; private set; }

    private float[,,] _densities;
    private FastNoiseLite _noise;
    private StaticBody3D _staticBody;
    private CollisionShape3D _collisionShape;

    /// <summary>
    /// Must be called by TerrainWorld before Generate().
    /// </summary>
    public void Configure( Vector3I chunkCoordinate, int cellsPerAxis, float voxelSize, float isoLevel, FastNoiseLite noise)
    {
        _staticBody = new StaticBody3D();  
        _collisionShape = new CollisionShape3D();  
        _staticBody.AddChild(_collisionShape);  
        AddChild(_staticBody);
        
        ChunkCoordinate = chunkCoordinate;
        CellsPerAxis = cellsPerAxis;
        VoxelSize = voxelSize;
        IsoLevel = isoLevel;
        _noise = noise;

        float chunkWorldSize = CellsPerAxis * VoxelSize;

        WorldOrigin = new Vector3(
            ChunkCoordinate.X * chunkWorldSize,
            ChunkCoordinate.Y * chunkWorldSize,
            ChunkCoordinate.Z * chunkWorldSize
        );

        // This controls where the chunk mesh appears in the Godot scene.
        Position = WorldOrigin;

        Name = $"Chunk_{ChunkCoordinate.X}_{ChunkCoordinate.Y}_{ChunkCoordinate.Z}";
    }

    /// <summary>
    /// Samples the density grid and creates this chunk's Marching Cubes mesh.
    /// </summary>
    public void Generate()
    {
        if (_noise == null)
        {
            GD.PrintErr($"[{Name}] Cannot generate: noise was not configured.");
            return;
        }

        GenerateDensityField();

        Mesh = TerrainMesher.GenerateMesh(
            _densities,
            IsoLevel,
            VoxelSize
        );
        
        if (Mesh != null)  
        {  
            ApplyMaterial();  
            BuildCollision();  
        }
    }

    private void GenerateDensityField()
    {
        // A chunk with N cells needs N + 1 samples on each axis.
        int samplesPerAxis = CellsPerAxis + 1;

        _densities = new float[ samplesPerAxis, samplesPerAxis, samplesPerAxis ];

        for (int x = 0; x < samplesPerAxis; x++)
        {
            for (int y = 0; y < samplesPerAxis; y++)
            {
                for (int z = 0; z < samplesPerAxis; z++)
                {
                    Vector3 localSamplePosition = new Vector3( x * VoxelSize, y * VoxelSize, z * VoxelSize );

                    // Critical for seamless chunks:
                    // all chunks sample exactly the same shared world field.
                    Vector3 worldSamplePosition = WorldOrigin + localSamplePosition;

                    _densities[x, y, z] = SampleDensity(worldSamplePosition);
                }
            }
        }
    }

    private float SampleDensity(Vector3 worldPosition)
    {
        // Use only X and Z to form a height map.
        float noiseValue = _noise.GetNoise2D(
            worldPosition.X,
            worldPosition.Z
        );

        // Increase these once the multi-chunk system works.
        // The WorldSizeInChunks.Y setting determines the maximum
        // vertical range that can visibly contain these mountains.
        const float baseHeight = 18.0f;
        const float heightAmplitude = 26.0f;

        float terrainHeight =
            baseHeight + noiseValue * heightAmplitude;

        // Negative = solid terrain
        // Positive = air
        // Zero = Marching Cubes surface
        return worldPosition.Y - terrainHeight;
    }
    
    private void ApplyMaterial()  
    {  
        var material = new StandardMaterial3D  
        {  
            AlbedoColor = new Color(0.45f, 0.62f, 0.31f), // grass green  
            ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex // fewer calculations, blocky look
        };  
  
        // Apply to the first surface of the mesh.  
        (Mesh as ArrayMesh)?.SurfaceSetMaterial(0, material);  
    }  
  
    private void BuildCollision()  
    {  
        /*// Remove any previous collision shape if regenerating.  
        foreach (var child in GetChildren())  
        {  
            if (child is StaticBody3D)  
                child.QueueFree();  
        }  
  
        var staticBody = new StaticBody3D();  
        AddChild(staticBody);  
  
        var collisionShape = new CollisionShape3D();  
        staticBody.AddChild(collisionShape); */ 
  
        var shape = new ConcavePolygonShape3D();  
        shape.SetFaces(Mesh.GetFaces());  
        _collisionShape.Shape = shape;  
    }
}