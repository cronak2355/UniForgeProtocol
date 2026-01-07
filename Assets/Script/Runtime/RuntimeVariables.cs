using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

public class RuntimeVariables : MonoBehaviour
{
    [SerializeField]
    private List<VariableSO> variables = new List<VariableSO>();

    public void AddVariable(VariableSO variable)
    {
        variables.Add(variable);
    }

    public VariableSO GetVariable(string id)
    {
        return variables.Find(v => v.id == id);
    }

    public T GetValue<T>(string id)
    {
        var v = GetVariable(id);
        return v != null ? (T)v.GetValue() : default;
    }

    public void SetValue(string id, object value)
    {
        var v = GetVariable(id);
        if (v != null)
        {
            v.SetValue(value);
        }
        else
        {
            Debug.LogWarning($"Variable not found: {id}");
        }
    }
}
