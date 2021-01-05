﻿using System.Collections;
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
                        CmdDestroyPlayer(hit.collider.gameObject);
                    }
                }
            }
        }
    }

    [Command]
    public void CmdDestroyPlayer(GameObject hit)
    {
        NetworkServer.Destroy(hit);
    }
}
