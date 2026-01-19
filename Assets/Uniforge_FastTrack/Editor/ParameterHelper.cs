using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Uniforge.FastTrack.Editor
{
    /// <summary>
    /// Shared utility methods for parameter extraction and formatting.
    /// Used by ActionCodeGenerator, ConditionCodeGenerator, and UniforgeScriptGenerator.
    /// </summary>
    public static class ParameterHelper
    {
        /// <summary>
        /// Sanitizes a name to be a valid C# identifier.
        /// </summary>
        public static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "_unnamed";

            var sb = new System.Text.StringBuilder();
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    sb.Append(c);
                else
                    sb.Append('_');
            }

            string result = sb.ToString();
            if (char.IsDigit(result[0]))
                result = "_" + result;

            return result;
        }

        /// <summary>
        /// Formats a value for C# code generation.
        /// </summary>
        public static string FormatValue(object value)
        {
            if (value == null) return "null";
            if (value is bool b) return b ? "true" : "false";
            if (value is float f) return $"{f}f";
            if (value is double d) return $"{d}f";
            if (value is int i) return i.ToString();

            string str = value.ToString();
            if (float.TryParse(str, out float fv))
                return $"{fv}f";

            return $"\"{str}\"";
        }

        /// <summary>
        /// Gets a string parameter from the dictionary, checking multiple keys.
        /// </summary>
        public static string GetParamString(Dictionary<string, object> p, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (p.ContainsKey(key) && p[key] != null)
                    return p[key].ToString();
            }
            return "";
        }

        /// <summary>
        /// Gets a float parameter with a default value.
        /// </summary>
        public static float GetParamFloat(Dictionary<string, object> p, string key, float defaultVal = 0)
        {
            if (p.ContainsKey(key) && p[key] != null)
            {
                if (float.TryParse(p[key].ToString(), out float result))
                    return result;
            }
            return defaultVal;
        }

        /// <summary>
        /// Gets a boolean parameter with a default value.
        /// </summary>
        public static bool GetParamBool(Dictionary<string, object> p, string key, bool defaultVal = false)
        {
            if (p.ContainsKey(key) && p[key] != null)
            {
                if (bool.TryParse(p[key].ToString(), out bool result))
                    return result;
            }
            return defaultVal;
        }

        /// <summary>
        /// Gets a Vector2 parameter, handling nested ValueSource format.
        /// </summary>
        public static Vector2 GetParamVector2(Dictionary<string, object> p, string key)
        {
            if (p.ContainsKey(key) && p[key] != null)
            {
                var obj = p[key];
                if (obj is JObject jo)
                {
                    // Check if it's a ValueSource with nested value (Frontend format)
                    // { "type": "literal", "value": { "x": 1, "y": 0 } }
                    if (jo["type"]?.ToString() == "literal" && jo["value"] is JObject valueObj)
                    {
                        float vx = valueObj["x"]?.Value<float>() ?? 0;
                        float vy = valueObj["y"]?.Value<float>() ?? 0;
                        return new Vector2(vx, vy);
                    }

                    // Direct vector format { "x": 1, "y": 0 }
                    float x = jo["x"]?.Value<float>() ?? 0;
                    float y = jo["y"]?.Value<float>() ?? 0;
                    return new Vector2(x, y);
                }
            }
            return Vector2.zero;
        }

        /// <summary>
        /// Gets operand code for variable operations, handling ValueSource objects.
        /// Supports: literal, variable, property, mouse
        /// </summary>
        public static string GetOperandCode(Dictionary<string, object> p, string key)
        {
            if (!p.ContainsKey(key) || p[key] == null) return "0";

            var val = p[key];

            // Check if it's a ValueSource object
            if (val is JObject jo)
            {
                string type = jo["type"]?.ToString() ?? "literal";

                switch (type)
                {
                    case "variable":
                        {
                            string varName = jo["name"]?.ToString() ?? "";
                            Debug.Log($"[GetOperandCode] Variable reference: name='{varName}', jo={jo.ToString(Newtonsoft.Json.Formatting.None)}");
                            return SanitizeName(varName);
                        }

                    case "literal":
                        {
                            var litVal = jo["value"];
                            if (litVal is JObject vec)
                            {
                                float x = vec["x"]?.Value<float>() ?? 0;
                                float y = vec["y"]?.Value<float>() ?? 0;
                                return $"new Vector2({x}f, {y}f)";
                            }
                            return FormatValue(litVal?.ToString());
                        }

                    case "property":
                        {
                            // Get property from another entity
                            string targetId = jo["targetId"]?.ToString() ?? "self";
                            string property = jo["property"]?.ToString() ?? "";

                            if (targetId == "self" || string.IsNullOrEmpty(targetId))
                            {
                                // Self property
                                switch (property)
                                {
                                    case "x": return "_transform.position.x";
                                    case "y": return "_transform.position.y";
                                    case "rotation": return "_transform.eulerAngles.z";
                                    case "scaleX": return "_transform.localScale.x";
                                    case "scaleY": return "_transform.localScale.y";
                                    default: return SanitizeName(property);
                                }
                            }
                            else
                            {
                                // Other entity property
                                return $"UniforgeEntity.FindById(\"{targetId}\")?.transform.position.{property} ?? 0f";
                            }
                        }

                    case "mouse":
                        {
                            // Mouse position
                            string axis = jo["axis"]?.ToString();
                            string mode = jo["mode"]?.ToString() ?? "absolute";

                            // If no axis specified, return Vector2
                            if (string.IsNullOrEmpty(axis))
                            {
                                if (mode == "screen")
                                {
                                    return "new Vector2(Input.mousePosition.x, Input.mousePosition.y)";
                                }
                                else if (mode == "relative")
                                {
                                    return "new Vector2(Camera.main.ScreenToWorldPoint(Input.mousePosition).x - _transform.position.x, " +
                                           "Camera.main.ScreenToWorldPoint(Input.mousePosition).y - _transform.position.y)";
                                }
                                else
                                {
                                    return "new Vector2(Camera.main.ScreenToWorldPoint(Input.mousePosition).x, " +
                                           "Camera.main.ScreenToWorldPoint(Input.mousePosition).y)";
                                }
                            }

                            // Single axis
                            if (mode == "screen")
                            {
                                return axis == "x" ? "Input.mousePosition.x" : "Input.mousePosition.y";
                            }
                            else if (mode == "relative")
                            {
                                // Relative to entity
                                return axis == "x"
                                    ? "(Camera.main.ScreenToWorldPoint(Input.mousePosition).x - _transform.position.x)"
                                    : "(Camera.main.ScreenToWorldPoint(Input.mousePosition).y - _transform.position.y)";
                            }
                            else
                            {
                                // Absolute world position
                                return axis == "x"
                                    ? "Camera.main.ScreenToWorldPoint(Input.mousePosition).x"
                                    : "Camera.main.ScreenToWorldPoint(Input.mousePosition).y";
                            }
                        }

                    default:
                        // Could be a raw vector2
                        if (jo["x"] != null && jo["y"] != null)
                        {
                            float x = jo["x"]?.Value<float>() ?? 0;
                            float y = jo["y"]?.Value<float>() ?? 0;
                            return $"new Vector2({x}f, {y}f)";
                        }
                        break;
                }
            }

            return FormatValue(val.ToString());
        }

        /// <summary>
        /// Gets a ValueSource as a complete expression (for complex sources like mouse).
        /// </summary>
        public static string GetValueSourceCode(Dictionary<string, object> p, string key, string defaultValue = "0")
        {
            if (!p.ContainsKey(key) || p[key] == null) return defaultValue;
            return GetOperandCode(p, key);
        }

        /// <summary>
        /// Checks if a parameter is a dynamic ValueSource (not a literal).
        /// </summary>
        public static bool IsDynamicValueSource(Dictionary<string, object> p, string key)
        {
            if (!p.ContainsKey(key) || p[key] == null) return false;

            var val = p[key];
            if (val is JObject jo)
            {
                string type = jo["type"]?.ToString() ?? "literal";
                return type != "literal";
            }

            return false;
        }
    }
}
