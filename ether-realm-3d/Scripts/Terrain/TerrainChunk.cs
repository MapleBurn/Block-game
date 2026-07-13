using Godot;

namespace EtherRealm3D.Scripts.Terrain;

[Tool]
public partial class TerrainChunk : Node3D
{
    // ── Configuration ────────────────────────────────────────────────────────
    public const int   CellsPerAxis = 32;       // Cube cells per axis.
    public const int   Samples      = CellsPerAxis + 1; // Density sample points per axis.
    public const float VoxelSize    = 1.5f;     // World units per cell.
    public const float IsoLevel     = 0.0f;

    // ── Noise ────────────────────────────────────────────────────────────────
    [Export] public FastNoiseLite HeightNoise = new()
    {
        NoiseType      = FastNoiseLite.NoiseTypeEnum.Perlin,
        Frequency      = 0.015f,
        FractalOctaves = 5,
        FractalGain    = 0.5f,
        FractalLacunarity = 2.0f,
    };

    [Export] public float TerrainAmplitude = 12.0f;
    [Export] public float TerrainBaseHeight = 8.0f;

    // ── Internal state ───────────────────────────────────────────────────────
    private float[,,]        _densities;
    private MeshInstance3D   _meshInstance;
    private StaticBody3D     _staticBody;
    private CollisionShape3D _collisionShape;

    // ── Godot lifecycle ──────────────────────────────────────────────────────
    public override void _Ready()
    {
        _meshInstance = new MeshInstance3D();
        AddChild(_meshInstance);

        _staticBody     = new StaticBody3D();
        _collisionShape = new CollisionShape3D();
        _staticBody.AddChild(_collisionShape);
        AddChild(_staticBody);

        Generate();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Samples the density field and rebuilds the mesh.
    /// Call this once on startup, or again after editing densities.
    /// </summary>
    public void Generate()
    {
        SampleDensities();
        RebuildMesh();
    }

    // ── Density sampling ─────────────────────────────────────────────────────

    private void SampleDensities()
    {
        _densities = new float[Samples, Samples, Samples];
        Vector3 chunkOrigin = GlobalPosition;

        for (int x = 0; x < Samples; x++)
        for (int y = 0; y < Samples; y++)
        for (int z = 0; z < Samples; z++)
        {
            Vector3 worldPos = chunkOrigin + new Vector3(x, y, z) * VoxelSize;
            _densities[x, y, z] = SampleDensity(worldPos);
        }
    }

    private float SampleDensity(Vector3 worldPos)
    {
        float height = HeightNoise.GetNoise2D(worldPos.X, worldPos.Z)
                       * TerrainAmplitude
                       + TerrainBaseHeight;

        // Positive above surface (air), negative below (solid).
        return worldPos.Y - height;
    }

    // ── Mesh building ─────────────────────────────────────────────────────────

    private void RebuildMesh()
    {
        ArrayMesh mesh = TerrainMesher.GenerateMesh(_densities, IsoLevel, VoxelSize);

        if (mesh == null)
        {
            GD.PrintErr("[TerrainChunk] Mesher returned null — chunk may be entirely solid or empty.");
            return;
        }

        _meshInstance.Mesh = mesh;

        // Assign a default material so the mesh is visible immediately.
        if (_meshInstance.GetSurfaceOverrideMaterial(0) == null)
        {
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.35f, 0.55f, 0.25f),
            };
            _meshInstance.SetSurfaceOverrideMaterial(0, mat);
        }

        // Collision — only build if the mesh has geometry.
        _collisionShape.Shape = mesh.CreateTrimeshShape();
    }
}