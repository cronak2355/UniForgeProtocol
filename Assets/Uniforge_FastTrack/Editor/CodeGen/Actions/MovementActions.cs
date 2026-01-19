using System.Collections.Generic;
using System.Text;
using Uniforge.FastTrack.Editor;

namespace Uniforge.FastTrack.Editor.CodeGen.Actions
{
    /// <summary>
    /// Generates code for movement-related actions.
    /// </summary>
    public class MovementActions : IActionGenerator
    {
        public string[] SupportedActions => new[]
        {
            "Move", "Rotate", "ChaseTarget", "MoveToward", "Jump"
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
                case "Move":
                    GenerateMove(sb, p, indent);
                    break;
                case "Rotate":
                    GenerateRotate(sb, p, indent);
                    break;
                case "ChaseTarget":
                    GenerateChaseTarget(sb, p, indent);
                    break;
                case "MoveToward":
                    GenerateMoveToward(sb, p, indent);
                    break;
                case "Jump":
                    GenerateJump(sb, p, indent);
                    break;
            }
        }

        private void GenerateMove(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            float speed = ParameterHelper.GetParamFloat(p, "speed", 200f);
            float unitySpeed = speed / 100f;

            if (ParameterHelper.IsDynamicValueSource(p, "direction"))
            {
                string dirCode = ParameterHelper.GetOperandCode(p, "direction");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    Vector2 moveDir = {dirCode};");
                sb.AppendLine($"{indent}    Vector3 dir3 = new Vector3(moveDir.x, moveDir.y, 0).normalized;");
                sb.AppendLine($"{indent}    _transform.Translate(dir3 * {unitySpeed}f * Time.deltaTime);");
                sb.AppendLine($"{indent}}}");
            }
            else
            {
                var dir = ParameterHelper.GetParamVector2(p, "direction");
                sb.AppendLine($"{indent}_transform.Translate(new Vector3({dir.x}f, {dir.y}f, 0).normalized * {unitySpeed}f * Time.deltaTime);");
            }
        }

        private void GenerateRotate(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            float speed = ParameterHelper.GetParamFloat(p, "speed", 90f);
            sb.AppendLine($"{indent}_transform.Rotate(0, 0, {speed}f * Time.deltaTime);");
        }

        private void GenerateChaseTarget(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            string targetId = ParameterHelper.GetParamString(p, "targetId");
            string targetRole = ParameterHelper.GetParamString(p, "targetRole");
            float speed = ParameterHelper.GetParamFloat(p, "speed", 80f) / 100f;

            sb.AppendLine($"{indent}// ChaseTarget: targetId={targetId}, targetRole={targetRole}");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    Transform chaseTarget = null;");

            if (!string.IsNullOrEmpty(targetId))
            {
                sb.AppendLine($"{indent}    var targetGo = GameObject.Find(\"{targetId}\");");
                sb.AppendLine($"{indent}    if (targetGo != null) chaseTarget = targetGo.transform;");
            }

            if (!string.IsNullOrEmpty(targetRole))
            {
                sb.AppendLine($"{indent}    if (chaseTarget == null)");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        chaseTarget = UniforgeEntity.FindNearestWithRole(\"{targetRole}\", _transform.position)?.transform;");
                sb.AppendLine($"{indent}    }}");
            }

            sb.AppendLine($"{indent}    if (chaseTarget != null)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        Vector3 dir = (chaseTarget.position - _transform.position).normalized;");
            sb.AppendLine($"{indent}        _transform.Translate(dir * {speed}f * Time.deltaTime);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine($"{indent}}}");
        }

        private void GenerateMoveToward(StringBuilder sb, Dictionary<string, object> p, string indent)
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
        }

        private void GenerateJump(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            float jumpForce = ParameterHelper.GetParamFloat(p, "force", 500f) / 100f;

            sb.AppendLine($"{indent}// Jump with physics");
            sb.AppendLine($"{indent}if (_isGrounded)");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    var rb = GetComponent<Rigidbody2D>();");
            sb.AppendLine($"{indent}    if (rb != null)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        rb.velocity = new Vector2(rb.velocity.x, 0);");
            sb.AppendLine($"{indent}        rb.AddForce(Vector2.up * {jumpForce}f, ForceMode2D.Impulse);");
            sb.AppendLine($"{indent}        _isGrounded = false;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine($"{indent}}}");
        }
    }
}
