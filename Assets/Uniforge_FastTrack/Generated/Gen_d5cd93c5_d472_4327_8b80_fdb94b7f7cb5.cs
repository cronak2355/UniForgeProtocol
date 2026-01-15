using UnityEngine;
using System.Collections;

public class Gen_d5cd93c5_d472_4327_8b80_fdb94b7f7cb5 : MonoBehaviour
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
