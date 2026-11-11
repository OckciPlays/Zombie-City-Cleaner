using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O progresso do personagem na árvore de habilidades (06/11/2026, <see cref="Habilidades"/>; a árvore nova, com os
    /// níveis em cada nó, em 09/11/2026): o XP e os pontos de cada nó, numa NetworkVariable escrita pelo anfitrião (todos leem: o efeito de cada habilidade é conferido onde
    /// a conta é feita, no dono ou no anfitrião). O anfitrião dá o XP (<see cref="GanharXp"/>, chamado de onde a ação
    /// acontece: o zumbi morto, o sobrevivente, o amanhecer, o curativo, o craft, o saque, a ligação direta); o dono vê o
    /// "+3 XP" e o nível novo (<see cref="XpNaTela"/>) e aprende (o clique) e tira (o botão direito) pela aba HABILIDADES da
    /// mochila (a tecla K). Vai no save do personagem (passa de um mundo a outro); morrer não tira nada. No Dia 0 não se
    /// ganha XP. Um save da árvore velha recebe os pontos de volta (<see cref="Habilidades.Migrar"/>, no
    /// <see cref="DefinirProgresso"/>).
    /// </summary>
    public partial class InventarioJogador
    {
        /// <summary>O XP e os pontos de cada nó (o anfitrião escreve).</summary>
        public readonly NetworkVariable<ProgressoDoPersonagem> Progresso = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>No dono: ganhou XP (quanto, o motivo, e o nível novo; 0 se não subiu).</summary>
        public static event System.Action<int, string, int> GanhouXp;
        /// <summary>No dono: pôs um ponto numa habilidade (e os pontos dela agora).</summary>
        public static event System.Action<Habilidade, int> Aprendeu;

        // no anfitrião: os carros em que ele já fez a ligação direta (o XP é só da primeira vez em cada um)
        readonly HashSet<ulong> _carrosLigados = new HashSet<ulong>();

        /// <summary>Só no anfitrião: dá XP pela ação (o motivo aparece na tela do dono: "+3 XP · curativo").</summary>
        public void GanharXp(int quanto, string motivo)
        {
            if (!IsServer || !IsSpawned || quanto <= 0) return;
            var mundo = World.MundoDaPartida.Instancia;
            if (mundo != null && mundo.NoDiaZero) return;   // o Dia 0 é a abertura: não conta
            var p = Progresso.Value;
            int antes = p.Nivel;
            p.xp = Mathf.Min(10_000_000, p.xp + quanto);
            Progresso.Value = p;
            int depois = p.Nivel;
            XpRpc(quanto, motivo ?? "", (byte)(depois > antes ? depois : 0));
        }

        [Rpc(SendTo.Owner)]
        void XpRpc(int quanto, string motivo, byte subiu) => GanhouXp?.Invoke(quanto, motivo, subiu);

        /// <summary>No dono (a aba HABILIDADES, o clique): pede ao anfitrião mais um ponto na habilidade.</summary>
        public void PedirAprender(Habilidade h)
        {
            if (!IsOwner || !IsSpawned) return;
            AprenderRpc((byte)h);
        }

        /// <summary>No dono (a aba HABILIDADES, o botão direito): pede ao anfitrião para tirar um ponto da habilidade.</summary>
        public void PedirTirar(Habilidade h)
        {
            if (!IsOwner || !IsSpawned) return;
            TirarRpc((byte)h);
        }

        /// <summary>No dono: já viu o aviso da árvore nova (a aba K); o anfitrião apaga.</summary>
        public void PedirVistoOAviso()
        {
            if (!IsOwner || !IsSpawned || !Progresso.Value.avisoDaArvore) return;
            VistoOAvisoRpc();
        }

        [Rpc(SendTo.Server)]
        void AprenderRpc(byte h, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || h >= Habilidades.Quantas) return;
            if (!Aprender(Progresso.Value, (Habilidade)h, out var p, out string motivo))
            {
                AvisoRpc(motivo.Replace('\n', ' '));
                return;
            }
            Progresso.Value = p;
            AprendeuRpc(h, (byte)p.PontosEm((Habilidade)h));
        }

        [Rpc(SendTo.Server)]
        void TirarRpc(byte h, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || h >= Habilidades.Quantas) return;
            var p = Progresso.Value;
            if (!Habilidades.PodeTirar(p, (Habilidade)h, out string motivo))
            {
                AvisoRpc(motivo);
                return;
            }
            p.PorPontos((Habilidade)h, p.PontosEm((Habilidade)h) - 1);
            Progresso.Value = p;
        }

        [Rpc(SendTo.Server)]
        void VistoOAvisoRpc(RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            var p = Progresso.Value;
            p.avisoDaArvore = false;
            Progresso.Value = p;
        }

        // mais um ponto (o aviso da árvore nova some: ele já está distribuindo)
        static bool Aprender(ProgressoDoPersonagem p, Habilidade h, out ProgressoDoPersonagem depois, out string motivo)
        {
            depois = p;
            if (!Habilidades.PodeAprender(p, h, out motivo)) return false;
            depois.PorPontos(h, p.PontosEm(h) + 1);
            depois.avisoDaArvore = false;
            return true;
        }

        [Rpc(SendTo.Owner)]
        void AprendeuRpc(byte h, byte pontos) => Aprendeu?.Invoke((Habilidade)h, pontos);

        /// <summary>
        /// Só no anfitrião: o progresso do save. Um save da árvore velha (o <see cref="ProgressoDoPersonagem.aprendidas"/>
        /// preenchido) recebe todos os pontos de volta e o aviso; e só ficam os pontos que cabem (até o máximo de cada nó,
        /// nenhum num "em breve", e não mais que o nível).
        /// </summary>
        public void DefinirProgresso(ProgressoDoPersonagem p)
        {
            if (!IsServer) return;
            p.xp = Mathf.Clamp(p.xp, 0, 10_000_000);
            if (Habilidades.Migrar(ref p))
                Debug.Log($"[Habilidades] A árvore mudou (a de 09/11/2026): os pontos de {_rede?.Aparencia.Value.nome} voltaram (nível {p.Nivel}, {p.xp} XP).");
            var limpo = new ProgressoDoPersonagem { xp = p.xp, avisoDaArvore = p.avisoDaArvore };
            for (int i = 0; i < Habilidades.Quantas; i++)
            {
                var h = (Habilidade)i;
                if (!Habilidades.Pronta(h)) continue;
                // um nó que perdeu níveis (o a1, a Pistola de sucata, de 2 para 1 em 10/11/2026): o que passa do máximo volta
                // como ponto livre, e o resto da árvore fica como estava
                if (p.PontosEm(h) > Habilidades.Maximo(h))
                    Debug.Log($"[Habilidades] {Habilidades.Nome(h)} tem {Habilidades.Maximo(h)} nível(is) agora: {p.PontosEm(h) - Habilidades.Maximo(h)} ponto(s) de {_rede?.Aparencia.Value.nome} voltaram.");
                limpo.PorPontos(h, Mathf.Min(p.PontosEm(h), Habilidades.Maximo(h)));
            }
            if (limpo.Gastos > limpo.Nivel) limpo.Esquecer();
            Progresso.Value = limpo;
        }

        /// <summary>
        /// No dono (o PassageiroDoCarro, quando o motor pega pela ligação direta): o anfitrião dá o XP, só na primeira vez
        /// em cada carro (ligar e desligar o mesmo carro não rende).
        /// </summary>
        public void LigouDireto(Vehicles.Carro carro)
        {
            if (!IsOwner || carro == null || !carro.IsSpawned) return;
            LigouDiretoRpc(carro.NetworkObject);
        }

        [Rpc(SendTo.Server)]
        void LigouDiretoRpc(NetworkObjectReference carro, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || !carro.TryGet(out var no)) return;
            if (!Habilidades.Tem(_rede, Habilidade.LigacaoDireta) || !_carrosLigados.Add(no.NetworkObjectId)) return;
            GanharXp(Habilidades.XpDaLigacaoDireta, "ligação direta");
        }

        /// <summary>
        /// No anfitrião (o F1): 0, +50 XP; 1, mais um nível; 9, mais 5 níveis; 2, o nível 10; 3, o nível 40; 4, tudo do
        /// zero; 5, esquecer a árvore; 6, um ponto no primeiro nó que dá; 7, tirar um ponto do último que dá; 8, um save da
        /// árvore velha (a migração, como ao carregar). Devolve o aviso.
        /// </summary>
        public string ProgressoDeTeste(int acao)
        {
            if (!IsServer) return "";
            var p = Progresso.Value;
            switch (acao)
            {
                case 0: GanharXp(50, "teste"); return "+50 XP.";
                case 1: GanharXp(Mathf.Max(1, Habilidades.XpAte(Mathf.Min(Habilidades.NivelMaximo, p.Nivel + 1)) - p.xp), "teste"); return "Mais um nível.";
                case 9: GanharXp(Mathf.Max(1, Habilidades.XpAte(Mathf.Min(Habilidades.NivelMaximo, p.Nivel + 5)) - p.xp), "teste"); return "Mais 5 níveis.";
                case 2: p.xp = Mathf.Max(p.xp, Habilidades.XpAte(10)); break;
                case 3: p.xp = Mathf.Max(p.xp, Habilidades.XpAte(Habilidades.NivelMaximo)); break;
                case 4: p = default; break;
                case 5: p.Esquecer(); break;
                case 6:
                    for (int i = 0; i < Habilidades.Quantas; i++)
                        if (Aprender(p, (Habilidade)i, out var depois, out _))
                        {
                            Progresso.Value = depois;
                            return $"+1 ponto: {Habilidades.Nome((Habilidade)i)} ({depois.PontosEm((Habilidade)i)}/{Habilidades.Maximo((Habilidade)i)}).";
                        }
                    return "Nenhum nó dá para aprender agora.";
                case 7:
                    for (int i = Habilidades.Quantas - 1; i >= 0; i--)
                        if (Habilidades.PodeTirar(p, (Habilidade)i, out _))
                        {
                            p.PorPontos((Habilidade)i, p.PontosEm((Habilidade)i) - 1);
                            Progresso.Value = p;
                            return $"-1 ponto: {Habilidades.Nome((Habilidade)i)}.";
                        }
                    return "Nenhum ponto dá para tirar.";
                case 8:
                    // como um save de 06/11 a 08/11/2026: o nível 10, com as 4 primeiras do Guerreiro e o Faz-tudo da árvore velha
                    p.xp = Mathf.Max(p.xp, Habilidades.XpAte(10));
                    p.aprendidas = 0b1111UL | (1UL << 30);
                    DefinirProgresso(p);
                    return "A árvore velha: os pontos voltaram (o aviso na aba K).";
            }
            Progresso.Value = p;
            return acao switch
            {
                2 => "Nível 10 (10 pontos).", 3 => $"Nível {Habilidades.NivelMaximo} ({Habilidades.NivelMaximo} pontos).",
                4 => "Tudo do zero: nível 1, nenhum ponto.", _ => "A árvore foi esquecida (os pontos voltaram).",
            };
        }

        /// <summary>O progresso em texto (o F1).</summary>
        public string ResumoDoProgresso()
        {
            var p = Progresso.Value;
            int nivel = p.Nivel;
            string falta = nivel >= Habilidades.NivelMaximo ? "nível máximo" : $"{p.xp - Habilidades.XpAte(nivel)}/{Habilidades.XpDoNivel(nivel)} XP";
            int Area(AreaDaHabilidade a) => Habilidades.GastosAcima(p, a, Habilidades.Fileiras + 1);
            return $"Nível {nivel} ({falta}), {p.xp} XP no total\n{p.Gastos} pontos gastos, {p.Pontos} livres\n"
                 + $"Combate {Area(AreaDaHabilidade.Combate)} · Resistência {Area(AreaDaHabilidade.Resistencia)} · Sobrevivência {Area(AreaDaHabilidade.Sobrevivencia)}"
                 + (p.avisoDaArvore ? "\n(o aviso da árvore nova ainda não foi visto)" : "");
        }
    }
}
