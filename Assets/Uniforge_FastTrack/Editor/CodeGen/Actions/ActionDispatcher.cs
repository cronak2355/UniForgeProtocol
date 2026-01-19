using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using Uniforge.FastTrack.Editor;

namespace Uniforge.FastTrack.Editor.CodeGen.Actions
{
    /// <summary>
    /// Central dispatcher for action code generation.
    /// Delegates to specialized action generators based on action type.
    /// </summary>
    public static class ActionDispatcher
    {
        private static readonly Dictionary<string, IActionGenerator> _generators = new Dictionary<string, IActionGenerator>();
        private static bool _initialized = false;

        /// <summary>
        /// Initializes all action generators.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_initialized) return;

            RegisterGenerator(new MovementActions());
            RegisterGenerator(new CombatActions());
            RegisterGenerator(new VisualActions());
            RegisterGenerator(new FlowActions());

            _initialized = true;
        }

        /// <summary>
        /// Registers an action generator.
        /// </summary>
        private static void RegisterGenerator(IActionGenerator generator)
        {
            foreach (var action in generator.SupportedActions)
            {
                _generators[action] = generator;
            }
        }

        /// <summary>
        /// Generates code for a single action.
        /// </summary>
        public static void GenerateActionCode(
            StringBuilder sb,
            string action,
            Dictionary<string, object> p,
            string indent,
            EntityJSON entity,
            System.Action<StringBuilder, NodeJSON, ModuleJSON, string, EntityJSON> traverseGraphCallback = null)
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(action)) return;
            p = p ?? new Dictionary<string, object>();

            if (_generators.TryGetValue(action, out var generator))
            {
                generator.Generate(sb, action, p, indent, entity, traverseGraphCallback);
            }
            else
            {
                sb.AppendLine($"{indent}// [WARNING] Unknown action: {action}");
                sb.AppendLine($"{indent}Debug.LogWarning(\"[Uniforge] Unknown action: {action}\");");
            }
        }

        /// <summary>
        /// Check if a list of actions contains any Wait action (requires Coroutine).
        /// </summary>
        public static bool HasWaitAction(List<ActionJSON> actions)
        {
            if (actions == null) return false;

            foreach (var action in actions)
            {
                if (action.type == "Wait") return true;

                var allParams = action.GetAllParams();
                if (action.type == "If")
                {
                    if (allParams.TryGetValue("thenActions", out var thenObj) && thenObj is JArray thenArr)
                    {
                        var thenActions = thenArr.ToObject<List<ActionJSON>>();
                        if (HasWaitAction(thenActions)) return true;
                    }
                    if (allParams.TryGetValue("elseActions", out var elseObj) && elseObj is JArray elseArr)
                    {
                        var elseActions = elseArr.ToObject<List<ActionJSON>>();
                        if (HasWaitAction(elseActions)) return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Check if a component contains Wait action.
        /// </summary>
        public static bool ComponentHasWait(ComponentJSON comp)
        {
            if (comp?.actions == null) return false;
            return HasWaitAction(comp.actions);
        }
    }
}
