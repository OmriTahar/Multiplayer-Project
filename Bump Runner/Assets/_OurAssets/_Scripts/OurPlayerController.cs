using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Photon.Pun;


public class OurPlayerController : MonoBehaviourPunCallbacks
{
    #region Private Fields
    [Header("Multiplayer Fields")]
    public static GameObject LocalPlayerInstance;

    [Header("General Settings")]
    [SerializeField] KeyCode _jumpKey = KeyCode.W;
    [SerializeField] KeyCode _dashKey = KeyCode.D;
    [SerializeField] SpriteRenderer _playerSprite;
    public PlayerUISettings PlayerUISettings;

    [Header("General Refrences")]
    [SerializeField] Rigidbody2D _rigidbody2d;
    [SerializeField] BoxCollider2D _boxCollider2D;
    [SerializeField] CapsuleCollider2D _capsuleCollider2D;

    [Header("Dash")]
    [SerializeField] float _dashPower;
    [SerializeField] float _dashCooldown;
    [SerializeField] float _currentDashCooldownRemaining;
    private bool _canDash = true;

    [Header("Gravity")]
    [SerializeField] float _gravityScale;
    [SerializeField] float _gravityCooldown;

    [Header("Jump")]
    [SerializeField] float _jumpPower;
    [SerializeField] LayerMask _jumpableGround;
    private bool _isGrounded;

    [Header("Animation")]
    [SerializeField] Animator _playerAnimator;

    [Header("Bumping")]
    public float BumpMultiplier = 3f;
    public bool WasInvolvedInABump = false;
    private OurPlayerController _otherPlayer;
    [SerializeField] float _invulnerabilityTime;
    bool _isVulnerable;
    public static float bumpYOffset;

    #endregion

    #region Tilemap Collision Fields

    [Header("Tilemap")]
    public Vector2 initialVelocity = new Vector2(1.0f, 10.0f);
    public GameObject ObstacleTilemap;
    private Tilemap _tilemap;

    #endregion

    private void Awake()
    {
        if (photonView.IsMine)
        {
            LocalPlayerInstance = gameObject;
        }
    }

    void Start()
    {
        SetTilemapCollision();
        _rigidbody2d.gravityScale = _gravityScale;

        if (_boxCollider2D == null)
            _boxCollider2D = GetComponent<BoxCollider2D>();

        if (_capsuleCollider2D == null)
            _capsuleCollider2D = GetComponent<CapsuleCollider2D>();

        if (!photonView.IsMine)
        {
            enabled = false;
        }
    }

    [PunRPC]
    public void SendDash()
    {
        Debug.Log("Player ID: " + GameManager.Instance.CurrentUserID + " is dashing!");
    }


    void Update()
    {
        if (Input.GetKeyDown(_dashKey))
            TryDash();

        if (!_canDash)
            ApplyCooldown();

        _isGrounded = CheckGrounded();

        if (Input.GetKeyDown(_jumpKey))
            Jump();
    }

    #region PlayersCollision
    private void OnTriggerEnter2D(Collider2D collision)
    {
        //make sure you hit a player
        if (collision.tag == "Player")
        {
            //Get other player script;
            if (_otherPlayer == null)
                _otherPlayer = collision.GetComponent<OurPlayerController>();
            //Make sure I was involved in a bump?
            if (!WasInvolvedInABump)
            {
                //ask if I was higher than the other Player
                if (WhoGotBumped(collision.transform))
                {
                    StartCoroutine(BumpedByOtherPlayer(_otherPlayer));
                }
            }
        }
    }

    bool WhoGotBumped(Transform otherPlayerTransform)
    {
        //If I was lower than the other player and his offset I got bumped
        if (transform.position.y < otherPlayerTransform.position.y + bumpYOffset)
            return true;
        else
            return false;

        //We should look if I am in front of the player or behind it, we might want to change the bump force accordingly
    }

