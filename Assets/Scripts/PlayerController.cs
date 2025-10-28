using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

/// Handles player locomotion, pickaxe positioning, and mining against the cave tilemap.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour {
    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference mineAction;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 6f;

    [Header("Pickaxe")]
    [SerializeField] private Transform pickaxeTransform;
    [SerializeField, Min(0f)] private float pickaxeDistance = 1f;
    [SerializeField, Min(0f)] private float miningCooldown = 0.2f;

    [Header("Mining")]
    [SerializeField] private Tilemap caveTilemap;
    [SerializeField] private CellularAutomata caveGenerator;

    private Rigidbody2D _rigidbody;
    private Vector2 _inputDirection;
    private float _mineTimer;

    /// Enables input actions when the component is activated.
    private void OnEnable() {
        if (moveAction != null && moveAction.action != null)
            moveAction.action.Enable();
        if (mineAction != null && mineAction.action != null)
            mineAction.action.Enable();
    }

    /// Disables input actions to avoid duplicate bindings.
    private void OnDisable() {
        if (moveAction != null && moveAction.action != null)
            moveAction.action.Disable();
        if (mineAction != null && mineAction.action != null)
            mineAction.action.Disable();
    }

    private void Awake() {
        _rigidbody = GetComponent<Rigidbody2D>();

        // Configure the rigidbody for top-down movement.
        _rigidbody.gravityScale = 0f;
        _rigidbody.freezeRotation = true;
    }

    private void Update() {
        ReadMovementInput();
        UpdatePickaxeTransform();
        HandleMiningTimer();
        HandleMineInput();
    }

    private void FixedUpdate() {
        _rigidbody.linearVelocity = _inputDirection * moveSpeed;
    }

    private void ReadMovementInput() {
        if (moveAction != null && moveAction.action != null) {
            _inputDirection = moveAction.action.ReadValue<Vector2>();
        }
        else if (Keyboard.current != null) {
            float x = 0f;
            float y = 0f;

            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                x += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                y -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                y += 1f;

            _inputDirection = new Vector2(x, y);
        }
        else {
            _inputDirection = Vector2.zero;
        }

        _inputDirection = Vector2.ClampMagnitude(_inputDirection, 1f);
    }

    /// Places and rotates the pickaxe relative to the mouse cursor.
    private void UpdatePickaxeTransform() {
        if (!pickaxeTransform)
            return;

        Camera activeCamera = Camera.main;
        if (activeCamera == null)
            return;

        Vector3 mouseWorld;
        if (Mouse.current != null) {
            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            float depth = activeCamera.orthographic
                ? 0f
                : Mathf.Abs(activeCamera.transform.position.z - pickaxeTransform.position.z);
            mouseWorld = activeCamera.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, depth));
        }
        else {
            mouseWorld = pickaxeTransform.position;
        }
        mouseWorld.z = pickaxeTransform.position.z;

        Vector2 toMouse = (mouseWorld - transform.position);
        if (toMouse.sqrMagnitude > 0.0001f)
            toMouse = toMouse.normalized;

        Vector3 desiredPosition = transform.position + (Vector3)(toMouse * pickaxeDistance);
        pickaxeTransform.position = desiredPosition;

        if (toMouse.sqrMagnitude > 0f) {
            float angle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg;
            pickaxeTransform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }

    /// Counts down the mining cooldown timer.
    private void HandleMiningTimer() {
        if (_mineTimer > 0f)
            _mineTimer -= Time.deltaTime;
    }

    /// Processes mining input and triggers excavation when allowed.
    private void HandleMineInput() {
        bool isMining =
            (mineAction != null && mineAction.action != null && mineAction.action.ReadValue<float>() >= 0.5f) ||
            (mineAction == null && Mouse.current != null && Mouse.current.leftButton.isPressed);

        if (!isMining)
            return;

        if (_mineTimer > 0f)
            return;

        if (TryMine())
            _mineTimer = miningCooldown;
    }

    /// Attempts to remove a tile from the cave map at the pickaxe position.
    private bool TryMine() {
        if (caveTilemap == null) {
            Debug.LogWarning($"{nameof(PlayerController)} on {name} cannot mine without a cave tilemap reference.");
            return false;
        }

        Vector3 toolPosition = pickaxeTransform != null
            ? pickaxeTransform.position
            : transform.position + (Vector3)(_inputDirection.normalized * pickaxeDistance);

        Vector3Int cell = caveTilemap.WorldToCell(toolPosition);
        TileBase tile = caveTilemap.GetTile(cell);
        if (tile == null)
            return false;

        caveTilemap.SetTile(cell, null);

        if (caveGenerator != null && caveGenerator.LastGeneratedMap != null) {
            Vector3Int origin = caveGenerator.TileOrigin;
            Vector3Int localCell = cell - origin;

            bool[,] map = caveGenerator.LastGeneratedMap;
            if (localCell.x >= 0 && localCell.y >= 0 &&
                localCell.x < map.GetLength(0) && localCell.y < map.GetLength(1)) {
                map[localCell.x, localCell.y] = false;
            }
        }

        return true;
    }
}
