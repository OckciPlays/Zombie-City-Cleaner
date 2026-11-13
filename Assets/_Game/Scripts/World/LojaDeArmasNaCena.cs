using System.Linq;
using UnityEngine;
using ZombieCityCleaner.Documentos;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Music;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Na cena de teste da loja de armas (o Plano_LojaDeArmas.md da pasta de arte): os papéis que se leem (o
    /// <see cref="PapeisDoLugar"/>) e os pontos dos sons (o <see cref="AmbienteSonoro"/>) acompanham a variação da loja. A
    /// cena (a <see cref="CenaDaLojaDeArmas"/>, do assembly do Mundo) avisa quando gera de novo (as teclas 1, 2, 3 e 0 trocam o
    /// estado da sala segura, o R sorteia outra loja), e daqui eles se refazem: o bilhete do dono só no estado C, o diário só
    /// com o acampamento, o som de dentro do cofre só no C.
    /// </summary>
    public class LojaDeArmasNaCena : MonoBehaviour
    {
        public PapeisDoLugar papeis;
        public AmbienteSonoro sons;

        void OnEnable() => CenaDaLojaDeArmas.Gerada += Refazer;
        void OnDisable() => CenaDaLojaDeArmas.Gerada -= Refazer;

        void Start()
        {
            var c = FindAnyObjectByType<CenaDaLojaDeArmas>();
            if (c != null) Refazer(c);
        }

        void Refazer(CenaDaLojaDeArmas c)
        {
            if (!Application.isPlaying || this == null) return;
            var v = c.Variacao;
            if (papeis != null)
            {
                papeis.papeis = DocumentosDaLojaDeArmas.Papeis(v).Select(PapeisDoLugar.De).ToList();
                papeis.Fazer();
            }
            if (sons != null)
                sons.pontos = SonsDaLojaDeArmas.Pontos(v).Select(p => new AmbienteSonoro.Ponto { lugar = p.lugar, tipo = p.tipo, area = p.area }).ToList();
        }
    }
}
