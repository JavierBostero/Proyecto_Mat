using UnityEngine;

namespace FisicaMatematica.Objetos
{
    /// <summary>
    /// Objeto conectado a un Riel. Lo que se une al riel es el PUNTO DE CONTACTO (un hijo del objeto
    /// que puedes mover en el editor): ese punto queda siempre sobre la línea del riel y solo puede
    /// desplazarse a lo largo de ella. El resto del objeto cuelga de ese punto.
    /// Si no hay punto de contacto se usa el pivote del objeto.
    /// Conserva la física: gravedad, empujes y motor del riel actúan como fuerzas (a = F/m).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class ObjetoEnRiel : MonoBehaviour
    {
        [SerializeField] private Riel riel;
        [Tooltip("Hijo del objeto que se une al riel. Muévelo en la escena (ver el gizmo cian). " +
                 "Clic derecho en el componente → 'Crear punto de contacto' para generarlo.")]
        [SerializeField] private Transform puntoDeContacto;
        [Tooltip("Bloquea la rotación del objeto mientras va por el riel.")]
        [SerializeField] private bool bloquearRotacion = true;
        [Tooltip("Distancia (m) que el punto de contacto se mantiene alejado de cada extremo del riel.")]
        [SerializeField, Min(0f)] private float margenExtremos = 0f;

        private Rigidbody _rb;
        private bool _inicializado;
        private Vector3 _anclajeLocal; // posición del punto de contacto en el espacio local del objeto

        public Riel Riel => riel;
        public Transform PuntoDeContacto => puntoDeContacto;

        private Vector3 PosicionAnclaje(Vector3 posCuerpo, Quaternion rotCuerpo)
        {
            return posCuerpo + rotCuerpo * Vector3.Scale(_anclajeLocal, transform.lossyScale);
        }

        /// <summary>Posición del punto de contacto a lo largo del riel, de 0 (inicio) a 1 (fin).</summary>
        public float PosicionNormalizada
        {
            get
            {
                if (riel == null) return 0f;
                Vector3 p = puntoDeContacto != null ? puntoDeContacto.position : transform.position;
                float t = Vector3.Dot(p - riel.Inicio, riel.Direccion);
                return Mathf.Clamp01(t / riel.Longitud);
            }
        }

        [ContextMenu("Crear punto de contacto")]
        private void CrearPuntoDeContacto()
        {
            if (puntoDeContacto != null) return;
            GameObject go = new GameObject("PuntoDeContacto");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            puntoDeContacto = go.transform;
        }

        private void Awake()
        {
            // Si el riel se asignó en el Inspector, pasa por Conectar para registrar todo.
            Riel inicial = riel;
            riel = null;
            if (inicial != null) inicial.Conectar(this);
        }

        public void Conectar(Riel nuevo)
        {
            if (nuevo == null) Desconectar();
            else nuevo.Conectar(this);
        }

        public void Desconectar()
        {
            if (riel != null) riel.Desconectar(this);
        }

        // Lo llama Riel.Conectar / Riel.Desconectar
        internal void AsignarRiel(Riel nuevo)
        {
            if (riel == nuevo) return;
            ConfigurarColisiones(riel, false);
            riel = nuevo;
            ConfigurarColisiones(riel, true);
            _inicializado = false;
        }

        // Evita que el objeto choque con el collider del propio riel.
        private void ConfigurarColisiones(Riel r, bool ignorar)
        {
            if (r == null) return;
            Collider[] propios = GetComponentsInChildren<Collider>();
            Collider[] delRiel = r.GetComponentsInChildren<Collider>();
            foreach (Collider p in propios)
            {
                foreach (Collider q in delRiel)
                {
                    if (p == null || q == null || p == q) continue;
                    if (q.attachedRigidbody != null) continue; // otros objetos móviles hijos del riel
                    Physics.IgnoreCollision(p, q, ignorar);
                }
            }
        }

        private void Inicializar()
        {
            _inicializado = true;

            if (_rb.isKinematic)
                Debug.LogWarning($"{name}: ObjetoEnRiel necesita un Rigidbody NO kinemático.", this);

            _anclajeLocal = puntoDeContacto != null
                ? transform.InverseTransformPoint(puntoDeContacto.position)
                : Vector3.zero;

            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            if (bloquearRotacion) _rb.constraints |= RigidbodyConstraints.FreezeRotation;
        }

        private void FixedUpdate()
        {
            if (riel == null) return;
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (!_inicializado) Inicializar();

            float dt = Time.fixedDeltaTime;
            Vector3 dir = riel.Direccion;
            Vector3 inicio = riel.Inicio;
            float longitud = riel.Longitud;
            float tMin = Mathf.Min(margenExtremos, longitud * 0.5f);
            float tMax = Mathf.Max(longitud - margenExtremos, longitud * 0.5f);

            // 1) Posición: el punto de contacto se proyecta sobre la recta del riel y se limita a los extremos;
            //    todo el cuerpo se traslada lo mismo.
            Vector3 anclaje = PosicionAnclaje(_rb.position, _rb.rotation);
            float t = Vector3.Dot(anclaje - inicio, dir);
            float tAj = Mathf.Clamp(t, tMin, tMax);
            Vector3 correccion = inicio + dir * tAj - anclaje;
            if (correccion.sqrMagnitude > 1e-6f) _rb.position += correccion;

            // 2) Velocidad: solo la componente a lo largo del riel; se corta contra los extremos.
            float vAlong = Vector3.Dot(_rb.linearVelocity, dir);
            int extremo = 0;
            if (t <= tMin + 0.001f) extremo = -1;
            else if (t >= tMax - 0.001f) extremo = 1;

            if ((extremo < 0 && vAlong < 0f) || (extremo > 0 && vAlong > 0f)) vAlong = 0f;
            if (extremo != 0) riel.NotificarExtremo(extremo);
            _rb.linearVelocity = dir * vAlong;

            // 3) Fuerzas a lo largo del riel.
            float m = _rb.mass;
            if (riel.MotorActivo || riel.FrenoActivo)
            {
                // F para alcanzar la velocidad objetivo en un paso (0 si solo frena), compensando la gravedad,
                // limitada a la fuerza del motor o del freno.
                float objetivo = riel.MotorActivo ? riel.VelocidadObjetivo : 0f;
                float fMax = riel.MotorActivo ? riel.FuerzaMotor : riel.FuerzaFreno;
                float aGravedad = Vector3.Dot(Physics.gravity, dir);
                float f = m * (objetivo - vAlong) / dt - m * aGravedad;
                f = Mathf.Clamp(f, -fMax, fMax);
                _rb.AddForce(dir * f, ForceMode.Force);
            }
            else if (riel.Rozamiento > 0f)
            {
                _rb.AddForce(-dir * (riel.Rozamiento * m * vAlong), ForceMode.Force);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 p = puntoDeContacto != null ? puntoDeContacto.position : transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(p, 0.12f);
            if (riel != null)
            {
                Vector3 d = riel.Direccion;
                Vector3 enRiel = riel.Inicio + d * Vector3.Dot(p - riel.Inicio, d);
                Gizmos.DrawLine(p, enRiel); // distancia que se corregirá al iniciar
            }
        }

        private void OnDestroy()
        {
            if (riel != null) riel.Desconectar(this);
        }
    }
}
