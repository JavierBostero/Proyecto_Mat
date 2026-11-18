using FisicaMatematica.Movimiento3D;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FisicaMatematica.Interaccion
{
    /// <summary>
    /// Detecta el Interactuable al que apunta la cámara y lo acciona con E.
    /// Muestra una mira y el texto de la acción (IMGUI, no necesita Canvas).
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractorPersonaje : MonoBehaviour
    {
        [Tooltip("Desde donde sale el rayo. Si está vacío usa la cámara hija / principal.")]
        [SerializeField] private Transform origen;
        [SerializeField, Min(0.5f)] private float distancia = 3f;
        [SerializeField] private LayerMask capas = ~0;
        [Tooltip("Distancia (m) delante de la cámara a la que se lleva un objeto agarrado.")]
        [SerializeField, Min(0.5f)] private float distanciaAgarre = 2f;
        [SerializeField] private bool mostrarHUD = true;

        private InputAction _interactuar;
        private Interactuable _apuntado;
        private GUIStyle _estilo;

        public ObjetoAgarrable Sostenido { get; internal set; }
        public CharacterController Controlador { get; private set; }
        public FuerzaPersonaje Fuerza { get; private set; }

        public Vector3 PuntoAgarre =>
            origen != null ? origen.position + origen.forward * distanciaAgarre : transform.position + transform.forward * distanciaAgarre;

        private void Awake()
        {
            Controlador = GetComponent<CharacterController>();
            Fuerza = GetComponent<FuerzaPersonaje>();

            if (origen == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam == null) cam = Camera.main;
                if (cam != null) origen = cam.transform;
            }

            _interactuar = new InputAction("Interactuar", InputActionType.Button, "<Keyboard>/e");
        }

        private void OnEnable() => _interactuar.Enable();
        private void OnDisable() => _interactuar.Disable();
        private void OnDestroy() => _interactuar?.Dispose();

        private void Update()
        {
            Buscar();

            if (!_interactuar.WasPressedThisFrame()) return;

            if (Sostenido != null) Sostenido.Soltar();
            else if (_apuntado != null && _apuntado.PuedeInteractuar(this)) _apuntado.Interactuar(this);
        }

        private void Buscar()
        {
            _apuntado = null;
            if (origen == null || Sostenido != null) return;

            if (Physics.Raycast(origen.position, origen.forward, out RaycastHit impacto, distancia, capas, QueryTriggerInteraction.Ignore))
            {
                Interactuable i = impacto.collider.GetComponentInParent<Interactuable>();
                if (i != null && impacto.distance <= i.DistanciaMaxima) _apuntado = i;
            }
        }

        private void OnGUI()
        {
            if (!mostrarHUD) return;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            GUI.DrawTexture(new Rect(cx - 2f, cy - 2f, 4f, 4f), Texture2D.whiteTexture);

            string texto = null;
            if (Sostenido != null) texto = "[E] Soltar";
            else if (_apuntado != null)
            {
                string t = _apuntado.TextoAccion(this);
                texto = _apuntado.PuedeInteractuar(this) ? "[E] " + t : t;
            }
            if (texto == null) return;

            if (_estilo == null)
            {
                _estilo = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
                _estilo.normal.textColor = Color.white;
            }
            GUI.Label(new Rect(cx - 250f, cy + 20f, 500f, 30f), texto, _estilo);
        }
    }
}
