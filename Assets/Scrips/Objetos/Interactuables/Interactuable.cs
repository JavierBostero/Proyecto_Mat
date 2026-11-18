using UnityEngine;

namespace FisicaMatematica.Interaccion
{
    /// <summary>
    /// Base de todo objeto con el que el jugador puede interactuar (tecla E, apuntando con la cámara).
    /// Heredar y sobrescribir Interactuar().
    /// </summary>
    public abstract class Interactuable : MonoBehaviour
    {
        [SerializeField] protected string textoAccion = "Interactuar";
        [SerializeField] protected bool habilitado = true;
        [Tooltip("Distancia máxima (m) desde la cámara para poder usarlo.")]
        [SerializeField, Min(0.1f)] protected float distanciaMaxima = 3f;

        public bool Habilitado { get => habilitado; set => habilitado = value; }
        public float DistanciaMaxima => distanciaMaxima;

        /// <summary>Texto que se muestra al apuntarlo.</summary>
        public virtual string TextoAccion(InteractorPersonaje interactor) => textoAccion;

        public virtual bool PuedeInteractuar(InteractorPersonaje interactor) => habilitado;

        public abstract void Interactuar(InteractorPersonaje interactor);
    }
}
