using UnityEngine;

public abstract class VariableSO : ScriptableObject
{
    public string name;
    public string id;

    public abstract object GetValue();
    public abstract void SetValue(object value);
}