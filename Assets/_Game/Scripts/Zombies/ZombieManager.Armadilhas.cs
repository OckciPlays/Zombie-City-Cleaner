using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// As armadilhas (11/11/2026, a etapa 5a; o mundo no Loot/ConstrucoesNoMundo.Armadilhas.cs), no anfitrião:
    ///  - a cerca de fogo acesa: cada ponto da linha é um fogo que os segura (como a área do molotov, com a Muralha de fogo do
    ///    dono), e quem está nela queima e pega fogo (<see cref="FogoNaLinha"/>);
    ///  - o dano das armadilhas (o tapete de pregos, as estacas): sem marcar o zumbi como ferido nem fazê-lo caçar ninguém;
    ///    quem montou ganha o XP se ele morrer (<see cref="FerirPelaArmadilha"/>);
    ///  - quem pisou nos pregos anda mais devagar por uns segundos (<see cref="PisouNosPregos"/>).
    /// </summary>
    public partial class ZombieManager
    {
        const int ChaveDaCerca = 400000;
        readonly List<(int chave, Vector3 pos, ulong dono)> _pontosDaCerca = new List<(int, Vector3, ulong)>();
        readonly Dictionary<Zumbi, float> _lentos = new Dictionary<Zumbi, float>();
        readonly List<Zumbi> _jaAndam = new List<Zumbi>();

        // os pontos de fogo das cercas acesas (no AtualizarFogos, depois do molotov)
        void FogosDasArmadilhas()
        {
            var c = ConstrucoesNoMundo.Instancia;
            if (c == null) return;
            _pontosDaCerca.Clear();
            c.PontosDeFogoDasCercas(_pontosDaCerca);
            foreach (var (chave, pos, dono) in _pontosDaCerca)
            {
                float m = FogoDoJogador.MuralhaDaChave(dono);
                Fogos.Add(new Fogo { centro = pos, raio = ArmadilhasDoJogador.RaioQueSegura * m, chave = ChaveDaCerca + chave, panico = m > 1f });
            }
        }

        /// <summary>
        /// Só no anfitrião: os zumbis a menos de <paramref name="largura"/> da linha (os pontos até o índice) queimam pelo passo
        /// (s) e pegam fogo, como na área do molotov.
        /// </summary>
        internal void FogoNaLinha(Vector3[] linha, int ate, float largura, float passo, float danoPorSegundo, JogadorRede dono, float segundosQueimando)
        {
            _naArea.Clear();
            foreach (var z in Ativos)
            {
                if (z == null || z.Morto) continue;
                if (ConstrucoesNoMundo.NaLinha(linha, ate, z.transform.position, largura, out _)) _naArea.Add(z);
            }
            foreach (var z in _naArea)
            {
                PorFogo(z, segundosQueimando, dono);
                Queimar(z, danoPorSegundo * passo, dono);
            }
        }

        /// <summary>
        /// Só no anfitrião: uma armadilha fere o zumbi (o Brutamonte também; a casca de pedra protege), sem o ferido e sem caçar
        /// ninguém; o dono (se estiver na partida) ganha o XP de quem morrer.
        /// </summary>
        internal void FerirPelaArmadilha(Zumbi z, float dano, JogadorRede dono)
        {
            if (z == null || z.Morto) return;
            bool abatido = z.SemComando;
            if (!z.FerirNaArmadilha(dano, Time.time)) return;
            if (!abatido && dono != null && dono.Inventario != null) dono.Inventario.GanharXp(XpDoZumbi(z), NomeNoXp(z));
        }

        /// <summary>Só no anfitrião: o zumbi pisou nos pregos e anda mais devagar por uns segundos.</summary>
        internal void PisouNosPregos(Zumbi z, float segundos)
        {
            float ate = Time.time + segundos;
            z.FicarLento(ate);
            _lentos[z] = ate;
        }

        /// <summary>Só no anfitrião (a cada tique das armadilhas): quem já pode andar normal volta à velocidade dele.</summary>
        internal void LentosAgora()
        {
            if (_lentos.Count == 0) return;
            _jaAndam.Clear();
            foreach (var kv in _lentos)
                if (kv.Key == null || kv.Key.Morto || Time.time >= kv.Value) _jaAndam.Add(kv.Key);
            foreach (var z in _jaAndam)
            {
                _lentos.Remove(z);
                if (z != null && !z.Morto) z.AplicarVelocidade();
            }
        }

        /// <summary>Quantos zumbis estão lentos pelos pregos agora (o teste).</summary>
        public int LentosPelosPregos => _lentos.Count;
    }
}
