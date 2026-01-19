using System.Collections.Generic;
using System.Text;
using Uniforge.FastTrack.Editor;

namespace Uniforge.FastTrack.Editor.CodeGen.Actions
{
    /// <summary>
    /// Interface for action code generators.
    /// </summary>
    public interface IActionGenerator
    {
        /// <summary>
        /// Returns the list of action types this generator handles.
        /// </summary>
        string[] SupportedActions { get; }

        /// <summary>
        /// Generates code for the specified action.
        /// </summary>
        void Generate(
            StringBuilder sb,
            string action,
            Dictionary<string, object> p,
            string indent,
            EntityJSON entity,
            System.Action<StringBuilder, NodeJSON, ModuleJSON, string, EntityJSON> traverseGraphCallback = null);
    }
}
