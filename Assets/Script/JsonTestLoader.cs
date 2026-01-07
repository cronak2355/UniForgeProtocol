using UnityEngine;
using System.IO;
using Newtonsoft.Json;

public class JsonTestLoader : MonoBehaviour
{
    void Start()
    {
        string path = Path.Combine(Application.streamingAssetsPath,
            "test_map.json"
        );

        string json = File.ReadAllText(path);

    }
}
