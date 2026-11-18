using System.Collections.Generic;
using UnityEngine;

namespace FisicaMatematica.Objetos
{
    public enum EjeRiel { X, Y, Z }

    /// <summary>
    /// Riel: una guía recta a lo largo de un eje local del objeto. Los ObjetoEnRiel conectados
    /// solo pueden desplazarse por él. Opcionalmente tiene un motor que los arrastra.
    /// Con el motor apagado se deslizan libremente (gravedad + rozamiento + empujes).
    ///
    /// Para activarlo desde un botón: Boton.alActivar → Riel.Activar / Alternar / InvertirSentido.
    /// </summary>
    [DisallowMultipleComponent]
    public class Riel : MonoBehaviour
    {
        [Header("Geometría")]
        [Tooltip("Eje local por el que corre el riel (Y = riel vertical).")]
        [SerializeField] private EjeRiel eje = EjeRiel.Y;
        [Tooltip("Usa el tamaño del BoxCollider (con la escala) como largo del riel.")]
        [SerializeField] private bool longitudDesdeCollider = true;
        [SerializeField, Min(0.1f)] private float longitudManual = 5f;

        [Header("Motor")]
        [SerializeField] private bool motorActivo;
        [Tooltip("Velocidad objetivo en m/s. El signo define el sentido inicial (+ = hacia el eje positivo).")]
        [SerializeField] private float velocidadMotor = 1.5f;
        [Tooltip("Fuerza máxima (N) del motor. Si es menor que el peso en un riel vertical, no levanta.")]
        [SerializeField, Min(0f)] private float fuerzaMotor = 500f;
        [Tooltip("Al llegar a un extremo invierte el sentido (ida y vuelta).")]
        [SerializeField] private bool invertirAlLlegar;
        [SerializeField, Min(0f)] private float pausaEnExtremos = 0.5f;

        [Header("Freno (motor apagado)")]
        [Tooltip("Con el motor apagado, mantiene los objetos quietos en vez de dejarlos caer por gravedad.")]
        [SerializeField] private bool frenoActivo = true;
        [Tooltip("Fuerza máxima (N) del freno. Si el peso del objeto la supera, se desliza.")]
        [SerializeField, Min(0f)] private float fuerzaFreno = 5000f;

        [Header("Física")]
        [Tooltip("Rozamiento viscoso (1/s) con motor y freno apagados: a = −c·v.")]
        [SerializeField, Min(0f)] private float rozamiento = 1f;

        [Header("Objetos conectados")]
        [SerializeField] private List<ObjetoEnRiel> objetosConectados = new List<ObjetoEnRiel>();

        private BoxCollider _caja;
        private int _sentido = 1;
        private float _pausaHasta;
        private float _bloqueoInversion;

        private BoxCollider Caja
        {
            get
            {
                if (_caja == null) _caja = GetComponent<BoxCollider>();
                return _caja;
            }
        }

        // ---------- Geometría ----------
        public Vector3 Direccion
        {
            get
            {
                Vector3 local = eje == EjeRiel.X ? Vector3.right : (eje == EjeRiel.Y ? Vector3.up : Vector3.forward);
                return transform.TransformDirection(local).normalized;
            }
        }

        public float Longitud
        {
            get
            {
                BoxCollider c = Caja;
                if (longitudDesdeCollider && c != null)
                {
                    int i = (int)eje;
                    return Mathf.Max(0.01f, c.size[i] * Mathf.Abs(transform.lossyScale[i]));
                }
                return longitudManual;
            }
        }

        public Vector3 Centro
        {
            get
            {
                BoxCollider c = Caja;
                return c != null ? transform.TransformPoint(c.center) : transform.position;
            }
        }

        public Vector3 Inicio => Centro - Direccion * (Longitud * 0.5f);
        public Vector3 Fin => Centro + Direccion * (Longitud * 0.5f);

        // ---------- Motor ----------
        public bool MotorActivo => motorActivo;
        public float FuerzaMotor => fuerzaMotor;
        public float Rozamiento => rozamiento;
        public bool FrenoActivo => frenoActivo;
        public float FuerzaFreno => fuerzaFreno;
        public IReadOnlyList<ObjetoEnRiel> Objetos => objetosConectados;

        /// <summary>Velocidad objetivo con signo (m/s) a lo largo de Direccion.</summary>
        public float VelocidadObjetivo =>
            (!motorActivo || Time.time < _pausaHasta) ? 0f : Mathf.Abs(velocidadMotor) * _sentido;

        private void Awake()
        {
            _sentido = velocidadMotor >= 0f ? 1 : -1;
            // copia: Conectar puede modificar la lista
            foreach (ObjetoEnRiel o in new List<ObjetoEnRiel>(objetosConectados)) Conectar(o);
        }

        public void Activar() => motorActivo = true;
        public void Desactivar() => motorActivo = false;
        public void Alternar() => motorActivo = !motorActivo;
        public void InvertirSentido() => _sentido = -_sentido;

        /// <summary>Enciende el motor hacia el INICIO del riel (en un riel vertical con eje Y: hacia abajo).</summary>
        public void MoverHaciaInicio() { _sentido = -1; _pausaHasta = 0f; motorActivo = true; }

        /// <summary>Enciende el motor hacia el FIN del riel (en un riel vertical con eje Y: hacia arriba).</summary>
        public void MoverHaciaFin() { _sentido = 1; _pausaHasta = 0f; motorActivo = true; }

        /// <summary>Apaga el motor; si el freno está activo, el objeto se queda donde está.</summary>
        public void Detener() => motorActivo = false;

        // ---------- Conexión ----------
        public void Conectar(ObjetoEnRiel objeto)
        {
            if (objeto == null) return;
            if (objeto.Riel != null && objeto.Riel != this) objeto.Riel.Desconectar(objeto);
            if (!objetosConectados.Contains(objeto)) objetosConectados.Add(objeto);
            objeto.AsignarRiel(this);
        }

        public void Desconectar(ObjetoEnRiel objeto)
        {
            if (objeto == null) return;
            objetosConectados.Remove(objeto);
            if (objeto.Riel == this) objeto.AsignarRiel(null);
        }

        internal void NotificarExtremo(int signo)
        {
            if (!motorActivo || !invertirAlLlegar) return;
            if (signo != _sentido) return;
            if (Time.time < _bloqueoInversion) return;
            _sentido = -_sentido;
            _pausaHasta = Time.time + pausaEnExtremos;
            _bloqueoInversion = _pausaHasta + 0.2f;
        }

        private void OnDrawGizmos()
        {
            Vector3 a = Inicio, b = Fin;
            Gizmos.color = motorActivo ? Color.green : Color.yellow;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(a, 0.1f);
            Gizmos.DrawWireSphere(b, 0.1f);
        }
    }
}
