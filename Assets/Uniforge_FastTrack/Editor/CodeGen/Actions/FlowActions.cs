using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Uniforge.FastTrack.Editor;

namespace Uniforge.FastTrack.Editor.CodeGen.Actions
{
    /// <summary>
    /// Generates code for flow control and variable actions.
    /// </summary>
    public class FlowActions : IActionGenerator
    {
        public string[] SupportedActions => new[]
        {
            "Log", "Wait", "Enable", "Disable", "ChangeScene", "Destroy",
            "SetVar", "IncrementVar", "If", "SetSignal", "RunModule",
            "EmitEventSignal", "ClearSignal", "ShowDialogue"
        };

        public void Generate(
            StringBuilder sb,
            string action,
            Dictionary<string, object> p,
            string indent,
            EntityJSON entity,
            System.Action<StringBuilder, NodeJSON, ModuleJSON, string, EntityJSON> traverseGraphCallback = null)
        {
            switch (action)
            {
                case "Log":
                    GenerateLog(sb, p, indent);
                    break;
                case "Wait":
                    GenerateWait(sb, p, indent);
                    break;
                case "Enable":
                    GenerateEnable(sb, p, indent);
                    break;
                case "Disable":
                    GenerateDisable(sb, p, indent);
                    break;
                case "ChangeScene":
                    GenerateChangeScene(sb, p, indent);
                    break;
                case "Destroy":
                    GenerateDestroy(sb, indent);
                    break;
                case "SetVar":
                    GenerateSetVar(sb, p, indent);
                    break;
                case "IncrementVar":
                    GenerateIncrementVar(sb, p, indent);
                    break;
                case "If":
                    GenerateIf(sb, p, indent, entity, traverseGraphCallback);
                    break;
                case "SetSignal":
                    GenerateSetSignal(sb, p, indent);
                    break;
                case "RunModule":
                    GenerateRunModule(sb, p, indent, entity, traverseGraphCallback);
                    break;
                case "EmitEventSignal":
                    GenerateEmitEventSignal(sb, p, indent);
                    break;
                case "ClearSignal":
                    GenerateClearSignal(sb, p, indent);
                    break;
                case "ShowDialogue":
                    GenerateShowDialogue(sb, p, indent);
                    break;
            }
        }

        private void GenerateLog(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            string msg = ParameterHelper.GetParamString(p, "message");
            sb.AppendLine($"{indent}Debug.Log(\"{msg}\");");
        }

        private void GenerateWait(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            float seconds = ParameterHelper.GetParamFloat(p, "seconds", 1f);
            sb.AppendLine($"{indent}yield return new WaitForSeconds({seconds}f);");
        }

        private void GenerateEnable(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            bool enabled = ParameterHelper.GetParamBool(p, "enabled", true);
            sb.AppendLine($"{indent}gameObject.SetActive({enabled.ToString().ToLower()});");
        }

        private void GenerateDisable(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            string targetId = ParameterHelper.GetParamString(p, "targetId");

            if (!string.IsNullOrEmpty(targetId))
            {
                sb.AppendLine($"{indent}Debug.Log(\"[Action] Disable Target: {targetId}\");");
                sb.AppendLine($"{indent}var disableTarget = GameObject.Find(\"{targetId}\");");
                sb.AppendLine($"{indent}if (disableTarget != null) disableTarget.SetActive(false);");
            }
            else
            {
                sb.AppendLine($"{indent}Debug.Log(\"[Action] Disable Self\");");
                sb.AppendLine($"{indent}gameObject.SetActive(false);");
            }
        }

        private void GenerateChangeScene(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            string sceneName = ParameterHelper.GetParamString(p, "sceneName", "sceneId");
            sb.AppendLine($"{indent}SceneManager.LoadScene(\"{sceneName}\");");
        }

        private void GenerateDestroy(StringBuilder sb, string indent)
        {
            sb.AppendLine($"{indent}Destroy(gameObject);");
        }

        private void GenerateSetVar(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            string varName = ParameterHelper.SanitizeName(ParameterHelper.GetParamString(p, "name"));
            string operation = ParameterHelper.GetParamString(p, "operation", "Set");
            var op1 = ParameterHelper.GetOperandCode(p, "operand1");
            var op2 = ParameterHelper.GetOperandCode(p, "operand2");

            switch (operation)
            {
                case "Set":
                    sb.AppendLine($"{indent}{varName} = {op1};");
                    break;
                case "Add":
                    sb.AppendLine($"{indent}{varName} = {op1} + {op2};");
                    break;
                case "Sub":
                    sb.AppendLine($"{indent}{varName} = {op1} - {op2};");
                    break;
                case "Multiply":
                    sb.AppendLine($"{indent}{varName} = {op1} * {op2};");
                    break;
                case "Divide":
                    sb.AppendLine($"{indent}{varName} = {op2} != 0 ? {op1} / {op2} : 0;");
                    break;
                default:
                    sb.AppendLine($"{indent}{varName} = {op1};");
                    break;
            }
        }

