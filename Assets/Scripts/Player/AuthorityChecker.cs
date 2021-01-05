using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

[RequireComponent(typeof(Player))]
public class AuthorityChecker : NetworkBehaviour
{
    private Player player;

    private void Start()
    {
        player = GetComponent<Player>();
        if(hasAuthority)
        {
            player.currentPlayerCamera = Instantiate(player.playerCamera, this.gameObject.transform);

            player.playerInfoCanvas.gameObject.SetActive(false);
        }
        else
        {
            GetComponent<PlayerMove>().enabled = false;
        }
    }
}
