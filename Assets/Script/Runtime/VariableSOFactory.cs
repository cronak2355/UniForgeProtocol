using UnityEngine;

public static class VariableSOFactory
{
    public static VariableSO Create(VariableDTO dto)
    {
        VariableSO so = null;

        switch (dto.type)
        {
            case "int":
                var intVar = ScriptableObject.CreateInstance<IntVariableSO>();
                intVar.value = System.Convert.ToInt32(dto.value);
                so = intVar;
                break;

                // case "float":
                // case "bool":
                // case "string":
        }

        if (so != null)
        {
            so.id = dto.id;
            so.name = dto.name;
        }

        return so;
    }
}