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

    GameObject player;

    private bool _initalized = false;

    private bool isRespawning = false;


    //TEMP
    bool initRespawn = false;
    //TEMP
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
            if(player == null && !isRespawning && initRespawn)
            {
                StartCoroutine(respawnAfterTime(5f));
                isRespawning = true;
            }
            if(Input.GetKeyDown(KeyCode.H))
            {
                CmdRespawnAllPlayers(true);
            }
        }
    }

    private IEnumerator respawnAfterTime(float time)
    {
        yield return new WaitForSeconds(time);
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
        GameObject _player = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        NetworkServer.Spawn(_player, base.connectionToClient);
        TargetSetPlayerVar(base.connectionToClient, _player);
    }
    [TargetRpc]
    public void TargetSetPlayerVar(NetworkConnection target, GameObject _player)
    {
        player = _player;
        isRespawning = false;
        initRespawn = true;
        print("test");
    }
    [Command]
    public void CmdRespawnAllPlayers(bool respawnExisting)
    {
        RpcRespawnAllPlayers(respawnExisting);
    }
    [ClientRpc]
    public void RpcRespawnAllPlayers(bool respawnExisting)
    {
        if(hasAuthority)
        {
            if (respawnExisting || player == null)
            {
                isRespawning = true;
                if (player != null) NetworkServer.Destroy(player);
                TryRespawn();
            }
        }
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
