using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class ClickDestroy : NetworkBehaviour
{
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
                    if(hit.collider.tag == "Player")
                    {
                        CmdCallDeath(hit.collider.gameObject);
                    }
                }
            }
        }
    }
    [Command]
    public void CmdCallDeath(GameObject go)
    {
        NetworkIdentity otherIdentity = go.GetComponent<NetworkIdentity>();
        go.GetComponent<Player>().TargetPlayerDeath(otherIdentity.connectionToClient);
    }
}
