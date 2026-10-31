using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts.Generation;

/// <summary>
/// Base terrain height split into parts, so biomes can adjust parts separately.
/// </summary>
public readonly record struct ShapeSample(float Base, float Hills)
{
    public float Height => Base + Hills;
}

/// <summary>
/// Turns climate fields into terrain height: continents and coasts from continentalness,
/// mountain ranges where ruggedness is high, and rolling hills everywhere.
/// </summary>
public sealed class TerrainShaper
{
    private readonly BakedCurve _continentalnessHeight;
    private readonly BakedCurve _ruggednessMountains;
    private readonly FastNoiseLite _mountains;
    private readonly FastNoiseLite _hills;

    private readonly float _minBaseHeight;
    private readonly float _maxBaseHeight;
    private readonly float _mountainHeight;
    private readonly float _inlandStart;
    private readonly float _inlandFull;
    private readonly float _hillHeight;

    public TerrainShaper(WorldGenSettings settings)
    {
        _continentalnessHeight = new BakedCurve(settings.ContinentalnessHeight);
        _ruggednessMountains   = new BakedCurve(settings.RuggednessMountains);

        _mountains = new FastNoiseLite
        {
            Seed = settings.Seed + 10,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Frequency = settings.MountainFrequency,
            FractalType = FastNoiseLite.FractalTypeEnum.Ridged,
            FractalOctaves = 5
        };
        _hills = new FastNoiseLite
        {
            Seed = settings.Seed + 11,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Frequency = settings.HillFrequency,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 4
        };

        _minBaseHeight  = settings.MinBaseHeight;
        _maxBaseHeight  = settings.MaxBaseHeight;
        _mountainHeight = settings.MountainHeight;
        _inlandStart    = settings.MountainInlandStart;
        _inlandFull     = settings.MountainInlandFull;
        _hillHeight     = settings.HillHeight;
    }

    public ShapeSample Sample(float x, float z, in Climate climate)
    {
        float continent = Mathf.Lerp(_minBaseHeight, _maxBaseHeight,
            _continentalnessHeight.SampleField(climate.Continentalness));

        float mountainAmount = _ruggednessMountains.SampleField(climate.Ruggedness)
                             * Mathf.SmoothStep(_inlandStart, _inlandFull, climate.Continentalness);
        float mountains = 0f;
        if (mountainAmount > 0.001f)
        {
            // Ridged noise is -1..1 with sharp crests at 1; shift to 0..1 so mountains only add height.
            float ridges = _mountains.GetNoise2D(x, z) * 0.5f + 0.5f;
            mountains = ridges * mountainAmount * _mountainHeight;
        }

        float hills = _hills.GetNoise2D(x, z) * _hillHeight;

        return new ShapeSample(continent + mountains, hills);
    }
}
