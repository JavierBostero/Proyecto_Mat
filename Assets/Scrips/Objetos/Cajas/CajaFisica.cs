using UnityEngine;

namespace FisicaMatematica.Objetos
{
    /// <summary>
    /// Física de una caja: masa, rozamiento, rebote y amortiguación.
    /// Agrega el Rigidbody solo (RequireComponent). Pensada para empujarse con
    /// FuerzaPersonaje o viajar por un Riel (ObjetoEnRiel).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class CajaFisica : MonoBehaviour
    {
        [Header("Masa y rozamiento")]
        [SerializeField, Min(0.01f)] private float masa = 10f;
        [SerializeField, Min(0f)] private float rozamientoDinamico = 0.5f;
        [SerializeField, Min(0f)] private float rozamientoEstatico = 0.6f;
        [SerializeField, Range(0f, 1f)] private float rebote = 0f;

        [Header("Resistencia al movimiento")]
        [SerializeField, Min(0f)] private float arrastreLineal = 0.05f;
        [SerializeField, Min(0f)] private float arrastreAngular = 0.5f;
        [Tooltip("Bloquea la rotación en X y Z para que la caja no se vuelque.")]
        [SerializeField] private bool evitarVuelco;

        [Header("Reaparición")]
        [Tooltip("Si cae por debajo de esta altura vuelve a su posición inicial.")]
        [SerializeField] private float limiteCaida = -50f;

        private Rigidbody _rb;
        private PhysicsMaterial _material;
        private Vector3 _posInicial;
        private Quaternion _rotInicial;

        public Rigidbody Cuerpo => _rb != null ? _rb : (_rb = GetComponent<Rigidbody>());
        public float Masa => masa;
        /// <summary>Peso en Newtons: P = m·g.</summary>
        public float Peso => masa * Mathf.Abs(Physics.gravity.y);
        /// <summary>Fuerza mínima (N) para empezar a deslizarla en el suelo: F = μe·m·g.</summary>
        public float FuerzaParaMover => rozamientoEstatico * Peso;

        private void Awake()
        {
            _posInicial = transform.position;
            _rotInicial = transform.rotation;

            _material = new PhysicsMaterial(name + "_Fisica");
            Aplicar();
        }

        private void OnValidate()
        {
            if (Application.isPlaying) { if (_rb != null) Aplicar(); return; }
            Aplicar();
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        private void Aplicar()
        {
            Rigidbody rb = Cuerpo;
            if (rb == null) return;

            rb.mass = masa;
            rb.linearDamping = arrastreLineal;
            rb.angularDamping = arrastreAngular;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // Solo se tocan los bits de X/Z para no pisar el bloqueo de ObjetoEnRiel.
            const RigidbodyConstraints xz = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            if (evitarVuelco) rb.constraints |= xz;
            else if (!Application.isPlaying) rb.constraints &= ~xz;

            if (_material != null)
            {
                _material.dynamicFriction = rozamientoDinamico;
                _material.staticFriction = rozamientoEstatico;
                _material.bounciness = rebote;
                foreach (Collider c in GetComponentsInChildren<Collider>())
                    c.sharedMaterial = _material;
            }
        }

        private void FixedUpdate()
        {
            if (transform.position.y < limiteCaida) Reiniciar();
        }

        /// <summary>Aplica un impulso instantáneo (N·s).</summary>
        public void AplicarImpulso(Vector3 impulso)
        {
            Cuerpo.AddForce(impulso, ForceMode.Impulse);
        }

        public void Reiniciar()
        {
            Rigidbody rb = Cuerpo;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = _posInicial;
            rb.rotation = _rotInicial;
            transform.SetPositionAndRotation(_posInicial, _rotInicial);
        }
    }
}
