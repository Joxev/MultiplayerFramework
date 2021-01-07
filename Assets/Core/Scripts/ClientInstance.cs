using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class ClientInstance : NetworkBehaviour
{
    public static ClientInstance Instance;

    [HideInInspector] public FrameworkNetworkManager networkManager;

    [HideInInspector] public Transform spawnPoint;

    public GameObject playerPrefab;

    public GameObject spectateCameraPrefab;

    GameObject player;

    GameObject spectateCamera;

    private bool _initalized = false;

    private bool isRespawning = false;

    [HideInInspector] public bool isDead;

    public Coroutine respawnTime;
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

    private void Update()
    {
        if(hasAuthority)
        {
            if(Input.GetKeyDown(KeyCode.H))
            {
                CmdRespawnAllPlayers(true);
            }
        }
    }

    public void playerDeath(bool createSpectateCamera)
    {
        isDead = true;
        if(createSpectateCamera)
        {
            spectateCamera = Instantiate(spectateCameraPrefab);
        }
    }

    public void respawnAfterTime()
    {
        respawnTime = StartCoroutine(iRespawnAfterTime(5f));
        isDead = true;
    }

    private IEnumerator iRespawnAfterTime(float time)
    {
        isRespawning = true;
        yield return new WaitForSeconds(time);
        isRespawning = false;
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

    #region Rpc/Commands

    [Client]
    public void TryRespawn()
    {
        CmdSpawnPlayer();
    }
    [Command]
    public void CmdSpawnPlayer()
    {
        GameObject _player = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        NetworkServer.Spawn(_player, base.connectionToClient);
        TargetSetPlayerVar(base.connectionToClient, _player);
    }

    [TargetRpc]
    public void TargetSetPlayerVar(NetworkConnection target, GameObject _player)
    {
        if(hasAuthority)
        {
            player = _player;
            isDead = false;
            if(spectateCamera != null) { Destroy(spectateCamera); }
        }
    }

    [Command]
    public void CmdRespawnAllPlayers(bool respawnExisting)
    {
        foreach(ClientInstance c in FrameworkNetworkManager.ClientInstances)
        {
            c.RpcRespawnAllPlayers(respawnExisting);
        }
    }
    [ClientRpc]
    public void RpcRespawnAllPlayers(bool respawnExisting)
    {
        if (respawnExisting || isDead)
        {
            if (respawnTime != null) { StopCoroutine(respawnTime);  }

            CmdDestroyMe(player);
            TryRespawn();
        }
    }
    [Command]
    public void CmdDestroyMe(GameObject _go)
    {
        if (_go != null) NetworkServer.Destroy(_go);
    }
    #endregion

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
