using System.Collections.Generic;
using System.Text;

namespace Uniforge.FastTrack.Editor
{
    /// <summary>
    /// Legacy action code generator - delegates to new modular ActionDispatcher.
    /// Maintained for backwards compatibility.
    /// </summary>
    public static class ActionCodeGenerator
    {
        /// <summary>
        /// Generates code for a single action.
        /// Delegates to CodeGen.Actions.ActionDispatcher.
        /// </summary>
        public static void GenerateActionCode(
            StringBuilder sb,
            string action,
            Dictionary<string, object> p,
            string indent,
            EntityJSON entity,
            System.Action<StringBuilder, NodeJSON, ModuleJSON, string, EntityJSON> traverseGraphCallback = null)
        {
            CodeGen.Actions.ActionDispatcher.GenerateActionCode(sb, action, p, indent, entity, traverseGraphCallback);
        }

        /// <summary>
        /// Check if a list of actions contains any Wait action (requires Coroutine).
        /// </summary>
        public static bool HasWaitAction(List<ActionJSON> actions)
        {
            return CodeGen.Actions.ActionDispatcher.HasWaitAction(actions);
        }

        /// <summary>
        /// Check if a component contains Wait action.
        /// </summary>
        public static bool ComponentHasWait(ComponentJSON comp)
        {
            return CodeGen.Actions.ActionDispatcher.ComponentHasWait(comp);
        }
    }
}
