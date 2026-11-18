using UnityEngine;

namespace FisicaMatematica.Interaccion
{
    /// <summary>
    /// Objeto que se puede levantar y transportar (E para agarrar, E para soltar).
    /// Solo se puede agarrar si su masa ≤ FuerzaPersonaje.MasaMaximaLevantable.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ObjetoAgarrable : Interactuable
    {
        [Header("Transporte")]
        [Tooltip("Qué tan rápido corrige su posición hacia el punto de agarre (1/s).")]
        [SerializeField, Min(0.1f)] private float rigidez = 12f;
        [SerializeField, Min(0.1f)] private float velocidadMaxima = 10f;
        [Tooltip("Si se aleja tanto (m) del punto de agarre (atascado en una pared) se suelta.")]
        [SerializeField, Min(0.1f)] private float distanciaRuptura = 1.5f;

        private Rigidbody _rb;
        private InteractorPersonaje _dueno;
        private bool _gravedadOriginal;

        public bool EstaAgarrado => _dueno != null;

        private void Reset()
        {
            textoAccion = "Agarrar";
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public override string TextoAccion(InteractorPersonaje interactor)
        {
            if (EstaAgarrado) return "Soltar";
            float masa = _rb != null ? _rb.mass : 0f;
            if (interactor != null && interactor.Fuerza != null && !interactor.Fuerza.PuedeLevantar(_rb))
                return $"Muy pesado ({masa:0.#} kg, máx. {interactor.Fuerza.MasaMaximaLevantable:0.#} kg)";
            return $"{textoAccion} ({masa:0.#} kg)";
        }

        public override bool PuedeInteractuar(InteractorPersonaje interactor)
        {
            if (!habilitado) return false;
            if (EstaAgarrado) return true;
            return interactor != null && interactor.Fuerza != null && interactor.Fuerza.PuedeLevantar(_rb);
        }

        public override void Interactuar(InteractorPersonaje interactor)
        {
            if (EstaAgarrado) Soltar();
            else Agarrar(interactor);
        }

        private void Agarrar(InteractorPersonaje interactor)
        {
            if (interactor == null || interactor.Sostenido != null) return;

            _dueno = interactor;
            interactor.Sostenido = this;
            _gravedadOriginal = _rb.useGravity;
            _rb.useGravity = false;
            IgnorarJugador(true);
        }

        public void Soltar()
        {
            if (_dueno == null) return;

            IgnorarJugador(false);
            _rb.useGravity = _gravedadOriginal;
            _rb.linearVelocity = Vector3.ClampMagnitude(_rb.linearVelocity, 3f);
            _dueno.Sostenido = null;
            _dueno = null;
        }

        private void IgnorarJugador(bool ignorar)
        {
            CharacterController cc = _dueno != null ? _dueno.Controlador : null;
            if (cc == null) return;
            foreach (Collider c in GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(c, cc, ignorar);
        }

        private void FixedUpdate()
        {
            if (_dueno == null) return;

            Vector3 error = _dueno.PuntoAgarre - _rb.worldCenterOfMass;
            if (error.magnitude > distanciaRuptura) { Soltar(); return; }

            _rb.linearVelocity = Vector3.ClampMagnitude(error * rigidez, velocidadMaxima);
            _rb.angularVelocity = Vector3.zero;
        }

        private void OnDisable()
        {
            Soltar();
        }
    }
}
