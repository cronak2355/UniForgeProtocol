using UnityEngine;

[CreateAssetMenu(menuName = "Protocol/Variables/Int")]
public class IntVariableSO : VariableSO
{
    public int value;

    public override object GetValue()
    {
        return value;
    }

    public override void SetValue(object v)
    {
        value = System.Convert.ToInt32(v);
    }
}