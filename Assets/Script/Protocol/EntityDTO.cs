using System.Collections.Generic;

[System.Serializable]
public class EntityDTO
{
    public string id;
    public string type;   // "sprite"
    public string name;

    public float x;
    public float y;

    public List<VariableDTO> variables;
    public List<EventDTO> events;
}