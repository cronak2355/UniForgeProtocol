using System.Collections.Generic;

[System.Serializable]
public class SceneDTO
{
    public string sceneId;
    public string name;

    public List<EntityDTO> entities;
    public List<TileDTO> tiles;
    public List<AssetDTO> assets;
}
