using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// As peças que mexem de uma arma no corpo (o ferrolho, o carregador, a telha e o cartucho), presas aos ossos delas e
    /// não à arma: ligam e desligam junto com a arma (o objeto dela em <see cref="CorpoDoPersonagem.naMao"/>). No
    /// personagem principal em blocos (28/10/2026), cada arma fica num vazio no weapon.R, e as peças, nos ossos slide, mag,
    /// pump e shell, que as animações de atirar e recarregar mexem. Montado pela montagem do jogador.
    /// </summary>
    public class PecasDaArma : MonoBehaviour
    {
        public GameObject[] pecas;

        void OnEnable() => Ligar(true);
        void OnDisable() => Ligar(false);

        void Ligar(bool ligado)
        {
            if (pecas == null) return;
            foreach (var p in pecas)
                if (p != null) p.SetActive(ligado);
        }

        /// <summary>Os renderers da arma e das peças dela.</summary>
        public static Renderer[] Renderers(GameObject arma)
        {
            var lista = new System.Collections.Generic.List<Renderer>(arma.GetComponentsInChildren<Renderer>(true));
            var pa = arma.GetComponent<PecasDaArma>();
            if (pa != null && pa.pecas != null)
                foreach (var p in pa.pecas)
                    if (p != null) lista.AddRange(p.GetComponentsInChildren<Renderer>(true));
            return lista.ToArray();
        }
    }
}
