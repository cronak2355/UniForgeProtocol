using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using Uniforge.FastTrack.Runtime;

public class Gen_fc0ee956_11f9_4b32_9bc3_658c29bb9306 : MonoBehaviour
{

    private Transform _transform;
    private Animator _animator;
    public float hp = 100f;

    void Awake()
    {
        _transform = transform;
        _animator = GetComponent<Animator>();
        var ent = GetComponent<UniforgeEntity>();
    }
    void Update()
    {
        if (Input.GetKey(KeyCode.A))
        {
            PrefabRegistry.SpawnStatic("", _transform.position + new Vector3(0f, 0f, 0));
        }
        if (Input.GetKey(KeyCode.A))
        {
            if (_animator != null) _animator.Play("PlayerAttack_default");
        }
    }
}
