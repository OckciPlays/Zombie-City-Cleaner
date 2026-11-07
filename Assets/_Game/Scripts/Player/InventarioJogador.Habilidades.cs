using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O progresso do personagem na árvore de habilidades (06/11/2026, <see cref="Habilidades"/>): o XP e as habilidades
    /// aprendidas, numa NetworkVariable escrita pelo anfitrião (todos leem: o efeito de cada habilidade é conferido onde
    /// a conta é feita, no dono ou no anfitrião). O anfitrião dá o XP (<see cref="GanharXp"/>, chamado de onde a ação
    /// acontece: o zumbi morto, o sobrevivente, o amanhecer, o curativo, o craft, o saque, a ligação direta); o dono vê o
    /// "+3 XP" e o nível novo (<see cref="XpNaTela"/>) e aprende pela aba HABILIDADES da mochila (a tecla K). Vai no save
    /// do personagem (passa de um mundo a outro); morrer não tira nada. No Dia 0 não se ganha XP.
    /// </summary>
    public partial class InventarioJogador
    {
        /// <summary>O XP e as habilidades aprendidas (o anfitrião escreve).</summary>
        public readonly NetworkVariable<ProgressoDoPersonagem> Progresso = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>No dono: ganhou XP (quanto, o motivo, e o nível novo; 0 se não subiu).</summary>
        public static event System.Action<int, string, int> GanhouXp;
        /// <summary>No dono: aprendeu uma habilidade.</summary>
        public static event System.Action<Habilidade> Aprendeu;

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

        /// <summary>No dono (a aba HABILIDADES): pede ao anfitrião para aprender.</summary>
        public void PedirAprender(Habilidade h)
        {
            if (!IsOwner || !IsSpawned) return;
            AprenderRpc((byte)h);
        }

        [Rpc(SendTo.Server)]
        void AprenderRpc(byte h, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || h >= Habilidades.Quantas) return;
            var p = Progresso.Value;
            if (!Habilidades.PodeAprender(p, (Habilidade)h, out string motivo))
            {
                AvisoRpc(motivo);
                return;
            }
            p.aprendidas |= 1UL << h;
            Progresso.Value = p;
            AprendeuRpc(h);
        }

        [Rpc(SendTo.Owner)]
        void AprendeuRpc(byte h) => Aprendeu?.Invoke((Habilidade)h);

        /// <summary>Só no anfitrião: o progresso do save (só as habilidades que existem).</summary>
        public void DefinirProgresso(ProgressoDoPersonagem p)
        {
            if (!IsServer) return;
            p.xp = Mathf.Clamp(p.xp, 0, 10_000_000);
            p.aprendidas &= (1UL << Habilidades.Quantas) - 1;
            Progresso.Value = p;
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

        /// <summary>No anfitrião (o F1): mais XP, um nível, ou tudo do zero.</summary>
        public void ProgressoDeTeste(int acao)
        {
            if (!IsServer) return;
            var p = Progresso.Value;
            switch (acao)
            {
                case 0: GanharXp(50, "teste"); return;
                case 1: GanharXp(Mathf.Max(1, Habilidades.XpAte(Mathf.Min(Habilidades.NivelMaximo, p.Nivel + 1)) - p.xp), "teste"); return;
                case 2: p.xp = Mathf.Max(p.xp, Habilidades.XpAte(10)); break;
                case 3: p.xp = Mathf.Max(p.xp, Habilidades.XpAte(Habilidades.NivelMaximo)); break;
                case 4: p = default; break;
                case 5: p.aprendidas = 0; break;
            }
            Progresso.Value = p;
        }

        /// <summary>O progresso em texto (o F1).</summary>
        public string ResumoDoProgresso()
        {
            var p = Progresso.Value;
            int nivel = p.Nivel;
            string falta = nivel >= Habilidades.NivelMaximo ? "nível máximo" : $"{p.xp - Habilidades.XpAte(nivel)}/{Habilidades.XpDoNivel(nivel)} XP";
            return $"Nível {nivel} ({falta}), {p.xp} XP no total\n{p.Aprendidas} aprendidas, {p.Pontos} pontos livres";
        }
    }
}
