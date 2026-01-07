using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public class RuntimeEvents : MonoBehaviour
{
    private List<EventDTO> events;
    private GameObject owner;

    public void Initialize(List<EventDTO> events, GameObject owner)
    {
        this.events = events;
        this.owner = owner;
    }

    void Start()
    {
        ExecuteTrigger("OnStart");
    }

    void ExecuteTrigger(string trigger)
    {
        foreach (var e in events)
        {
            if (e.trigger == trigger)
            {
                ExecuteAction(e);
            }
        }
    }

    void ExecuteAction(EventDTO e)
    {
        switch (e.action)
        {
            case "SetVariable":
                HandleSetVariable(e.@params);
                break;
        }
    }

    void HandleSetVariable(JObject param)
    {
        string name = param["name"].ToString();
        object value = param["value"].ToObject<object>();

        var vars = owner.GetComponent<RuntimeVariables>();
        vars.SetValue(name, value);
    }
}