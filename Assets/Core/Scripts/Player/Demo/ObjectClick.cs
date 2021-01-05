using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class ObjectClick : NetworkBehaviour
{
    public GameObject prefabSpawn;
    public LayerMask hittable;
    private void Update()
    {
        if (hasAuthority)
        {
            if (Input.GetMouseButtonDown(0))
            {
                RaycastHit hit;
                if (Physics.Raycast(GetComponent<Player>().currentPlayerCamera.transform.position, GetComponent<Player>().currentPlayerCamera.transform.forward, out hit, 100, hittable))
                {
                    CmdSpawnObject(hit.point);
                }
            }
        }
    }
    
    [Command]
    public void CmdSpawnObject(Vector3 hit)
    {
        GameObject _g = Instantiate(prefabSpawn, hit, Quaternion.Euler(0, 0, 0));
        NetworkServer.Spawn(_g);
    }
}
