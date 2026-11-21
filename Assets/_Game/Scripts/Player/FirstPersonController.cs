using UnityEngine;
using UnityEngine.InputSystem;

namespace FalseParadise
{
    /// <summary>
    /// Контроллер от первого лица: WASD, мышь, Shift — бег, Space — прыжок.
    /// Иерархия: Player (этот скрипт + CharacterController) → CameraPivot (наклон) → Main Camera.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [SerializeField] Transform cameraPivot;
        [SerializeField] float walkSpeed = 4f;
        [SerializeField] float sprintSpeed = 6.5f;
        [SerializeField] float jumpHeight = 1f;
        [SerializeField] float gravity = -20f;
        [Tooltip("Градусов на пиксель движения мыши")]
        [SerializeField] float mouseSensitivity = 0.12f;
        [SerializeField] float maxPitch = 85f;

        /// <summary>Блокирует движение и обзор (осмотр предмета, диалог, катсцена).</summary>
        public bool InputLocked { get; set; }

        /// <summary>Скорость по горизонтали, 0..1, для покачивания оружия и звуков шагов.</summary>
        public float MoveAmount { get; private set; }

        CharacterController cc;
        float pitch;
        float verticalVelocity;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (cameraPivot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraPivot = cam.transform;
            }
        }

        void OnEnable() => LockCursor(true);

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            // Esc отпускает курсор, клик возвращает (удобно в редакторе).
            if (kb.escapeKey.wasPressedThisFrame) LockCursor(false);
            else if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked && !InputLocked) LockCursor(true);

            bool canControl = !InputLocked && Cursor.lockState == CursorLockMode.Locked;

            if (canControl && cameraPivot != null)
            {
                Vector2 d = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0f, d.x, 0f);
                pitch = Mathf.Clamp(pitch - d.y, -maxPitch, maxPitch);
                cameraPivot.localEulerAngles = new Vector3(pitch, 0f, 0f);
            }

            Vector2 move = Vector2.zero;
            if (canControl)
            {
                if (kb.wKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
            }
            move = Vector2.ClampMagnitude(move, 1f);
            MoveAmount = move.magnitude;

            float speed = canControl && kb.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
            Vector3 horizontal = (transform.right * move.x + transform.forward * move.y) * speed;

            if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (canControl && cc.isGrounded && kb.spaceKey.wasPressedThisFrame)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            verticalVelocity += gravity * Time.deltaTime;

            cc.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
        }
    }
}
