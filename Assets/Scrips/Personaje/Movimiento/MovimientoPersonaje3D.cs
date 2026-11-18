using FisicaMatematica.Interaccion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FisicaMatematica.Movimiento3D
{
    /// <summary>
    /// Movimiento 3D en primera persona con CharacterController.
    /// WASD = mover, Shift = correr, Espacio = saltar.
    /// El movimiento es relativo a la cámara (referenciaCamara). El giro con el mouse
    /// lo hace CamaraMouse.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class MovimientoPersonaje3D : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Si está vacío se usa la cámara hija del personaje.")]
        [SerializeField] private Transform referenciaCamara;
        [SerializeField] private CharacterController controlador;

        [Header("Movimiento")]
        [SerializeField] private float velocidad = 4f;
        [SerializeField] private float velocidadCarrera = 7f;
        [Tooltip("m/s² con los que se alcanza la velocidad objetivo.")]
        [SerializeField, Min(0.1f)] private float aceleracion = 30f;
        [Tooltip("Fracción de la aceleración disponible en el aire.")]
        [SerializeField, Range(0f, 1f)] private float controlEnAire = 0.4f;

        [Header("Salto y gravedad")]
        [SerializeField] private float alturaSalto = 1.2f;
        [SerializeField] private float gravedad = -9.81f;
        [Tooltip("Si la posición Y baja de este valor, el personaje reaparece.")]
        [SerializeField] private float limiteCaida = -20f;
        [SerializeField, Min(0f)] private float tiempoCoyote = 0.12f;
        [SerializeField, Min(0f)] private float tiempoBufferSalto = 0.12f;

        [Header("Configuración automática")]
        [Tooltip("Agrega FuerzaPersonaje, InteractorPersonaje y CamaraMouse si faltan, " +
                 "y desactiva el CapsuleCollider redundante.")]
        [SerializeField] private bool configurarAutomaticamente = true;

        private InputAction _accionMover;
        private InputAction _accionSaltar;
        private InputAction _accionCorrer;

        private Vector3 _velHorizontal;
        private float _velVertical;
        private float _ultimoTiempoSuelo = -999f;
        private float _ultimoSaltoPedido = -999f;
        private Rigidbody _suelo;

        private Vector3 _posInicial;
        private Quaternion _rotInicial;

        public Vector3 VelocidadHorizontal => _velHorizontal;
        public bool EnSuelo => controlador != null && controlador.isGrounded;

        private void Awake()
        {
            if (controlador == null) controlador = GetComponent<CharacterController>();
            _posInicial = transform.position;
            _rotInicial = transform.rotation;

            if (configurarAutomaticamente) ConfigurarComponentes();

            _accionMover = new InputAction("Mover", InputActionType.Value);
            _accionMover.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _accionMover.AddBinding("<Gamepad>/leftStick");

            _accionSaltar = new InputAction("Saltar", InputActionType.Button, "<Keyboard>/space");
            _accionCorrer = new InputAction("Correr", InputActionType.Button, "<Keyboard>/leftShift");
        }

        private void ConfigurarComponentes()
        {
            // El CharacterController ya es un collider; una cápsula extra empuja las cajas dos veces.
            CapsuleCollider capsula = GetComponent<CapsuleCollider>();
            if (capsula != null && capsula.enabled) capsula.enabled = false;

            if (!TryGetComponent<FuerzaPersonaje>(out _)) gameObject.AddComponent<FuerzaPersonaje>();
            if (!TryGetComponent<InteractorPersonaje>(out _)) gameObject.AddComponent<InteractorPersonaje>();

            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                if (referenciaCamara == null) referenciaCamara = cam.transform;
                if (!cam.TryGetComponent<CamaraMouse>(out _)) cam.gameObject.AddComponent<CamaraMouse>();
            }
        }

        private void OnEnable()
        {
            _accionMover.Enable();
            _accionSaltar.Enable();
            _accionCorrer.Enable();
        }

        private void OnDisable()
        {
            _accionMover.Disable();
            _accionSaltar.Disable();
            _accionCorrer.Disable();
        }

        private void OnDestroy()
        {
            _accionMover?.Dispose();
            _accionSaltar?.Dispose();
            _accionCorrer?.Dispose();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool enSuelo = controlador.isGrounded;
            if (enSuelo) _ultimoTiempoSuelo = Time.time;

            // Velocidad de la plataforma (riel, caja) sobre la que estamos parados.
            Vector3 velPlataforma = Vector3.zero;
            if (enSuelo && _suelo != null) velPlataforma = _suelo.linearVelocity;
            _suelo = null; // se vuelve a llenar en OnControllerColliderHit durante Move

            // --- Horizontal ---
            Vector2 entrada = Vector2.ClampMagnitude(_accionMover.ReadValue<Vector2>(), 1f);
            float vel = _accionCorrer.IsPressed() ? velocidadCarrera : velocidad;
            Vector3 objetivo = DireccionRelativa(entrada) * vel;
            float acel = aceleracion * (enSuelo ? 1f : controlEnAire);
            _velHorizontal = Vector3.MoveTowards(_velHorizontal, objetivo, acel * dt);

            // --- Vertical ---
            if (_accionSaltar.WasPressedThisFrame()) _ultimoSaltoPedido = Time.time;
            if (enSuelo && _velVertical < 0f) _velVertical = -2f;

            bool puedeSaltar = Time.time - _ultimoTiempoSuelo <= tiempoCoyote;
            if (puedeSaltar && Time.time - _ultimoSaltoPedido <= tiempoBufferSalto)
            {
                // v = sqrt(2·g·h)
                _velVertical = Mathf.Sqrt(2f * alturaSalto * -gravedad);
                _ultimoTiempoSuelo = -999f;
                _ultimoSaltoPedido = -999f;
            }
            _velVertical += gravedad * dt;

            // --- Mover ---
            Vector3 desplazamiento = (_velHorizontal + Vector3.up * _velVertical + velPlataforma) * dt;
            CollisionFlags flags = controlador.Move(desplazamiento);
            if ((flags & CollisionFlags.Above) != 0 && _velVertical > 0f) _velVertical = 0f;

            if (transform.position.y < limiteCaida) Reaparecer();
        }

        private Vector3 DireccionRelativa(Vector2 entrada)
        {
            if (entrada.sqrMagnitude < 0.0001f) return Vector3.zero;
            Transform r = referenciaCamara != null ? referenciaCamara : transform;
            Vector3 adelante = Vector3.ProjectOnPlane(r.forward, Vector3.up);
            if (adelante.sqrMagnitude < 0.0001f) adelante = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            adelante.Normalize();
            Vector3 derecha = Vector3.Cross(Vector3.up, adelante);
            return adelante * entrada.y + derecha * entrada.x;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > 0.5f && hit.collider.attachedRigidbody != null)
                _suelo = hit.collider.attachedRigidbody;
        }

        public void Reaparecer()
        {
            controlador.enabled = false;
            transform.SetPositionAndRotation(_posInicial, _rotInicial);
            controlador.enabled = true;
            _velHorizontal = Vector3.zero;
            _velVertical = 0f;
        }
    }
}
