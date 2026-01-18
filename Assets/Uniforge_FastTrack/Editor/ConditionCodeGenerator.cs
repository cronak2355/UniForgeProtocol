using System.Collections.Generic;

namespace Uniforge.FastTrack.Editor
{
    /// <summary>
    /// Generates C# condition code from UniForge condition definitions.
    /// Handles Variable checks, Input checks, Status checks, etc.
    /// </summary>
    public static class ConditionCodeGenerator
    {
        /// <summary>
        /// Generates a single condition expression.
        /// </summary>
        public static string GenerateSingleCondition(ConditionJSON cond)
        {
            var p = cond.@params ?? new Dictionary<string, object>();

            switch (cond.type)
            {
                // === Variable Conditions (with Frontend aliases) ===
                case "VariableEquals":
                case "IfVariableEquals":
                case "VarEquals":  // Frontend alias
                    {
                        string varName = ParameterHelper.GetParamString(p, "variable", "name");
                        string value = ParameterHelper.GetParamString(p, "value");
                        return $"{ParameterHelper.SanitizeName(varName)} == {ParameterHelper.FormatValue(value)}";
                    }
                case "VarNotEquals":
                    {
                        string varName = ParameterHelper.GetParamString(p, "variable", "name");
                        string value = ParameterHelper.GetParamString(p, "value");
                        return $"{ParameterHelper.SanitizeName(varName)} != {ParameterHelper.FormatValue(value)}";
                    }
                case "VariableGreaterThan":
                case "IfVariableGreaterThan":
                case "VarGreaterThan":  // Frontend alias
                    {
                        string varName = ParameterHelper.GetParamString(p, "variable", "name");
                        string value = ParameterHelper.GetParamString(p, "value");
                        return $"{ParameterHelper.SanitizeName(varName)} > {ParameterHelper.FormatValue(value)}";
                    }
                case "VariableLessThan":
                case "IfVariableLessThan":
                case "VarLessThan":  // Frontend alias
                    {
                        string varName = ParameterHelper.GetParamString(p, "variable", "name");
                        string value = ParameterHelper.GetParamString(p, "value");
                        return $"{ParameterHelper.SanitizeName(varName)} < {ParameterHelper.FormatValue(value)}";
                    }
                case "VarGreaterOrEqual":
                    {
                        string varName = ParameterHelper.GetParamString(p, "variable", "name");
                        string value = ParameterHelper.GetParamString(p, "value");
                        return $"{ParameterHelper.SanitizeName(varName)} >= {ParameterHelper.FormatValue(value)}";
                    }
                case "VarLessOrEqual":
                    {
                        string varName = ParameterHelper.GetParamString(p, "variable", "name");
                        string value = ParameterHelper.GetParamString(p, "value");
                        return $"{ParameterHelper.SanitizeName(varName)} <= {ParameterHelper.FormatValue(value)}";
                    }

                // === Status Conditions ===
                case "IsAlive":
                    return "hp > 0";
                case "HpBelow":
                    {
                        string value = ParameterHelper.GetParamString(p, "value", "0");
                        return $"hp < {ParameterHelper.FormatValue(value)}";
                    }
                case "HpAbove":
                    {
                        string value = ParameterHelper.GetParamString(p, "value", "0");
                        return $"hp > {ParameterHelper.FormatValue(value)}";
                    }
                case "RoleEquals":
                    {
                        // For role checks, we'd need a role field. For now, always true.
                        return "true /* RoleEquals not implemented */";
                    }

                // === Input Conditions ===
                case "InputDown":
                case "InputHeld":
                case "InputKey":  // Frontend alias
                    {
                        // Get key from condition object directly
                        string key = cond.key ?? "";
                        if (string.IsNullOrEmpty(key) && p.ContainsKey("key"))
                            key = p["key"]?.ToString() ?? "";

                        string unityKeyCode = ConvertWebKeyToUnity(key);
                        // Frontend InputDown = "key is held down", so use GetKey not GetKeyDown
                        return $"Input.GetKey(KeyCode.{unityKeyCode})";
                    }
                case "InputUp":
                    {
                        string key = cond.key ?? "";
                        if (string.IsNullOrEmpty(key) && p.ContainsKey("key"))
                            key = p["key"]?.ToString() ?? "";
                        string unityKeyCode = ConvertWebKeyToUnity(key);
                        return $"Input.GetKeyUp(KeyCode.{unityKeyCode})";
                    }

                default:
                    return "true";
            }
        }

        /// <summary>
        /// Converts web key codes (KeyA, ArrowUp) to Unity KeyCode names.
        /// </summary>
        public static string ConvertWebKeyToUnity(string webKey)
        {
            if (string.IsNullOrEmpty(webKey)) return "None";

            // Handle "Key" prefix (KeyA -> A)
            if (webKey.StartsWith("Key"))
                return webKey.Substring(3);

            // Handle arrow keys
            switch (webKey)
            {
                case "ArrowUp": return "UpArrow";
                case "ArrowDown": return "DownArrow";
                case "ArrowLeft": return "LeftArrow";
                case "ArrowRight": return "RightArrow";
                case "Space": return "Space";
                case "Enter": return "Return";
                case "Escape": return "Escape";
                case "ShiftLeft":
                case "ShiftRight": return "LeftShift";
                case "ControlLeft":
                case "ControlRight": return "LeftControl";
                default:
                    // Try to use as-is (Digit0 -> Alpha0, etc.)
                    if (webKey.StartsWith("Digit"))
                        return "Alpha" + webKey.Substring(5);
                    return webKey;
            }
        }
    }
}
