using UnityEngine;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// (14/11/2026) As medidas de um modelo pronto (os animais do Tripo do Gustavo), no espaço da raiz do prefab (o x para
    /// a direita do bicho, o y para cima, o z para a frente), em metros: saem do Blender (Blender/Fauna/Animais/
    /// exportar_unity.py, o Medidas.json) e o Editor/MontagemDosAnimais.cs grava no prefab. O ModelosDosBichos põe os
    /// colisores do corpo e da cabeça por elas, a boca (a carga: o galho do castor, o salmão do urso) e a altura do lombo.
    /// </summary>
    public sealed class MedidasDoModelo : MonoBehaviour
    {
        public Vector3 corpoCentro, corpoTamanho;
        public Vector3 cabecaCentro, cabecaTamanho;
        public Vector3 boca;
        /// <summary>A altura do lombo (m) e o comprimento (m), no tamanho do modelo.</summary>
        public float altura, comprimento;
    }
}
