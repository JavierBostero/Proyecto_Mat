using UnityEngine;
using UnityEngine.InputSystem;

namespace FisicaMatematica.Movimiento3D
{
    /// <summary>
    /// Cámara en primera persona controlada con el mouse.
    /// Poner en la cámara hija del Personaje: el giro horizontal rota el cuerpo
    /// y el vertical rota solo la cámara. Esc libera el cursor, clic lo vuelve a bloquear.
    /// </summary>
    [DisallowMultipleComponent]
    public class CamaraMouse : MonoBehaviour
    {
        [Tooltip("Objeto que gira en horizontal (el Personaje). Si está vacío se usa el padre.")]
        [SerializeField] private Transform cuerpo;
        [Tooltip("Grados por píxel de movimiento del mouse.")]
        [SerializeField, Min(0.001f)] private float sensibilidad = 0.12f;
        [SerializeField, Range(10f, 89f)] private float limiteVertical = 85f;
        [SerializeField] private bool invertirY;
        [SerializeField] private bool bloquearCursorAlIniciar = true;

        private InputAction _mirar;
        private float _inclinacion;

        private void Awake()
        {
            if (cuerpo == null)
            {
                CharacterController cc = GetComponentInParent<CharacterController>();
                cuerpo = cc != null ? cc.transform : (transform.parent != null ? transform.parent : transform);
            }

            _mirar = new InputAction("Mirar", InputActionType.Value, "<Mouse>/delta", expectedControlType: "Vector2");

            float x = transform.localEulerAngles.x;
            _inclinacion = x > 180f ? x - 360f : x;
        }

        private void OnEnable()
        {
            _mirar.Enable();
            if (bloquearCursorAlIniciar) BloquearCursor();
        }

        private void OnDisable()
        {
            _mirar.Disable();
        }

        private void OnDestroy()
        {
            _mirar?.Dispose();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                LiberarCursor();

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                    BloquearCursor();
                return;
            }

            // El delta del mouse ya es "por frame": no se multiplica por deltaTime.
            Vector2 delta = _mirar.ReadValue<Vector2>() * sensibilidad;

            cuerpo.Rotate(0f, delta.x, 0f, Space.World);

            _inclinacion += invertirY ? delta.y : -delta.y;
            _inclinacion = Mathf.Clamp(_inclinacion, -limiteVertical, limiteVertical);
            transform.localRotation = Quaternion.Euler(_inclinacion, 0f, 0f);
        }

        public void BloquearCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void LiberarCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
