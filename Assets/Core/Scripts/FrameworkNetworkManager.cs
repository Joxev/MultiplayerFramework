using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
public class FrameworkNetworkManager : NetworkManager
{
    public static Dictionary<NetworkConnection, NetworkIdentity> LocalPlayers = new Dictionary<NetworkConnection, NetworkIdentity>();

    public static List<ClientInstance> ClientInstances = new List<ClientInstance>();
    #region Server

    public override void OnServerAddPlayer(NetworkConnection conn)
    {
        GameObject player = (GameObject)Instantiate(playerPrefab, GetStartPosition().position, Quaternion.identity);
        player.GetComponent<ClientInstance>().networkManager = this;

        LocalPlayers[conn] = player.GetComponent<NetworkIdentity>();
        ClientInstances.Add(player.GetComponent<ClientInstance>());

        NetworkServer.AddPlayerForConnection(conn, player);

    }

    public override void OnServerDisconnect(NetworkConnection conn)
    {
        ClientInstances.Remove(ClientInstance.ReturnClientInstance(conn));
        LocalPlayers.Remove(conn);
        base.OnServerDisconnect(conn);
    }
    #endregion

    #region Client

    #endregion
}
