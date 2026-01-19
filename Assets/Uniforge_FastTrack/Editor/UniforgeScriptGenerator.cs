using System;
using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Uniforge.FastTrack.Editor
{
    /// <summary>
    /// v3: Refactored script generator that uses components array directly.
    /// No more events array - components is the single source of truth.
    /// </summary>
    public static class UniforgeScriptGenerator
    {
        private static string GeneratedFolderPath = "Assets/Uniforge_FastTrack/Generated";
        private static StringBuilder _cachedSb = new StringBuilder(8192);

        public static void Generate(EntityJSON entity)
        {
            // v3: Use components directly, no conversion needed
            var logicComponents = GetLogicComponents(entity);

            // Get special component types (playanimation, particle, etc.)
            var specialComponents = GetSpecialComponents(entity);

            if (logicComponents.Count == 0 && specialComponents.Count == 0 &&
                (entity.variables == null || entity.variables.Count == 0))
            {
                Debug.Log($"<color=gray>[ScriptGen]</color> Skipping '{entity.name}' - no logic, special components, or variables");
                return;
            }

            if (!Directory.Exists(GeneratedFolderPath))
            {
                Directory.CreateDirectory(GeneratedFolderPath);
            }

            string className = $"Gen_{entity.id.Replace("-", "_")}";

            _cachedSb.Clear();
            var sb = _cachedSb;

            // Header
            sb.AppendLine("#pragma warning disable 0414");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.SceneManagement;");
            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using Uniforge.FastTrack.Runtime;");
            sb.AppendLine();
            sb.AppendLine($"public class {className} : MonoBehaviour");
            sb.AppendLine("{");

            // User Variables
            GenerateVariableDeclarations(sb, entity.variables);

            // System Fields
            GenerateSystemFields(sb, logicComponents, entity.variables);

            // Awake
            GenerateAwakeMethod(sb);

            // Generate methods from components (including special components)
            GenerateMethodsFromComponents(sb, logicComponents, entity, specialComponents);

            // Helper Methods
            GenerateHelperMethods(sb, entity.variables);

            sb.AppendLine("}");

            // Write to file
            string filePath = Path.Combine(GeneratedFolderPath, $"{className}.cs");
            File.WriteAllText(filePath, sb.ToString());

            Debug.Log($"<color=green>[ScriptGen]</color> Generated: {className} ({logicComponents.Count} logic blocks)");
        }

        /// <summary>
        /// Get logic components from entity (supports both v3 and legacy formats)
        /// </summary>
        private static List<ComponentJSON> GetLogicComponents(EntityJSON entity)
        {
            // v3: Primary source is components array
            if (entity.components != null && entity.components.Count > 0)
            {
                return entity.components.Where(c => c.type == "Logic").ToList();
            }

            // Frontend unified logic format: { kind: "component", component: {...} }
            if (entity.logic != null && entity.logic.Count > 0)
            {
                Debug.Log($"<color=cyan>[ScriptGen]</color> Using unified logic format for '{entity.name}'");
                return ConvertLogicItemsToComponents(entity.logic);
            }

            // Legacy fallback: Convert events to components
            if (entity.events != null && entity.events.Count > 0)
            {
                Debug.Log($"<color=yellow>[ScriptGen]</color> Using legacy events for '{entity.name}'");
                return entity.events.Select(ConvertEventToComponent).ToList();
            }

            return new List<ComponentJSON>();
        }

        /// <summary>
        /// Get special components (playanimation, particle, render, etc.) from entity
        /// These are non-Logic components that need special handling
        /// </summary>
        private static List<ComponentJSON> GetSpecialComponents(EntityJSON entity)
        {
            var result = new List<ComponentJSON>();

            if (entity.components == null) return result;

            // Case-insensitive matching for component types
            foreach (var comp in entity.components)
            {
                if (comp == null || string.IsNullOrEmpty(comp.type)) continue;

                string typeLC = comp.type.ToLowerInvariant();

                // Check for special component types
                if (typeLC == "playanimation" || typeLC == "animation" ||
                    typeLC == "particle" || typeLC == "particleemitter" ||
                    typeLC == "render" || typeLC == "sprite")
                {
                    result.Add(comp);
                    Debug.Log($"<color=cyan>[ScriptGen]</color> Found special component: {comp.type}");
                }
            }

            return result;
        }

        /// <summary>
        /// Convert Frontend unified logic items to ComponentJSON list
        /// Logic items have format: { kind: "component", component: {...} }
        /// </summary>
        private static List<ComponentJSON> ConvertLogicItemsToComponents(List<LogicItemJSON> logicItems)
        {
            var result = new List<ComponentJSON>();

            foreach (var item in logicItems)
            {
                if (item == null) continue;

                // Check if this is a wrapper format: { kind: "component", component: {...} }
                if (item.kind == "component" && item.component != null)
                {
                    // Use the nested component directly
                    if (item.component.type == "Logic" || !string.IsNullOrEmpty(item.component.@event))
                    {
                        result.Add(item.component);
                    }
                    continue;
                }

                // Direct format (no wrapper)
                var comp = new ComponentJSON
                {
                    id = item.id,
                    type = item.type ?? "Logic",
                    @event = item.@event,
                    eventParams = item.eventParams,
                    conditions = item.conditions,
                    conditionLogic = item.conditionLogic ?? "AND",
                    actions = item.actions
                };

                if (!string.IsNullOrEmpty(comp.@event) || (comp.actions != null && comp.actions.Count > 0))
                {
                    result.Add(comp);
                }
            }

            return result.Where(c => c.type == "Logic" || !string.IsNullOrEmpty(c.@event)).ToList();
        }

        /// <summary>
        /// Convert legacy EventJSON to ComponentJSON
        /// </summary>
        private static ComponentJSON ConvertEventToComponent(EventJSON evt)
        {
            var comp = new ComponentJSON
            {
                id = evt.id,
                type = "Logic",
                @event = evt.trigger,
                eventParams = evt.triggerParams,
                conditions = evt.conditions,
                conditionLogic = evt.conditionLogic ?? "AND",
                elseActions = evt.elseActions
            };

            // Handle actions
            if (evt.actions != null && evt.actions.Count > 0)
            {
                comp.actions = evt.actions;
            }
            else if (!string.IsNullOrEmpty(evt.action) && evt.action != "None")
            {
                comp.actions = new List<ActionJSON>
                {
                    new ActionJSON
                    {
                        type = evt.action,
                        AdditionalData = evt.@params != null
                            ? new Dictionary<string, object>(evt.@params)
                            : new Dictionary<string, object>()
                    }
                };
            }

            return comp;
        }

        private static void GenerateVariableDeclarations(StringBuilder sb, List<VariableJSON> variables)
        {
            sb.AppendLine("    // === User Variables ===");
            if (variables != null)
            {
                foreach (var v in variables)
                {
                    string type = GetCSharpType(v.type);
                    string defaultValue = GetDefaultValue(v.type, v.value);
                    
                    // Primary field: Use Sanitized v.name (the original variable name)
                    string primaryFieldName = ParameterHelper.SanitizeName(v.name);
                    sb.AppendLine($"    public {type} {primaryFieldName} = {defaultValue};");
                    
                    // [FIX] Also generate ID-based alias if v.id exists and differs from name
                    // This supports legacy data where actions reference variables by ID (var_1)
                    if (!string.IsNullOrEmpty(v.id))
                    {
                        string idFieldName = ParameterHelper.SanitizeName(v.id);
                        // Only add alias if it's different from primary field name
                        if (idFieldName != primaryFieldName)
                        {
                            // Generate a property that references the main field
                            sb.AppendLine($"    private {type} {idFieldName} {{ get => {primaryFieldName}; set => {primaryFieldName} = value; }}");
                        }
                    }
                }
            }
            sb.AppendLine();
        }

        private static void GenerateSystemFields(StringBuilder sb, List<ComponentJSON> components, List<VariableJSON> userVariables)
        {
            sb.AppendLine("    // === System Fields ===");
            sb.AppendLine("    private Transform _transform;");
            sb.AppendLine("    private Animator _animator;");
            sb.AppendLine("    private Rigidbody2D _rigidbody;");
            sb.AppendLine("    private Camera _mainCamera;");
            sb.AppendLine();

            // Build set of user-defined variable names (case-insensitive)
            var userVarNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (userVariables != null)
            {
                foreach (var v in userVariables)
                {
                    userVarNames.Add(ParameterHelper.SanitizeName(v.name));
                }
            }

            // Only add hp/maxHp if not already defined by user
            if (!userVarNames.Contains("hp"))
            {
                sb.AppendLine("    public float hp = 100f;");
            }
            if (!userVarNames.Contains("maxHp"))
            {
                sb.AppendLine("    public float maxHp = 100f;");
            }

            sb.AppendLine("    private float _lastAttackTime = -999f;");
            sb.AppendLine("    private bool _isGrounded = true;");
            sb.AppendLine("    [SerializeField] private float _groundCheckDistance = 0.1f;");
            sb.AppendLine("    [SerializeField] private LayerMask _groundLayer = -1;");
            sb.AppendLine("    private Dictionary<string, bool> _signalFlags = new Dictionary<string, bool>();");
            sb.AppendLine("    private Dictionary<string, float> _cooldowns = new Dictionary<string, float>();");
            sb.AppendLine("    private Dictionary<string, Transform> _activeEmitters = new Dictionary<string, Transform>();");
            sb.AppendLine();

            // Timer fields for OnTimer triggers
            var timerComponents = components.Where(c => c.@event == "OnTimer").ToList();
            if (timerComponents.Count > 0)
            {
                sb.AppendLine("    // Timer System");
                for (int i = 0; i < timerComponents.Count; i++)
                {
                    var comp = timerComponents[i];
                    float interval = GetEventParamFloat(comp.eventParams, "interval", 1000f) / 1000f;
                    sb.AppendLine($"    private float _timer{i} = 0f;");
                    sb.AppendLine($"    private float _timerInterval{i} = {interval}f;");
                }
                sb.AppendLine();
            }
        }

        private static void GenerateAwakeMethod(StringBuilder sb)
        {
            sb.AppendLine("    void Awake()");
            sb.AppendLine("    {");
            sb.AppendLine("        Uniforge.FastTrack.Runtime.UniforgeRuntime.EnsureExists();");
            sb.AppendLine("        _transform = transform;");
            sb.AppendLine("        _animator = GetComponent<Animator>();");
            sb.AppendLine("        if (_animator == null) _animator = gameObject.AddComponent<Animator>();");
            sb.AppendLine("        _rigidbody = GetComponent<Rigidbody2D>();");
            sb.AppendLine("        _mainCamera = Camera.main;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private static void GenerateMethodsFromComponents(StringBuilder sb, List<ComponentJSON> components, EntityJSON entity, List<ComponentJSON> specialComponents = null)
        {
            // Group by trigger
            var groups = components.GroupBy(c => c.@event ?? "OnUpdate").ToDictionary(g => g.Key, g => g.ToList());
            var generatedMethods = new HashSet<string>();

            // Ensure Start method exists if we have special components
            if (specialComponents != null && specialComponents.Count > 0)
            {
                if (!groups.ContainsKey("OnStart"))
                {
                    groups["OnStart"] = new List<ComponentJSON>();
                }
            }

            // Process each trigger group
            foreach (var kvp in groups)
            {
                string trigger = kvp.Key;
                var comps = kvp.Value;

                switch (trigger)
                {
                    case "OnStart":
                        GenerateStartMethod(sb, comps, entity, specialComponents);
                        generatedMethods.Add("Start");
                        break;

                    case "OnUpdate":
                        GenerateUpdateMethod(sb, comps, entity, groups);
                        generatedMethods.Add("Update");
                        break;

                    case "OnKeyDown":
                    case "OnKeyUp":
                    case "OnKeyPress":
                        // Key events go into Update
                        if (!generatedMethods.Contains("Update"))
                        {
                            GenerateUpdateMethod(sb, new List<ComponentJSON>(), entity, groups);
                            generatedMethods.Add("Update");
                        }
                        break;

                    case "OnTimer":
                        // Timer events also go into Update
                        if (!generatedMethods.Contains("Update"))
                        {
                            GenerateUpdateMethod(sb, new List<ComponentJSON>(), entity, groups);
                            generatedMethods.Add("Update");
                        }
                        break;

                    case "OnCollisionEnter":
                    case "OnCollision":
                        GenerateCollisionEnterMethod(sb, comps, entity);
                        break;

                    case "OnCollisionExit":
                        GenerateCollisionExitMethod(sb, comps, entity);
                        break;

                    case "OnTriggerEnter":
                        GenerateTriggerEnterMethod(sb, comps, entity);
                        break;

                    case "OnTriggerExit":
                        GenerateTriggerExitMethod(sb, comps, entity);
                        break;

                    case "OnClick":
                        GenerateClickMethod(sb, comps, entity);
                        break;

                    case "OnDestroy":
                        GenerateDestroyMethod(sb, comps, entity);
                        break;

                    default:
                        sb.AppendLine($"    // TODO: Unhandled trigger: {trigger}");
                        break;
                }
            }
        }

        private static void GenerateStartMethod(StringBuilder sb, List<ComponentJSON> components, EntityJSON entity, List<ComponentJSON> specialComponents = null)
        {
            // Separate components with Wait from those without
            var normalComps = new List<ComponentJSON>();
            var waitComps = new List<ComponentJSON>();

            foreach (var comp in components)
            {
                if (ActionCodeGenerator.ComponentHasWait(comp))
                    waitComps.Add(comp);
                else
                    normalComps.Add(comp);
            }

            sb.AppendLine("    void Start()");
            sb.AppendLine("    {");

            // Generate special component initialization code (playanimation, particle, etc.)
            if (specialComponents != null)
            {
                foreach (var comp in specialComponents)
                {
                    GenerateSpecialComponentCode(sb, comp, "        ", entity);
                }
            }

            // Generate normal components inline
            foreach (var comp in normalComps)
            {
                GenerateComponentCode(sb, comp, "        ", entity);
            }

            // Start coroutines for components with Wait
            for (int i = 0; i < waitComps.Count; i++)
            {
                sb.AppendLine($"        StartCoroutine(Start_Coroutine_{i}());");
            }

            sb.AppendLine("    }");
            sb.AppendLine();

            // Generate coroutine methods for Wait components
            for (int i = 0; i < waitComps.Count; i++)
            {
                sb.AppendLine($"    private System.Collections.IEnumerator Start_Coroutine_{i}()");
                sb.AppendLine("    {");
                GenerateComponentCode(sb, waitComps[i], "        ", entity);
                sb.AppendLine("        yield break;");
                sb.AppendLine("    }");
                sb.AppendLine();
            }
        }

        /// <summary>
        /// Generate code for special component types (playanimation, particle, etc.)
        /// </summary>
        private static void GenerateSpecialComponentCode(StringBuilder sb, ComponentJSON comp, string indent, EntityJSON entity)
        {
            if (comp == null || string.IsNullOrEmpty(comp.type)) return;

            string typeLC = comp.type.ToLowerInvariant();
            // Use GetAllParams to merge eventParams and AdditionalData
            var p = comp.GetAllParams();

            Debug.Log($"[ScriptGen] GenerateSpecialComponentCode: type={comp.type}, params=[{string.Join(", ", p.Select(kv => $"{kv.Key}={kv.Value}"))}]");

            switch (typeLC)
            {
                case "playanimation":
                case "animation":
                    {
                        // PlayAnimation component - auto-play animation on start
                        string animName = GetEventParamString(p, "animationName", "animation", "name", "state");
                        bool loop = GetEventParamBool(p, "loop", true);

                        if (!string.IsNullOrEmpty(animName))
                        {
                            sb.AppendLine($"{indent}// PlayAnimation component: {animName} (loop={loop})");
                            sb.AppendLine($"{indent}if (_animator != null)");
                            sb.AppendLine($"{indent}{{");
                            sb.AppendLine($"{indent}    _animator.Play(\"{animName}\");");
                            sb.AppendLine($"{indent}    Debug.Log(\"[PlayAnimation] Auto-playing animation: {animName}\");");
                            sb.AppendLine($"{indent}}}");
                        }
                        else
                        {
                            sb.AppendLine($"{indent}// PlayAnimation component: No animation name specified");
                        }
                        break;
                    }

                case "particle":
                case "particleemitter":
                    {
                        // Particle component - start particle emitter on entity
                        string particleSystemId = GetEventParamString(p, "particleSystemId", "systemId", "preset", "effect");
                        string emitterId = GetEventParamString(p, "emitterId", "id");
                        float offsetX = GetEventParamFloat(p, "offsetX", 0) / 100f;
                        float offsetY = GetEventParamFloat(p, "offsetY", 0) / 100f;
                        bool attachToEntity = GetEventParamBool(p, "attachToEntity", true);
                        bool playOnStart = GetEventParamBool(p, "playOnStart", true);

                        if (string.IsNullOrEmpty(emitterId))
                        {
                            emitterId = $"emitter_{entity.id}_{comp.id ?? System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
                        }

                        if (playOnStart && !string.IsNullOrEmpty(particleSystemId))
                        {
                            sb.AppendLine($"{indent}// Particle component: {particleSystemId}");
                            sb.AppendLine($"{indent}{{");
                            sb.AppendLine($"{indent}    var emitter = ParticleEmitterManager.StartEmitter(");
                            sb.AppendLine($"{indent}        \"{emitterId}\",");
                            sb.AppendLine($"{indent}        \"{particleSystemId}\",");
                            sb.AppendLine($"{indent}        {(attachToEntity ? "_transform" : "null")},");
                            sb.AppendLine($"{indent}        new Vector3({offsetX}f, {-offsetY}f, 0)");
                            sb.AppendLine($"{indent}    );");
                            sb.AppendLine($"{indent}    if (emitter != null)");
                            sb.AppendLine($"{indent}    {{");
                            sb.AppendLine($"{indent}        _activeEmitters[\"{emitterId}\"] = emitter;");
                            sb.AppendLine($"{indent}        Debug.Log(\"[Particle] Started emitter: {emitterId} with system: {particleSystemId}\");");
                            sb.AppendLine($"{indent}    }}");
                            sb.AppendLine($"{indent}}}");
                        }
                        else if (!string.IsNullOrEmpty(particleSystemId))
                        {
                            sb.AppendLine($"{indent}// Particle component configured but playOnStart=false: {particleSystemId}");
                        }
                        break;
                    }

                case "render":
                case "sprite":
                    {
                        // Render/Sprite component - typically handled by importer, but can have special settings
                        int sortingOrder = (int)GetEventParamFloat(p, "sortingOrder", 0);
                        string sortingLayer = GetEventParamString(p, "sortingLayer", "layer");

                        if (sortingOrder != 0 || !string.IsNullOrEmpty(sortingLayer))
                        {
                            sb.AppendLine($"{indent}// Render component settings");
                            sb.AppendLine($"{indent}var sr = GetComponent<SpriteRenderer>();");
                            sb.AppendLine($"{indent}if (sr != null)");
                            sb.AppendLine($"{indent}{{");
                            if (sortingOrder != 0)
                            {
                                sb.AppendLine($"{indent}    sr.sortingOrder = {sortingOrder};");
                            }
                            if (!string.IsNullOrEmpty(sortingLayer))
                            {
                                sb.AppendLine($"{indent}    sr.sortingLayerName = \"{sortingLayer}\";");
                            }
                            sb.AppendLine($"{indent}}}");
                        }
                        break;
                    }

                default:
                    sb.AppendLine($"{indent}// Unknown special component type: {comp.type}");
                    break;
            }
        }

        private static void GenerateUpdateMethod(StringBuilder sb, List<ComponentJSON> updateComponents, EntityJSON entity, Dictionary<string, List<ComponentJSON>> allGroups)
        {
            sb.AppendLine("    void Update()");
            sb.AppendLine("    {");
            sb.AppendLine("        CheckGrounded();");
            sb.AppendLine();

            // Timer events
            if (allGroups.ContainsKey("OnTimer"))
            {
                var timerComps = allGroups["OnTimer"];
                for (int i = 0; i < timerComps.Count; i++)
                {
                    var comp = timerComps[i];
                    bool repeat = GetEventParamBool(comp.eventParams, "repeat", true);

                    sb.AppendLine($"        _timer{i} += Time.deltaTime;");
                    sb.AppendLine($"        if (_timer{i} >= _timerInterval{i})");
                    sb.AppendLine($"        {{");
                    sb.AppendLine(repeat ? $"            _timer{i} = 0f;" : $"            _timer{i} = float.MaxValue;");
                    GenerateComponentCode(sb, comp, "            ", entity);
                    sb.AppendLine($"        }}");
                }
            }

            // Key events
            foreach (var triggerType in new[] { "OnKeyDown", "OnKeyUp", "OnKeyPress" })
            {
                if (!allGroups.ContainsKey(triggerType)) continue;

                foreach (var comp in allGroups[triggerType])
                {
                    string key = GetEventParamString(comp.eventParams, "key");
                    string unityKey = ConditionCodeGenerator.ConvertWebKeyToUnity(key);
                    string inputMethod = triggerType switch
                    {
                        "OnKeyDown" => "GetKeyDown",
                        "OnKeyUp" => "GetKeyUp",
                        "OnKeyPress" => "GetKey",
                        _ => "GetKeyDown"
                    };

                    sb.AppendLine($"        if (Input.{inputMethod}(KeyCode.{unityKey}))");
                    sb.AppendLine($"        {{");
                    GenerateComponentCode(sb, comp, "            ", entity);
                    sb.AppendLine($"        }}");
                }
            }

            // Regular Update components
            foreach (var comp in updateComponents)
            {
                GenerateComponentCode(sb, comp, "        ", entity);
            }

            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private static void GenerateCollisionEnterMethod(StringBuilder sb, List<ComponentJSON> components, EntityJSON entity)
        {
            sb.AppendLine("    void OnCollisionEnter2D(Collision2D collision)");
            sb.AppendLine("    {");
            sb.AppendLine("        _isGrounded = true;");
            foreach (var comp in components)
            {
                GenerateCollisionComponentCode(sb, comp, "        ", entity, "collision.gameObject");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private static void GenerateCollisionExitMethod(StringBuilder sb, List<ComponentJSON> components, EntityJSON entity)
        {
            sb.AppendLine("    void OnCollisionExit2D(Collision2D collision)");
            sb.AppendLine("    {");
            foreach (var comp in components)
            {
                GenerateCollisionComponentCode(sb, comp, "        ", entity, "collision.gameObject");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private static void GenerateTriggerEnterMethod(StringBuilder sb, List<ComponentJSON> components, EntityJSON entity)
        {
            sb.AppendLine("    void OnTriggerEnter2D(Collider2D other)");
            sb.AppendLine("    {");
            foreach (var comp in components)
            {
                GenerateCollisionComponentCode(sb, comp, "        ", entity, "other.gameObject");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private static void GenerateTriggerExitMethod(StringBuilder sb, List<ComponentJSON> components, EntityJSON entity)
        {
            sb.AppendLine("    void OnTriggerExit2D(Collider2D other)");
            sb.AppendLine("    {");
            foreach (var comp in components)
            {
                GenerateCollisionComponentCode(sb, comp, "        ", entity, "other.gameObject");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private static void GenerateClickMethod(StringBuilder sb, List<ComponentJSON> components, EntityJSON entity)
        {
            sb.AppendLine("    void OnMouseDown()");
            sb.AppendLine("    {");
            foreach (var comp in components)
            {
                GenerateComponentCode(sb, comp, "        ", entity);
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private static void GenerateDestroyMethod(StringBuilder sb, List<ComponentJSON> components, EntityJSON entity)
        {
            sb.AppendLine("    void OnDestroy()");
            sb.AppendLine("    {");
            foreach (var comp in components)
            {
                GenerateComponentCode(sb, comp, "        ", entity);
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        /// <summary>
        /// Generate code for a component with optional withTag filter
        /// </summary>
        private static void GenerateCollisionComponentCode(StringBuilder sb, ComponentJSON comp, string indent, EntityJSON entity, string otherObj)
        {
            string withTag = GetEventParamString(comp.eventParams, "withTag", "withRole");

            if (!string.IsNullOrEmpty(withTag))
            {
                sb.AppendLine($"{indent}var otherEntity = {otherObj}.GetComponent<UniforgeEntity>();");
                sb.AppendLine($"{indent}if (otherEntity != null && otherEntity.Role == \"{withTag}\")");
                sb.AppendLine($"{indent}{{");
                GenerateComponentCode(sb, comp, indent + "    ", entity);
                sb.AppendLine($"{indent}}}");
            }
            else
            {
                GenerateComponentCode(sb, comp, indent, entity);
            }
        }

        /// <summary>
        /// Generate code for a single component (conditions + actions)
        /// </summary>
        private static void GenerateComponentCode(StringBuilder sb, ComponentJSON comp, string indent, EntityJSON entity)
        {
            bool hasConditions = comp.conditions != null && comp.conditions.Count > 0;
            string innerIndent = indent;

            // Conditions wrapper
            if (hasConditions)
            {
                string condCode = ConditionCodeGenerator.GenerateConditionsCode(comp.conditions, comp.conditionLogic);
                sb.AppendLine($"{indent}if ({condCode})");
                sb.AppendLine($"{indent}{{");
                innerIndent = indent + "    ";
            }

            // Actions
            if (comp.actions != null)
            {
                foreach (var action in comp.actions)
                {
                    ActionCodeGenerator.GenerateActionCode(sb, action.type, action.GetAllParams(), innerIndent, entity, null);
                }
            }

            // Else actions
            if (hasConditions && comp.elseActions != null && comp.elseActions.Count > 0)
            {
                sb.AppendLine($"{indent}}}");
                sb.AppendLine($"{indent}else");
                sb.AppendLine($"{indent}{{");
                foreach (var action in comp.elseActions)
                {
                    ActionCodeGenerator.GenerateActionCode(sb, action.type, action.GetAllParams(), innerIndent, entity, null);
                }
            }

            if (hasConditions)
            {
                sb.AppendLine($"{indent}}}");
            }
        }

        private static void GenerateHelperMethods(StringBuilder sb, List<VariableJSON> userVariables)
        {
            // Find user-defined hp variable name and type (case-insensitive match)
            string hpVarName = "hp"; // default
            string hpVarType = "float"; // default
            if (userVariables != null)
            {
                foreach (var v in userVariables)
                {
                    string sanitized = ParameterHelper.SanitizeName(v.name);
                    if (sanitized.Equals("hp", StringComparison.OrdinalIgnoreCase))
                    {
                        hpVarName = sanitized; // Use the actual user-defined name (e.g., "Hp", "HP")
                        hpVarType = GetCSharpType(v.type); // Convert to C# type
                        break;
                    }
                }
            }

            // Generate damage subtraction with proper type casting
            string damageExpr = hpVarType == "int" ? "(int)damage" : "damage";
            string zeroValue = hpVarType == "int" ? "0" : "0f";

            sb.AppendLine("    // === Helper Methods ===");
            sb.AppendLine();
            sb.AppendLine("    private Vector3 GetMouseWorldPosition()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_mainCamera == null) _mainCamera = Camera.main;");
            sb.AppendLine("        if (_mainCamera == null) return Vector3.zero;");
            sb.AppendLine("        return _mainCamera.ScreenToWorldPoint(Input.mousePosition);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private Vector2 GetMouseRelativePosition()");
            sb.AppendLine("    {");
            sb.AppendLine("        Vector3 mouseWorld = GetMouseWorldPosition();");
            sb.AppendLine("        return new Vector2(mouseWorld.x - _transform.position.x, mouseWorld.y - _transform.position.y);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private void CheckGrounded()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_rigidbody == null) return;");
            sb.AppendLine("        var hit = Physics2D.Raycast(_transform.position, Vector2.down, _groundCheckDistance, _groundLayer);");
            sb.AppendLine("        _isGrounded = hit.collider != null;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private bool IsCooldownReady(string id) => !_cooldowns.ContainsKey(id) || Time.time >= _cooldowns[id];");
            sb.AppendLine("    private void StartCooldown(string id, float duration) => _cooldowns[id] = Time.time + duration;");
            sb.AppendLine();
            sb.AppendLine("    private void OnDeath() { Debug.Log($\"[{gameObject.name}] Died\"); }");
            sb.AppendLine($"    public void OnTakeDamage(float damage) {{ {hpVarName} -= {damageExpr}; if ({hpVarName} <= 0) {{ {hpVarName} = {zeroValue}; OnDeath(); }} }}");
            sb.AppendLine("    public void TakeDamage(float damage) => OnTakeDamage(damage);");
        }

        #region Utility Methods

        private static string GetCSharpType(string type)
        {
            if (string.IsNullOrEmpty(type)) return "object";

            // Normalize type name (case-insensitive)
            string normalized = type.ToLowerInvariant().Trim();

            return normalized switch
            {
                "int" or "integer" or "number" => "int",
                "float" or "double" or "decimal" => "float",
                "bool" or "boolean" => "bool",
                "string" or "text" => "string",
                "vector2" or "vec2" => "Vector2",
                "vector3" or "vec3" => "Vector3",
                "color" or "colour" => "Color",
                _ => "object"
            };
        }

        private static string GetDefaultValue(string type, object val)
        {
            string csharpType = GetCSharpType(type);

            // Handle null or empty value
            if (val == null || (val is string s && string.IsNullOrEmpty(s)))
            {
                return csharpType switch
                {
                    "int" => "0",
                    "float" => "0f",
                    "bool" => "false",
                    "Vector2" => "Vector2.zero",
                    "Vector3" => "Vector3.zero",
                    "Color" => "Color.white",
                    "string" => "\"\"",
                    _ => "null"
                };
            }

            // Unwrap JValue to get actual value
            if (val is Newtonsoft.Json.Linq.JValue jVal)
            {
                val = jVal.Value;
            }

            try
            {
                switch (csharpType)
                {
                    case "string":
                        // Escape special characters in string
                        string strVal = val.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"");
                        return $"\"{strVal}\"";

                    case "int":
                        // Handle various numeric formats
                        if (val is long l) return l.ToString();
                        if (val is int i) return i.ToString();
                        if (val is double d) return ((int)d).ToString();
                        if (val is float f) return ((int)f).ToString();
                        if (int.TryParse(val.ToString(), out int parsed)) return parsed.ToString();
                        if (double.TryParse(val.ToString(), out double dParsed)) return ((int)dParsed).ToString();
                        return "0";

                    case "float":
                        // Ensure proper float format
                        if (val is double dVal) return $"{dVal}f";
                        if (val is float fVal) return $"{fVal}f";
                        if (val is int iVal) return $"{iVal}f";
                        if (val is long lVal) return $"{lVal}f";
                        if (double.TryParse(val.ToString(), out double parsedFloat))
                            return $"{parsedFloat}f";
                        return "0f";

                    case "bool":
                        if (val is bool b) return b ? "true" : "false";
                        string boolStr = val.ToString().ToLowerInvariant();
                        return (boolStr == "true" || boolStr == "1" || boolStr == "yes") ? "true" : "false";

                    case "Vector2":
                        return ParseVector2(val);

                    case "Vector3":
                        return ParseVector3(val);

                    case "Color":
                        return ParseColor(val);

                    default:
                        // For object type, try to infer and format properly
                        if (val is string) return $"\"{val}\"";
                        return val.ToString();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ScriptGen] Failed to parse value '{val}' as {csharpType}: {ex.Message}");
                return csharpType switch
                {
                    "int" => "0",
                    "float" => "0f",
                    "bool" => "false",
                    "string" => "\"\"",
                    _ => "null"
                };
            }
        }

        private static string ParseVector2(object val)
        {
            try
            {
                if (val is Newtonsoft.Json.Linq.JObject jObj)
                {
                    float x = jObj["x"]?.ToObject<float>() ?? 0f;
                    float y = jObj["y"]?.ToObject<float>() ?? 0f;
                    return $"new Vector2({x}f, {y}f)";
                }
                if (val is Newtonsoft.Json.Linq.JArray jArr && jArr.Count >= 2)
                {
                    float x = jArr[0]?.ToObject<float>() ?? 0f;
                    float y = jArr[1]?.ToObject<float>() ?? 0f;
                    return $"new Vector2({x}f, {y}f)";
                }
            }
            catch { }
            return "Vector2.zero";
        }

        private static string ParseVector3(object val)
        {
            try
            {
                if (val is Newtonsoft.Json.Linq.JObject jObj)
                {
                    float x = jObj["x"]?.ToObject<float>() ?? 0f;
                    float y = jObj["y"]?.ToObject<float>() ?? 0f;
                    float z = jObj["z"]?.ToObject<float>() ?? 0f;
                    return $"new Vector3({x}f, {y}f, {z}f)";
                }
                if (val is Newtonsoft.Json.Linq.JArray jArr && jArr.Count >= 3)
                {
                    float x = jArr[0]?.ToObject<float>() ?? 0f;
                    float y = jArr[1]?.ToObject<float>() ?? 0f;
                    float z = jArr[2]?.ToObject<float>() ?? 0f;
                    return $"new Vector3({x}f, {y}f, {z}f)";
                }
            }
            catch { }
            return "Vector3.zero";
        }

        private static string ParseColor(object val)
        {
            try
            {
                if (val is string colorStr)
                {
                    // Handle hex color (#RRGGBB or #RRGGBBAA)
                    if (colorStr.StartsWith("#"))
                    {
                        colorStr = colorStr.Substring(1);
                        if (colorStr.Length >= 6)
                        {
                            float r = int.Parse(colorStr.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                            float g = int.Parse(colorStr.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                            float b = int.Parse(colorStr.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                            float a = colorStr.Length >= 8
                                ? int.Parse(colorStr.Substring(6, 2), System.Globalization.NumberStyles.HexNumber) / 255f
                                : 1f;
                            return $"new Color({r}f, {g}f, {b}f, {a}f)";
                        }
                    }
                    // Handle named colors
                    return colorStr.ToLowerInvariant() switch
                    {
                        "red" => "Color.red",
                        "green" => "Color.green",
                        "blue" => "Color.blue",
                        "white" => "Color.white",
                        "black" => "Color.black",
                        "yellow" => "Color.yellow",
                        "cyan" => "Color.cyan",
                        "magenta" => "Color.magenta",
                        "gray" or "grey" => "Color.gray",
                        "clear" => "Color.clear",
                        _ => "Color.white"
                    };
                }
                if (val is Newtonsoft.Json.Linq.JObject jObj)
                {
                    float r = jObj["r"]?.ToObject<float>() ?? 1f;
                    float g = jObj["g"]?.ToObject<float>() ?? 1f;
                    float b = jObj["b"]?.ToObject<float>() ?? 1f;
                    float a = jObj["a"]?.ToObject<float>() ?? 1f;
                    return $"new Color({r}f, {g}f, {b}f, {a}f)";
                }
            }
            catch { }
            return "Color.white";
        }

        private static string GetEventParamString(Dictionary<string, object> p, params string[] keys)
        {
            if (p == null) return "";
            foreach (var key in keys)
                if (p.ContainsKey(key) && p[key] != null)
                    return p[key].ToString();
            return "";
        }

        private static float GetEventParamFloat(Dictionary<string, object> p, string key, float def)
        {
            if (p == null) return def;

            // Try primary key first
            if (p.TryGetValue(key, out var value) && value != null)
            {
                return float.TryParse(value.ToString(), out float result) ? result : def;
            }

            // Try aliases for common parameters
            string[] aliases = key switch
            {
                "interval" => new[] { "time", "delay", "period", "duration", "ms" },
                "delay" => new[] { "interval", "time", "waitTime" },
                _ => Array.Empty<string>()
            };

            foreach (var alias in aliases)
            {
                if (p.TryGetValue(alias, out value) && value != null)
                {
                    return float.TryParse(value.ToString(), out float result) ? result : def;
                }
            }

            return def;
        }

        private static bool GetEventParamBool(Dictionary<string, object> p, string key, bool def)
        {
            if (p == null) return def;

            // Try primary key first
            if (p.TryGetValue(key, out var value) && value != null)
            {
                return bool.TryParse(value.ToString(), out bool result) ? result : def;
            }

            // Try aliases for common parameters
            string[] aliases = key switch
            {
                "repeat" => new[] { "loop", "repeating", "recurring" },
                _ => Array.Empty<string>()
            };

            foreach (var alias in aliases)
            {
                if (p.TryGetValue(alias, out value) && value != null)
                {
                    return bool.TryParse(value.ToString(), out bool result) ? result : def;
                }
            }

            return def;
        }

        #endregion

        #region Public Wrappers

        public static void AttachScriptsToEntities() => ScriptAttacher.AttachScriptsToEntities();
        public static void AttachScriptsFromJson() => ScriptAttacher.AttachScriptsFromJson();
        public static void AttachScript(GameObject go, string entityId) => ScriptAttacher.AttachScript(go, entityId);

        #endregion
    }
}
