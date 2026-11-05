using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// O fogo na chuva (05/11/2026, a pedido: "apagar [a tocha e a fogueira] durante a chuva"; o Pedro escolheu "qualquer
    /// chuva, até o chuvisco"). No anfitrião, a cada meio segundo (o tique do <see cref="ConstrucoesNoMundo"/>):
    /// - a fogueira acesa fora de um teto (<see cref="Tempo.SobTeto"/>) vai molhando com a chuva (<see cref="Tempo.Chuva"/>):
    ///   apaga em <see cref="SegundosParaApagarAFogueira"/> dividido pela chuva (o chuvisco, 0,28: uns 40 s; a chuva, 0,62:
    ///   uns 18 s; a tempestade: uns 12 s). Apagada, a lenha que sobrava se perde (como na gosma do Long) e quem está perto
    ///   recebe o aviso. Na chuva, fora de um teto, ela não acende nem pega lenha (a madeira não se perde);
    /// - a tocha acesa na mão de quem está fora de um teto (e fora do carro) molha na metade do tempo e apaga: fica na mão,
    ///   apagada (<see cref="Itens.TochaApagada"/>), e reacende numa fogueira acesa (E nela, com a tocha na mão);
    /// - debaixo de um teto, ou sem chuva, a molhadura seca em <see cref="SegundosParaSecar"/> s.
    /// Longe de todos, o anfitrião não tem a colisão em volta (o TerrenoDoMundo monta só perto dos jogadores): a fogueira
    /// guarda o último teto que conferiu e, sem nenhum, não molha até alguém chegar perto (senão a de dentro de uma casa
    /// apagaria sozinha). Os tetos são os de colisão: a casa, o prédio, a laje, o viaduto, o carro e o bunker; a copa da
    /// árvore, o toldo e a marquise não seguram a chuva (a que se vê também passa por eles).
    /// </summary>
    public partial class ConstrucoesNoMundo
    {
        public const float SegundosParaApagarAFogueira = 11.2f;
        public const float SegundosParaApagarATocha = 5.6f;
        public const float SegundosParaSecar = 20f;
        /// <summary>A partir daqui a chuva molha o fogo (o chuvisco é 0,28; antes disso há só as nuvens).</summary>
        public const float ChuvaQueMolha = 0.05f;
        const float ConferirOTetoACada = 5f;
        const float AvisarAte = 25f;
        const float InformarAte = 120f;

        readonly Dictionary<ushort, float> _molhadas = new Dictionary<ushort, float>();
        readonly Dictionary<ushort, (bool coberta, float em)> _tetos = new Dictionary<ushort, (bool, float)>();
        // a molhadura da tocha de cada jogador, com o Id dela (trocar para outra tocha começa seca)
        readonly Dictionary<ulong, (ushort id, float m)> _tochasMolhadas = new Dictionary<ulong, (ushort, float)>();
        // no anfitrião: as fogueiras em que um cliente viu um teto (o posto, a casa do acampamento: o anfitrião só monta os de
        // perto do jogador dele); no cliente: as que ele já contou
        readonly HashSet<ushort> _tetosVistos = new HashSet<ushort>();
        readonly HashSet<ushort> _tetosInformados = new HashSet<ushort>();
        float _proximoInforme;

        /// <summary>Chove o bastante para molhar o fogo (no anfitrião, a chuva dele; nos outros, a suavizada que chega).</summary>
        public static bool ChoveNoFogo => Tempo.Chuva > ChuvaQueMolha;

        /// <summary>Aqui a chuva molha o fogo agora (chove e não há teto): para as dicas, em qualquer máquina.</summary>
        public static bool ChuvaEm(Vector3 pes) => ChoveNoFogo && !Tempo.SobTeto(pes);

        /// <summary>No anfitrião: o quanto a fogueira já molhou (0 a 1; o F1).</summary>
        public float Molhada(ushort id) => _molhadas.TryGetValue(id, out var m) ? m : 0f;

        /// <summary>No anfitrião: o quanto a tocha da mão do jogador já molhou (0 a 1; o F1).</summary>
        public float TochaMolhada(ulong cliente) => _tochasMolhadas.TryGetValue(cliente, out var t) ? t.m : 0f;

        /// <summary>
        /// No anfitrião: a fogueira está debaixo de um teto (conferido a cada <see cref="ConferirOTetoACada"/> s; sem a
        /// colisão em volta, o último conferido; sem nenhum, null).
        /// </summary>
        bool? Coberta(Construcao c)
        {
            if (_tetosVistos.Contains(c.id)) return true;
            if (_tetos.TryGetValue(c.id, out var t) && Time.time - t.em < ConferirOTetoACada) return t.coberta;
            bool coberta;
            if (BunkerDoMundo.Abrigado(c.pos)) coberta = true;
            else
            {
                var terreno = TerrenoDoMundo.Instancia;
                if (terreno != null && !terreno.Pronto(c.pos, 3f)) return _tetos.TryGetValue(c.id, out t) ? t.coberta : (bool?)null;
                coberta = Tempo.SobTeto(c.pos);
            }
            _tetos[c.id] = (coberta, Time.time);
            return coberta;
        }

        /// <summary>No anfitrião: a fogueira está na chuva agora (chove e ela está fora de um teto conferido).</summary>
        bool NaChuva(Construcao c) => ChoveNoFogo && Coberta(c) == false;

        /// <summary>No anfitrião, no tique de meio segundo: a chuva molha as fogueiras e as tochas fora do teto.</summary>
        void MolharNaChuva(float passo)
        {
            float chuva = Tempo.Chuva;
            float seca = passo / SegundosParaSecar;
            for (int k = 0; k < _lista.Count; k++)
            {
                var c = _lista[k];
                if (c.tipo != Item.Fogueira) continue;
                if (!Acesa(c))
                {
                    _molhadas.Remove(c.id);
                    continue;
                }
                _molhadas.TryGetValue(c.id, out float m);
                m = NaChuva(c) ? m + passo * chuva / SegundosParaApagarAFogueira : Mathf.Max(0f, m - seca);
                if (m >= 1f)
                {
                    c.valor = Agora;   // apagou: a lenha que sobrava se perde (como na gosma); E acende de novo, sem chuva
                    _lista[k] = c;
                    _molhadas.Remove(c.id);
                    AvisarPerto(c.pos, "A chuva apagou a fogueira");
                    continue;
                }
                _molhadas[c.id] = m;
            }

            foreach (var cliente in NetworkManager.ConnectedClientsList)
            {
                var objeto = cliente.PlayerObject;
                var rede = objeto != null ? objeto.GetComponent<JogadorRede>() : null;
                ulong quem = cliente.ClientId;
                // a molhadura é da tocha: com outra tocha na mão, começa seca; com outra coisa (a pistola por um instante),
                // a da tocha guardada seca aos poucos e volta com ela
                bool temTocha = rede != null && rede.NaMao == Item.Tocha;
                bool tem = _tochasMolhadas.TryGetValue(quem, out var t);
                ushort idNaMao = temTocha ? rede.Inventario.IdNaMao : tem ? t.id : (ushort)0;
                float m = tem && t.id == idNaMao ? t.m : 0f;
                // o teto: o do anfitrião ou o que o dono viu (o posto, a casa do acampamento longe do anfitrião)
                bool molha = rede != null && rede.TochaAcesa && ChoveNoFogo && !rede.DebaixoDeTeto.Value && !Tempo.SobTeto(objeto.transform.position);
                m = molha ? m + passo * chuva / SegundosParaApagarATocha : Mathf.Max(0f, m - seca);
                if (m >= 1f)
                {
                    rede.Inventario.MolharTochaNaMao();
                    m = 0f;
                }
                if (m > 0f) _tochasMolhadas[quem] = (idNaMao, m);
                else _tochasMolhadas.Remove(quem);
            }
        }

        /// <summary>
        /// No cliente, na chuva (a cada 2 s): as fogueiras perto dele (até <see cref="InformarAte"/> m: o anfitrião monta a
        /// colisão do chão até uns 90 m de cada jogador, mas não o posto nem a casa do acampamento longe do jogador dele) com
        /// um teto em cima. Cada uma é contada uma vez.
        /// </summary>
        void InformarTetos()
        {
            if (IsServer || Time.time < _proximoInforme || Tempo.Chuva <= 0.01f) return;
            _proximoInforme = Time.time + 2f;
            var local = JogadorRede.Local;
            if (local == null) return;
            Vector3 pe = local.transform.position;
            foreach (var c in _lista)
            {
                if (c.tipo != Item.Fogueira || _tetosInformados.Contains(c.id) || (c.pos - pe).sqrMagnitude > InformarAte * InformarAte) continue;
                if (!Tempo.SobTeto(c.pos)) continue;
                _tetosInformados.Add(c.id);
                TetoDaFogueiraRpc(c.id);
            }
        }

        [Rpc(SendTo.Server)]
        void TetoDaFogueiraRpc(ushort id) => _tetosVistos.Add(id);

        // as contas da chuva começam de novo (o save carregado: os ids recomeçam)
        void EsquecerAChuva()
        {
            _molhadas.Clear();
            _tetos.Clear();
            _tochasMolhadas.Clear();
            _tetosVistos.Clear();
            _tetosInformados.Clear();
        }

        // o aviso para quem está perto (no HUD dele)
        void AvisarPerto(Vector3 ponto, string texto)
        {
            foreach (var cliente in NetworkManager.ConnectedClientsList)
            {
                var objeto = cliente.PlayerObject;
                if (objeto == null || (objeto.transform.position - ponto).sqrMagnitude > AvisarAte * AvisarAte) continue;
                objeto.GetComponent<InventarioJogador>()?.AvisarDono(texto);
            }
        }

        // ------------------------------------------------------------------ os zumbis e o F1

        /// <summary>As fogueiras acesas (o id e o ponto no chão): os zumbis ficam longe delas (ZombieManager.Luz.cs).</summary>
        public void FogueirasAcesas(List<(ushort id, Vector3 pos)> saida)
        {
            foreach (var c in _lista)
                if (Acesa(c)) saida.Add((c.id, c.pos));
        }

        /// <summary>A fogueira acesa mais perto do ponto (até o alcance; o F1).</summary>
        public bool FogueiraPerto(Vector3 ponto, float alcance, out Construcao fogueira)
        {
            fogueira = default;
            float menor = alcance * alcance;
            bool achou = false;
            foreach (var c in _lista)
            {
                if (c.tipo != Item.Fogueira) continue;
                float d = (c.pos - ponto).sqrMagnitude;
                if (d >= menor) continue;
                menor = d;
                fogueira = c;
                achou = true;
            }
            return achou;
        }

        /// <summary>No anfitrião, o F1: o resumo da fogueira (o teto e o quanto molhou).</summary>
        public string ResumoDaFogueira(Construcao c) =>
            (Acesa(c) ? $"acesa ({Mathf.Max(0f, c.valor - Agora) / 60f:0.0} min)" : "apagada")
            + (Coberta(c) switch { true => " · coberta", false => " · no aberto", _ => " · teto ?" })
            + (Acesa(c) ? $" · molhou {Molhada(c.id) * 100f:0}%" : "");

        /// <summary>No anfitrião, o F1: uma fogueira acesa no chão, no ponto (com a lenha de um kit).</summary>
        public void FogueiraDeTeste(Vector3 ponto, float giro, ulong dono)
        {
            if (!IsServer || !IsSpawned) return;
            if (Physics.Raycast(ponto + Vector3.up * 1.5f, Vector3.down, out var chao, 4f, 1, QueryTriggerInteraction.Ignore)) ponto = chao.point;
            _lista.Add(new Construcao { id = _proximoId++, tipo = Item.Fogueira, pos = ponto, giro = giro, valor = Agora + MinutosPorLenha * 60f, dono = dono });
        }
    }
}
