using System.Collections.Generic;
using System.Text;
using Uniforge.FastTrack.Editor;

namespace Uniforge.FastTrack.Editor.CodeGen.Actions
{
    /// <summary>
    /// Generates code for visual/audio-related actions.
    /// </summary>
    public class VisualActions : IActionGenerator
    {
        public string[] SupportedActions => new[]
        {
            "PlayAnimation", "Pulse", "PlayParticle", "StartParticleEmitter", "StopParticleEmitter", "PlaySound"
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
                case "PlayAnimation":
                    GeneratePlayAnimation(sb, p, indent);
                    break;
                case "Pulse":
                    GeneratePulse(sb, p, indent);
                    break;
                case "PlayParticle":
                    GeneratePlayParticle(sb, p, indent);
                    break;
                case "StartParticleEmitter":
                    GenerateStartParticleEmitter(sb, p, indent);
                    break;
                case "StopParticleEmitter":
                    GenerateStopParticleEmitter(sb, p, indent);
                    break;
                case "PlaySound":
                    GeneratePlaySound(sb, p, indent);
                    break;
            }
        }

        private void GeneratePlayAnimation(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            // Try multiple parameter name variations
            string animName = ParameterHelper.GetParamString(p, "animationName");
            if (string.IsNullOrEmpty(animName))
                animName = ParameterHelper.GetParamString(p, "animation");
            if (string.IsNullOrEmpty(animName))
                animName = ParameterHelper.GetParamString(p, "name");
            if (string.IsNullOrEmpty(animName))
                animName = ParameterHelper.GetParamString(p, "anim");
            if (string.IsNullOrEmpty(animName))
                animName = ParameterHelper.GetParamString(p, "state");

            if (string.IsNullOrEmpty(animName))
            {
                sb.AppendLine($"{indent}Debug.LogWarning(\"[Action] PlayAnimation: No animation name specified\");");
            }
            else
            {
                sb.AppendLine($"{indent}if (_animator != null) {{ Debug.Log(\"[Action] PlayAnimation: {animName}\"); _animator.Play(\"{animName}\"); }}");
                sb.AppendLine($"{indent}else {{ Debug.LogWarning(\"[Action] PlayAnimation Failed: Animator is null for {animName}\"); }}");
            }
        }

        private void GeneratePulse(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            float speed = ParameterHelper.GetParamFloat(p, "speed", 2f);
            float minScale = ParameterHelper.GetParamFloat(p, "minScale", 0.9f);
            float maxScale = ParameterHelper.GetParamFloat(p, "maxScale", 1.1f);

            sb.AppendLine($"{indent}float pulse = Mathf.Lerp({minScale}f, {maxScale}f, (Mathf.Sin(Time.time * {speed}f) + 1f) / 2f);");
            sb.AppendLine($"{indent}_transform.localScale = new Vector3(pulse, pulse, 1f);");
        }

        private void GeneratePlayParticle(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            string preset = ParameterHelper.GetParamString(p, "preset", "hit_spark");
            float scale = ParameterHelper.GetParamFloat(p, "scale", 1f);

            sb.AppendLine($"{indent}Debug.Log(\"[Action] PlayParticle: {preset}\");");
            sb.AppendLine($"{indent}ParticleManager.PlayStatic(\"{preset}\", _transform.position, {scale}f);");
        }

        private void GenerateStartParticleEmitter(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            string emitterId = ParameterHelper.GetParamString(p, "emitterId");
            string particleSystemId = ParameterHelper.GetParamString(p, "particleSystemId", "preset");
            float offsetX = ParameterHelper.GetParamFloat(p, "offsetX", 0) / 100f;
            float offsetY = ParameterHelper.GetParamFloat(p, "offsetY", 0) / 100f;
            bool attachToEntity = ParameterHelper.GetParamBool(p, "attachToEntity", true);

            sb.AppendLine($"{indent}// StartParticleEmitter: {emitterId} (system: {particleSystemId})");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    var emitter = ParticleEmitterManager.StartEmitter(");
            sb.AppendLine($"{indent}        \"{emitterId}\",");
            sb.AppendLine($"{indent}        \"{particleSystemId}\",");
            sb.AppendLine($"{indent}        {(attachToEntity ? "_transform" : "null")},");
            sb.AppendLine($"{indent}        new Vector3({offsetX}f, {-offsetY}f, 0)");
            sb.AppendLine($"{indent}    );");
            sb.AppendLine($"{indent}    if (emitter != null) _activeEmitters[\"{emitterId}\"] = emitter;");
            sb.AppendLine($"{indent}}}");
        }

        private void GenerateStopParticleEmitter(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            string emitterId = ParameterHelper.GetParamString(p, "emitterId");
            bool destroy = ParameterHelper.GetParamBool(p, "destroy", false);

            sb.AppendLine($"{indent}// StopParticleEmitter: {emitterId}");
            sb.AppendLine($"{indent}if (_activeEmitters.TryGetValue(\"{emitterId}\", out var emitterToStop))");
            sb.AppendLine($"{indent}{{");

            if (destroy)
            {
                sb.AppendLine($"{indent}    Destroy(emitterToStop.gameObject);");
            }
            else
            {
                sb.AppendLine($"{indent}    var psToStop = emitterToStop.GetComponent<ParticleSystem>();");
                sb.AppendLine($"{indent}    if (psToStop != null) psToStop.Stop();");
            }

            sb.AppendLine($"{indent}    _activeEmitters.Remove(\"{emitterId}\");");
            sb.AppendLine($"{indent}}}");
        }

        private void GeneratePlaySound(StringBuilder sb, Dictionary<string, object> p, string indent)
        {
            string soundId = ParameterHelper.GetParamString(p, "soundId");
            sb.AppendLine($"{indent}AudioManager.PlayStatic(\"{soundId}\");");
        }
    }
}
