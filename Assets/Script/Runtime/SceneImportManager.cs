using UnityEngine;
using System.IO;
using Newtonsoft.Json;

public class SceneImportManager : MonoBehaviour
{
    [Header("JSON File Name")]
    public string jsonFileName = "scene.json";

    [Header("Prefab Root Path")]
    public string prefabPath = "Prefabs"; // Resources/Prefabs/

    [ContextMenu("Load Scene From JSON")]
    public void SceneFromJson()
    {
        LoadSceneFromJson();
        Debug.Log("[SceneImport] Scene loaded");
    }

    void LoadSceneFromJson()
    {
        string path = Path.Combine(Application.streamingAssetsPath, jsonFileName);

        if (!File.Exists(path))
        {
            Debug.LogError($"[SceneImport] JSON not found: {path}");
            return;
        }

        string json = File.ReadAllText(path);
        SceneDTO scene = JsonConvert.DeserializeObject<SceneDTO>(json);

        if (scene == null)
        {
            Debug.LogError("[SceneImport] SceneDTO deserialize failed");
            return;
        }

        Debug.Log($"[SceneImport] Load Scene: {scene.sceneId}");

        foreach (var entity in scene.entities)
        {
            CreateEntity(entity);
        }
    }

    void CreateEntity(EntityDTO entity)
    {
        GameObject go = new GameObject(entity.name);

        // 1️⃣ 위치 설정 (JSON 좌표 그대로 사용)
        Vector3 position = ConvertPosition(entity.x, entity.y);
        go.transform.position = position;

        Debug.Log($"[SceneImport] Spawn Entity: {entity.id} at {position}");

        // 2️⃣ 프리팹 로드 (id 기준)
        if (!string.IsNullOrEmpty(entity.id))
        {
            GameObject prefab = Resources.Load<GameObject>($"{prefabPath}/{entity.id}");
            if (prefab != null)
            {
                Instantiate(prefab, go.transform);
            }
            else
            {
                Debug.LogWarning($"[SceneImport] Prefab not found: {entity.id}");
            }
        }

        // 3️⃣ 변수 컴포넌트
        if (entity.variables != null && entity.variables.Count > 0)
        {
            CreateVariables(go, entity.variables);
        }

        // 4️⃣ 이벤트 컴포넌트
        if (entity.events != null && entity.events.Count > 0)
        {
            var events = go.AddComponent<RuntimeEvents>();
            events.Initialize(entity.events, go);
        }
    }

    void CreateVariables(GameObject go, System.Collections.Generic.List<VariableDTO> vars)
    {
        var container = go.AddComponent<RuntimeVariables>();

        foreach (var dto in vars)
        {
            VariableSO so = VariableSOFactory.Create(dto);
            if (so != null)
            {
                container.AddVariable(so);
            }
        }
    }

    Vector3 ConvertPosition(float x, float y)
    {
        // JSON 좌표를 Unity 월드 좌표로 그대로 사용
        return new Vector3(x, y, 0f);
    }
}
