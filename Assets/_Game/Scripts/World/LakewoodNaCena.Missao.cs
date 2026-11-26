using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Sobreviventes;
using ZombieCityCleaner.Vehicles;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A primeira missão depois dos 5 anos (24/11/2026, o Pedro: "Perdemos todos os itens na loucura do D0, a primeira missão
    /// da gente é retomar a cidade com o npc capitão (junto com os colegas) e lá recuperamos os itens que compramos no D0,
    /// também podemos concertar a caminhonete (onde ela estará totalmente danificada, precisando trocar as rodas e os
    /// componentes do motor) Ai daria um sentido (essa seria a nossa primeira missão pós 5 anos)"). No anfitrião:
    ///  - <b>na fuga</b> (25/11/2026), cada um sai de casa só com as roupas: o resto fica no armazém da garagem
    ///    (LakewoodNaCena.Armazem.cs; antes, na chegada ao bunker, ia para a caçamba e o que não cabia ficava no chão);
    ///  - <b>nos 5 anos</b>, a caminhonete fica na entrada da garagem da casa da família, na Woodcreek, sem as rodas, sem a
    ///    vela, sem a bateria e sem o radiador (o macaco, na caçamba), com as compras do Dia 0 na caçamba;
    ///  - <b>a missão</b> (<see cref="MundoDeLakewood.Missao"/>): falar com o Capitão no pátio do bunker; ir com ele até a casa;
    ///    retomar a rua (os Ocos e os Corredores que aparecem quando alguém chega perto); recuperar as compras e consertar a
    ///    caminhonete (as rodas e as peças tiradas dos carros abandonados da vila). Consertada, a missão acaba e o Capitão
    ///    volta a esperar no bunker.
    /// O save não muda: a missão sai do estado da caminhonete (quebrada longe do bunker, ela volta à conversa com o Capitão).
    /// Sem o Dia 0 (o menu, ou um save de antes dele), a caminhonete aparece quebrada na casa do mesmo jeito, com a chave no
    /// armazém da garagem (25/11/2026, a revisão: o bug 77, o <see cref="GarantirACaminhonete"/>).
    /// </summary>
    public partial class LakewoodNaCena
    {
        /// <summary>O grupo do Capitão na identidade (as outras máquinas acham ele por isto, para a conversa).</summary>
        public const ushort GrupoDoCapitao = 64999;

        Sobrevivente _capitao;
        readonly List<(Zumbi z, int id, byte geracao)> _horda = new List<(Zumbi, int, byte)>();
        bool _hordaPosta;
        Vector3 _caminhoneteNaCasa;
        float _giroNaCasa, _proximaContaDaMissao, _capitaoMorreuEm = -99f, _proximaCaronaDoCapitao;
        // o posto do Capitão no pátio do bunker (o "Espere aqui" muda o posto; no fim da missão, ele volta para este)
        Vector3 _postoDoCapitao;
        float _giroDoPostoDoCapitao;
        bool _temCaminhoneteNaCasa;
        // (25/11/2026, a revisão: o bug 77) a caminhonete quebrada que a missão pôs na casa (um jogo sem o Dia 0, ou um save
        // de antes dele: uma vez por partida), desde quando o bunker e a fase do Capitão estão sem ela (-1: com ela) e a
        // próxima conferida
        bool _caminhonetePelaMissao;
        float _semCaminhoneteDesde = -1f, _proximaCaminhonete;
        // (25/11/2026, a revisão do bug 77) a chave dessa caminhonete, se na hora não teve lugar (as estantes e os itens do
        // mundo ainda fora da rede): tenta de novo a cada conferida, em vez de a caminhonete ficar sem chave nenhuma
        Item _chaveSemLugar = Item.Nada;

        /// <summary>Quantos zumbis a rua da casa da família tem para retomar (os 2 primeiros, Corredores).</summary>
        const int TamanhoDaHorda = 14;
        /// <summary>A caminhonete fica na entrada da garagem, este tanto para a rua a partir da boca dela (m: o mesmo da WoodcreekDoDiaZero).</summary>
        const float CaminhoneteNaEntrada = 3.0f;

        Carro CaminhoneteDaFamilia() =>
            caminhonete == null ? null : Carro.Todos.FirstOrDefault(c => c != null && c.IsSpawned && c.name.StartsWith(caminhonete.name));

        /// <summary>
        /// No anfitrião (o carro, ao pôr uma roda ou uma peça do motor): a caminhonete da família, depois dos 5 anos, só aceita
        /// as peças na submissão "Conserte a caminhonete" e depois dela (25/11/2026, a etapa 7: "as peças só entram nela nesta
        /// etapa"). Os outros carros e o Dia 0, sempre.
        /// </summary>
        public static bool PodeConsertar(Carro carro, out string motivo)
        {
            motivo = null;
            var lw = Instancia;
            var m = MundoDeLakewood.Instancia;
            if (lw == null || m == null || !m.IsSpawned || carro == null || lw.caminhonete == null || !carro.name.StartsWith(lw.caminhonete.name)) return true;
            if (m.NoDiaZero || m.Fase == FaseDaCampanha.Livre || m.Fase == FaseDaCampanha.Nenhuma) return true;
            if (m.Fase == FaseDaCampanha.RetomeAWoodcreek && m.Passo >= CampanhaDeLakewood.ConserteACaminhonete) return true;
            motivo = m.Fase == FaseDaCampanha.RetomeAWoodcreek
                ? "A caminhonete fica para depois: primeiro retomem a rua e vejam o armazém (o caderno)."
                : "A caminhonete fica para depois: o Capitão ainda não chamou para retomar a Woodcreek.";
            return false;
        }

        /// <summary>
        /// Em qualquer máquina (o minimapa): a área da missão da vez, a casa da família e a rua em volta (50 m), da ida até o
        /// conserto da caminhonete; <paramref name="deLuta"/> na submissão de retomar a rua.
        /// </summary>
        public static bool AreaDaMissao(out Vector3 centro, out float raio, out bool deLuta)
        {
            centro = default;
            raio = 50f;
            deLuta = false;
            var m = MundoDeLakewood.Instancia;
            if (m == null || !m.IsSpawned || m.Fase != FaseDaCampanha.RetomeAWoodcreek) return false;
            int p = m.Passo;
            if (p < CampanhaDeLakewood.VaParaACasa || p > CampanhaDeLakewood.ConserteACaminhonete) return false;
            centro = CasaDaFamiliaNoMundo();
            deLuta = p == CampanhaDeLakewood.RetomeARua;
            return true;
        }

        /// <summary>A caminhonete anda: as quatro rodas sem furo, a vela boa, a bateria que gira o arranque e o radiador.</summary>
        public static bool Consertada(Carro c)
        {
            if (c == null) return false;
            var n = c.Pneus;
            for (int i = 0; i < PneusDoCarro.Quantas; i++) if (!n.Tem(i) || n.Furado(i)) return false;
            var p = c.Pecas;
            return p.VelaBoa && p.BateriaGira && p.Tem(PecasDoMotor.Radiador);
        }

        /// <summary>Quantas coisas faltam na caminhonete (cada roda e cada peça do motor ruim ou sem ela).</summary>
        public static int QuantasFaltam(Carro c)
        {
            if (c == null) return 0;
            int k = 0;
            var n = c.Pneus;
            for (int i = 0; i < PneusDoCarro.Quantas; i++) if (!n.Tem(i) || n.Furado(i)) k++;
            var p = c.Pecas;
            if (!p.VelaBoa) k++;
            if (!p.BateriaGira) k++;
            if (!p.Tem(PecasDoMotor.Radiador)) k++;
            return k;
        }

        // ------------------------------------------------------------------ os 5 anos

        // no anfitrião, nos 5 anos depois da fuga: a caminhonete na casa da família, sem as rodas e as peças do motor. (25/11/2026)
        // O que ficou na caçamba vai antes para o armazém da garagem, e a comida fresca estraga (LakewoodNaCena.Armazem.cs).
        // (A chegada ao bunker não tira mais nada de ninguém: o PerderTudoNaFuga saiu; cada um sai de casa só com as roupas.)
        void DeixarACaminhoneteNaCasa()
        {
            CincoAnosNoArmazem();
            if (caminhonete == null || !_temCaminhoneteNaCasa)
            {
                LevarACaminhoneteAoBunker();
                return;
            }
            CriarACaminhoneteQuebrada();
            int naCacamba = EstantesDoBunker.Instancia != null ? EstantesDoBunker.Instancia.Quantos(CacambaDaFamilia) : 0;
            Debug.Log($"[Missão] 5 anos depois: a caminhonete ficou na casa da família ({SetorDe(_caminhoneteNaCasa)}), sem as rodas e as peças do motor, com {naCacamba} coisas na caçamba.");
        }

        // no anfitrião: a caminhonete da família no lugar dela na casa (_caminhoneteNaCasa), sem as rodas, a vela, a bateria e
        // o radiador, com o macaco na caçamba e um pouco de gasolina (a de antes, se havia, sai)
        Carro CriarACaminhoneteQuebrada()
        {
            foreach (var c in Carro.Todos.ToArray())
                if (c != null && c.IsSpawned && c.name.StartsWith(caminhonete.name)) c.NetworkObject.Despawn(true);
            _caminhoneteDoDia = null;
            var nova = Instantiate(caminhonete, _caminhoneteNaCasa, Quaternion.Euler(0f, _giroNaCasa, 0f));
            nova.name = caminhonete.name;
            var carro = nova.GetComponent<Carro>();
            if (carro != null)
            {
                carro.velaInicial = carro.bateriaInicial = carro.radiadorInicial = -1;
                carro.pneusIniciais = new[] { -1, -1, -1, -1 };
                carro.estepeInicial = -1;
                carro.macacoInicial = true;
                carro.combustivel = 0.25f;
            }
            nova.GetComponent<NetworkObject>().Spawn(true);
            return carro;
        }

        /// <summary>
        /// No anfitrião, a cada meio segundo (25/11/2026, a revisão: o bug 77). A caminhonete da família só nascia pelo palco
        /// do Dia 0: num jogo novo com "COMEÇAR PELO DIA 0: NÃO", ou num save do Lakewood de antes do Dia 0, ela nunca existia,
        /// e a fase do Capitão travava em "Conserte a caminhonete" ("faltam 0 peças" para sempre). Agora, no bunker (o tutorial
        /// do Gus) e na fase do Capitão até o conserto, sem caminhonete nenhuma há 2 s, ela aparece quebrada na entrada da
        /// garagem da casa da família, como nos 5 anos (a mesma conta da WoodcreekDoDiaZero), e a chave dela vai para o
        /// armazém da garagem. Uma vez por partida; depois, o save a guarda, como a do Dia 0 (o QualDoCarro).
        /// Fora disso, sem caminhonete (sem o prefab, ou ela sumiu), as submissões dela não travam (os <see cref="SemCaminhonete"/>).
        /// </summary>
        void GarantirACaminhonete(MundoDeLakewood m)
        {
            if (Time.time < _proximaCaminhonete) return;
            _proximaCaminhonete = Time.time + 0.5f;
            if (_chaveSemLugar != Item.Nada)
            {
                string onde = GuardarAChaveDaCaminhonete(_chaveSemLugar);
                if (_chaveSemLugar == Item.Nada) Debug.Log($"[Missão] A chave da caminhonete da família teve lugar: {onde}.");
            }
            bool naFase = m.Fase == FaseDaCampanha.OBunker || m.Fase == FaseDaCampanha.RetomeAWoodcreek;
            if (!naFase || CaminhoneteDaFamilia() != null)
            {
                _semCaminhoneteDesde = -1f;
                return;
            }
            if (_semCaminhoneteDesde < 0f) _semCaminhoneteDesde = Time.time;
            // (os 2 s: o save e o palco do Dia 0 põem a caminhonete deles antes; esta é só para quando nenhum pôs)
            bool antesDoConserto = m.Fase == FaseDaCampanha.OBunker || m.Passo <= CampanhaDeLakewood.ConserteACaminhonete;
            if (_caminhonetePelaMissao || caminhonete == null || !antesDoConserto || Time.time - _semCaminhoneteDesde < 2f) return;
            if (!LugarDaCaminhoneteNaCasa(out var lugar, out float giro)) return;
            _caminhonetePelaMissao = true;
            _caminhoneteNaCasa = lugar;
            _giroNaCasa = giro;
            _temCaminhoneteNaCasa = true;
            var carro = CriarACaminhoneteQuebrada();
            _semCaminhoneteDesde = -1f;
            string chave = carro != null && carro.chaveDoVeiculo != Item.Nada ? GuardarAChaveDaCaminhonete(carro.chaveDoVeiculo) : "sem chave";
            Debug.Log($"[Missão] A caminhonete da família não existia (o jogo sem o Dia 0, ou um save de antes dele): ela apareceu quebrada na casa ({SetorDe(lugar)}), {chave}.");
        }

        // no anfitrião: a chave da caminhonete posta pela missão no armazém da garagem (o que a família guardou lá); sem lugar
        // nas estantes, no chão da sala de operações. (25/11/2026, a revisão do bug 77) Sem nenhum dos dois na rede, ela fica
        // no _chaveSemLugar para a próxima conferida (antes, a caminhonete ficava sem chave, e só a ligação direta, a ponta do
        // Ladrão, a levava). Devolve onde ela ficou, para o Debug.Log
        string GuardarAChaveDaCaminhonete(Item chave)
        {
            _chaveSemLugar = Item.Nada;
            var estantes = EstantesDoBunker.Instancia;
            if (estantes != null && estantes.IsSpawned && estantes.GuardarNasEstantes(EstantesDoBunker.OArmazem, chave, -1, 0, 0))
                return "a chave no armazém da garagem";
            if (PorAChaveNaSala(chave)) return "a chave no chão da sala de operações";
            _chaveSemLugar = chave;
            return "a chave ainda sem lugar (as estantes e os itens do mundo fora da rede: vai na próxima conferida)";
        }

        /// <summary>A fase está sem a caminhonete da família há pelo menos <paramref name="segundos"/> (no anfitrião).</summary>
        bool SemCaminhonete(float segundos) => _semCaminhoneteDesde >= 0f && Time.time - _semCaminhoneteDesde >= segundos;

        // onde a caminhonete fica na casa da família sem o palco do Dia 0: a conta da WoodcreekDoDiaZero (a boca da garagem do
        // lote, 3 m para a rua, o nariz para a garagem e a caçamba para a rua)
        bool LugarDaCaminhoneteNaCasa(out Vector3 lugar, out float giro)
        {
            lugar = default;
            giro = 0f;
            var lote = LoteDaFamilia();
            if (lote == null || ChaoNoMapa == null) return false;
            var entrada = lote.Plano.EntradaDoCarro ?? new Vector2(0f, -lote.Lote.fundo * 0.5f + 4f);
            var boca = lote.Lote.NoMapa(entrada.x, entrada.y);
            var ponto = boca + lote.Lote.Frente * CaminhoneteNaEntrada;
            lugar = OrigemDeLakewood.NoMundo(new Vector3(ponto.x, ChaoNoMapa.Altura(ponto.x, ponto.y) + 0.5f, ponto.y));
            var fundos = lote.Lote.Fundos;
            giro = Mathf.Atan2(fundos.x, fundos.y) * Mathf.Rad2Deg;
            return true;
        }

        // ------------------------------------------------------------------ a missão (a cada quadro)

        /// <summary>A casa da família na Woodcreek (a Spruce Lane), no mundo: o meio do lote, no chão.</summary>
        public static Vector3 CasaDaFamiliaNoMundo()
        {
            var p = WoodcreekDoDiaZero.CasaDaFamilia;
            var lw = Instancia;
            float y = lw != null && lw.Chao != null ? lw.Chao.Altura(p.x, p.y) : 0f;
            return OrigemDeLakewood.NoMundo(new Vector3(p.x, y, p.y));
        }

        // o Capitão visto nesta máquina (achado pela identidade a cada meio segundo: PorAConversaNoCapitao)
        static Sobrevivente _capitaoVisto;

        /// <summary>Onde está o Capitão (em qualquer máquina), ou null.</summary>
        public static Vector3? PosicaoDoCapitao() =>
            _capitaoVisto != null && _capitaoVisto.IsSpawned ? _capitaoVisto.transform.position : (Vector3?)null;

        static float Horizontal(Vector3 d) => new Vector2(d.x, d.z).magnitude;

        bool AlguemPerto(Vector3 ponto, float raio)
        {
            foreach (var j in JogadorRede.Todos)
                if (j != null && j.Criado && Horizontal(j.transform.position - ponto) < raio) return true;
            return false;
        }

        // (25/11/2026, o caderno das missões: World/CampanhaDeLakewood.cs) a fase "Retome a Woodcreek", no anfitrião: só a
        // submissão da vez é conferida (antes, consertar a caminhonete a qualquer hora pulava tudo)
        void AcompanharAMissao()
        {
            var m = MundoDeLakewood.Instancia;
            if (m == null || !m.IsSpawned) return;
            if (GetComponent<UI.CadernoDeMissoes>() == null) gameObject.AddComponent<UI.CadernoDeMissoes>();
            PorAConversaNoCapitao();
            PorAConversaNoGus();        // (25/11/2026, a etapa 6: o tutorial do Gus, LakewoodNaCena.Tutorial.cs)
            SegurarNoTutorial(m);
            var rede = NetworkManager.Singleton;
            if (rede == null || !rede.IsServer || _antes || _palco != null) return;
            // o Capitão fica no pátio do bunker em toda a época de depois (ele é o Peter Ziquinatti da comunidade)
            PorOCapitao();
            CapitaoNaCaminhonete();
            AcompanharOTutorial(m);
            GarantirACaminhonete(m);   // (25/11/2026, a revisão: o bug 77, o jogo sem o Dia 0)
            if (m.Fase != FaseDaCampanha.RetomeAWoodcreek)
            {
                // (25/11/2026) fora da fase, ele volta para o posto do bunker (de longe, quando ninguém vê: Sobrevivente.Escolta.cs)
                if (_capitao != null && (_capitao.escolta || _capitao.posto != _postoDoCapitao) && _postoDoCapitao != Vector3.zero)
                {
                    _capitao.escolta = false;
                    _capitao.lider = null;
                    _capitao.posto = _postoDoCapitao;
                    _capitao.giroDoPosto = _giroDoPostoDoCapitao;
                }
                return;
            }
            if (Time.time < _proximaContaDaMissao) return;
            _proximaContaDaMissao = Time.time + 0.5f;
            var c = CaminhoneteDaFamilia();
            var casa = CasaDaFamiliaNoMundo();
            bool alguemPerto = AlguemPerto(casa, 80f);
            bool capitaoPerto = _capitao != null && !_capitao.Morto && Horizontal(_capitao.transform.position - casa) < 80f;
            switch (m.Passo)
            {
                case CampanhaDeLakewood.VaParaACasa:
                    // o grupo e o Capitão chegam: a rua está tomada
                    if (alguemPerto && capitaoPerto)
                    {
                        PorAHorda(casa);
                        m.Concluir(FaseDaCampanha.RetomeAWoodcreek, CampanhaDeLakewood.VaParaACasa);
                    }
                    break;
                case CampanhaDeLakewood.RetomeARua:
                {
                    if (!_hordaPosta && alguemPerto) PorAHorda(casa);
                    if (!_hordaPosta)
                    {
                        // (25/11/2026, a revisão: o bug 98) a horda não vai no save: depois de carregar, até alguém chegar
                        // perto da casa, o caderno dizia "0 zumbis" (a rua já limpa). Agora, a horda que vai aparecer
                        m.Numeros(TamanhoDaHorda, 0, 0);
                        break;
                    }
                    HordaNaRua(out int vivos, out int dePedra);
                    m.Numeros(vivos, dePedra, 0);
                    if (alguemPerto && vivos <= 2 && m.Concluir(FaseDaCampanha.RetomeAWoodcreek, CampanhaDeLakewood.RetomeARua))
                        foreach (var (_, id, geracao) in _horda) ZombieManager.Instancia?.DaMissao.Remove((id, geracao));
                    break;
                }
                case CampanhaDeLakewood.AbraOArmazem:
                    // (25/11/2026) alguém chegou nas estantes do fundo da garagem (LakewoodNaCena.Armazem.cs)
                    if (AlguemNoArmazem(3.5f)) m.Concluir(FaseDaCampanha.RetomeAWoodcreek, CampanhaDeLakewood.AbraOArmazem);
                    break;
                case CampanhaDeLakewood.ConserteACaminhonete:
                    if (c == null)
                    {
                        // (25/11/2026, a revisão: o bug 77) sem caminhonete nenhuma (o GarantirACaminhonete põe uma em 2 s;
                        // passados 10 s, não deu: sem o prefab, ou ela sumiu), a submissão não trava em "faltam 0 peças"
                        if (SemCaminhonete(10f)) m.Concluir(FaseDaCampanha.RetomeAWoodcreek, CampanhaDeLakewood.ConserteACaminhonete);
                        break;
                    }
                    m.Numeros(QuantasFaltam(c), 0, 0);
                    if (Consertada(c)) m.Concluir(FaseDaCampanha.RetomeAWoodcreek, CampanhaDeLakewood.ConserteACaminhonete);
                    break;
                case CampanhaDeLakewood.VolteAoBunker:
                {
                    // a caminhonete e o Capitão no pátio do bunker; (25/11/2026, a revisão: o bug 77) sem caminhonete nenhuma
                    // há 10 s, basta o Capitão e alguém do grupo no pátio
                    if (!PortaoDoBunker(out var portao)) break;
                    bool capitaoNoPatio = _capitao != null && Horizontal(_capitao.transform.position - portao) < 45f;
                    bool caminhoneteNoPatio = c != null ? Consertada(c) && Horizontal(c.transform.position - portao) < 45f
                                                        : SemCaminhonete(10f) && AlguemPerto(portao, 45f);
                    if (capitaoNoPatio && caminhoneteNoPatio) m.Concluir(FaseDaCampanha.RetomeAWoodcreek, CampanhaDeLakewood.VolteAoBunker);
                    break;
                }
            }
        }

        // no anfitrião: os Ocos e os Corredores em volta da casa (a rua para retomar), entre 14 e 48 m dela; guardados pelo Id e
        // pela geração (25/11/2026: o pool devolve o mesmo objeto para um zumbi novo, que voltava a contar)
        void PorAHorda(Vector3 casa)
        {
            var zm = ZombieManager.Instancia;
            if (zm == null || !zm.IsSpawned) return;
            _hordaPosta = true;
            foreach (var (_, id, geracao) in _horda) zm.DaMissao.Remove((id, geracao));
            _horda.Clear();
            var rnd = new System.Random(9091);
            for (int i = 0; i < TamanhoDaHorda; i++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, dist = 14f + (float)rnd.NextDouble() * 34f;
                var p = casa + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
                if (NavMesh.SamplePosition(p, out var hit, 5f, NavMesh.AllAreas)) p = hit.position;
                var z = zm.NascerEspecial(i < 2 ? TipoDeZumbi.Corredor : TipoDeZumbi.Oco, p, (float)rnd.NextDouble() * 360f);
                if (z == null) continue;
                _horda.Add((z, z.Id, z.Geracao));
                zm.DaMissao.Add((z.Id, z.Geracao));   // (não volta ao pool longe de todos: ZombieManager.Recolher)
            }
            Debug.Log($"[Missão] A rua da casa da família: {_horda.Count} zumbis para retomar.");
        }

        // quantos da horda ainda estão de pé (os de pedra também contam: precisam ser quebrados) e quantos viraram pedra
        void HordaNaRua(out int vivos, out int dePedra)
        {
            vivos = dePedra = 0;
            var zm = ZombieManager.Instancia;
            if (zm == null) return;
            foreach (var (z, id, geracao) in _horda)
            {
                // (os longe de todos, sem o corpo na tela, contam também: o mesmo Id e a mesma geração, ainda na lista)
                if (z == null || z.Id != id || z.Geracao != geracao || z.Morto || !zm.Ativos.Contains(z)) continue;
                vivos++;
                if (z.Petrificado) dePedra++;
            }
        }

        // no anfitrião: o Capitão no pátio do bunker (de novo, se ele caiu: depois de 60 s, sem ninguém olhando de perto)
        void PorOCapitao()
        {
            if (_capitao != null && _capitao.IsSpawned && !_capitao.Morto) return;
            if (_capitao != null && _capitaoMorreuEm < 0f) _capitaoMorreuEm = Time.time;
            if (_capitao != null && Time.time - _capitaoMorreuEm < 60f) return;
            var sdm = SobreviventesDoMundo.Instancia;
            var b = Chao != null ? Chao.Bunker : null;
            if (sdm == null || sdm.prefab == null || b == null) return;
            var noMapa = b.NoMapa(PlantaDeBunker.PortaoX0 + PlantaDeBunker.PortaoLargura * 0.5f + 2.5f, PlantaDeBunker.Z0 - 4f);
            var ponto = OrigemDeLakewood.NoMundo(new Vector3(noMapa.x, b.Altura + 0.05f, noMapa.y));
            foreach (var j in JogadorRede.Todos)
                if (j != null && j.Criado && _capitao != null && (j.transform.position - ponto).sqrMagnitude < 25f * 25f) return;
            if (NavMesh.SamplePosition(ponto, out var hit, 4f, NavMesh.AllAreas)) ponto = hit.position;
            var fora = OrigemDeLakewood.NoMundo(new Vector3(b.NoMapa(PlantaDeBunker.PortaoX0, PlantaDeBunker.Z0 - 20f).x, b.Altura, b.NoMapa(PlantaDeBunker.PortaoX0, PlantaDeBunker.Z0 - 20f).y)) - ponto;
            float giro = Mathf.Atan2(fora.x, fora.z) * Mathf.Rad2Deg;
            var go = Instantiate(sdm.prefab, ponto, Quaternion.Euler(0f, giro, 0f));
            var sob = go.GetComponent<Sobrevivente>();
            sob.Identidade.Value = new IdentidadeDoSobrevivente
            {
                semente = 7331, arma = TipoDeArma.Rifle, roupa = 1, grupo = GrupoDoCapitao, papel = PapelDoSobrevivente.Militar, indice = 0,
            };
            var g = new GrupoDeSobreviventes { cidade = -1, aliado = true, quantos = 1 };
            sob.grupo = g;
            sob.nome = "Capitão";
            sob.posto = ponto;
            sob.giroDoPosto = giro;
            SobreviventesDoMundo.Encher(sob, TipoDeArma.Rifle, true);
            go.GetComponent<NetworkObject>().Spawn(true);
            g.membros.Add(sob);
            _capitao = sob;
            _capitaoMorreuEm = -99f;
            _postoDoCapitao = ponto;
            _giroDoPostoDoCapitao = giro;
            Debug.Log($"[Missão] O Capitão espera no pátio do bunker ({SetorDe(ponto)}).");
        }

        /// <summary>
        /// No anfitrião, a cada quadro (25/11/2026, a pedido: "quando pegarmos a caminhonete, ele deveria subir em cima dela,
        /// como se fosse um novo passageiro"): quem o Capitão segue entrou na caminhonete da família, e ele, a até 9 m dela
        /// (ou a mais de 45 m, onde ninguém vê), sobe na caçamba; quem ele segue desceu (ou a escolta acabou), e ele desce
        /// do lado direito dela, com ela quase parada. A corrida até ela é do Sobrevivente.Escolta (a <c>carona</c>).
        /// </summary>
        void CapitaoNaCaminhonete()
        {
            if (_capitao == null || !_capitao.IsSpawned || Time.time < _proximaCaronaDoCapitao) return;
            _proximaCaronaDoCapitao = Time.time + 0.25f;
            var c = CaminhoneteDaFamilia();
            _capitao.carona = c;
            var quem = _capitao.escolta ? _capitao.QuemEleSegue(out _) : null;
            bool nela = c != null && quem != null && quem.Passageiro != null && quem.Passageiro.NoCarro && quem.Passageiro.Carro == c;
            if (_capitao.NaCacamba)
            {
                var carro = _capitao.CarroDaCacamba;
                if (carro == null)
                {
                    // (o carro sumiu: de volta ao posto)
                    _capitao.VoltarAoPosto();
                    return;
                }
                if (nela && carro == c) return;
                // (descer com ela andando: só depois que ela quase para)
                if (carro != null && carro.Velocidade > 2.5f) return;
                _capitao.DescerDaCacamba(PontoParaDescer(carro));
                return;
            }
            if (!nela) return;
            // (25/11/2026, a revisão do bug 78) com alguém conversando com ele, fica: antes, quem ele segue entrava na
            // caminhonete e ele pulava para a caçamba no meio da conversa do outro (e a tela dele fechava pela distância).
            // Quando a última tela fecha, ele corre até ela (a escolta) ou, longe e sem ninguém ver, aparece nela
            if (_capitao.conversandoCom != null) return;
            float d = Horizontal(_capitao.transform.position - c.transform.position);
            if (d < 9f || (d > Sobrevivente.DistanciaDeAparecer && Sobrevivente.NinguemVe(_capitao.transform.position + Vector3.up * 1.2f)))
            {
                _capitao.SubirNaCacamba(c);
                Debug.Log($"[Missão] O Capitão subiu na caçamba da caminhonete ({d:0} m dela).");
            }
        }

        // o chão do lado direito da caçamba (o NavMesh perto; sem ele, o chão embaixo)
        static Vector3 PontoParaDescer(Carro c)
        {
            if (c == null) return Vector3.zero;
            var p = c.transform.TransformPoint(new Vector3(1.65f, 0f, -1.75f));
            if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) return hit.position;
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var chao, 6f, 1, QueryTriggerInteraction.Ignore)) return chao.point;
            return p;
        }

        /// <summary>
        /// No anfitrião (a conversa, <see cref="MundoDeLakewood.EscoltaRpc"/>): o Capitão vem junto (seguindo quem pediu) ou
        /// espera onde está. "Vamos retomar a Woodcreek" é a primeira submissão da fase.
        /// </summary>
        public void MudarAEscolta(bool vem, JogadorRede quem)
        {
            var m = MundoDeLakewood.Instancia;
            if (m == null || _capitao == null) return;
            _capitao.escolta = vem;
            if (vem && quem != null) _capitao.lider = quem;
            if (vem) m.Concluir(FaseDaCampanha.RetomeAWoodcreek, CampanhaDeLakewood.FaleComOCapitao);
            Debug.Log($"[Missão] O Capitão {(vem ? $"vem junto ({(quem != null ? quem.Aparencia.Value.nome.ToString() : "?")})" : "espera onde está")}.");
            if (!vem) _capitao.posto = _capitao.transform.position;
        }

        /// <summary>
        /// No anfitrião (<see cref="MundoDeLakewood.ConversaComOCapitaoRpc"/>): quem abriu a conversa com o Capitão o segura
        /// parado, olhando para ele, até fechar (no máximo 2 min, se a tela fechou sem avisar). (25/11/2026, a revisão: o bug
        /// 78) Cada um que conversa conta: ele só volta à escolta, ou ao posto, quando a última tela fecha.
        /// </summary>
        public void ConversaComOCapitao(JogadorRede quem, bool comecou)
        {
            if (_capitao == null || quem == null) return;
            if (comecou) _capitao.ComecarAConversa(quem);
            else _capitao.AcabarAConversa(quem);
        }

        // em cada máquina: a conversa no Capitão (ele é achado pela identidade)
        void PorAConversaNoCapitao()
        {
            if (Time.frameCount % 30 != 0) return;
            if (_capitaoVisto != null && !_capitaoVisto.IsSpawned) _capitaoVisto = null;
            foreach (var s in FindObjectsByType<Sobrevivente>(FindObjectsSortMode.None))
            {
                if (s == null || !s.IsSpawned || s.Identidade.Value.grupo != GrupoDoCapitao) continue;
                _capitaoVisto = s;
                if (s.GetComponent<ConversaComNpc>() != null) continue;
                var conversa = s.gameObject.AddComponent<ConversaDoCapitao>();
                conversa.capitao = s;
                ConversaComNpc.Por(s.gameObject, conversa);
            }
        }
    }
}
