using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using Random = UnityEngine.Random;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A proteção de sucata vestida (11/11/2026, a etapa 3; os números no <see cref="Player.Protecao"/>): uma peça por lugar
    /// (o capacete, o colete, as braçadeiras e as caneleiras), cada uma com o desgaste dela, como as roupas (vestir troca
    /// com a que estava no lugar; tirar volta para a mochila ou larga no chão). O capacete não vai com o chapéu: vestir um
    /// manda o outro para a mochila. O anfitrião corta o golpe e gasta a peça (<see cref="ProtegerDoGolpe"/>, chamado pelo
    /// <see cref="VidaJogador"/>). O corpo é vestido pelo <see cref="ProtecaoNoCorpo"/>, em todas as máquinas.
    /// </summary>
    public partial class InventarioJogador
    {
        /// <summary>A proteção vestida (do anfitrião).</summary>
        public readonly NetworkVariable<ProtecaoVestida> Protecao = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>A proteção vestida mudou (em todas as máquinas; também o desgaste).</summary>
        public event Action ProtecaoMudou;

        void NascerProtecao()
        {
            Protecao.OnValueChanged += AoMudarProtecao;
            if (IsServer) Protecao.Value = default;
        }

        void SumirProtecao() => Protecao.OnValueChanged -= AoMudarProtecao;

        void AoMudarProtecao(ProtecaoVestida antes, ProtecaoVestida depois)
        {
            ProtecaoMudou?.Invoke();
            Mudou?.Invoke();
        }

        // ------------------------------------------------------------------ o dono pede

        /// <summary>Veste a peça da mochila no lugar dela (a que estava lá volta para a mochila).</summary>
        public void VestirProtecao(ushort id)
        {
            if (!Player.Protecao.EhPeca(Do(id))) return;
            VestirProtecaoRpc(id);
        }

        /// <summary>Tira a peça do lugar para a mochila: na célula pedida (se cabe) ou na primeira livre.</summary>
        public void TirarProtecao(LugarDaProtecao l, int x = -1, int y = -1)
        {
            if (Protecao.Value.Peca(l) == Item.Nada) return;
            TirarProtecaoRpc(l, (short)x, (short)y);
        }

        /// <summary>Tira a peça do lugar e larga no chão (à frente, como o G).</summary>
        public void LargarProtecao(LugarDaProtecao l)
        {
            if (Protecao.Value.Peca(l) == Item.Nada) return;
            LargarProtecaoRpc(l, PontoParaLargar(_rede.Olho), transform.eulerAngles.y + Random.Range(-50f, 50f));
            Efeitos.Som(Efeitos.Largar, transform.position + Vector3.up * 0.8f, 0.5f, false);
        }

        /// <summary>A peça vestida pode ser consertada agora (gasta, com um kit de reparo na mochila); 'motivo' diz por que não.</summary>
        public bool PodeConsertarProtecao(LugarDaProtecao l, out string motivo)
        {
            var p = Protecao.Value;
            motivo = null;
            if (p.Peca(l) == Item.Nada || p.Desgaste(l) <= 0) motivo = "inteira";
            else if (Player.Protecao.ConsertoPedeORemendo && !Habilidades.Tem(_rede, Habilidade.RemendoDeChapa)) motivo = "precisa do Remendo de chapa";
            else if (Quantos(Item.KitDeReparo) <= 0) motivo = "precisa de um kit de reparo";
            return motivo == null;
        }

        /// <summary>Conserta a peça vestida com um kit de reparo (o anfitrião confere e gasta o kit).</summary>
        public void ConsertarProtecao(LugarDaProtecao l)
        {
            if (!PodeConsertarProtecao(l, out _)) return;
            ConsertarProtecaoRpc(l);
            Efeitos.Som(Efeitos.Largar, transform.position + Vector3.up * 1.1f, 0.6f, false);
        }

        // ------------------------------------------------------------------ no anfitrião

        // a peça (com o desgaste) volta para a mochila: na célula (x, y) se cabe, senão na primeira livre; sem espaço, cai aos pés
        void GuardarOuLargar(Item item, int desgaste, ulong cores, int x, int y, ref int caidos)
        {
            bool girado = false;
            if (!(x >= 0 && y >= 0 && x < Colunas && y < Linhas && Cabe(item, false, x, y)) && !Espaco(item, out x, out y, out girado))
            {
                ItensNoMundo.Instancia?.Criar(item, PontoAosPes(++caidos), transform.eulerAngles.y + caidos * 70f, 0, desgaste, cores);
                AvisoRpc($"Sem espaço {(MochilaVestida.Value != Item.Nada ? "na mochila" : "nos bolsos")}: {Itens.NomeComArtigo(item)} caiu no chão");
                return;
            }
            Mochila.Add(new ItemNaMochila { id = NovoId(), item = item, x = (byte)x, y = (byte)y, girado = girado, desgaste = (byte)desgaste, cores = cores });
        }

        [Rpc(SendTo.Server)]
        void VestirProtecaoRpc(ushort id, RpcParams parametros = default)
        {
            int k = Indice(id);
            if (parametros.Receive.SenderClientId != OwnerClientId || k < 0) return;
            var it = Mochila[k];
            if (!Player.Protecao.EhPeca(it.item)) return;
            var l = Player.Protecao.LugarDe(it.item);
            var p = Protecao.Value;
            var antes = p.Peca(l);
            int gastoAntes = p.Desgaste(l);
            Mochila.RemoveAt(k);
            p.Por(l, it.item, it.desgaste);
            int caidos = 0;
            // a que estava no lugar volta para a célula da vestida (ou outra)
            if (antes != Item.Nada) GuardarOuLargar(antes, gastoAntes, 0, it.x, it.y, ref caidos);
            // o capacete não vai com o chapéu: o chapéu volta para a mochila
            if (l == LugarDaProtecao.Capacete && Roupas.Value.Peca(LugarDaRoupa.Chapeu) != Item.Nada)
            {
                var r = Roupas.Value;
                var chapeu = r.Peca(LugarDaRoupa.Chapeu);
                GuardarOuLargar(chapeu, 0, r.Cores(LugarDaRoupa.Chapeu), antes == Item.Nada ? it.x : -1, antes == Item.Nada ? it.y : -1, ref caidos);
                r.Por(LugarDaRoupa.Chapeu, Item.Nada, 0);
                Roupas.Value = r;
                AvisoRpc($"{TelaDoInventario.Maiuscula(Itens.NomeComArtigo(chapeu))} foi para a mochila: não cabe com o capacete");
            }
            Protecao.Value = p;
        }

        /// <summary>
        /// Só no anfitrião, ao vestir um chapéu (<see cref="VestirRpc"/>): o capacete sai da cabeça para a mochila, na
        /// célula (x, y) do chapéu se couber. Devolve false (e avisa) se não há espaço para ele: o chapéu não é vestido.
        /// </summary>
        bool TirarOCapaceteParaOChapeu(int x, int y, ushort chapeu)
        {
            var p = Protecao.Value;
            if (p.capacete == Item.Nada) return true;
            bool girado = false;
            if (!Cabe(p.capacete, false, x, y, chapeu) && !Espaco(p.capacete, out x, out y, out girado))
            {
                AvisoRpc("Sem espaço para o capacete: o chapéu não cabe com ele");
                return false;
            }
            Mochila.Add(new ItemNaMochila { id = NovoId(), item = p.capacete, x = (byte)x, y = (byte)y, girado = girado, desgaste = p.gastoCapacete });
            AvisoRpc($"{TelaDoInventario.Maiuscula(Itens.NomeComArtigo(p.capacete))} foi para a mochila: não cabe com o chapéu");
            p.Por(LugarDaProtecao.Capacete, Item.Nada, 0);
            Protecao.Value = p;
            return true;
        }

        [Rpc(SendTo.Server)]
        void TirarProtecaoRpc(LugarDaProtecao l, short x, short y, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            var p = Protecao.Value;
            var item = p.Peca(l);
            if (item == Item.Nada) return;
            int cx, cy;
            bool girado = false;
            if (x >= 0 && y >= 0 && x < Colunas && y < Linhas && Cabe(item, false, x, y)) { cx = x; cy = y; }
            else if (!Espaco(item, out cx, out cy, out girado))
            {
                AvisoRpc($"Sem espaço {(MochilaVestida.Value != Item.Nada ? "na mochila" : "nos bolsos")} para {Itens.NomeComArtigo(item)}");
                return;
            }
            Mochila.Add(new ItemNaMochila { id = NovoId(), item = item, x = (byte)cx, y = (byte)cy, girado = girado, desgaste = p.Desgaste(l) });
            p.Por(l, Item.Nada, 0);
            Protecao.Value = p;
        }

        [Rpc(SendTo.Server)]
        void LargarProtecaoRpc(LugarDaProtecao l, Vector3 ponto, float giro, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || ItensNoMundo.Instancia == null) return;
            var p = Protecao.Value;
            var item = p.Peca(l);
            if (item == Item.Nada) return;
            if ((ponto - transform.position).sqrMagnitude > 4f * 4f) ponto = transform.position;
            ItensNoMundo.Instancia.Criar(item, ponto, giro, 0, p.Desgaste(l));
            p.Por(l, Item.Nada, 0);
            Protecao.Value = p;
        }

        [Rpc(SendTo.Server)]
        void ConsertarProtecaoRpc(LugarDaProtecao l, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || !PodeConsertarProtecao(l, out _)) return;
            bool poupou = PoupouOKit();   // (o Reparador também vale aqui)
            if (!poupou && !GastarItem(Item.KitDeReparo, 1)) return;
            var p = Protecao.Value;
            var item = p.Peca(l);
            p.Por(l, item, 0);
            Protecao.Value = p;
            AvisoRpc(Player.Protecao.Consertada(item) + (poupou ? " (o kit não foi gasto)" : ""));
        }

        /// <summary>
        /// Só no anfitrião, no golpe que pegou na 'parte' (<see cref="VidaJogador"/>, o Ferir): a peça inteira que cobre a
        /// parte corta o golpe e se gasta com ele (o Remendo de chapa gasta menos); no último ponto, quebra (segura este
        /// golpe e não protege mais). Devolve o golpe que passa. 'couraca': a mordida pegou numa peça, com a Couraça (a
        /// ferida que ela abrir não infecciona).
        /// </summary>
        public float ProtegerDoGolpe(ParteDoCorpo parte, TipoDeGolpe tipo, float valor, out bool couraca)
        {
            couraca = false;
            if (!IsServer || !IsSpawned || valor <= 0f || !Player.Protecao.Segura(tipo) || !PartesDoCorpo.Valida(parte)) return valor;
            var l = Player.Protecao.LugarQueCobre(parte);
            var p = Protecao.Value;
            if (!p.Protege(l)) return valor;
            var item = p.Peca(l);
            int novo = Mathf.Min(100, p.Desgaste(l) + Player.Protecao.GastoDoGolpe(l, valor, Habilidades.Pontos(_rede, Habilidade.RemendoDeChapa)));
            p.Por(l, item, novo);
            Protecao.Value = p;
            if (novo >= 100) AvisoRpc(Player.Protecao.Quebrou(item));
            couraca = tipo == TipoDeGolpe.Mordida && Habilidades.Tem(_rede, Habilidade.Couraca);
            return valor * (1f - Player.Protecao.Corte(l));
        }

        /// <summary>Só no anfitrião, na morte (<see cref="MochilasNoChao.DaMorte"/>): as peças vestidas vão para a mochila do chão.</summary>
        public void TirarAProtecao(List<ItemNaMochila> itens)
        {
            if (!IsServer) return;
            var p = Protecao.Value;
            for (int k = 0; k < Player.Protecao.Quantos; k++)
            {
                var l = (LugarDaProtecao)k;
                if (p.Peca(l) != Item.Nada) itens.Add(new ItemNaMochila { item = p.Peca(l), qtd = 1, desgaste = p.Desgaste(l) });
            }
            Protecao.Value = default;
        }

        /// <summary>
        /// Só no anfitrião, quando o personagem salvo entra (<see cref="Aplicar"/>, depois das roupas): as peças que ele
        /// vestia. Com o capacete e um chapéu (não deveria acontecer), o chapéu vai para a mochila.
        /// </summary>
        void VestirAProtecaoSalva(ProtecaoVestida salva)
        {
            if (!IsServer) return;
            var p = new ProtecaoVestida();
            for (int k = 0; k < Player.Protecao.Quantos; k++)
            {
                var l = (LugarDaProtecao)k;
                var item = salva.Peca(l);
                if (Player.Protecao.EhPeca(item) && Player.Protecao.LugarDe(item) == l) p.Por(l, item, salva.Desgaste(l));
            }
            if (p.capacete != Item.Nada && Roupas.Value.Peca(LugarDaRoupa.Chapeu) != Item.Nada)
            {
                var r = Roupas.Value;
                int caidos = 0;
                GuardarOuLargar(r.Peca(LugarDaRoupa.Chapeu), 0, r.Cores(LugarDaRoupa.Chapeu), -1, -1, ref caidos);
                r.Por(LugarDaRoupa.Chapeu, Item.Nada, 0);
                Roupas.Value = r;
            }
            Protecao.Value = p;
        }
    }
}
