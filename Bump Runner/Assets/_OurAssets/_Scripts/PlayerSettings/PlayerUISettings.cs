using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

public class PlayerUISettings : MonoBehaviourPunCallbacks
{

    [SerializeField] TextMeshProUGUI _playerName;
    [SerializeField] int _playerID;
    [SerializeField] Image _playerImage;
    public Image PlayerImage => _playerImage;

    public void SetPlayerSettings(string playerNickName)
    {
        _playerName.text = playerNickName;
    }

    public void ChangePlayerImageColor(Color color)
    {
        PlayerImage.color = color;
    }
}