        private void GenerateIncrementVar(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            string varName = ParameterHelper.SanitizeName(ParameterHelper.GetParamString(p, "name"));
            float amount = ParameterHelper.GetParamFloat(p, "amount", 0);

            if (amount == 0)
                sb.AppendLine($"{indent}{varName} += Time.deltaTime;");
            else
                sb.AppendLine($"{indent}{varName} += {amount}f;");
        }

        private void GenerateIf(
            StringBuilder sb,
            Dictionary<string, object> p,
            string indent,
            EntityJSON entity,
            System.Action<StringBuilder, NodeJSON, ModuleJSON, string, EntityJSON> traverseGraphCallback)
        {
            sb.AppendLine($"{indent}// Inline If Action");

            var conditionObj = p.ContainsKey("condition") ? p["condition"] : null;
            var thenActions = p.ContainsKey("then") ? p["then"] : null;
            var elseActions = p.ContainsKey("else") ? p["else"] : null;

            string conditionCode = GenerateInlineCondition(conditionObj);
            sb.AppendLine($"{indent}if ({conditionCode})");
            sb.AppendLine($"{indent}{{");

            if (thenActions is JArray thenArr)
            {
                foreach (var actionObj in thenArr)
                {
                    if (actionObj is JObject jo)
                    {
                        string actionType = jo["type"]?.ToString() ?? "";
                        var actionParams = jo.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
                        actionParams.Remove("type");
                        ActionDispatcher.GenerateActionCode(sb, actionType, actionParams, indent + "    ", entity, traverseGraphCallback);
                    }
                }
            }

            sb.AppendLine($"{indent}}}");

            if (elseActions is JArray elseArr && elseArr.Count > 0)
            {
                sb.AppendLine($"{indent}else");
                sb.AppendLine($"{indent}{{");
                foreach (var actionObj in elseArr)
                {
                    if (actionObj is JObject jo)
                    {
                        string actionType = jo["type"]?.ToString() ?? "";
                        var actionParams = jo.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
                        actionParams.Remove("type");
                        ActionDispatcher.GenerateActionCode(sb, actionType, actionParams, indent + "    ", entity, traverseGraphCallback);
                    }
                }
                sb.AppendLine($"{indent}}}");
            }
        }

        private string GenerateInlineCondition(object conditionObj)
        {
            if (conditionObj == null) return "true";

            if (conditionObj is JObject jo)
            {
                string condType = jo["type"]?.ToString() ?? "";
                var condParams = jo.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();

                var cond = new ConditionJSON
                {
                    type = condType,
                    key = jo["key"]?.ToString(),
                    @params = condParams
                };

                return ConditionCodeGenerator.GenerateSingleCondition(cond);
            }

            return "true";
        }

        private void GenerateSetSignal(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            string key = ParameterHelper.GetParamString(p, "key");
            bool value = ParameterHelper.GetParamBool(p, "value", true);
            sb.AppendLine($"{indent}_signalFlags[\"{key}\"] = {value.ToString().ToLower()};");
        }

        private void GenerateRunModule(
            StringBuilder sb,
            Dictionary<string, object> p,
            string indent,
            EntityJSON entity,
            System.Action<StringBuilder, NodeJSON, ModuleJSON, string, EntityJSON> traverseGraphCallback)
        {
            string moduleId = ParameterHelper.GetParamString(p, "moduleId");
            sb.AppendLine($"{indent}// RunModule: {moduleId}");

            if (traverseGraphCallback != null && entity?.modules != null)
            {
                var targetModule = entity.modules.FirstOrDefault(m => m.id == moduleId);
                if (targetModule != null)
                {
                    var entry = targetModule.nodes?.FirstOrDefault(n => n.kind == "Entry");
                    if (entry != null)
                    {
                        traverseGraphCallback(sb, entry, targetModule, indent, entity);
                    }
                }
                else
                {
                    sb.AppendLine($"{indent}// Warning: Module {moduleId} not found");
                }
            }
        }

        private void GenerateEmitEventSignal(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            string signalKey = ParameterHelper.GetParamString(p, "signalKey");
            sb.AppendLine($"{indent}EventBus.Emit(\"{signalKey}\");");
        }

        private void GenerateClearSignal(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            string key = ParameterHelper.GetParamString(p, "key");
            sb.AppendLine($"{indent}EventBus.Clear(\"{key}\");");
        }

        private void GenerateShowDialogue(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            string text = ParameterHelper.GetParamString(p, "text");
            sb.AppendLine($"{indent}DialogueManager.Show(\"{text}\");");
        }
    }
}
