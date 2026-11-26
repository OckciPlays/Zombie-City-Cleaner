using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// As receitas aprendidas (25/11/2026, a pedido: "as receitas vão sendo aprendidas conforme o jogador pega os
    /// materiais"). Cada receita tem o material-chave (o primeiro do que ela gasta: <see cref="Receitas.Chave"/>); na
    /// primeira vez que ele está na mochila, a receita é aprendida e o dono vê "Nova receita: Lança", com o som da caneta
    /// do caderno. As da árvore de habilidades aparecem com a habilidade (e cada melhoria com o nível dela); as do fogo
    /// (o assar, que agora é na própria fogueira) não aparecem. O anfitrião olha a mochila a cada meio segundo e escreve os
    /// bits no <see cref="Progresso"/> (vai no save do personagem). A primeira olhada depois de o personagem chegar é calada:
    /// quem já tinha as coisas na mochila (um save de antes) aprende sem o aviso aparecer de enxurrada.
    /// </summary>
    public partial class InventarioJogador
    {
        const float SegundosEntreAsOlhadas = 0.5f;
        // sem o personagem chegar (o novo não passa pelo DefinirProgresso), as olhadas começam sozinhas depois disso
        const float SegundosAteOlharSozinho = 10f;

        float _proximaOlhada, _nasceuParaAsReceitas = -1f;
        bool _progressoChegou, _olhadaCalada = true;
        readonly List<string> _receitasNovas = new List<string>();

        /// <summary>
        /// O jogador sabe a receita (o índice em <see cref="Receitas.Todas"/>)? A escondida, nunca; a da habilidade, com os
        /// pontos dela; as outras, aprendidas pelo material-chave. (Todas as máquinas: a NetworkVariable do progresso.)
        /// </summary>
        public bool ConheceAReceita(int k)
        {
            if (k < 0 || k >= Receitas.Todas.Length) return false;
            var r = Receitas.Todas[k];
            if (Receitas.Escondida(r)) return false;
            var p = Progresso.Value;
            if (r.habilidade >= 0) return p.PontosEm((Habilidade)r.habilidade) >= r.nivel;
            return p.SabeAReceita(k);
        }

        /// <summary>Quantas receitas ele sabe (o F1 e o teste).</summary>
        public int ReceitasConhecidas()
        {
            int n = 0;
            for (int k = 0; k < Receitas.Todas.Length; k++)
                if (ConheceAReceita(k)) n++;
            return n;
        }

        // no anfitrião, do Update: a olhada na mochila
        void OlharAsReceitas()
        {
            if (Time.time < _proximaOlhada) return;
            _proximaOlhada = Time.time + SegundosEntreAsOlhadas;
            if (_nasceuParaAsReceitas < 0f) _nasceuParaAsReceitas = Time.time;
            if (!_progressoChegou && Time.time - _nasceuParaAsReceitas < SegundosAteOlharSozinho) return;
            var p = Progresso.Value;
            _receitasNovas.Clear();
            bool mudou = false;
            for (int i = 0; i < Mochila.Count; i++) mudou |= AprenderPelo(Mochila[i].item, ref p);
            if (PneuNasMaos.Value != SemPneu) mudou |= AprenderPelo(Item.Pneu, ref p);
            bool calada = _olhadaCalada;
            _olhadaCalada = false;
            if (!mudou) return;
            Progresso.Value = p;
            if (calada || _receitasNovas.Count == 0) return;
            string texto = _receitasNovas.Count == 1 ? $"Nova receita: {TelaDoInventario.Maiuscula(_receitasNovas[0])}"
                : $"Novas receitas: {TelaDoInventario.Maiuscula(Juntar(_receitasNovas))}";
            ReceitaNovaRpc(texto);
        }

        // as receitas desse material-chave que ele ainda não sabia
        bool AprenderPelo(Item item, ref ProgressoDoPersonagem p)
        {
            var ks = Receitas.DaChave(item);
            if (ks == null) return false;
            bool mudou = false;
            foreach (int k in ks)
            {
                if (p.SabeAReceita(k)) continue;
                p.AprenderAReceita(k);
                mudou = true;
                // (as da mesa de oficina vêm marcadas: longe dela, a aba Criar não as mostra)
                string nome = Itens.Nome(Receitas.Todas[k].resultado) + (Receitas.Todas[k].bancada ? " (mesa)" : "");
                if (!_receitasNovas.Contains(nome)) _receitasNovas.Add(nome);
            }
            return mudou;
        }

        // até 3 nomes; com mais, "e mais N" (o aviso cabe numa linha da tela e na da mochila: o primeiro teste, com 14 de uma
        // vez, passava da borda)
        const int NomesNoAviso = 3;

        static string Juntar(List<string> nomes)
        {
            if (nomes.Count <= 1) return nomes.Count == 1 ? nomes[0] : "";
            if (nomes.Count > NomesNoAviso)
                return string.Join(", ", nomes.GetRange(0, NomesNoAviso)) + $" e mais {nomes.Count - NomesNoAviso}";
            return string.Join(", ", nomes.GetRange(0, nomes.Count - 1)) + " e " + nomes[nomes.Count - 1];
        }

        [Rpc(SendTo.Owner)]
        void ReceitaNovaRpc(string texto)
        {
            AvisoLocal(texto);
            UI.CadernoDeMissoes.TocarAqui(UI.CadernoDeMissoes.SomDeEscrever, 0.45f);
        }

        /// <summary>No anfitrião (o DefinirProgresso): o personagem chegou; a próxima olhada é calada.</summary>
        void ProgressoChegou()
        {
            _progressoChegou = true;
            _olhadaCalada = true;
        }

        /// <summary>No anfitrião (o F1): todas as receitas aprendidas (as da habilidade continuam pedindo a habilidade). Devolve quantas eram novas.</summary>
        public int AprenderTodasAsReceitas()
        {
            if (!IsServer) return 0;
            var p = Progresso.Value;
            int novas = 0;
            for (int k = 0; k < Receitas.Todas.Length; k++)
            {
                var r = Receitas.Todas[k];
                if (Receitas.Escondida(r) || r.habilidade >= 0 || p.SabeAReceita(k)) continue;
                p.AprenderAReceita(k);
                novas++;
            }
            Progresso.Value = p;
            return novas;
        }

        /// <summary>No anfitrião (o F1): esquece as receitas aprendidas (as da habilidade ficam com a habilidade).</summary>
        public void EsquecerAsReceitas()
        {
            if (!IsServer) return;
            var p = Progresso.Value;
            p.receitas0 = p.receitas1 = 0UL;
            Progresso.Value = p;
            _olhadaCalada = true;   // (o que está na mochila volta a ensinar, sem o aviso)
        }

        // ------------------------------------------------------------------ a animação do craft (AnimacaoDoCraft.cs)

        AnimacaoDoCraft _animacaoDoCraft;

        /// <summary>A animação do craft deste jogador (criada na primeira vez).</summary>
        public AnimacaoDoCraft AnimacaoDoCraft => _animacaoDoCraft != null ? _animacaoDoCraft
            : _animacaoDoCraft = GetComponent<AnimacaoDoCraft>() ?? gameObject.AddComponent<AnimacaoDoCraft>();

        /// <summary>
        /// No dono, o CRIAR da aba Criar: a câmera vai para a terceira pessoa, os materiais aparecem na mesa (ou no chão), o
        /// personagem martela, e no fim a receita é feita (<see cref="Criar"/>). Onde a animação não cabe (no carro, na água,
        /// no modo de vídeo), faz na hora.
        /// </summary>
        public void CriarComAnimacao(int receita)
        {
            if (receita < 0 || receita >= Receitas.Todas.Length || !ConheceAReceita(receita) || !PodeCriar(Receitas.Todas[receita], out _)) return;
            if (!AnimacaoDoCraft.Comecar(receita)) Criar(receita);
        }

        /// <summary>No dono, ao começar a animação: os outros veem os materiais e o personagem trabalhando.</summary>
        public void AnunciarCraft(int receita, Vector3 centro, float giro, bool naMesa) => CraftAVistaRpc((byte)receita, centro, giro, naMesa);

        [Rpc(SendTo.NotMe)]
        void CraftAVistaRpc(byte receita, Vector3 centro, float giro, bool naMesa)
        {
            if (receita >= Receitas.Todas.Length) return;
            AnimacaoDoCraft.Mostrar(receita, centro, giro, naMesa);
        }
    }
}
