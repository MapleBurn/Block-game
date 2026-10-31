using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts.Generation;

/// <summary>
/// Lookup-table copy of a Godot Curve, so it can be sampled from background threads
/// without touching the Godot object.
/// </summary>
public sealed class BakedCurve
{
    private readonly float[] _values;

    /// <param name="curve">Curve to bake; null bakes a straight 0 -> 1 line.</param>
    public BakedCurve(Curve curve, int resolution = 256)
    {
        _values = new float[resolution];
        for (int i = 0; i < resolution; i++)
        {
            float t = i / (float)(resolution - 1);
            _values[i] = curve == null ? t : curve.Sample(Mathf.Lerp(curve.MinDomain, curve.MaxDomain, t));
        }
    }

    /// <summary>Samples with t in 0..1 (clamped), linearly interpolating between baked points.</summary>
    public float Sample(float t)
    {
        float position = Mathf.Clamp(t, 0f, 1f) * (_values.Length - 1);
        int index = Mathf.Min((int)position, _values.Length - 2);
        return Mathf.Lerp(_values[index], _values[index + 1], position - index);
    }

    /// <summary>Samples with a field value in -1..1.</summary>
    public float SampleField(float value) => Sample(value * 0.5f + 0.5f);
}
