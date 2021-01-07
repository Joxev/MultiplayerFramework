using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.UI;

public class Player : NetworkBehaviour
{
    [Header("Player Setup Data")]
    public Text playerNameText;
    public Canvas playerInfoCanvas;

    public GameObject playerCamera;
    [HideInInspector] public GameObject currentPlayerCamera;

    private Material playerMaterialClone;

    [SyncVar(hook = nameof(OnNameChanged))]
    public string playerName;

    [SyncVar(hook = nameof(OnColorChanged))]
    public Color playerColor = Color.white;

    private void Start()
    {
        string name = "Player" + Random.Range(100, 999);
        Color color = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));

        CmdSetupPlayer(name, color);
    }
    void OnNameChanged(string _Old, string _New)
    {
        playerNameText.text = playerName;
    }
    void OnColorChanged(Color _Old, Color _New)
    {
        playerNameText.color = _New;
        playerMaterialClone = new Material(GetComponent<Renderer>().material);
        playerMaterialClone.color = _New;
        GetComponent<Renderer>().material = playerMaterialClone;
    }

    #region Networked

    [TargetRpc]
    public void TargetPlayerDeath(NetworkConnection target)
    {
        if(hasAuthority)
        {
            ClientInstance.Instance.playerDeath(true);
            ClientInstance.Instance.respawnAfterTime();
            CmdPlayerDeath(this.gameObject);
        }
    }
    [Command]
    public void CmdPlayerDeath(GameObject _go)
    {
        NetworkServer.Destroy(_go);
    }

    [Command]
    public void CmdSetupPlayer(string _name, Color _col)
    {
        // player info sent to server, then server updates sync vars which handles it on all clients
        playerName = _name;
        playerColor = _col;
    }
    #endregion


    void Update()
    {
        if (!hasAuthority)
        {
            // make non-local players run this
            if(playerInfoCanvas != null) playerInfoCanvas.transform.LookAt(Camera.main.transform);
            return;
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.K))
            {
                TargetPlayerDeath(connectionToClient);
            }
        }
    }
}