    IEnumerator BumpedByOtherPlayer(OurPlayerController otherPlayer)
    {
        WasInvolvedInABump = true;
        otherPlayer.WasInvolvedInABump = true;
        print(name + " Just got bumped by: " + otherPlayer.name);
       
        _boxCollider2D.isTrigger = true;
        Dash((-_dashPower * BumpMultiplier) * Time.deltaTime);

        yield return new WaitForSeconds(0.15f);

        _rigidbody2d.Sleep();
        _boxCollider2D.isTrigger = false;

        WasInvolvedInABump = false;
        otherPlayer.WasInvolvedInABump = false;
    }

    #endregion

    #region Tilemap Collision
    private void SetTilemapCollision()
    {
        _rigidbody2d.velocity = initialVelocity.x * UnityEngine.Random.Range(-1f, 1f) * Vector3.right + initialVelocity.y * Vector3.down;

        if (ObstacleTilemap != null)
        {
            _tilemap = ObstacleTilemap.GetComponent<Tilemap>();
            GameManager.Instance.tilemap = _tilemap;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        Vector3 hitPosition = Vector3.zero;

        if (_tilemap != null && ObstacleTilemap == collision.gameObject)
        {
            foreach (ContactPoint2D hit in collision.contacts)
            {
                hitPosition.x = hit.point.x - 0.01f * hit.normal.x;
                hitPosition.y = hit.point.y - 0.01f * hit.normal.y;
                GameManager.Instance.SendTileDestruction(hitPosition);
            }

            Dash(-_dashPower);
        }
    }
    #endregion

    #region Dash
    private void TryDash()
    {
        if (!_canDash)
            return;

        photonView.RPC("SendDash", RpcTarget.AllBuffered);

        Dash(_dashPower);

        _canDash = false;
        _currentDashCooldownRemaining = _dashCooldown;

        _rigidbody2d.gravityScale = 0;
        StartCoroutine(DisableGravity());
    }

    private void Dash(float dashPower)
    {
        _rigidbody2d.AddForce(new Vector2(transform.position.x + dashPower, transform.position.y));
    }

    void ApplyCooldown()
    {
        _currentDashCooldownRemaining -= Time.deltaTime;

        if (_currentDashCooldownRemaining <= 0f)
        {
            _currentDashCooldownRemaining = 0;
            _canDash = true;
        }

        var cooldownPrecentage = _currentDashCooldownRemaining / _dashCooldown;
        GameManager.Instance.UiHandler.DashCooldownUI(cooldownPrecentage);
    }

    IEnumerator DisableGravity()
    {
        yield return new WaitForSeconds(_gravityCooldown);
        _rigidbody2d.gravityScale = _gravityScale;
    }
    #endregion

    #region Jump
    private void Jump()
    {
        if (!_isGrounded)
            return;

        _rigidbody2d.AddForce(new Vector2(0, _jumpPower), ForceMode2D.Impulse);
    }
    private bool CheckGrounded()
    {
        var isGrounded = Physics2D.BoxCast(_boxCollider2D.bounds.center, _boxCollider2D.bounds.size, 0f, Vector2.down, .1f, _jumpableGround);

        if (isGrounded)
        {
            _playerAnimator.SetBool("IsJumping", false);
        }
        else
        {
            _playerAnimator.SetBool("IsJumping", true);
        }

        return isGrounded;
    }
    #endregion

    public void SetPlayer(GameObject obstaclesTilemap, int id)
    {
        ObstacleTilemap = obstaclesTilemap;
        SetPlayerUISettings();
    }
    private void SetPlayerUISettings()
    {
        var color = PlayerUISettings.PlayerImage.color;
        var RGB = new Vector3(color.r, color.g, color.b);
        photonView.RPC("SetPlayerColor", RpcTarget.AllBuffered, RGB);
        GameManager.Instance.UiHandler.SetDashColor(color);
    }
    [PunRPC]
    public void SetPlayerColor(Vector3 color)
    {
        _playerSprite.color = new Color(color.x,color.y,color.z);
    }
}
