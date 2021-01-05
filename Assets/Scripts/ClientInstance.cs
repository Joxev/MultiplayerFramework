using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class ClientInstance : NetworkBehaviour
{
    public static ClientInstance Instance;

    [HideInInspector] public FrameworkNetworkManager networkManager;

    [HideInInspector] public Transform spawnPoint;

    public GameObject player;

    private bool _initalized = false;

    private void Start()
    {
        if(hasAuthority) { networkManager = (FrameworkNetworkManager)FrameworkNetworkManager.singleton; }

        if (hasAuthority)
        {
            spawnPoint = networkManager.GetStartPosition();
        }
        else
        {
            spawnPoint = transform;
        }

        TryRespawn();
    }

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        Instance = this;
    }
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
    }

    private void FixedUpdate()
    {
        if (!_initalized) { return; }
    }
    [Client]
    public void TryRespawn()
    {
        CmdSpawnPlayer();
    }
    [Command]
    public void CmdSpawnPlayer()
    {
        GameObject _player = Instantiate(player, spawnPoint.position, spawnPoint.rotation);
        NetworkServer.Spawn(_player, base.connectionToClient);
    }

    public static ClientInstance ReturnClientInstance(NetworkConnection conn)
    {
        if(NetworkServer.active && conn != null)
        {
            NetworkIdentity localPlayer;
            if (FrameworkNetworkManager.LocalPlayers.TryGetValue(conn, out localPlayer)) { return localPlayer.GetComponent<ClientInstance>(); }
            else { return null; }
            
        }
        else
        {
            return Instance;
        }
    }
}
