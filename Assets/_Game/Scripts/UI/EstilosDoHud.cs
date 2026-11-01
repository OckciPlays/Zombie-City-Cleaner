using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// Os estilos de texto do HUD, guardados (31/10/2026: cada OnGUI do HUD criava os seus GUIStyle a cada chamada, várias
    /// vezes por quadro, e eles eram boa parte do lixo de memória do jogo, que faz o coletor parar tudo de vez em quando).
    /// Pedidos dentro do OnGUI (o <c>GUI.skin</c> só existe lá), um por combinação de tamanho (já na escala da tela), letra,
    /// alinhamento e texto rico; a cor fica com quem usa, posta antes de cada desenho (o mesmo estilo serve a vários). Com a
    /// tela de outra altura, todos são feitos de novo.
    /// </summary>
    public static class EstilosDoHud
    {
        static readonly Dictionary<int, GUIStyle> _estilos = new Dictionary<int, GUIStyle>();
        static int _altura = -1;

        /// <summary>Um rótulo do <c>GUI.skin.label</c> com estes ajustes (o mesmo objeto a cada pedido igual).</summary>
        public static GUIStyle Rotulo(int tamanho, FontStyle letra = FontStyle.Normal, TextAnchor alinhamento = TextAnchor.UpperLeft, bool rico = false)
        {
            if (Screen.height != _altura)
            {
                _estilos.Clear();
                _altura = Screen.height;
            }
            int chave = (tamanho & 1023) | ((int)letra << 10) | ((int)alinhamento << 13) | (rico ? 1 << 18 : 0);
            if (!_estilos.TryGetValue(chave, out var estilo) || estilo == null)
            {
                estilo = new GUIStyle(GUI.skin.label) { fontSize = tamanho, fontStyle = letra, alignment = alinhamento, richText = rico };
                _estilos[chave] = estilo;
            }
            return estilo;
        }

        /// <summary>Os números de 0 a 99 já em texto (os atalhos, as contagens), sem criar uma string por quadro.</summary>
        public static string Numero(int n) => n >= 0 && n < Numeros.Length ? Numeros[n] : n.ToString();

        static readonly string[] Numeros = CriarNumeros();

        static string[] CriarNumeros()
        {
            var lista = new string[100];
            for (int i = 0; i < lista.Length; i++) lista[i] = i.ToString();
            return lista;
        }
    }
}
