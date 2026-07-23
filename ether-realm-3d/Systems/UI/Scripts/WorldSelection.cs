using Godot;

namespace EtherRealm3D.Systems.UI.Scripts;

public partial class WorldSelection : Control
{
    public void OnFilePressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/world.tscn");
    }
}