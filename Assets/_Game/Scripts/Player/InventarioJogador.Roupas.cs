using System;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using Random = UnityEngine.Random;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// As roupas vestidas, uma por lugar (<see cref="LugarDaRoupa"/>: o chapéu, os óculos, a máscara, a camiseta, as luvas,
    /// a jaqueta ou o colete, o cinto ou o coldre, a calça e o sapato), cada uma com as cores dela. Vai pela rede inteira (uns
    /// 90 bytes), escrita pelo anfitrião.
    /// </summary>
    public struct RoupasVestidas : INetworkSerializeByMemcpy, IEquatable<RoupasVestidas>
    {
        public Item chapeu, oculos, mascara, camiseta, luvas, porCima, cinto, calca, sapato;
        public ulong coresChapeu, coresOculos, coresMascara, coresCamiseta, coresLuvas, coresPorCima, coresCinto, coresCalca, coresSapato;

        /// <summary>A roupa vestida no lugar (Nada: vazio).</summary>
        public Item Peca(LugarDaRoupa l) => l switch
        {
            LugarDaRoupa.Chapeu => chapeu, LugarDaRoupa.Oculos => oculos, LugarDaRoupa.Mascara => mascara,
            LugarDaRoupa.Camiseta => camiseta, LugarDaRoupa.Luvas => luvas, LugarDaRoupa.PorCima => porCima,
            LugarDaRoupa.Cinto => cinto, LugarDaRoupa.Calca => calca, _ => sapato,
        };

        /// <summary>As cores da roupa vestida no lugar (0: as do catálogo).</summary>
        public ulong Cores(LugarDaRoupa l) => l switch
        {
            LugarDaRoupa.Chapeu => coresChapeu, LugarDaRoupa.Oculos => coresOculos, LugarDaRoupa.Mascara => coresMascara,
            LugarDaRoupa.Camiseta => coresCamiseta, LugarDaRoupa.Luvas => coresLuvas, LugarDaRoupa.PorCima => coresPorCima,
            LugarDaRoupa.Cinto => coresCinto, LugarDaRoupa.Calca => coresCalca, _ => coresSapato,
        };

        /// <summary>Veste (ou tira, com Nada) a roupa no lugar.</summary>
        public void Por(LugarDaRoupa l, Item item, ulong cores)
        {
            switch (l)
            {
                case LugarDaRoupa.Chapeu: chapeu = item; coresChapeu = cores; break;
                case LugarDaRoupa.Oculos: oculos = item; coresOculos = cores; break;
                case LugarDaRoupa.Mascara: mascara = item; coresMascara = cores; break;
                case LugarDaRoupa.Camiseta: camiseta = item; coresCamiseta = cores; break;
                case LugarDaRoupa.Luvas: luvas = item; coresLuvas = cores; break;
                case LugarDaRoupa.PorCima: porCima = item; coresPorCima = cores; break;
                case LugarDaRoupa.Cinto: cinto = item; coresCinto = cores; break;
                case LugarDaRoupa.Calca: calca = item; coresCalca = cores; break;
                default: sapato = item; coresSapato = cores; break;
            }
        }

        public bool Equals(RoupasVestidas o)
        {
            for (int k = 0; k < Itens.QuantosLugaresDeRoupa; k++)
                if (Peca((LugarDaRoupa)k) != o.Peca((LugarDaRoupa)k) || Cores((LugarDaRoupa)k) != o.Cores((LugarDaRoupa)k)) return false;
            return true;
        }

        public override bool Equals(object o) => o is RoupasVestidas r && Equals(r);
        public override int GetHashCode() => HashCode.Combine(chapeu, camiseta, porCima, calca, sapato, coresCamiseta, coresCalca);
    }

    /// <summary>
    /// As roupas do inventário (a parte 4 do Plano_Personagem.md da pasta de arte, 31/10/2026): as achadas vão para a
    /// grade (uma célula cada, com as cores delas: <see cref="ItemNaMochila.cores"/>) e se vestem nos lugares em volta do
    /// retrato da tela (<see cref="Roupas"/>). Vestir troca com a que estava no lugar (ela vai para a célula da vestida);
    /// tirar volta para a grade (se houver espaço) ou larga no chão. As do começo (a camiseta, a calça e o tênis da tela de
    /// criar) são vestidas pelo anfitrião quando o personagem é criado (<see cref="VestirOComeco"/>). O corpo (a
    /// <see cref="ZombieCityCleaner.Roupas.Vestimenta"/>) é vestido pelo <see cref="JogadorRede"/>, em todas as máquinas.
    /// </summary>
    public partial class InventarioJogador
    {
        /// <summary>As roupas vestidas (do anfitrião).</summary>
        public readonly NetworkVariable<RoupasVestidas> Roupas = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>As roupas vestidas mudaram (em todas as máquinas).</summary>
        public event Action RoupasMudaram;

        // só no anfitrião: as do começo já foram vestidas (uma vez por partida)
        bool _comecoVestido;

        void NascerRoupas()
        {
            Roupas.OnValueChanged += AoMudarRoupas;
            if (IsServer)
            {
                Roupas.Value = default;
                _comecoVestido = false;
            }
        }

        void SumirRoupas() => Roupas.OnValueChanged -= AoMudarRoupas;

        void AoMudarRoupas(RoupasVestidas antes, RoupasVestidas depois)
        {
            RoupasMudaram?.Invoke();
            Mudou?.Invoke();
        }

        /// <summary>
        /// Só no anfitrião, quando o personagem é criado: veste as roupas do começo (as da tela de criar: a camiseta, a
        /// calça e o tênis de cano), uma vez por partida.
        /// </summary>
        public void VestirOComeco(AparenciaDoJogador a)
        {
            if (!IsServer || _comecoVestido || a.definida == 0) return;
            _comecoVestido = true;
            var r = Roupas.Value;
            foreach (var (item, cores) in a.RoupasDoComeco())
            {
                var dados = Itens.Roupa(item);
                if (dados != null && r.Peca(dados.lugar) == Item.Nada) r.Por(dados.lugar, item, cores);
            }
            Roupas.Value = r;
        }

        /// <summary>Há uma célula livre na grade (as roupas ocupam uma)?</summary>
        public bool CabeUmaRoupa => TemEspaco(Item.PrimeiraRoupa);

        // ------------------------------------------------------------------ o dono pede

        /// <summary>Veste a roupa da mochila no lugar dela (a que estava lá vai para a célula dela).</summary>
        public void Vestir(ushort id)
        {
            if (!Itens.EhRoupa(Do(id))) return;
            VestirRpc(id);
        }

        /// <summary>Tira a roupa do lugar para a grade: na célula pedida (se livre) ou na primeira livre.</summary>
        public void TirarRoupa(LugarDaRoupa l, int x = -1, int y = -1)
        {
            if (Roupas.Value.Peca(l) == Item.Nada) return;
            TirarRoupaRpc(l, (short)x, (short)y);
        }

        /// <summary>Tira a roupa do lugar e larga no chão (à frente, como o G).</summary>
        public void LargarRoupa(LugarDaRoupa l)
        {
            if (Roupas.Value.Peca(l) == Item.Nada) return;
            LargarRoupaRpc(l, PontoParaLargar(_rede.Olho), transform.eulerAngles.y + Random.Range(-50f, 50f));
            Efeitos.Som(Efeitos.Largar, transform.position + Vector3.up * 0.8f, 0.5f, false);
        }

        // ------------------------------------------------------------------ no anfitrião

        [Rpc(SendTo.Server)]
        void VestirRpc(ushort id, RpcParams parametros = default)
        {
            int k = Indice(id);
            if (parametros.Receive.SenderClientId != OwnerClientId || k < 0) return;
            var it = Mochila[k];
            var dados = Itens.Roupa(it.item);
            if (dados == null) return;
            var r = Roupas.Value;
            var antes = r.Peca(dados.lugar);
            ulong coresAntes = r.Cores(dados.lugar);
            r.Por(dados.lugar, it.item, it.cores);
            // a que estava no lugar vai para a célula da que foi vestida (as duas ocupam uma)
            if (antes != Item.Nada) Mochila[k] = new ItemNaMochila { id = NovoId(), item = antes, x = it.x, y = it.y, cores = coresAntes };
            else Mochila.RemoveAt(k);
            Roupas.Value = r;
        }

        [Rpc(SendTo.Server)]
        void TirarRoupaRpc(LugarDaRoupa l, short x, short y, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            var r = Roupas.Value;
            var item = r.Peca(l);
            if (item == Item.Nada) return;
            int cx, cy;
            bool girado = false;
            if (x >= 0 && y >= 0 && x < Colunas && y < Linhas && Cabe(item, false, x, y)) { cx = x; cy = y; }
            else if (!Espaco(item, out cx, out cy, out girado))
            {
                AvisoRpc($"Sem espaço {(MochilaVestida.Value != Item.Nada ? "na mochila" : "nos bolsos")} para {Itens.NomeComArtigo(item)}");
                return;
            }
            Mochila.Add(new ItemNaMochila { id = NovoId(), item = item, x = (byte)cx, y = (byte)cy, girado = girado, cores = r.Cores(l) });
            r.Por(l, Item.Nada, 0);
            Roupas.Value = r;
        }

        [Rpc(SendTo.Server)]
        void LargarRoupaRpc(LugarDaRoupa l, Vector3 ponto, float giro, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || ItensNoMundo.Instancia == null) return;
            var r = Roupas.Value;
            var item = r.Peca(l);
            if (item == Item.Nada) return;
            if ((ponto - transform.position).sqrMagnitude > 4f * 4f) ponto = transform.position;
            ItensNoMundo.Instancia.Criar(item, ponto, giro, 0, 0, r.Cores(l));
            r.Por(l, Item.Nada, 0);
            Roupas.Value = r;
        }
    }
}
