using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Uniforge.FastTrack.Editor
{
    public static class UniforgeScriptGenerator
    {
        private static string GeneratedFolderPath = "Assets/Uniforge_FastTrack/Generated";

        public static void Generate(EntityJSON entity)
        {
            if ((entity.modules == null || entity.modules.Count == 0) && (entity.events == null || entity.events.Count == 0)) return;

            // Ensure folder exists
            if (!Directory.Exists(GeneratedFolderPath))
            {
                Directory.CreateDirectory(GeneratedFolderPath);
            }

            // Clean ID for class name
            string className = $"Gen_{entity.id.Replace("-", "_")}";
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using System.Collections;");
            sb.AppendLine();
            sb.AppendLine($"public class {className} : MonoBehaviour");
            sb.AppendLine("{");

            // Process Modules (Nodes/Graphs)
            if (entity.modules != null && entity.modules.Count > 0)
            {
                var module = entity.modules[0];
                sb.AppendLine("    void Start()");
                sb.AppendLine("    {");
                var entryNode = module.nodes.FirstOrDefault(n => n.kind == "Entry");
                if (entryNode != null) TraverseGraph(sb, entryNode, module, "        ");
                sb.AppendLine("    }");
            }

            // Process Simple Events (Trigger -> Action)
            if (entity.events != null)
            {
                foreach (var evt in entity.events)
                {
                    if (evt.trigger == "OnUpdate")
                    {
                        sb.AppendLine("    void Update()");
                        sb.AppendLine("    {");
                        GenerateActionCode(sb, evt.action, evt.@params, "        ");
                        sb.AppendLine("    }");
                    }
                    else if (evt.trigger == "OnStart")
                    {
                        // Potential future implementation
                    }
                }
            }

            sb.AppendLine("}");

            string filePath = Path.Combine(GeneratedFolderPath, $"{className}.cs");
            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"[UniforgeScriptGenerator] Generated: {className}");
        }

        private static void TraverseGraph(StringBuilder sb, NodeJSON currentNode, ModuleJSON module, string indent)
        {
            // Simple DFS traversal following 'flow' edges
            // 1. Find outgoing edge
            var outEdge = module.edges.FirstOrDefault(e => e.fromNodeId == currentNode.id && e.fromPort == "out");
            
            if (outEdge == null) return;

            // 2. Find target node
            var nextNode = module.nodes.FirstOrDefault(n => n.id == outEdge.toNodeId);
            if (nextNode == null) return;

            // 3. Generate code for next node
            GenerateNodeCode(sb, nextNode, module, indent);

            // 4. Recurse (if not Stop or Branching handled inside)
            if (nextNode.kind != "Condition") // Condition handles its own branching
            {
                TraverseGraph(sb, nextNode, module, indent);
            }
        }

        private static void GenerateActionCode(StringBuilder sb, string action, Dictionary<string, object> parameters, string indent)
        {
            if (action == "Log")
            {
                string msg = parameters != null && parameters.ContainsKey("message") ? parameters["message"].ToString() : "Hello";
                sb.AppendLine($"{indent}Debug.Log(\"{msg}\");");
            }
            else if (action == "Move")
            {
                float x = 0, y = 0;
                if (parameters != null)
                {
                    if (parameters.ContainsKey("x")) float.TryParse(parameters["x"].ToString(), out x);
                    if (parameters.ContainsKey("y")) float.TryParse(parameters["y"].ToString(), out y);
                }
                sb.AppendLine($"{indent}transform.Translate(new Vector3({x}f, {y}f, 0));");
            }
            else if (action == "Rotate")
            {
                sb.AppendLine($"{indent}transform.Rotate(0, 0, 100f * Time.deltaTime);");
            }
        }

        private static void GenerateNodeCode(StringBuilder sb, NodeJSON node, ModuleJSON module, string indent)
        {
            if (node.kind == "Action")
            {
                GenerateActionCode(sb, node.action, node.@params, indent);
            }
            else if (node.kind == "Condition")
            {
                string variable = node.@params != null && node.@params.ContainsKey("variable") ? node.@params["variable"].ToString() : "true";
                string op = node.@params != null && node.@params.ContainsKey("operator") ? node.@params["operator"].ToString() : "==";
                string val = node.@params != null && node.@params.ContainsKey("value") ? node.@params["value"].ToString() : "true";

                sb.AppendLine($"{indent}if ({variable} {op} {val})"); 
                sb.AppendLine($"{indent}{{");
                
                var trueEdge = module.edges.FirstOrDefault(e => e.fromNodeId == node.id && (e.fromPort == "true" || e.fromPort == "then"));
                if (trueEdge != null)
                {
                    var trueNode = module.nodes.FirstOrDefault(n => n.id == trueEdge.toNodeId);
                    if (trueNode != null)
                    {
                        GenerateNodeCode(sb, trueNode, module, indent + "    ");
                        TraverseGraph(sb, trueNode, module, indent + "    ");
                    }
                }
                
                sb.AppendLine($"{indent}}}");
                sb.AppendLine($"{indent}else");
                sb.AppendLine($"{indent}{{");
                
                var falseEdge = module.edges.FirstOrDefault(e => e.fromNodeId == node.id && (e.fromPort == "false" || e.fromPort == "else"));
                if (falseEdge != null)
                {
                    var falseNode = module.nodes.FirstOrDefault(n => n.id == falseEdge.toNodeId);
                    if (falseNode != null)
                    {
                        GenerateNodeCode(sb, falseNode, module, indent + "    ");
                        TraverseGraph(sb, falseNode, module, indent + "    ");
                    }
                }
                sb.AppendLine($"{indent}}}");
            }
            else if (node.kind == "Stop")
            {
                sb.AppendLine($"{indent}return;");
            }
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            string jsonPath = Path.Combine(Application.streamingAssetsPath, "project.json");
            if (!File.Exists(jsonPath)) return;

            string json = File.ReadAllText(jsonPath);
            var gameData = JsonConvert.DeserializeObject<GameDataJSON>(json);
            if (gameData == null || gameData.scenes == null) return;

            bool attachedAny = false;

            foreach (var scene in gameData.scenes)
            {
                if (scene.entities == null) continue;
                foreach (var entity in scene.entities)
                {
                    GameObject go = GameObject.Find(entity.name);
                    if (go == null) continue;

                    string className = $"Gen_{entity.id.Replace("-", "_")}";
                    string fullClassName = className;

                    System.Type scriptType = System.Type.GetType(fullClassName + ", Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
                    
                    if (scriptType == null)
                    {
                        var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                        foreach (var asm in assemblies)
                        {
                            scriptType = asm.GetType(fullClassName);
                            if (scriptType != null) break;
                        }
                    }

                    if (scriptType != null)
                    {
                        if (go.GetComponent(scriptType) == null)
                        {
                            go.AddComponent(scriptType);
                            Debug.Log($"[UniforgeScriptGenerator] Auto-Attached {className} to {go.name}");
                            attachedAny = true;
                        }
                    }
                }
            }
        }
    }
}
