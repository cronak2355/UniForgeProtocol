using UnityEngine;
using System.Collections;

public class Gen_e8aa3eba_165b_4488_9fd5_d9721e954cd5 : MonoBehaviour
{
    void Start()
    {
        return;
    }
    void Update()
    {
        transform.Rotate(0, 0, 100f * Time.deltaTime);
        Debug.Log("Rotating... " + transform.rotation.z);
    }
}
