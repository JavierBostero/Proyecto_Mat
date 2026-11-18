using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace FisicaMatematica.Interaccion
{
    public enum ModoBoton { Pulsador, Interruptor }

    /// <summary>
    /// Botón accionable con E. Dispara UnityEvents que se conectan en el Inspector
    /// (por ejemplo Riel.Activar, Riel.Alternar, Riel.InvertirSentido).
    ///  - Pulsador: alActivar, y alDesactivar tras duracionPulso segundos.
    ///  - Interruptor: alterna entre activo e inactivo en cada pulsación.
    /// </summary>
    public class BotonInteractuable : Interactuable
    {
        [Header("Comportamiento")]
        [SerializeField] private ModoBoton modo = ModoBoton.Pulsador;
        [SerializeField, Min(0.05f)] private float duracionPulso = 0.4f;
        [SerializeField] private bool inicialmenteActivo;

        [Header("Eventos")]
        [SerializeField] private UnityEvent alActivar;
        [SerializeField] private UnityEvent alDesactivar;

        [Header("Retroalimentación visual (opcional)")]
        [SerializeField] private Renderer indicador;
        [SerializeField] private Color colorInactivo = new Color(0.8f, 0.2f, 0.2f);
        [SerializeField] private Color colorActivo = new Color(0.2f, 0.8f, 0.3f);

        private static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int IdColor = Shader.PropertyToID("_Color");

        private bool _activo;
        private Coroutine _pulso;
        private MaterialPropertyBlock _bloque;

        public bool Activo => _activo;

        private void Reset()
        {
            textoAccion = "Presionar";
        }

        private void Start()
        {
            if (inicialmenteActivo) Establecer(true);
            else ActualizarVisual();
        }

        public override bool PuedeInteractuar(InteractorPersonaje interactor)
        {
            if (!habilitado) return false;
            return !(modo == ModoBoton.Pulsador && _pulso != null);
        }

        public override void Interactuar(InteractorPersonaje interactor)
        {
            if (modo == ModoBoton.Interruptor) Establecer(!_activo);
            else if (_pulso == null) _pulso = StartCoroutine(Pulso());
        }

        private IEnumerator Pulso()
        {
            Establecer(true);
            yield return new WaitForSeconds(duracionPulso);
            Establecer(false);
            _pulso = null;
        }

        private void Establecer(bool valor)
        {
            if (_activo == valor) return;
            _activo = valor;
            ActualizarVisual();
            (valor ? alActivar : alDesactivar)?.Invoke();
        }

        private void ActualizarVisual()
        {
            if (indicador == null) return;
            if (_bloque == null) _bloque = new MaterialPropertyBlock();
            Color c = _activo ? colorActivo : colorInactivo;
            indicador.GetPropertyBlock(_bloque);
            _bloque.SetColor(IdBaseColor, c); // URP
            _bloque.SetColor(IdColor, c);     // Built-in
            indicador.SetPropertyBlock(_bloque);
        }
    }
}
