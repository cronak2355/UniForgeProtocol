using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Uniforge.FastTrack.Editor
{
    /// <summary>
    /// Generates C# action code from UniForge action definitions.
    /// Handles Movement, Combat, Variables, Visual effects, etc.
    /// </summary>
    public static class ActionCodeGenerator
    {
        /// <summary>
        /// Generates code for a single action.
        /// </summary>
        /// <param name="sb">StringBuilder to append code to</param>
        /// <param name="action">Action type name</param>
        /// <param name="p">Action parameters</param>
        /// <param name="indent">Current indentation</param>
        /// <param name="entity">Parent entity (for module references)</param>
        /// <param name="traverseGraphCallback">Callback for RunModule action to traverse graphs</param>
        public static void GenerateActionCode(
            StringBuilder sb, 
            string action, 
            Dictionary<string, object> p, 
            string indent, 
            EntityJSON entity,
            System.Action<StringBuilder, NodeJSON, ModuleJSON, string, EntityJSON> traverseGraphCallback = null)
        {
            if (string.IsNullOrEmpty(action)) return;
            p = p ?? new Dictionary<string, object>();

            switch (action)
            {
                // === Basic ===
                case "Log":
                    {
                        string msg = ParameterHelper.GetParamString(p, "message");
                        sb.AppendLine($"{indent}Debug.Log(\"{msg}\");");
                        break;
                    }

                // === Movement ===
                case "Move":
                    {
                        float speed = ParameterHelper.GetParamFloat(p, "speed", 200f);
                        var dir = ParameterHelper.GetParamVector2(p, "direction");
                        // Web speed is in pixels/sec, Unity uses units/sec. Divide by PPU (100)
                        float unitySpeed = speed / 100f;
                        // Web Y-Down to Unity Y-Up: Negate Y direction
                        sb.AppendLine($"{indent}_transform.Translate(new Vector3({dir.x}f, {-dir.y}f, 0).normalized * {unitySpeed}f * Time.deltaTime);");
                        break;
                    }
                case "Rotate":
                    {
                        float speed = ParameterHelper.GetParamFloat(p, "speed", 90f);
                        sb.AppendLine($"{indent}_transform.Rotate(0, 0, {speed}f * Time.deltaTime);");
                        break;
                    }
                case "ChaseTarget":
                    {
                        string targetId = ParameterHelper.GetParamString(p, "targetId");
                        float speed = ParameterHelper.GetParamFloat(p, "speed", 80f) / 100f;
                        sb.AppendLine($"{indent}// ChaseTarget: {targetId}");
                        sb.AppendLine($"{indent}var target = GameObject.Find(\"{targetId}\");");
                        sb.AppendLine($"{indent}if (target != null)");
                        sb.AppendLine($"{indent}{{");
                        sb.AppendLine($"{indent}    Vector3 dir = (target.transform.position - _transform.position).normalized;");
                        sb.AppendLine($"{indent}    _transform.Translate(dir * {speed}f * Time.deltaTime);");
                        sb.AppendLine($"{indent}}}");
                        break;
                    }
                case "MoveToward":
                    {
                        float speed = ParameterHelper.GetParamFloat(p, "speed", 100f) / 100f;
                        float targetX = ParameterHelper.GetParamFloat(p, "x", 0) / 100f;
                        float targetY = ParameterHelper.GetParamFloat(p, "y", 0) / 100f;
                        sb.AppendLine($"{indent}// MoveToward: ({targetX * 100}, {targetY * 100}) -> Unity ({targetX}, {-targetY})");
                        sb.AppendLine($"{indent}Vector3 targetPos = new Vector3({targetX}f, {-targetY}f, 0);");
                        sb.AppendLine($"{indent}Vector3 direction = (targetPos - _transform.position).normalized;");
                        sb.AppendLine($"{indent}if (Vector3.Distance(_transform.position, targetPos) > 0.05f)");
                        sb.AppendLine($"{indent}{{");
                        sb.AppendLine($"{indent}    _transform.Translate(direction * {speed}f * Time.deltaTime);");
                        sb.AppendLine($"{indent}}}");
                        break;
                    }

                // === Variables ===
                case "SetVar":
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
                        break;
                    }
                case "IncrementVar":
                    {
                        string varName = ParameterHelper.SanitizeName(ParameterHelper.GetParamString(p, "name"));
                        float amount = ParameterHelper.GetParamFloat(p, "amount", 0);
                        if (amount == 0)
                            sb.AppendLine($"{indent}{varName} += Time.deltaTime;");
                        else
                            sb.AppendLine($"{indent}{varName} += {amount}f;");
                        break;
                    }

                // === Flow Control ===
                case "Wait":
                    {
                        float seconds = ParameterHelper.GetParamFloat(p, "seconds", 1f);
                        sb.AppendLine($"{indent}yield return new WaitForSeconds({seconds}f);");
                        break;
                    }
                case "Enable":
                    {
                        bool enabled = ParameterHelper.GetParamBool(p, "enabled", true);
                        sb.AppendLine($"{indent}gameObject.SetActive({enabled.ToString().ToLower()});");
                        break;
                    }
                case "ChangeScene":
                    {
                        string sceneName = ParameterHelper.GetParamString(p, "sceneName", "sceneId");
                        sb.AppendLine($"{indent}SceneManager.LoadScene(\"{sceneName}\");");
                        break;
                    }
                case "Destroy":
                    {
                        sb.AppendLine($"{indent}Destroy(gameObject);");
                        break;
                    }

                // === Combat ===
                case "TakeDamage":
                case "Heal":
                    {
                        float amount = ParameterHelper.GetParamFloat(p, "amount", 10f);
                        string sign = action == "Heal" ? "+" : "-";
                        sb.AppendLine($"{indent}// {action}: {amount}");
                        sb.AppendLine($"{indent}hp {sign}= {amount}f;");
                        break;
                    }
                case "Attack":
                    {
                        float range = ParameterHelper.GetParamFloat(p, "range", 100f);
                        float damage = ParameterHelper.GetParamFloat(p, "damage", 10f);
                        sb.AppendLine($"{indent}// Attack: range={range}, damage={damage}");
                        sb.AppendLine($"{indent}var hits = Physics2D.OverlapCircleAll(_transform.position, {range}f / 100f);");
                        sb.AppendLine($"{indent}foreach (var hit in hits)");
                        sb.AppendLine($"{indent}{{");
                        sb.AppendLine($"{indent}    if (hit.gameObject != gameObject)");
                        sb.AppendLine($"{indent}        hit.SendMessage(\"OnTakeDamage\", {damage}f, SendMessageOptions.DontRequireReceiver);");
                        sb.AppendLine($"{indent}}}");
                        break;
                    }
                case "FireProjectile":
                    {
                        float speed = ParameterHelper.GetParamFloat(p, "speed", 500f) / 100f;
                        float damage = ParameterHelper.GetParamFloat(p, "damage", 10f);
                        string targetRole = ParameterHelper.GetParamString(p, "targetRole", "enemy");
                        sb.AppendLine($"{indent}ProjectileManager.FireStatic(_transform.position, \"{targetRole}\", {speed}f, {damage}f);");
                        break;
                    }

                case "SpawnEntity":
                    {
                        string templateId = ParameterHelper.GetParamString(p, "templateId");
                        string posMode = ParameterHelper.GetParamString(p, "positionMode", "relative");
                        float offsetX = ParameterHelper.GetParamFloat(p, "offsetX", 0) / 100f;
                        float offsetY = ParameterHelper.GetParamFloat(p, "offsetY", 0) / 100f;
                        float absX = ParameterHelper.GetParamFloat(p, "x", 0) / 100f;
                        float absY = ParameterHelper.GetParamFloat(p, "y", 0) / 100f;

                        if (posMode == "absolute")
                        {
                            sb.AppendLine($"{indent}PrefabRegistry.SpawnStatic(\"{templateId}\", new Vector3({absX}f, {-absY}f, 0));");
                        }
                        else if (templateId == "__self__")
                        {
                            sb.AppendLine($"{indent}PrefabRegistry.SpawnSelfStatic(gameObject, _transform.position + new Vector3({offsetX}f, {-offsetY}f, 0));");
                        }
                        else
                        {
                            sb.AppendLine($"{indent}PrefabRegistry.SpawnStatic(\"{templateId}\", _transform.position + new Vector3({offsetX}f, {-offsetY}f, 0));");
                        }
                        break;
                    }

                // === Visual ===
                case "PlayAnimation":
                    {
                        string animName = ParameterHelper.GetParamString(p, "animationName");
                        sb.AppendLine($"{indent}if (_animator != null) _animator.Play(\"{animName}\");");
                        break;
                    }
                case "Pulse":
                    {
                        float speed = ParameterHelper.GetParamFloat(p, "speed", 2f);
                        float minScale = ParameterHelper.GetParamFloat(p, "minScale", 0.9f);
                        float maxScale = ParameterHelper.GetParamFloat(p, "maxScale", 1.1f);
                        sb.AppendLine($"{indent}float pulse = Mathf.Lerp({minScale}f, {maxScale}f, (Mathf.Sin(Time.time * {speed}f) + 1f) / 2f);");
                        sb.AppendLine($"{indent}_transform.localScale = new Vector3(pulse, pulse, 1f);");
                        break;
                    }
                case "PlayParticle":
                    {
                        string preset = ParameterHelper.GetParamString(p, "preset", "hit_spark");
                        float scale = ParameterHelper.GetParamFloat(p, "scale", 1f);
                        sb.AppendLine($"{indent}ParticleManager.PlayStatic(\"{preset}\", _transform.position, {scale}f);");
                        break;
                    }
                case "StartParticleEmitter":
                    {
                        string emitterId = ParameterHelper.GetParamString(p, "emitterId");
                        string preset = ParameterHelper.GetParamString(p, "preset", "fire");
                        sb.AppendLine($"{indent}// StartParticleEmitter: {emitterId} ({preset})");
                        break;
                    }
                case "StopParticleEmitter":
                    {
                        string emitterId = ParameterHelper.GetParamString(p, "emitterId");
                        sb.AppendLine($"{indent}// StopParticleEmitter: {emitterId}");
                        break;
                    }

                case "PlaySound":
                    {
                        string soundId = ParameterHelper.GetParamString(p, "soundId");
                        sb.AppendLine($"{indent}AudioManager.PlayStatic(\"{soundId}\");");
                        break;
                    }

                // === Events ===
                case "EmitEventSignal":
                    {
                        string signalKey = ParameterHelper.GetParamString(p, "signalKey");
                        sb.AppendLine($"{indent}EventBus.Emit(\"{signalKey}\");");
                        break;
                    }
                case "ClearSignal":
                    {
                        string key = ParameterHelper.GetParamString(p, "key");
                        sb.AppendLine($"{indent}EventBus.Clear(\"{key}\");");
                        break;
                    }
                case "ShowDialogue":
                    {
                        string text = ParameterHelper.GetParamString(p, "text");
                        sb.AppendLine($"{indent}DialogueManager.Show(\"{text}\");");
                        break;
                    }

                // === Modules ===
                case "RunModule":
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
                        break;
                    }

                default:
                    sb.AppendLine($"{indent}// Unknown action: {action}");
                    break;
            }
        }
    }
}
