using Newtonsoft.Json.Linq;

[System.Serializable]
public class EventDTO
{
    public string trigger;
    public string action;

    public JObject @params;
}