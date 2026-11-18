using System.Collections.Generic;
using UnityEngine;

namespace FisicaMatematica.Movimiento3D
{
    /// <summary>
    /// Fuerza del personaje, en Newtons. Con ella:
    ///  - EMPUJA objetos con Rigidbody al caminar contra ellos (CharacterController no lo hace solo).
    ///  - Define la masa máxima que puede LEVANTAR (ObjetoAgarrable).
    /// La física decide el resto: una caja se mueve solo si F > μ·m·g (rozamiento),
    /// y su aceleración es a = (F − μ·m·g) / m.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class FuerzaPersonaje : MonoBehaviour
    {
        [Header("Empuje")]
        [Tooltip("Fuerza horizontal (N) aplicada a lo que se empuja.")]
        [SerializeField, Min(0f)] private float fuerzaEmpuje = 150f;
        [Tooltip("Deja de empujar si el objeto ya va más rápido que esto (m/s) en la dirección del empuje.")]
        [SerializeField, Min(0.1f)] private float velocidadMaximaEmpuje = 2.5f;

        [Header("Levantamiento")]
        [Tooltip("Fuerza (N) para agarrar objetos. Masa máxima = fuerza / g.")]
        [SerializeField, Min(0f)] private float fuerzaLevantamiento = 400f;

        private struct Contacto
        {
            public Rigidbody cuerpo;
            public Vector3 direccion;
            public float tiempo;
        }

        private const float VentanaContacto = 0.1f;
        private readonly List<Contacto> _contactos = new List<Contacto>();

        public float FuerzaEmpuje { get => fuerzaEmpuje; set => fuerzaEmpuje = Mathf.Max(0f, value); }
        public float FuerzaLevantamiento { get => fuerzaLevantamiento; set => fuerzaLevantamiento = Mathf.Max(0f, value); }

        /// <summary>Masa máxima (kg) que se puede levantar.</summary>
        public float MasaMaximaLevantable => fuerzaLevantamiento / Mathf.Abs(Physics.gravity.y);

        public bool PuedeLevantar(Rigidbody cuerpo) => cuerpo != null && cuerpo.mass <= MasaMaximaLevantable;

        /// <summary>Masa máxima (kg) que se puede empujar con un coeficiente de rozamiento μ.</summary>
        public float MasaMaximaEmpujable(float mu) => fuerzaEmpuje / (Mathf.Max(0.0001f, mu) * Mathf.Abs(Physics.gravity.y));

        // Se llama mientras CharacterController.Move choca; solo registramos el contacto
        // y la fuerza se aplica en FixedUpdate para que no dependa de los FPS.
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody rb = hit.collider.attachedRigidbody;
            if (rb == null || rb.isKinematic) return;
            if (hit.moveDirection.y < -0.3f) return; // estamos parados encima, no empujando

            Vector3 dir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            Contacto c = new Contacto { cuerpo = rb, direccion = dir, tiempo = Time.realtimeSinceStartup };
            for (int i = 0; i < _contactos.Count; i++)
            {
                if (_contactos[i].cuerpo == rb) { _contactos[i] = c; return; }
            }
            _contactos.Add(c);
        }

        private void FixedUpdate()
        {
            float ahora = Time.realtimeSinceStartup;
            for (int i = _contactos.Count - 1; i >= 0; i--)
            {
                Contacto c = _contactos[i];
                if (c.cuerpo == null || ahora - c.tiempo > VentanaContacto)
                {
                    _contactos.RemoveAt(i);
                    continue;
                }

                float vAlong = Vector3.Dot(c.cuerpo.linearVelocity, c.direccion);
                if (vAlong < velocidadMaximaEmpuje)
                    c.cuerpo.AddForce(c.direccion * fuerzaEmpuje, ForceMode.Force);
            }
        }
    }
}
