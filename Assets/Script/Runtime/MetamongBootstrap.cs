using UnityEngine;

public class MetamongBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Debug.Log("MetamongBootstrap Init called");
        // SceneImportManager
        if (Object.FindAnyObjectByType<SceneImportManager>() == null)
        {
            Debug.Log("SIM");
            GameObject sim = new GameObject("SceneImportManager");
            sim.AddComponent<SceneImportManager>();
            Object.DontDestroyOnLoad(sim);
            Debug.Log("SIM");
        }

        // MainThreadDispatcher
        if (Object.FindAnyObjectByType<UnityMainThreadDispatcher>() == null)
        {
            Debug.Log("UMTD");
            GameObject mtd = new GameObject("MainThreadDispatcher");
            mtd.AddComponent<UnityMainThreadDispatcher>();
            Object.DontDestroyOnLoad(mtd);
            Debug.Log("UMTD");
        }
    }
}
