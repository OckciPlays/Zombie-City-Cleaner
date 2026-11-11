using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// As melhorias das armas de sucata (10/11/2026, a etapa 2 do Armeiro de sucata; os números no <see cref="Melhorias"/>):
    /// na bancada (ou na mesa de oficina), o painel MELHORIAS da aba Criar põe e tira as peças. O que está posto fica na
    /// própria arma (os bits do <see cref="ItemNaMochila.cores"/>) e anda com ela: o chão, a estante, a mochila no chão e o
    /// save já levam o campo. Pôr gasta a peça da mochila (a que estava no lugar volta para ela); tirar devolve. O
    /// silenciador volta com o desgaste dos tiros que já deu (não volta novo), e o anfitrião conta os tiros dele a cada tiro
    /// (<see cref="TiroDaMaoNoAnfitriao"/>): no fim, ele se desfaz, com o aviso.
    /// </summary>
    public partial class InventarioJogador
    {
        /// <summary>As melhorias postas na arma de sucata da mochila com este Id (0: nenhuma, ou não é uma de sucata).</summary>
        public ulong MelhoriasDe(ushort id) => id != 0 && Achar(id, out var it) && Itens.DeSucata(it.item) ? it.cores : 0UL;

        /// <summary>As melhorias da arma da mão (em qualquer máquina: a mochila e os atalhos vão pela rede).</summary>
        public ulong MelhoriasDaMao => MelhoriasDe(IdNaMao);

        /// <summary>A peça pode ir nesta arma agora? Se não, o motivo (para o painel).</summary>
        public bool PodePorMelhoria(ushort arma, ushort peca, out string motivo)
        {
            motivo = null;
            if (!Achar(arma, out var a) || !Itens.DeSucata(a.item)) motivo = "Escolha uma arma de sucata";
            else if (!Achar(peca, out var p) || !Melhorias.EhPeca(p.item)) motivo = "Essa peça não está na mochila";
            else if (!Melhorias.Serve(Melhorias.PecaDo(p.item), Itens.Arma(a.item)))
                motivo = $"{TelaDoInventario.Maiuscula(Itens.NomeComArtigo(p.item))} não serve n{(Itens.NomeComArtigo(a.item).StartsWith("o ") ? "o" : "a")} {Itens.Nome(a.item)}";
            else if (Melhorias.Posta(a.cores, Melhorias.PecaDo(p.item))) motivo = "Já está posta";
            else if (!Bancada.Perto(transform.position)) motivo = "Precisa de uma bancada perto";
            return motivo == null;
        }

        /// <summary>No dono: põe a peça da mochila na arma de sucata (o anfitrião confere e gasta a peça).</summary>
        public void PorMelhoria(ushort arma, ushort peca)
        {
            if (!PodePorMelhoria(arma, peca, out var motivo))
            {
                if (motivo != null) AvisoLocal(motivo);
                return;
            }
            PorMelhoriaRpc(arma, peca);
            Efeitos.Som(Efeitos.Recarga, transform.position + Vector3.up * 1.2f, 0.5f, false);
        }

        /// <summary>No dono: tira a peça da arma e devolve para a mochila (o que não couber cai aos pés).</summary>
        public void TirarMelhoria(ushort arma, PecaDaMelhoria peca)
        {
            if (!Achar(arma, out var a) || !Itens.DeSucata(a.item) || !Melhorias.Posta(a.cores, peca)) return;
            if (!Bancada.Perto(transform.position))
            {
                AvisoLocal("Precisa de uma bancada perto");
                return;
            }
            // o carregador estendido que sai leva as balas a mais: o anfitrião precisa saber quantas o pente tem agora
            if (peca == PecaDaMelhoria.CarregadorLongo && _arma != null)
            {
                int balas = _arma.MunicaoDe(arma, a.item);
                GuardarPenteRpc(arma, (ushort)Mathf.Max(0, balas));
                _arma.LimitarPente(arma, Armas.Dados(Itens.Arma(a.item)).capacidade);
            }
            TirarMelhoriaRpc(arma, (byte)peca);
            Efeitos.Som(Efeitos.Recarga, transform.position + Vector3.up * 1.2f, 0.5f, false);
        }

        [Rpc(SendTo.Server)]
        void PorMelhoriaRpc(ushort arma, ushort peca, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || !PodePorMelhoria(arma, peca, out _)) return;
            Achar(peca, out var p);
            var qual = Melhorias.PecaDo(p.item);
            TirarUm(peca);
            int k = Indice(arma);
            if (k < 0) return;
            var it = Mochila[k];
            var saiu = Melhorias.NoLugarDe(it.cores, qual);
            int tirosDaQueSaiu = Melhorias.TirosDoSilenciador(it.cores);
            it.cores = Melhorias.Por(it.cores, qual, Melhorias.TirosDoDesgaste(qual, p.desgaste));
            Mochila[k] = it;
            // a que estava no lugar volta para a mochila (o silenciador, com o desgaste dos tiros que deu)
            if (saiu.HasValue) Devolver(saiu.Value, tirosDaQueSaiu);
            AvisoRpc($"{TelaDoInventario.Maiuscula(Itens.Nome(p.item))} {(Feminino(p.item) ? "posta" : "posto")} n{(Itens.NomeComArtigo(it.item).StartsWith("o ") ? "o" : "a")} {Itens.Nome(it.item)}"
                     + (saiu.HasValue ? $" ({Itens.Nome(Melhorias.ItemDa(saiu.Value))} voltou para a mochila)" : ""));
        }

        [Rpc(SendTo.Server)]
        void TirarMelhoriaRpc(ushort arma, byte peca, RpcParams parametros = default)
        {
            int k = Indice(arma);
            if (parametros.Receive.SenderClientId != OwnerClientId || k < 0 || peca >= Melhorias.Quantas || !Bancada.Perto(transform.position)) return;
            var it = Mochila[k];
            var qual = (PecaDaMelhoria)peca;
            if (!Itens.DeSucata(it.item) || !Melhorias.Posta(it.cores, qual)) return;
            int tiros = Melhorias.TirosDoSilenciador(it.cores);
            it.cores = Melhorias.Tirar(it.cores, qual);
            // sem o carregador estendido, as balas que não cabem mais voltam numa caixa
            int sobra = 0;
            if (qual == PecaDaMelhoria.CarregadorLongo)
            {
                int cabe = Armas.Dados(Itens.Arma(it.item)).capacidade;
                sobra = Mathf.Max(0, it.qtd - cabe);
                it.qtd = (ushort)Mathf.Min(it.qtd, cabe);
            }
            Mochila[k] = it;
            if (sobra > 0) Entregar(Itens.CaixaDe(Itens.CalibreDaArma(it.item)), sobra);
            Devolver(qual, tiros);
            AvisoRpc($"{TelaDoInventario.Maiuscula(Itens.Nome(Melhorias.ItemDa(qual)))} {(Feminino(Melhorias.ItemDa(qual)) ? "tirada" : "tirado")}: voltou para a mochila");
        }

        static bool Feminino(Item i) => Itens.NomeComArtigo(i).StartsWith("a ");

        // a peça solta volta para a mochila (o silenciador, com o desgaste dos tiros que já deu); sem lugar, cai aos pés
        void Devolver(PecaDaMelhoria peca, int tiros)
        {
            var item = Melhorias.ItemDa(peca);
            int desgaste = Melhorias.DesgasteDosTiros(peca, tiros);
            if (!Receber(item, -1, desgaste)) ItensNoMundo.Instancia?.Criar(item, PontoAosPes(1), transform.eulerAngles.y + 60f, -1, desgaste);
        }

        /// <summary>
        /// No anfitrião, a cada tiro do jogador (o dele e o dos outros, que chega pelo TiroRpc): o silenciador da arma da mão
        /// conta o tiro e, no fim (<see cref="Melhorias.TirosDoPET"/>, <see cref="Melhorias.TirosDoFiltro"/>), se desfaz.
        /// </summary>
        public void TiroDaMaoNoAnfitriao()
        {
            if (!IsServer) return;
            int k = Indice(IdNaMao);
            if (k < 0) return;
            var it = Mochila[k];
            if (!Itens.DeSucata(it.item)) return;
            var s = Melhorias.SilenciadorPosto(it.cores);
            if (!s.HasValue) return;
            int tiros = Melhorias.TirosDoSilenciador(it.cores) + 1;
            if (tiros >= Melhorias.Duracao(s.Value))
            {
                it.cores = Melhorias.Tirar(it.cores, s.Value);
                AvisoRpc($"{TelaDoInventario.Maiuscula(Itens.NomeComArtigo(Melhorias.ItemDa(s.Value)))} se desfez");
            }
            else it.cores = Melhorias.ComTiros(it.cores, tiros);
            Mochila[k] = it;
        }
    }
}
