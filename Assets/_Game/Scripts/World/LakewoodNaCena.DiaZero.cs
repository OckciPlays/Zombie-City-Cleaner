using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O Dia 0 na cena do Lakewood (22/11/2026, a etapa 4 do Docs/23-Campanha-e-Mapa.md, "o D0 na Woodcreek"). Com o Dia 0 da
    /// rede (<see cref="MundoDeLakewood.NoDiaZero"/>), em cada máquina:
    ///  - o palco entra (o prefab <see cref="palcoDoDiaZero"/>: a <see cref="WoodcreekDoDiaZero"/>, a introdução, os NPCs e os
    ///    carros), e os pontos de nascimento passam para a sala da casa da família; com a época de depois, ele sai e os
    ///    nascimentos voltam para a sala de operações do bunker;
    ///  - as lojas que vendem abrem quando a obra delas entra (<see cref="PorAObra"/> chama a <see cref="WoodcreekDoDiaZero.AbrirALoja"/>);
    ///  - quem está na casa espera parado até ela ter a colisão (<see cref="EsperarACasa"/>: o jogador nasce antes da obra);
    ///  - no anfitrião, a caminhonete da família na entrada da garagem (<see cref="caminhonete"/>): as compras do dia vão para a
    ///    caçamba dela (<see cref="GuardarNaCacamba"/>, uma estante das <see cref="EstantesDoBunker"/>); no fim do dia, ela vai
    ///    para o pátio do bunker, com a chave no chão da sala de operações.
    /// </summary>
    public partial class LakewoodNaCena
    {
        [Header("O Dia 0 na Woodcreek (22/11/2026)")]
        [Tooltip("O palco do Dia 0 (Prefabs/DiaZero/PalcoDoDiaZeroNaWoodcreek: a WoodcreekDoDiaZero, a introdução, os NPCs e os carros da vila).")]
        public GameObject palcoDoDiaZero;
        [Tooltip("A caminhonete da família (Prefabs/Veiculos/Caminhonete): na entrada da garagem no Dia 0, com as compras na caçamba.")]
        public GameObject caminhonete;

        GameObject _palco;
        bool _caminhonetePosta, _esperandoACasa, _chavePosta;
        int _chaveDoDia = -1;
        NetworkObject _caminhoneteDoDia;
        readonly Dictionary<int, ObraNaCena> _obraDoLote = new Dictionary<int, ObraNaCena>();

        /// <summary>O palco do Dia 0 está na cena (a Woodcreek do Dia 0).</summary>
        public static bool ComPalcoDoDiaZero => Instancia != null && Instancia._palco != null;

        /// <summary>
        /// Em cada máquina (no Update, e já no spawn da <see cref="MundoDeLakewood"/>, antes de o jogador nascer): o palco entra
        /// com o Dia 0 e sai com a época de depois (a tela dos 5 anos já cobriu a troca).
        /// </summary>
        public void AcompanharODiaZero()
        {
            var m = MundoDeLakewood.Instancia;
            bool noDia = m != null && m.NoDiaZero;
            if (noDia && _palco == null && palcoDoDiaZero != null) CriarOPalco();
            else if (!noDia && _palco != null && !(m != null && m.Antes)) TirarOPalco();
            if (_palco != null)
            {
                PorACaminhonete();
                PorAChave();
                EsperarACasa();
            }
            else if (_esperandoACasa) SoltarDaCasa();
            AcompanharAFugaNaCena();   // (a fuga: LakewoodNaCena.Fuga.cs)
            AcompanharAMissao();   // (24/11/2026: a primeira missão depois dos 5 anos, LakewoodNaCena.Missao.cs)
        }

        void CriarOPalco()
        {
            _palco = Instantiate(palcoDoDiaZero);   // (a WoodcreekDoDiaZero monta no OnEnable)
            _palco.name = "Dia 0 (a Woodcreek)";
            var w = _palco.GetComponent<WoodcreekDoDiaZero>();
            if (w != null && w.Pronta)
            {
                PontosDeNascimento.Definir(w.Nascimentos, w.GirosDosNascimentos);
                // quem já nasceu (na sala de operações) vai já para a sala da casa
                var j = JogadorRede.Local;
                var rede = NetworkManager.Singleton;
                if (j != null && rede != null && !w.DentroDaCasa(j.transform.position))
                {
                    var pose = PontosDeNascimento.Ponto(rede.LocalClientId);
                    j.GetComponent<MovimentoPrimeiraPessoa>().Teleportar(pose.position, pose.rotation.eulerAngles.y);
                }
            }
            _caminhonetePosta = false;
            _chavePosta = false;
        }

        void TirarOPalco()
        {
            Destroy(_palco);
            _palco = null;
            if (_nascimentosNoBunker != null) PontosDeNascimento.Definir(_nascimentosNoBunker, _girosNoBunker);
            if (_esperandoACasa) SoltarDaCasa();
            var rede = NetworkManager.Singleton;
            if (rede != null && rede.IsServer)
            {
                // (24/11/2026) depois da fuga, a caminhonete fica quebrada na casa da família (a primeira missão); pulando o Dia 0
                // pelo F1, ela vai para o pátio do bunker, como antes
                var mundo = MundoDeLakewood.Instancia;
                if (mundo != null && mundo.Chegou) DeixarACaminhoneteNaCasa();
                else LevarACaminhoneteAoBunker();
            }
            Debug.Log("[Dia 0] O palco da Woodcreek saiu (a época de depois).");
        }

        // ------------------------------------------------------------------ as obras do lote (para o palco)

        ObraNaCena ObraNaCenaDoLote(int lote)
        {
            if (_obraDoLote.Count == 0) foreach (var o in _obras) _obraDoLote[o.Lote.Indice] = o;
            return _obraDoLote.TryGetValue(lote, out var obra) ? obra : null;
        }

        /// <summary>A obra do lote montada na cena (inteira, com as etapas acabadas), ou null.</summary>
        public GameObject ObraDoLote(int lote)
        {
            var o = ObraNaCenaDoLote(lote);
            return o != null && o.Perto != null && o.Pronta == null ? o.Perto : null;
        }

        /// <summary>A obra do lote está montada e com a colisão cozida (dá para pisar nela).</summary>
        public bool ObraComColisao(int lote)
        {
            var o = ObraNaCenaDoLote(lote);
            return o != null && o.Perto != null && o.Pronta == null && o.Cozinhando.Count == 0;
        }

        /// <summary>Onde fica a obra do lote no mundo (a mesma conta do PorAObra).</summary>
        public Pose PoseDaObra(ChaoDeLakewood.LoteNoChao l)
        {
            float acima = l.Plano != null && l.Plano.Uso == UsoDoLote.LojaDeArmas ? 0.03f - PlantaDaLojaDeArmas.ChaoDeFora : 0f;
            var meio = l.Plano != null ? l.Plano.Meio : Vector2.zero;
            float giro = l.Lote.giro + (l.Plano != null ? l.Plano.GiroNoLote : 0f);
            var pai = _paiDasObras != null ? _paiDasObras : _pai;
            return new Pose(pai.TransformPoint(l.NoMundo(meio.x, meio.y, acima)), pai.rotation * Quaternion.Euler(0f, giro, 0f));
        }

        // a obra de uma loja entrou na cena (a etapa dos móveis do PorAObra): no Dia 0, ela abre
        void AbrirNoDiaZero(GameObject go, ObraNaCena o, ObraPronta p, MalhasDasCasas.CasaPronta c)
        {
            var w = WoodcreekDoDiaZero.Atual;
            if (w == null || !_antes) return;
            var uso = o.Plano.Uso;
            if (uso == UsoDoLote.Loja || uso == UsoDoLote.Posto || uso == UsoDoLote.LojaDeArmas) w.AbrirALoja(go, o.Lote, o.Plano, p, c);
        }

        // ------------------------------------------------------------------ a casa da família

        // o jogador local dentro da casa da família, com a obra dela ainda sem a colisão (ele nasce antes): parado até ela
        void EsperarACasa()
        {
            var w = WoodcreekDoDiaZero.Atual;
            var j = JogadorRede.Local;
            var mov = j != null ? j.GetComponent<MovimentoPrimeiraPessoa>() : null;
            if (w == null || !w.Pronta || mov == null || w.LoteDaFamilia == null) return;
            bool falta = !mov.Voando && w.DentroDaCasa(j.transform.position) && !ObraComColisao(w.LoteDaFamilia.Indice);
            if (falta)
            {
                if (!mov.Esperando)
                {
                    mov.Esperando = true;
                    _esperandoACasa = true;
                }
            }
            else if (_esperandoACasa) SoltarDaCasa();
        }

        void SoltarDaCasa()
        {
            _esperandoACasa = false;
            var j = JogadorRede.Local;
            var mov = j != null ? j.GetComponent<MovimentoPrimeiraPessoa>() : null;
            if (mov != null) mov.Esperando = false;
        }

        // ------------------------------------------------------------------ a caminhonete e a caçamba (no anfitrião)

        /// <summary>O número da estante da caçamba da caminhonete da família (as compras do Dia 0).</summary>
        public const int CacambaDaFamilia = EstantesDoBunker.BaseDasCacambas;

        /// <summary>Quantas coisas estão na caçamba da caminhonete da família.</summary>
        public static int CacambaComAsCompras => EstantesDoBunker.Instancia != null ? EstantesDoBunker.Instancia.Quantos(CacambaDaFamilia) : 0;

        // no anfitrião, uma vez por Dia 0: a caminhonete na entrada da garagem da casa da família (parada até alguém chegar
        // e o chão de lá ter colisão: o Carro.Parado)
        void PorACaminhonete()
        {
            if (_caminhonetePosta || caminhonete == null) return;
            var rede = NetworkManager.Singleton;
            if (rede == null || !rede.IsListening) return;
            _caminhonetePosta = true;
            if (!rede.IsServer) return;
            var w = WoodcreekDoDiaZero.Atual;
            if (w == null || !w.Pronta) return;
            foreach (var c in Carro.Todos.ToArray())
                if (c != null && c.IsSpawned && c.name.StartsWith(caminhonete.name)) c.NetworkObject.Despawn(true);
            var nova = Instantiate(caminhonete, w.LugarDaCaminhonete, Quaternion.Euler(0f, w.GiroDaCaminhonete, 0f));
            nova.name = caminhonete.name;
            var carro = nova.GetComponent<Carro>();
            if (carro != null) carro.ArrumadoDeFabrica();
            _caminhoneteDoDia = nova.GetComponent<NetworkObject>();
            _caminhoneteDoDia.Spawn(true);
            // (24/11/2026: onde ela fica nos 5 anos depois da fuga, quebrada: a primeira missão)
            _caminhoneteNaCasa = w.LugarDaCaminhonete;
            _giroNaCasa = w.GiroDaCaminhonete;
            _temCaminhoneteNaCasa = true;
            Debug.Log($"[Dia 0] A caminhonete da família na entrada da garagem ({SetorDe(w.LugarDaCaminhonete)}).");
        }

        /// <summary>
        /// Na fuga, a caminhonete da família não pega (23/11/2026, o Pedro: "pode deixar ela, só colocar como ela não está
        /// pegando na hora de dar a partida"): ela continua na entrada da garagem, mas o motor só tosse (pela chave ou pela
        /// ligação direta), e se tinha ficado ligada, apaga (<c>Carro.PegarAgora</c>, <c>Carro.Ignicao</c>). A fuga é a pé.
        /// Em todas as máquinas: a fuga e o nome do carro são os mesmos em todas.
        /// </summary>
        public static bool NaoPegaNaFuga(Carro c)
        {
            var m = MundoDeLakewood.Instancia;
            var lw = Instancia;
            return c != null && m != null && m.NaFuga && lw != null && lw.caminhonete != null && c.name.StartsWith(lw.caminhonete.name);
        }

        // no anfitrião, uma vez por Dia 0 (depois da caminhonete, e com os itens do mundo já na rede): a chave dela na mesa da
        // sala da nossa casa (23/11/2026, o Pedro: "está faltando a chave da caminhonete no início do jogo, na nossa casa").
        // Se já há uma chave ali (um save do meio do Dia 0), não põe outra.
        void PorAChave()
        {
            if (_chavePosta || !_caminhonetePosta) return;
            var rede = NetworkManager.Singleton;
            if (rede == null || !rede.IsListening) return;
            if (!rede.IsServer)
            {
                _chavePosta = true;
                return;
            }
            var w = WoodcreekDoDiaZero.Atual;
            var itens = ItensNoMundo.Instancia;
            var carro = _caminhoneteDoDia != null ? _caminhoneteDoDia.GetComponent<Carro>() : null;
            if (w == null || !w.Pronta || itens == null || !itens.IsSpawned || carro == null) return;
            _chavePosta = true;
            var chave = carro.chaveDoVeiculo;
            if (chave == Item.Nada) return;
            foreach (var it in itens.Copia())
                if (it.item == chave && (it.apoio - w.LugarDaChave).sqrMagnitude < 1f)
                {
                    _chaveDoDia = it.id;
                    return;
                }
            itens.Criar(chave, w.LugarDaChave, w.GiroDaChave);
            _chaveDoDia = itens.UltimoCriado;
            Debug.Log($"[Dia 0] A chave da caminhonete em {w.MovelDaChave}, na sala da nossa casa ({SetorDe(w.LugarDaChave)}).");
        }

        /// <summary>
        /// No anfitrião (o caixa do Dia 0): a compra vai para a caçamba da caminhonete da família (a pilha completa a do
        /// mesmo item). Sem lugar, ela fica no chão atrás da caminhonete (false).
        /// </summary>
        public static bool GuardarNaCacamba(Item item, byte desgaste)
        {
            var estantes = EstantesDoBunker.Instancia;
            if (estantes != null && estantes.IsSpawned && estantes.GuardarNasEstantes(new[] { CacambaDaFamilia }, item, -1, desgaste, 0)) return true;
            var lw = Instancia;
            var itens = ItensNoMundo.Instancia;
            if (lw == null || itens == null) return false;
            var c = lw._caminhoneteDoDia != null ? lw._caminhoneteDoDia.GetComponent<Carro>() : null;
            var ponto = c != null ? c.PontoDoPortaMalas - c.transform.forward * 0.8f : (WoodcreekDoDiaZero.Atual != null ? WoodcreekDoDiaZero.Atual.LugarDaCaminhonete : Vector3.zero);
            ponto += new Vector3(Random.Range(-0.6f, 0.6f), 0.3f, Random.Range(-0.6f, 0.6f));
            itens.Criar(item, ponto, Random.Range(0f, 360f), -1, desgaste);
            return false;
        }

        // no anfitrião, no fim do Dia 0: a caminhonete (com a caçamba como estava) no pátio do bunker, de frente para o
        // portão, e a chave no chão da sala de operações (ao lado das dos outros)
        void LevarACaminhoneteAoBunker()
        {
            var lugar = Chao.Bunker;
            if (caminhonete == null || lugar == null) return;
            foreach (var c in Carro.Todos.ToArray())
                if (c != null && c.IsSpawned && c.name.StartsWith(caminhonete.name)) c.NetworkObject.Despawn(true);
            _caminhoneteDoDia = null;
            var noPatio = lugar.NoMapa(14f, -20f);
            var pos = OrigemDeLakewood.NoMundo(new Vector3(noPatio.x, lugar.Altura + 0.5f, noPatio.y));
            var nova = Instantiate(caminhonete, pos, Quaternion.Euler(0f, lugar.Giro + 180f, 0f));
            nova.name = caminhonete.name;
            var carro = nova.GetComponent<Carro>();
            if (carro != null) carro.ArrumadoDeFabrica();
            nova.GetComponent<NetworkObject>().Spawn(true);
            var itens = ItensNoMundo.Instancia;
            // a chave do Dia 0 (a da mesa da sala) pega por alguém vai com ele: no bunker, não aparece outra
            bool pegaram = itens != null && carro != null && _chaveDoDia >= 0 && itens.Do((ushort)_chaveDoDia) != carro.chaveDoVeiculo;
            _chaveDoDia = -1;
            if (pegaram) Debug.Log("[Dia 0] A chave da caminhonete está com quem a pegou na mesa da sala: no bunker, não vai outra.");
            if (itens != null && carro != null && carro.chaveDoVeiculo != Item.Nada && !pegaram && _nascimentosNoBunker != null && _nascimentosNoBunker.Length >= 2)
            {
                var meio = Vector3.Lerp(_nascimentosNoBunker[0], _nascimentosNoBunker[1], 0.5f) + Vector3.down * 0.05f;
                var lado = _nascimentosNoBunker[1] - _nascimentosNoBunker[0];
                lado.y = 0f;
                itens.Criar(carro.chaveDoVeiculo, meio + (lado.sqrMagnitude > 0.01f ? lado.normalized * 0.75f : Vector3.zero), lugar.Giro + 55f);
            }
            int naCacamba = EstantesDoBunker.Instancia != null ? EstantesDoBunker.Instancia.Quantos(CacambaDaFamilia) : 0;
            Debug.Log($"[Dia 0] A caminhonete foi para o pátio do bunker, com {naCacamba} coisas na caçamba; a chave na sala de operações.");
        }
    }
}
