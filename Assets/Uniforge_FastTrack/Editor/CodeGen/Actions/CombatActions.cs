using System.Collections.Generic;
using System.Text;
using Uniforge.FastTrack.Editor;

namespace Uniforge.FastTrack.Editor.CodeGen.Actions
{
    /// <summary>
    /// Generates code for combat-related actions.
    /// </summary>
    public class CombatActions : IActionGenerator
    {
        public string[] SupportedActions => new[]
        {
            "TakeDamage", "Heal", "Attack", "FireProjectile", "SpawnEntity"
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
                case "TakeDamage":
                case "Heal":
                    GenerateDamageHeal(sb, action, p, indent);
                    break;
                case "Attack":
                    GenerateAttack(sb, p, indent);
                    break;
                case "FireProjectile":
                    GenerateFireProjectile(sb, p, indent);
                    break;
                case "SpawnEntity":
                    GenerateSpawnEntity(sb, p, indent);
                    break;
            }
        }

        private void GenerateDamageHeal(StringBuilder sb, string action, Dictionary<string, object> p, string indent)
        {
            float amount = ParameterHelper.GetParamFloat(p, "amount", 10f);
            string sign = action == "Heal" ? "+" : "-";

            sb.AppendLine($"{indent}// {action}: {amount}");
            sb.AppendLine($"{indent}hp {sign}= {amount}f;");

            if (action == "Heal")
            {
                sb.AppendLine($"{indent}if (hp > maxHp) hp = maxHp;");
            }
            else
            {
                sb.AppendLine($"{indent}if (hp <= 0) {{ hp = 0; OnDeath(); }}");
            }
        }

        private void GenerateAttack(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            float range = ParameterHelper.GetParamFloat(p, "range", 100f) / 100f;
            float damage = ParameterHelper.GetParamFloat(p, "damage", 10f);
            float cooldown = ParameterHelper.GetParamFloat(p, "cooldown", 500f) / 1000f;
            string targetId = ParameterHelper.GetParamString(p, "targetId");
            string targetRole = ParameterHelper.GetParamString(p, "targetRole");
            string hitEffect = ParameterHelper.GetParamString(p, "hitEffect", "hit_spark");

            sb.AppendLine($"{indent}// Attack: range={range * 100}, damage={damage}, cooldown={cooldown}s");
            sb.AppendLine($"{indent}if (Time.time >= _lastAttackTime + {cooldown}f)");
            sb.AppendLine($"{indent}{{");

            if (!string.IsNullOrEmpty(targetId))
            {
                sb.AppendLine($"{indent}    var attackTarget = GameObject.Find(\"{targetId}\");");
                sb.AppendLine($"{indent}    if (attackTarget != null && Vector3.Distance(_transform.position, attackTarget.transform.position) <= {range}f)");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        attackTarget.SendMessage(\"OnTakeDamage\", {damage}f, SendMessageOptions.DontRequireReceiver);");
                if (!string.IsNullOrEmpty(hitEffect) && hitEffect != "none")
                {
                    sb.AppendLine($"{indent}        ParticleManager.PlayStatic(\"{hitEffect}\", attackTarget.transform.position, 1f);");
                }
                sb.AppendLine($"{indent}        _lastAttackTime = Time.time;");
                sb.AppendLine($"{indent}    }}");
            }
            else if (!string.IsNullOrEmpty(targetRole))
            {
                sb.AppendLine($"{indent}    foreach (var entity in UniforgeEntity.RegisteredEntities)");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        if (entity.Role == \"{targetRole}\" && entity.gameObject != gameObject)");
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine($"{indent}            float dist = Vector3.Distance(_transform.position, entity.transform.position);");
                sb.AppendLine($"{indent}            if (dist <= {range}f)");
                sb.AppendLine($"{indent}            {{");
                sb.AppendLine($"{indent}                entity.SendMessage(\"OnTakeDamage\", {damage}f, SendMessageOptions.DontRequireReceiver);");
                if (!string.IsNullOrEmpty(hitEffect) && hitEffect != "none")
                {
                    sb.AppendLine($"{indent}                ParticleManager.PlayStatic(\"{hitEffect}\", entity.transform.position, 1f);");
                }
                sb.AppendLine($"{indent}                _lastAttackTime = Time.time;");
                sb.AppendLine($"{indent}            }}");
                sb.AppendLine($"{indent}        }}");
                sb.AppendLine($"{indent}    }}");
            }
            else
            {
                sb.AppendLine($"{indent}    ParticleManager.PlayStatic(\"hit_spark\", _transform.position, 0.5f);");
                sb.AppendLine($"{indent}    var hits = Physics2D.OverlapCircleAll(_transform.position, {range}f);");
                sb.AppendLine($"{indent}    foreach (var hit in hits)");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        if (hit.gameObject != gameObject)");
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine($"{indent}            hit.SendMessage(\"OnTakeDamage\", {damage}f, SendMessageOptions.DontRequireReceiver);");
                if (!string.IsNullOrEmpty(hitEffect) && hitEffect != "none")
                {
                    sb.AppendLine($"{indent}            ParticleManager.PlayStatic(\"{hitEffect}\", hit.transform.position, 1f);");
                }
                sb.AppendLine($"{indent}        }}");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine($"{indent}    _lastAttackTime = Time.time;");
            }

            sb.AppendLine($"{indent}}}");
        }

