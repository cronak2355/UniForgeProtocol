using UnityEngine;
using System.Collections;

public class Gen_2c312322_b86f_48f8_a946_c5ac5635dc6f : MonoBehaviour
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
