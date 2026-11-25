using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A fuga na cena do Lakewood (23/11/2026, as etapas 5 e 6 do Docs/23-Campanha-e-Mapa.md). Em cada máquina, com a fuga
    /// da rede (<see cref="MundoDeLakewood.NaFuga"/>): o palco vira a noite do colapso (<see cref="WoodcreekDoDiaZero.VirarAFuga"/>),
    /// os zumbis voltam a nascer em volta de quem joga (o <see cref="ZombieManager.ForaDaEpoca"/> desliga) e a tela da missão
    /// entra (<see cref="FugaNaTela"/>). No anfitrião:
    ///  - a cada tentativa, com a tela já fora: os zumbis da fuga postos de novo pelo caminho (<see cref="PorOsZumbisDaFuga"/>:
    ///    os Ocos em cima dos corpos, o Brutamonte no comércio, o Long e os Corredores mais à frente e o grupo da Sawmill
    ///    Road), todos curados (quem caiu, de pé na sala da casa), o portão do bunker aberto e a marca "Bunker" no mapa;
    ///  - alguém caiu: a fuga falhou (ela recomeça para todos);
    ///  - todos os vivos no portão (ou dentro do bunker): a chegada. No fim, o portão fecha e a marca sai.
    /// </summary>
    public partial class LakewoodNaCena
    {
        int _tentativaMontada;
        // os zumbis da fuga ainda por nascer: o tipo, o lugar e o giro (nascem com alguém a menos de 85 m)
        readonly List<(TipoDeZumbi tipo, Vector3 ponto, float yaw)> _porNascer = new List<(TipoDeZumbi, Vector3, float)>();
        int _tentativaDasFiguras;
        const float NascerAte = 85f;
        string _ultimaChegada;
        bool _fugaLimpa = true;

        // (no AcompanharODiaZero, a cada quadro)
        void AcompanharAFugaNaCena()
        {
            var m = MundoDeLakewood.Instancia;
            bool naFuga = m != null && m.NaFuga;
            // os zumbis do povoamento: no antes, só na fuga (e não na tela dos 30 dias, com todos parados)
            ZombieManager.ForaDaEpoca = _antes && !(naFuga && m.TempoDaFuga >= 0f);
            if (GetComponent<FugaNaTela>() == null) gameObject.AddComponent<FugaNaTela>();
            var rede = NetworkManager.Singleton;
            bool anfitriao = rede != null && rede.IsServer;
            if (!naFuga)
            {
                if (!_fugaLimpa && anfitriao) LimparAFuga(m);
                _tentativaMontada = 0;
                _tentativaDasFiguras = 0;
                return;
            }
            _fugaLimpa = false;
            var w = WoodcreekDoDiaZero.Atual;
            if (w == null || !w.Pronta) return;
            w.VirarAFuga();
            // as figuras dos Ocos (os que devoram os corpos e os que perseguem quem foge), de novo a cada tentativa, em cada
            // máquina (24/11/2026)
            if (m.TempoDaFuga >= 0f && _tentativaDasFiguras != m.Tentativa)
            {
                _tentativaDasFiguras = m.Tentativa;
                int figuras = w.RefazerAsFiguras();
                Debug.Log($"[Fuga] A tentativa {m.Tentativa}: {figuras} Ocos devorando os corpos e perseguindo quem foge (as figuras).");
            }
            if (!anfitriao || m.TempoDaFuga < 0f) return;
            if (_tentativaMontada != m.Tentativa)
            {
                _tentativaMontada = m.Tentativa;
                MontarATentativa(m, w);
            }
            if (!m.Falhou && !m.Chegou) NascerOsDePerto();
            if (m.Falhou || m.Chegou || m.TempoDaFuga < 2f) return;
            // alguém caiu: a fuga falhou
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.Criado) continue;
                var vida = j.GetComponent<VidaJogador>();
                if (vida != null && vida.Caido.Value)
                {
                    m.FalharAFuga(j.Aparencia.Value.nome.ToString());
                    return;
                }
            }
            // todos no portão (ou dentro do bunker): a chegada
            if (!PortaoDoBunker(out var portao)) return;
            int criados = 0, chegaram = 0;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.Criado) continue;
                criados++;
                var p = j.transform.position;
                var d = p - portao;
                d.y = 0f;
                if ((d.magnitude < 7f && Mathf.Abs(p.y - portao.y) < 4f) || BunkerDoMundo.NaCaixa(p)) chegaram++;
                _ultimaChegada = $"{d.magnitude:F1} m do portão, na caixa {BunkerDoMundo.NaCaixa(p)}";
            }
            if (criados > 0 && chegaram == criados)
            {
                Debug.Log($"[Fuga] A chegada: {_ultimaChegada}.");
                m.ChegarAoBunker();
            }
        }

        // no anfitrião, a cada quadro: os zumbis da fuga com alguém a menos de 85 m nascem (no NavMesh de lá, se já houver)
        void NascerOsDePerto()
        {
            if (_porNascer.Count == 0) return;
            var zm = ZombieManager.Instancia;
            if (zm == null || !zm.IsSpawned) return;
            for (int i = _porNascer.Count - 1; i >= 0; i--)
            {
                var (tipo, ponto, yaw) = _porNascer[i];
                bool perto = false;
                foreach (var j in JogadorRede.Todos)
                {
                    if (j == null || !j.Criado) continue;
                    var d = j.transform.position - ponto;
                    d.y = 0f;
                    if (d.sqrMagnitude < NascerAte * NascerAte) { perto = true; break; }
                }
                if (!perto) continue;
                _porNascer.RemoveAt(i);
                if (NavMesh.SamplePosition(ponto, out var hit, 3f, NavMesh.AllAreas)) ponto = hit.position;
                var z = zm.NascerEspecial(tipo, ponto, yaw);
                if (z != null && tipo != TipoDeZumbi.Oco) Debug.Log($"[Fuga] Nasceu um {tipo} ({z.tipo}) em {SetorDe(ponto)}.");
            }
        }

        /// <summary>
        /// No anfitrião (a figura de um Oco acordou, <see cref="DiaZero.FiguraDeOco"/>): um Oco de verdade no lugar dela,
        /// virado para quem chegou.
        /// </summary>
        public void NascerUmOcoNaFuga(Vector3 ponto, float yaw)
        {
            var zm = ZombieManager.Instancia;
            if (zm == null || !zm.IsSpawned) return;
            if (NavMesh.SamplePosition(ponto, out var hit, 3f, NavMesh.AllAreas)) ponto = hit.position;
            zm.NascerEspecial(TipoDeZumbi.Oco, ponto, yaw);
        }

        /// <summary>O meio do portão do bunker, no chão do pátio (no mundo). False sem o bunker.</summary>
        public bool PortaoDoBunker(out Vector3 ponto)
        {
            ponto = default;
            var b = Chao != null ? Chao.Bunker : null;
            if (b == null) return false;
            var p = b.NoMapa(PlantaDeBunker.PortaoX0 + PlantaDeBunker.PortaoLargura * 0.5f, PlantaDeBunker.Z0 - 0.8f);
            ponto = OrigemDeLakewood.NoMundo(new Vector3(p.x, b.Altura, p.y));
            return true;
        }

        // no anfitrião, a cada tentativa: os zumbis de antes saem, os da fuga entram, todos de pé e curados, o portão aberto e
        // a marca no mapa
        void MontarATentativa(MundoDeLakewood m, WoodcreekDoDiaZero w)
        {
            var zm = ZombieManager.Instancia;
            if (zm != null) zm.RecolherTodos();
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null) continue;
                // a lanterna da gaveta da casa, para quem não tem uma (a noite do colapso é escura)
                if (j.Inventario != null && j.Inventario.Quantos(Loot.Item.Lanterna) == 0) j.Inventario.Receber(Loot.Item.Lanterna);
                var vida = j.GetComponent<VidaJogador>();
                if (vida == null) continue;
                if (vida.Caido.Value) vida.RenascerAgora();
                else
                {
                    vida.Vida.Value = vida.vidaMaxima;
                    vida.SararTudo();
                    vida.AcalmarTudo();
                }
            }
            int n = PlanejarOsZumbisDaFuga(w);
            var bunker = BunkerDoMundo.Instancia;
            if (bunker != null) bunker.PorAPorta(BunkerDoMundo.Portao, true);
            if (PortaoDoBunker(out var portao)) m.PorAMarcaDoBunker(portao);
            Debug.Log($"[Fuga] A tentativa {m.Tentativa}: {n} zumbis pelo caminho (nascem quando alguém chega perto), o portão do bunker aberto.");
        }

        // no anfitrião, no fim da fuga: o portão fecha e a marca sai
        void LimparAFuga(MundoDeLakewood m)
        {
            _fugaLimpa = true;
            _porNascer.Clear();
            var bunker = BunkerDoMundo.Instancia;
            if (bunker != null) bunker.PorAPorta(BunkerDoMundo.Portao, false);
            if (m != null) m.TirarAMarcaDoBunker();
            var zm = ZombieManager.Instancia;
            if (zm != null && _antes) zm.RecolherTodos();
        }

        /// <summary>
        /// No anfitrião: os zumbis da fuga pelo caminho (o Pedro: "pessoas sendo devoradas pelos Ocos, brutamontes na cidade,
        /// long também"): um Oco em cima de cada corpo, o Brutamonte e uns Ocos no comércio, o Long na beira da rodovia com
        /// dois Corredores, um grupo na descida e outro na Sawmill Road, perto do bunker. Devolve quantos nasceram.
        /// </summary>
        int PlanejarOsZumbisDaFuga(WoodcreekDoDiaZero w)
        {
            _porNascer.Clear();
            int n = 0;
            var rnd = new System.Random(4242 + MundoDeLakewood.Instancia.Tentativa * 17);
            void Um(TipoDeZumbi tipo, Vector3 ponto, float yaw)
            {
                _porNascer.Add((tipo, ponto, yaw));
                n++;
            }
            Vector3 NoCaminho(float f, float desvio) => w.NoCaminhoNoMundo(f, desvio, 0.1f);
            float Para(Vector3 de, Vector3 para) => Mathf.Atan2(para.x - de.x, para.z - de.z) * Mathf.Rad2Deg;
            // (24/11/2026: os Ocos em cima dos corpos são as figuras que devoram, em cada máquina; o de verdade nasce quando
            // alguém chega perto, NascerUmOcoNaFuga)
            if (w.ComprimentoDaFuga < 50f) return n;
            // o meio da vila: o Brutamonte no meio da rua e três Ocos em volta
            var bruto = NoCaminho(0.42f, 0f);
            Um(TipoDeZumbi.Brutamonte, bruto, Para(bruto, NoCaminho(0.3f, 0f)));
            for (int i = 0; i < 3; i++) Um(TipoDeZumbi.Oco, NoCaminho(0.36f + 0.04f * i, (i - 1) * 4f), (float)rnd.NextDouble() * 360f);
            // mais à frente: o Long na beira e dois Corredores
            var cuspidor = NoCaminho(0.6f, 9f);
            Um(TipoDeZumbi.Long, cuspidor, Para(cuspidor, NoCaminho(0.55f, 0f)));
            for (int i = 0; i < 2; i++) Um(TipoDeZumbi.Corredor, NoCaminho(0.64f + 0.03f * i, (i == 0 ? -3f : 3f)), (float)rnd.NextDouble() * 360f);
            // a descida: um grupo de Ocos
            for (int i = 0; i < 3; i++) Um(TipoDeZumbi.Oco, NoCaminho(0.76f + 0.02f * i, (i - 1) * 3f), (float)rnd.NextDouble() * 360f);
            // perto do bunker: dois Ocos e um Corredor
            for (int i = 0; i < 2; i++) Um(TipoDeZumbi.Oco, NoCaminho(0.91f + 0.03f * i, (i == 0 ? -4f : 4f)), (float)rnd.NextDouble() * 360f);
            Um(TipoDeZumbi.Corredor, NoCaminho(0.87f, 0f), (float)rnd.NextDouble() * 360f);
            return n;
        }
    }
}
