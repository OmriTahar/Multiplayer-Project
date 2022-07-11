using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;


public class GameManager : MonoSingleton<GameManager>
{
    public UIHandler UiHandler;
    [SerializeField] GameObject _playerPrefab;
    [SerializeField] List<GameObject> _playersSpawnPoints;

    [SerializeField] private ColorHandler _colorHandler;
    public ColorHandler colorHandler => _colorHandler;

    [SerializeField] GameObject _readyButton;
    [SerializeField] GameObject _obstaclesTilemap;
    [SerializeField] SideScroll _gridScroll;
    Tilemap _tilemap;
    public Tilemap tilemap { set => _tilemap = value; }

    public int CurrentUserID;

    bool _isPlayerReady;
    bool _isPlaying = false;
    bool _isGameWon = false;
    bool _isGameLost = false;

    [SerializeField] float _slowTimeOverSeconds;
    float _currentTimeScale;


    public override void Awake()
    {
        base.Awake();
    }

    void Start()
    {
        //Time.timeScale = 0;

        if (PhotonNetwork.IsMasterClient)
        {
            _readyButton.SetActive(true);
        }
        else
        {
            _readyButton.SetActive(false);
        }

        CurrentUserID = PhotonNetwork.CurrentRoom.PlayerCount;
        CurrentUserID -= 1;

        photonView.RPC("EnteredRoom", RpcTarget.AllBuffered, CurrentUserID);
    }

    [PunRPC]
    public void EnteredRoom(int playerId)
    {
        print("RPC FUNC: Player ID: " + playerId + " has entered the room");
    }

    private void Update()
    {
        //if all players in room are ready play
        if (!_isPlaying)
        {
            if (_isPlayerReady)
            {
                Time.timeScale = 1;
                _isPlayerReady = false;
            }
        }

        if (_isGameWon)
        {
            SlowTime(true);
        }
        else if (_isGameLost)
        {
            SlowTime(false);
        }
    }
    void StartSetUP()
    {
        UiHandler.SetReadyScreen(true);
    }
    void StartGame()
    {
        _isPlayerReady = true;
        UiHandler.SetReadyScreen(false);
        //set player stuff
        var currentPlayer = PhotonNetwork.Instantiate(_playerPrefab.name, _playersSpawnPoints[CurrentUserID].transform.position, Quaternion.identity, 0);
        var ourPlayerController = currentPlayer.GetComponent<OurPlayerController>();

        if (ourPlayerController != null)
        {
            ourPlayerController.PlayerUISettings = _colorHandler.Players[CurrentUserID];
            ourPlayerController.SetPlayer(_obstaclesTilemap, CurrentUserID);
        }
        if(PhotonNetwork.IsMasterClient)
        {
            _gridScroll.StartGrid();
        }
    }

    public void PlayerIsReady()
    {
        photonView.RPC("StartGameForAll",RpcTarget.AllBuffered);
    }

    [PunRPC]
    void StartGameForAll()
    {
        Debug.Log($"Starting Game for player: {CurrentUserID}");
        StartGame();
    }

    public void GameWon()
    {
        _isGameWon = true;
    }

    public void GameLost()
    {
        _isGameLost = true;
    }

    public void SlowTime(bool isGameWon)
    {
        _currentTimeScale = Time.timeScale;

        if (_currentTimeScale <= 0.1)
        {
            _currentTimeScale = 0;
            Time.timeScale = 0;

            _isPlaying = false;
            _isGameWon = false;

            UiHandler.ShowResultPanel(isGameWon);
        }
        else
        {
            _currentTimeScale -= Time.deltaTime;
            Time.timeScale = _currentTimeScale;
        }
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
    }

    public void PlayAgain()
    {
        SceneManager.LoadScene(1);
    }

    public void GoToLobby()
    {
        _isGameWon = false;
        _isGameLost = false;
        Time.timeScale = 1;
        PhotonNetwork.LeaveRoom();
        SceneManager.LoadScene(0);
    }

    public void SendTileDestruction(Vector3 hitPosition)
    {
        photonView.RPC("DestroyTile", RpcTarget.AllBuffered, hitPosition);
    }
    [PunRPC]
    public void DestroyTile(Vector3 hitPosition)
    {
        _tilemap.SetTile(_tilemap.WorldToCell(hitPosition), null);
    }
}