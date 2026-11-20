using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Os sons gravados da repaginada (19/11/2026, a pedido do Pedro: "quero tirar os sons improvisados e começar a colocar
    /// esses de verdade"), em Assets/_Game/Audio/Resources. Saem dos pacotes grátis que ele importou e foram preparados pelo
    /// Trabalho-Claude/sons_repaginada/preparar.py:
    ///  - Armas/&lt;arma&gt;: os tiros (o Free Weapon SFX, que ele preferiu; a submetralhadora com os da pistola, um pouco
    ///    mais agudos, e a sniper com os do fuzil, mais graves) e os ecos de cada lugar (o Weapons of Choice, ver
    ///    <see cref="EcoDoTiro"/>);
    ///  - Armas/Manuseio e Armas/Capsulas: o pente, a alavanca, a bomba da espingarda, o cartucho, o ferrolho, a luneta e as
    ///    cápsulas caindo;
    ///  - Passos/&lt;chão&gt;: o Footsteps - Essentials (o concreto, a madeira e a grama; o carpete continua o feito em código);
    ///  - Golpes/&lt;grupo&gt;: o Deadly Kombat (o vento do soco, a pancada, a parede, o corte, o bastão e o osso);
    ///  - Vozes: <see cref="VozDoPersonagem"/>.
    /// Desde 20/11/2026 (a pedido: "podemos excluir os sons que já foram substituídos, para não dar interferência no jogo"),
    /// os sons feitos em código que eles trocavam saíram: sem um gravado, as funções daqui devolvem null e o som não toca.
    /// Só os que ainda não têm gravação continuam feitos em código.
    /// </summary>
    public static class SonsGravados
    {
        static readonly Dictionary<string, AudioClip[]> _pastas = new Dictionary<string, AudioClip[]>();

        /// <summary>Os clipes de Resources/&lt;caminho&gt; (null se a pasta estiver vazia).</summary>
        public static AudioClip[] Pasta(string caminho)
        {
            if (!_pastas.TryGetValue(caminho, out var c) || (c != null && c.Length > 0 && c[0] == null))
            {
                _pastas[caminho] = c = Resources.LoadAll<AudioClip>(caminho);
                MixerDeSom.MarcarDaPasta(caminho, c);   // (20/11/2026: o canal do mixer de cada clipe, pela pasta)
            }
            return c != null && c.Length > 0 ? c : null;
        }

        /// <summary>Os da pasta que começam com o prefixo, em ordem de nome (null se nenhum).</summary>
        public static AudioClip[] Varios(string caminho, string prefixo) => Comecando(caminho, prefixo);

        /// <summary>Um dos da pasta que começam com o prefixo, sorteado (null se nenhum).</summary>
        public static AudioClip Um(string caminho, string prefixo)
        {
            var c = Comecando(caminho, prefixo);
            return c != null ? c[Random.Range(0, c.Length)] : null;
        }

        static AudioClip[] Comecando(string caminho, string prefixo)
        {
            string chave = caminho + "|" + prefixo;
            if (_pastas.TryGetValue(chave, out var c) && (c == null || c.Length == 0 || c[0] != null))
                return c != null && c.Length > 0 ? c : null;
            var todos = Pasta(caminho);
            var lista = new List<AudioClip>();
            if (todos != null)
                foreach (var a in todos)
                    if (a != null && a.name.StartsWith(prefixo)) lista.Add(a);
            lista.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            _pastas[chave] = c = lista.ToArray();
            return c.Length > 0 ? c : null;
        }

        /// <summary>Os tiros gravados da arma (as de sucata usam os da real).</summary>
        public static AudioClip[] Tiros(TipoDeArma a) => Comecando("Armas/" + Armas.Real(a), "tiro_");

        /// <summary>O eco do tiro da arma no lugar (aberto, rua, montanha, sala, corredor).</summary>
        public static AudioClip[] Eco(TipoDeArma a, string lugar) => Comecando("Armas/" + Armas.Real(a), "eco_" + lugar + "_");

        /// <summary>Um som do manuseio das armas (null se não houver).</summary>
        public static AudioClip Manuseio(string nome)
        {
            var c = Comecando("Armas/Manuseio", nome);
            return c != null ? c[0] : null;
        }

        /// <summary>As cápsulas caindo no chão.</summary>
        public static AudioClip[] Capsulas => Pasta("Armas/Capsulas");

        /// <summary>Os passos gravados do chão, andando ou correndo.</summary>
        public static AudioClip[] Passos(Superficie s, bool correndo) => Comecando("Passos/" + s, correndo ? "correndo_" : "andando_");

        /// <summary>Um grupo de golpes (SocoVento, SocoCarne, SocoParede, Corte, Bastao, Osso).</summary>
        public static AudioClip[] Golpes(string grupo) => Pasta("Golpes/" + grupo);
    }
}
