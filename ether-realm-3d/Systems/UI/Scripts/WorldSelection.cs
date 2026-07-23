using Godot;

namespace EtherRealm3D.Systems.UI.Scripts;

public partial class WorldSelection : Control
{
    public const string WorldScenePath = "res://Entities/World/world.tscn";
    
    public void OnFilePressed()
    {
        GetTree().ChangeSceneToFile(WorldScenePath);
    }
}