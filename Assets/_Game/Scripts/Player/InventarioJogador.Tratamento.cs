using Unity.Netcode;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os tratamentos do corpo (05/11/2026, o sistema de vida por membro): a atadura, a bandagem de pano, a tala, o kit
    /// médico, os comprimidos, o xarope e, na etapa 2, a bandagem antisséptica, o spray (que gasta um dos 5 usos) e o
    /// antibiótico não passam pelo comer (<c>ConsumirRpc</c>). O dono faz a barra (<see cref="Tratamento"/>)
    /// e, no fim, pede ao anfitrião (<see cref="TratarRpc"/>), que confere de novo (o item, o corpo agora), só gasta o
    /// item se ele tratar alguma coisa e devolve o aviso ("O braço esquerdo enfaixado: parou de sangrar"). Se o item
    /// estava na mão e era o último, a mão volta para a última arma, como depois de comer.
    /// </summary>
    public partial class InventarioJogador
    {
        /// <summary>O jogador desta máquina está tratando (a barra correndo): nem troca nem larga.</summary>
        static bool Tratando => Tratamento.Local != null && Tratamento.Local.Ativo;

        /// <summary>
        /// O que o clique faz com o tratamento na mão (a linha do HUD: "clique: enfaixar"): as faixas enfaixam, o spray
        /// borrifa, o que vai pela boca (os comprimidos, o xarope, o antibiótico) se toma.
        /// </summary>
        static string VerboDoTratamento(Item i) => i switch
        {
            Item.Atadura or Item.Bandagem or Item.BandagemAntisseptica => "enfaixar", Item.SprayAntisseptico => "borrifar",
            Item.Tala => "pôr a tala", Item.KitMedico => "usar o kit", _ => "tomar",
        };

        /// <summary>Os usos que sobram, para pôr depois do nome: " (3 usos)", " (1 uso)" (o spray; <see cref="Itens.ComUsos"/>).</summary>
        public static string UsosEmTexto(int desgaste)
        {
            int n = Itens.UsosQueSobram(desgaste);
            return n == 1 ? " (1 uso)" : $" ({n} usos)";
        }

        /// <summary>
        /// No dono, no fim da barra do <see cref="Tratamento"/>: pede ao anfitrião para usar o item <paramref name="id"/>
        /// na <paramref name="parte"/> (<see cref="PartesDoCorpo.Sortear"/>: ele escolhe).
        /// </summary>
        public void Tratar(ushort id, ParteDoCorpo parte)
        {
            if (!IsOwner || !PartesDoCorpo.EhTratamento(Do(id))) return;
            VoltarAMaoSeForOUltimo(id);
            TratarRpc(id, (byte)parte);
        }

        // o da mão: se for o último, a mão volta para a última arma (veja AoMudarMochila)
        // (só se for mesmo o último: o spray com usos sobrando e a pilha continuam na mão)
        void VoltarAMaoSeForOUltimo(ushort id)
        {
            if (id == IdNaMao && Achar(id, out var it)
                && (Itens.ComUsos(it.item) ? Itens.UsosQueSobram(it.desgaste) <= 1 : !Itens.EmPilha(it.item) || Itens.Quantos(it.item, it.qtd) <= 1))
            {
                _consumindo = id;
                _voltarPara = _arma != null ? _arma.UltimaArma : (ushort)0;
            }
        }

        [Rpc(SendTo.Server)]
        void TratarRpc(ushort id, byte parte, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            var item = Do(id);
            if (!PartesDoCorpo.EhTratamento(item)) return;
            var vida = GetComponent<VidaJogador>();
            if (vida == null) return;
            var p = (ParteDoCorpo)parte;
            if (!PartesDoCorpo.Valida(p)) p = PartesDoCorpo.Sortear;
            // o corpo mudou durante a barra (o kit de outro, a ferida que fechou): nada a tratar, o item fica
            if (!vida.PodeTratar(item, ref p, out string motivo))
            {
                if (!string.IsNullOrEmpty(motivo)) AvisoRpc(motivo);
                return;
            }
            // o spray gasta um uso (os 5 vão no desgaste; no último, ele acaba); o resto, um da pilha
            bool acabou = false;
            if (Itens.ComUsos(item))
            {
                acabou = Achar(id, out var antes) && Itens.UsosQueSobram(antes.desgaste) <= 1;
                GastarUso(id, false);
            }
            else TirarUm(id);
            string texto = vida.Tratar(item, p);
            if (acabou)
            {
                string fim = $"{TelaDoInventario.Maiuscula(Itens.NomeComArtigo(item))} acabou";
                texto = string.IsNullOrEmpty(texto) ? fim : $"{texto} ({Itens.NomeComArtigo(item)} acabou)";
            }
            if (!string.IsNullOrEmpty(texto)) AvisoRpc(texto);
        }

        // ------------------------------------------------ o amigo (a fase 3) ------------------------------------------------

        /// <summary>
        /// No dono, no fim da barra do curativo num amigo (05/11/2026, a fase 3, a pedido: o amigo faz o curativo em você):
        /// pede ao anfitrião para usar o item <paramref name="id"/> da SUA mochila na <paramref name="parte"/> do
        /// <paramref name="alvo"/>.
        /// </summary>
        public void TratarOAmigo(ushort id, ParteDoCorpo parte, JogadorRede alvo)
        {
            if (!IsOwner || alvo == null || !PartesDoCorpo.EhTratamento(Do(id))) return;
            VoltarAMaoSeForOUltimo(id);
            TratarOutroRpc(id, (byte)parte, alvo.NetworkObject);
        }

        /// <summary>No dono, ao começar o curativo num amigo: ele fica sabendo ("Fulano está enfaixando o seu braço esquerdo").</summary>
        public void AvisarQueTrata(JogadorRede alvo, Item item, ParteDoCorpo parte)
        {
            if (!IsOwner || alvo == null) return;
            AvisarQueTrataRpc(alvo.NetworkObject, (ushort)item, (byte)parte);
        }

        string NomeDeQuemTrata => _rede != null && !string.IsNullOrEmpty(_rede.Aparencia.Value.nome.ToString()) ? _rede.Aparencia.Value.nome.ToString() : "Um amigo";

        // o amigo perto o bastante e fora do carro (com folga: a barra para aos 2,8 m no dono, e as posições que o anfitrião
        // vê chegam atrasadas)
        bool AmigoPerto(NetworkObjectReference quem, out JogadorRede alvo)
        {
            const float alcance = Tratamento.AlcanceDoAmigo + 0.8f;
            alvo = quem.TryGet(out var no) ? no.GetComponent<JogadorRede>() : null;
            return alvo != null && alvo.gameObject != gameObject && alvo.AlvoValido && alvo.Vida != null && !Tratamento.NoCarro(alvo)
                   && (alvo.transform.position - transform.position).sqrMagnitude <= alcance * alcance;
        }

        [Rpc(SendTo.Server)]
        void AvisarQueTrataRpc(NetworkObjectReference quem, ushort item, byte parte, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || !AmigoPerto(quem, out var alvo)) return;
            var i = (Item)item;
            var p = (ParteDoCorpo)parte;
            bool fem = PartesDoCorpo.Valida(p) && PartesDoCorpo.NomeComArtigo(p)[0] == 'a';
            string nome = PartesDoCorpo.Valida(p) ? PartesDoCorpo.Nome(p) : "corpo";
            string onde = PartesDoCorpo.PelaBoca(i) ? $"dando {Itens.NomeComArtigo(i)} a você"
                        : PartesDoCorpo.CorpoTodo(i) ? "usando o kit médico em você"
                        : i == Item.Tala ? $"pondo a tala {(fem ? "na sua " : "no seu ")}{nome}"
                        : $"cuidando {(fem ? "da sua " : "do seu ")}{nome}";
            alvo.Inventario?.AvisarDono($"{NomeDeQuemTrata} está {onde}");
        }

        [Rpc(SendTo.Server)]
        void TratarOutroRpc(ushort id, byte parte, NetworkObjectReference quem, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            var item = Do(id);
            if (!PartesDoCorpo.EhTratamento(item)) return;
            // quem trata: de pé e fora do carro
            var minhaVida = GetComponent<VidaJogador>();
            if ((minhaVida != null && minhaVida.Caido.Value) || Tratamento.NoCarro(_rede)) return;
            if (!AmigoPerto(quem, out var alvo))
            {
                AvisoRpc("Longe demais do amigo: o curativo não foi feito");
                return;
            }
            var p = (ParteDoCorpo)parte;
            if (!PartesDoCorpo.Valida(p)) p = PartesDoCorpo.Sortear;
            if (!alvo.Vida.PodeTratar(item, ref p, out string motivo))
            {
                if (!string.IsNullOrEmpty(motivo)) AvisoRpc(motivo);
                return;
            }
            bool acabou = false;
            if (Itens.ComUsos(item))
            {
                acabou = Achar(id, out var antes) && Itens.UsosQueSobram(antes.desgaste) <= 1;
                GastarUso(id, false);
            }
            else TirarUm(id);
            string texto = alvo.Vida.Tratar(item, p);
            string nomeDoAmigo = alvo.Aparencia.Value.nome.ToString();
            if (string.IsNullOrEmpty(nomeDoAmigo)) nomeDoAmigo = "o amigo";
            string meu = string.IsNullOrEmpty(texto) ? $"Você tratou {nomeDoAmigo}" : $"{nomeDoAmigo}: {texto}";
            if (acabou) meu = $"{meu} ({Itens.NomeComArtigo(item)} acabou)";
            AvisoRpc(meu);
            if (!string.IsNullOrEmpty(texto)) alvo.Inventario?.AvisarDono($"{NomeDeQuemTrata} tratou você. {texto}");
        }
    }
}
