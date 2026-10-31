using EtherRealm3D.Entities.World.Terrain.Scripts.Chunks;
using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts.Generation;

/// <summary>
/// Deterministic density source. Holds no scene state, so it is safe to call from background threads.
/// </summary>
public sealed class TerrainGenerator
{
    private readonly FastNoiseLite _heightNoise;
    private readonly float _baseHeight;
    private readonly float _amplitude;
    private readonly int _cellsPerAxis;
    private readonly float _voxelSize;

    public TerrainGenerator(int seed, float frequency, float baseHeight, float amplitude, int cellsPerAxis, float voxelSize)
    {
        _heightNoise = new FastNoiseLite
        {
            Seed = seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            Frequency = frequency
        };
        _baseHeight = baseHeight;
        _amplitude = amplitude;
        _cellsPerAxis = cellsPerAxis;
        _voxelSize = voxelSize;
    }

    public float SampleHeight(float x, float z) => _baseHeight + _heightNoise.GetNoise2D(x, z) * _amplitude;

    public ChunkData GenerateChunk(Vector3I coord)
    {
        int samples = _cellsPerAxis + 1;
        float chunkWorldSize = _cellsPerAxis * _voxelSize;
        var origin = new Vector3(coord.X, coord.Y, coord.Z) * chunkWorldSize;

        // Height is 2D, so sample it once per column instead of once per voxel.
        var heights = new float[samples * samples];
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        for (int x = 0; x < samples; x++)
        for (int z = 0; z < samples; z++)
        {
            float h = SampleHeight(origin.X + x * _voxelSize, origin.Z + z * _voxelSize);
            heights[x * samples + z] = h;
            if (h < minHeight) minHeight = h;
            if (h > maxHeight) maxHeight = h;
        }

        // Skip chunks the surface never crosses; the mesher would emit nothing for them anyway.
        float bottom = origin.Y;
        float top = origin.Y + chunkWorldSize;
        if (bottom >= maxHeight) return ChunkData.Uniform(coord, samples, ChunkContent.Air);
        if (top < minHeight) return ChunkData.Uniform(coord, samples, ChunkContent.Solid);

        var densities = new float[samples * samples * samples];
        int i = 0;
        for (int x = 0; x < samples; x++)
        for (int y = 0; y < samples; y++)
        {
            float worldY = origin.Y + y * _voxelSize;
            int column = x * samples;
            for (int z = 0; z < samples; z++)
                densities[i++] = worldY - heights[column + z];
        }

        return new ChunkData(coord, samples, densities);
    }
}
