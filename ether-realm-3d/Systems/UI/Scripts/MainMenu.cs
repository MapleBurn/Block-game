using Godot;

namespace EtherRealm3D.Systems.UI.Scripts;

public partial class MainMenu : Control
{
    private const string WorldSelectionNodePath = "res://Systems/UI/Scenes/world_selection.tscn";
    
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }

    private void OnSingleplayerPressed()
    {
        GetTree().ChangeSceneToFile(WorldSelectionNodePath);
    }
}