using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O fogo na mochila (11/11/2026, a etapa 4 do ramo Fogo; os números no <see cref="FogoDoJogador"/>):
    ///  - o lampião: o desgaste é o combustível que já foi (0, cheio; 100, vazio). Aceso na mão (fora do carro), o anfitrião
    ///    o gasta (<see cref="FogoDoJogador.MinutosDoLampiao"/> por carga); "Reabastecer" (o botão direito) gasta 1
    ///    combustível. Largado da mão, aceso, ele continua aceso no chão (o ItensNoMundo.Lampiao.cs);
    ///  - o combustível das receitas e do lampião: 1 álcool ou, sem ele, <see cref="FogoDoJogador.LitrosPorCarga"/> L de
    ///    gasolina de um galão;
    ///  - o molotov: o arremesso chega aqui (o anfitrião tira a garrafa da mochila, põe a próxima na mão e a joga: o
    ///    ConstrucoesNoMundo.Molotov.cs).
    /// </summary>
    public partial class InventarioJogador
    {
        /// <summary>O lampião da mão tem combustível (aceso, se ninguém o esconder: o <see cref="JogadorRede.LampiaoAceso"/>).</summary>
        public bool LampiaoAcesoNaMao => NaMao == Item.Lampiao && Achar(IdNaMao, out var it) && it.desgaste < 100;

        float _gastoDoLampiao;

        /// <summary>No anfitrião: o lampião aceso na mão gasta o combustível.</summary>
        void GastarLampiao(float dt)
        {
            if (_rede == null || !_rede.LampiaoAceso)
            {
                _gastoDoLampiao = 0f;
                return;
            }
            _gastoDoLampiao += dt * 100f / (FogoDoJogador.MinutosDoLampiao * 60f);
            if (_gastoDoLampiao < 1f) return;
            int n = Mathf.FloorToInt(_gastoDoLampiao);
            _gastoDoLampiao -= n;
            int k = Indice(IdNaMao);
            if (k < 0 || Mochila[k].item != Item.Lampiao) return;
            var it = Mochila[k];
            it.desgaste = (byte)Mathf.Min(100, it.desgaste + n);
            Mochila[k] = it;
            if (it.desgaste >= 100) AvisoRpc("O lampião apagou: acabou o combustível (botão direito na mochila: Reabastecer)");
        }

        // ------------------------------------------------------------------ o combustível

        /// <summary>O galão com mais gasolina (o Id e os litros; sem nenhum, 0).</summary>
        float MelhorGalao(out ushort id)
        {
            id = 0;
            float melhor = 0f;
            foreach (var it in Mochila)
            {
                float l = LitrosDe(it);
                if (l > melhor)
                {
                    melhor = l;
                    id = it.id;
                }
            }
            return melhor;
        }

        /// <summary>Tem 1 combustível na mochila: 1 álcool ou a gasolina de uma carga num galão.</summary>
        public bool TemCombustivel => Quantos(Item.Desinfetante) > 0 || MelhorGalao(out _) >= FogoDoJogador.LitrosPorCarga - 0.01f;

        /// <summary>
        /// Quantos combustíveis a mochila tem (11/11/2026, a cerca de fogo pede 2): os álcoois e as cargas de meio litro de
        /// cada galão.
        /// </summary>
        public int CargasDeCombustivel
        {
            get
            {
                int n = Quantos(Item.Desinfetante);
                foreach (var it in Mochila) n += Mathf.FloorToInt((LitrosDe(it) + 0.01f) / FogoDoJogador.LitrosPorCarga);
                return n;
            }
        }

        /// <summary>No anfitrião: gasta 1 combustível (o álcool primeiro; sem ele, a gasolina do galão mais cheio).</summary>
        internal bool GastarCombustivel(out string qual)
        {
            qual = null;
            if (!IsServer) return false;
            if (GastarItem(Item.Desinfetante, 1))
            {
                qual = "1 álcool";
                return true;
            }
            if (MelhorGalao(out var galao) < FogoDoJogador.LitrosPorCarga - 0.01f) return false;
            int k = Indice(galao);
            if (k < 0) return false;
            var it = Mochila[k];
            float sobra = Mathf.Max(0f, LitrosDe(it) - FogoDoJogador.LitrosPorCarga);
            it.desgaste = (byte)Mathf.Clamp(Mathf.RoundToInt(100f - sobra / Itens.LitrosDoGalao * 100f), 0, 100);
            Mochila[k] = it;
            qual = $"{FogoDoJogador.LitrosPorCarga:0.#} L de gasolina";
            return true;
        }

        /// <summary>O texto do combustível (a tela).</summary>
        public static string NomeDoCombustivel => $"1 combustível (álcool ou {FogoDoJogador.LitrosPorCarga:0.#} L de gasolina)";

        /// <summary>O texto de n combustíveis (a tela; 11/11/2026, a cerca de fogo).</summary>
        public static string NomeDosCombustiveis(int n) => n <= 1 ? NomeDoCombustivel
            : $"{n} combustíveis (cada um, álcool ou {FogoDoJogador.LitrosPorCarga:0.#} L de gasolina)";

        // ------------------------------------------------------------------ o lampião

        public bool PodeReabastecer(ushort id) => Achar(id, out var it) && it.item == Item.Lampiao && it.desgaste > 0 && TemCombustivel;

        public void Reabastecer(ushort id)
        {
            if (!PodeReabastecer(id)) return;
            ReabastecerRpc(id);
        }

        [Rpc(SendTo.Server)]
        void ReabastecerRpc(ushort id, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || !PodeReabastecer(id)) return;
            if (!GastarCombustivel(out string qual)) return;
            int k = Indice(id);
            if (k < 0) return;
            var it = Mochila[k];
            it.desgaste = 0;
            Mochila[k] = it;
            AvisoRpc($"O lampião foi reabastecido ({qual})");
        }

        /// <summary>No anfitrião, ao largar: o lampião aceso da mão continua aceso no chão (o qtd dele lá).</summary>
        int QtdAoLargar(ushort id, ItemNaMochila it) =>
            it.item == Item.Lampiao && id == IdNaMao && _rede != null && _rede.LampiaoAceso ? FogoDoJogador.LampiaoAcesoNoChao
            : it.item == Item.Lampiao ? 0 : it.qtd;

        /// <summary>No anfitrião, depois de largar: o dono do lampião aceso no chão (a Muralha de fogo dele).</summary>
        void DepoisDeLargar(ItemNaMochila it, int qtd)
        {
            if (it.item != Item.Lampiao || qtd != FogoDoJogador.LampiaoAcesoNoChao || ItensNoMundo.Instancia == null) return;
            ItensNoMundo.Instancia.DonoDoLampiao(ItensNoMundo.Instancia.UltimoCriado, _rede != null ? _rede.Chave.Value : 0UL);
        }

        // ------------------------------------------------------------------ o molotov

        /// <summary>O dono: a garrafa saiu da mão (o quadro 8 do arremesso), da origem, na direção (o anfitrião a joga).</summary>
        public void ArremessarMolotov(ushort id, Vector3 origem, Vector3 direcao) => ArremessarMolotovRpc(id, origem, direcao);

        [Rpc(SendTo.Server)]
        void ArremessarMolotovRpc(ushort id, Vector3 origem, Vector3 direcao, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || !Achar(id, out var it) || it.item != Item.Molotov) return;
            Vector3 olho = transform.position + Vector3.up * 1.6f;
            if ((origem - olho).sqrMagnitude > 1.5f * 1.5f) origem = olho;
            if (direcao.sqrMagnitude < 0.01f) direcao = transform.forward;
            TirarUm(id);
            // a próxima garrafa da mochila vai para a mão (como o pente seguinte)
            foreach (var o in Mochila)
                if (o.item == Item.Molotov)
                {
                    PorNaMao(o.id);
                    break;
                }
            ConstrucoesNoMundo.Instancia?.LancarMolotov(origem, direcao.normalized * FogoDoJogador.VelocidadeDoArremesso, _rede);
        }
    }
}
