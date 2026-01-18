using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Uniforge.FastTrack.Editor
{
    /// <summary>
    /// Generates MonoBehaviour scripts from UniForge JSON data.
    /// Optimized for fast generation with minimal runtime overhead.
    /// Refactored to use componentized generators.
    /// </summary>
    public static class UniforgeScriptGenerator
    {
        private static string GeneratedFolderPath = "Assets/Uniforge_FastTrack/Generated";
        private static StringBuilder _cachedSb = new StringBuilder(4096); // Reuse for GC optimization

        public static void Generate(EntityJSON entity)
        {
            if ((entity.modules == null || entity.modules.Count == 0) && 
                (entity.events == null || entity.events.Count == 0) &&
                (entity.variables == null || entity.variables.Count == 0)) 
                return;

            if (!Directory.Exists(GeneratedFolderPath))
            {
                Directory.CreateDirectory(GeneratedFolderPath);
            }

            string className = $"Gen_{entity.id.Replace("-", "_")}";
            
            _cachedSb.Clear();
            var sb = _cachedSb;

            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.SceneManagement;");
            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using Uniforge.FastTrack.Runtime;");
            sb.AppendLine();
            sb.AppendLine($"public class {className} : MonoBehaviour");
            sb.AppendLine("{");

            // === Generate Variable Declarations ===
            GenerateVariableDeclarations(sb, entity.variables);

            // === Generate Cached References (Optimization) ===
            sb.AppendLine("    private Transform _transform;");
            sb.AppendLine("    private Animator _animator;");
            sb.AppendLine("    public float hp = 100f;");
            sb.AppendLine();

            sb.AppendLine("    void Awake()");
            sb.AppendLine("    {");
            sb.AppendLine("        _transform = transform;");
            sb.AppendLine("        _animator = GetComponent<Animator>();");
            sb.AppendLine("        var ent = GetComponent<UniforgeEntity>();");
            // Sync variables if needed
            sb.AppendLine("    }");

            // === Generate Event Handlers (OnUpdate, OnStart, etc.) ===
            // Group events by trigger
            var eventGroups = entity.events?.GroupBy(e => e.trigger) ?? Enumerable.Empty<IGrouping<string, EventJSON>>();

            foreach (var group in eventGroups)
            {
                string trigger = group.Key;
                if (string.IsNullOrEmpty(trigger)) continue;

                if (trigger == "OnStart") sb.AppendLine("    void Start()");
                else if (trigger == "OnUpdate") sb.AppendLine("    void Update()");
                else if (trigger == "OnCollision") sb.AppendLine("    void OnCollisionEnter2D(Collision2D collision)");
                else sb.AppendLine($"    // Trigger: {trigger} (Custom implementation required)");

                if (trigger == "OnStart" || trigger == "OnUpdate" || trigger == "OnCollision")
                {
                    sb.AppendLine("    {");
                    foreach (var evt in group)
                    {
                        GenerateEventCode(sb, evt, "        ", entity);
                    }
                    sb.AppendLine("    }");
                }
            }
            
            // Generate Module Functions (for Logic Graph)
            if (entity.modules != null)
            {
                foreach (var module in entity.modules)
                {
                    // Generate a method for this module logic? 
                    // Currently we inline graph traversal for RunModule action context, 
                    // but complex graphs might need their own methods.
                    // For now, we only traverse when invoked.
                }
            }

            sb.AppendLine("}");

            // Write to file
            string filePath = Path.Combine(GeneratedFolderPath, $"{className}.cs");
            File.WriteAllText(filePath, sb.ToString());
        }

        private static void GenerateVariableDeclarations(StringBuilder sb, List<VariableJSON> variables)
        {
            if (variables == null) return;
            foreach (var v in variables)
            {
                string type = GetCSharpType(v.type);
                string defaultValue = GetDefaultValue(v.type, v.value);
                string name = ParameterHelper.SanitizeName(v.name);
                sb.AppendLine($"    public {type} {name} = {defaultValue};");
            }
            sb.AppendLine();
        }

        private static string GetCSharpType(string types)
        {
            switch (types) {
                case "int": return "int";
                case "float": return "float";
                case "bool": return "bool";
                case "string": return "string";
                case "vector2": return "Vector2";
                default: return "object";
            }
        }

        private static string GetDefaultValue(string types, object val)
        {
            if (val == null) {
                switch (types) {
                    case "int": return "0";
                    case "float": return "0f";
                    case "bool": return "false";
                    case "vector2": return "Vector2.zero";
                    default: return "null";
                }
            }
            if (types == "string") return $"\"{val}\"";
            if (types == "float") return $"{val}f";
            if (types == "bool") return val.ToString().ToLower();
            return val.ToString();
        }

        private static void GenerateEventCode(StringBuilder sb, EventJSON evt, string indent, EntityJSON entity)
        {
            // Generate conditions wrapper if any
            bool hasConditions = evt.conditions != null && evt.conditions.Count > 0;
            if (hasConditions)
            {
                string conditionCode = GenerateConditionsCode(evt.conditions, evt.conditionLogic);
                sb.AppendLine($"{indent}if ({conditionCode})");
                sb.AppendLine($"{indent}{{");
                indent += "    ";
            }

            // Generate action
            // Using ActionCodeGenerator
            ActionCodeGenerator.GenerateActionCode(sb, evt.action, evt.@params, indent, entity, TraverseGraph);

            if (hasConditions)
            {
                indent = indent.Substring(4);
                sb.AppendLine($"{indent}}}");
            }
        }

        private static string GenerateConditionsCode(List<ConditionJSON> conditions, string logic)
        {
            if (conditions == null || conditions.Count == 0) return "true";

            var parts = new List<string>();
            foreach (var cond in conditions)
            {
                parts.Add(ConditionCodeGenerator.GenerateSingleCondition(cond));
            }

            string op = (logic?.ToUpper() == "OR") ? " || " : " && ";
            return string.Join(op, parts);
        }

        // === Graph Traversal ===
        private static void TraverseGraph(StringBuilder sb, NodeJSON currentNode, ModuleJSON module, string indent, EntityJSON entity)
        {
            if (currentNode == null) return;

            // Generate code for this node
            if (currentNode.kind == "Action")
            {
                sb.AppendLine($"{indent}// Node: {currentNode.id}");
                ActionCodeGenerator.GenerateActionCode(sb, currentNode.action, currentNode.@params, indent, entity, TraverseGraph);
            }
            else if (currentNode.kind == "Condition")
            {
                // Condition Node logic
                // Not fully implemented in detailed generator yet, 
                // typically Condition nodes branch to other nodes. 
                // Assuming linear flow for now or specialized handling.
            }
            
            // Find next node via edges
            var edges = module.edges?.Where(e => e.fromNodeId == currentNode.id).ToList();
            if (edges != null && edges.Count > 0)
            {
                foreach (var edge in edges)
                {
                    var nextNode = module.nodes?.FirstOrDefault(n => n.id == edge.toNodeId);
                    if (nextNode != null)
                    {
                        TraverseGraph(sb, nextNode, module, indent, entity);
                    }
                }
            }
        }

        // === Wrappers for ScriptAttacher ===
        public static void AttachScriptsToEntities()
        {
            ScriptAttacher.AttachScriptsToEntities();
        }

        public static void AttachScriptsFromJson()
        {
            ScriptAttacher.AttachScriptsFromJson();
        }

        public static void AttachScript(GameObject go, string entityId)
        {
            ScriptAttacher.AttachScript(go, entityId);
        }
    }
}