        private void GenerateFireProjectile(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            float speed = ParameterHelper.GetParamFloat(p, "speed", 500f) / 100f;
            float damage = ParameterHelper.GetParamFloat(p, "damage", 10f);
            string targetId = ParameterHelper.GetParamString(p, "targetId");
            string targetRole = ParameterHelper.GetParamString(p, "targetRole", "enemy");
            float targetX = ParameterHelper.GetParamFloat(p, "targetX", float.NaN);
            float targetY = ParameterHelper.GetParamFloat(p, "targetY", float.NaN);

            sb.AppendLine($"{indent}// FireProjectile: speed={speed * 100}, damage={damage}");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    Vector3 fireDirection = Vector3.right;");

            if (!float.IsNaN(targetX) && !float.IsNaN(targetY))
            {
                sb.AppendLine($"{indent}    Vector3 targetPos = new Vector3({targetX / 100f}f, {-targetY / 100f}f, 0);");
                sb.AppendLine($"{indent}    fireDirection = (targetPos - _transform.position).normalized;");
            }
            else if (!string.IsNullOrEmpty(targetId))
            {
                sb.AppendLine($"{indent}    var fireTarget = GameObject.Find(\"{targetId}\");");
                sb.AppendLine($"{indent}    if (fireTarget != null) fireDirection = (fireTarget.transform.position - _transform.position).normalized;");
            }
            else if (!string.IsNullOrEmpty(targetRole))
            {
                sb.AppendLine($"{indent}    var nearestTarget = UniforgeEntity.FindNearestWithRole(\"{targetRole}\", _transform.position);");
                sb.AppendLine($"{indent}    if (nearestTarget != null) fireDirection = (nearestTarget.transform.position - _transform.position).normalized;");
            }

            sb.AppendLine($"{indent}    ProjectileManager.FireDirectionStatic(_transform.position, fireDirection, {speed}f, {damage}f);");
            sb.AppendLine($"{indent}}}");
        }

        private void GenerateSpawnEntity(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            string templateId = ParameterHelper.GetParamString(p, "templateId");
            if (string.IsNullOrEmpty(templateId))
                templateId = ParameterHelper.GetParamString(p, "prefabId");
            if (string.IsNullOrEmpty(templateId))
                templateId = ParameterHelper.GetParamString(p, "sourceAssetId");

            string posMode = ParameterHelper.GetParamString(p, "positionMode", "relative");
            float offsetX = ParameterHelper.GetParamFloat(p, "offsetX", 0) / 100f;
            float offsetY = ParameterHelper.GetParamFloat(p, "offsetY", 0) / 100f;
            float absX = ParameterHelper.GetParamFloat(p, "x", 0) / 100f;
            float absY = ParameterHelper.GetParamFloat(p, "y", 0) / 100f;
            string role = ParameterHelper.GetParamString(p, "role", "neutral");

            sb.AppendLine($"{indent}// SpawnEntity: {templateId}");
            sb.AppendLine($"{indent}{{");

            if (posMode == "absolute")
            {
                sb.AppendLine($"{indent}    var spawnedObj = PrefabRegistry.SpawnStatic(\"{templateId}\", new Vector3({absX}f, {-absY}f, 0));");
            }
            else if (templateId == "__self__")
            {
                sb.AppendLine($"{indent}    var spawnedObj = PrefabRegistry.SpawnSelfStatic(gameObject, _transform.position + new Vector3({offsetX}f, {-offsetY}f, 0));");
            }
            else
            {
                sb.AppendLine($"{indent}    var spawnedObj = PrefabRegistry.SpawnStatic(\"{templateId}\", _transform.position + new Vector3({offsetX}f, {-offsetY}f, 0));");
            }

            if (!string.IsNullOrEmpty(role))
            {
                sb.AppendLine($"{indent}    if (spawnedObj != null)");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        var entity = spawnedObj.GetComponent<UniforgeEntity>();");
                sb.AppendLine($"{indent}        if (entity != null) entity.Role = \"{role}\";");
                sb.AppendLine($"{indent}    }}");
            }
            sb.AppendLine($"{indent}}}");
        }
    }
}
