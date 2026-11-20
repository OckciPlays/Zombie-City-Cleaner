using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Os sons da madeira (18/11/2026, o corte com os pinheiros novos: antes o machado na árvore tocava a batida na parede),
    /// todos gravados, em Resources/Arvore:
    /// <list type="bullet">
    /// <item>o golpe do machado no tronco (os do sfxjar que o Pedro aprovou);</item>
    /// <item>os estalos da árvore rachando (os Madeira_01 a 03 do ambiente, copiados como Estalo_01 a 03);</item>
    /// <item>o tombo da árvore no chão.</item>
    /// </list>
    /// (20/11/2026: o golpe e o tombo feitos em código, a reserva dos gravados, saíram; sem a gravação, o som não toca.)
    /// </summary>
    public static class SomDaMadeira
    {
        static AudioClip[] _estalos;

        /// <summary>Os golpes do machado na madeira (os gravados; null sem eles).</summary>
        public static AudioClip[] Golpes => SonsGravados.Varios("Arvore", "machado_");   // (20/11/2026, os do sfxjar que o Pedro aprovou)

        /// <summary>Os estalos da madeira rachando (a árvore começando a cair, os galhos no tombo): os gravados.</summary>
        public static AudioClip[] Estalos
        {
            get
            {
                if (_estalos == null || _estalos.Length == 0)
                {
                    var l = new System.Collections.Generic.List<AudioClip>();
                    for (int i = 1; i <= 3; i++)
                    {
                        var c = Resources.Load<AudioClip>($"Arvore/Estalo_0{i}");
                        if (c != null) l.Add(c);
                    }
                    _estalos = l.ToArray();
                }
                return _estalos;
            }
        }

        /// <summary>A árvore batendo no chão (o gravado, sorteado; null sem ele).</summary>
        public static AudioClip Tombo => SonsGravados.Um("Arvore", "tombo_");   // (20/11/2026: o gravado, sem a motosserra)
    }
}
