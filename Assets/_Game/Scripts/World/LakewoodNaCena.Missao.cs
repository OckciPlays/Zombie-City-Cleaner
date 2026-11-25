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
    ///  - <b>na chegada ao bunker</b> (o fim da fuga), tudo o que cada um levava vai para a caçamba da caminhonete (o que não
    ///    couber, no chão atrás dela): "perdemos todos os itens na loucura";
    ///  - <b>nos 5 anos</b>, a caminhonete fica na entrada da garagem da casa da família, na Woodcreek, sem as rodas, sem a
    ///    vela, sem a bateria e sem o radiador (o macaco, na caçamba), com as compras do Dia 0 na caçamba;
    ///  - <b>a missão</b> (<see cref="MundoDeLakewood.Missao"/>): falar com o Capitão no pátio do bunker; ir com ele até a casa;
    ///    retomar a rua (os Ocos e os Corredores que aparecem quando alguém chega perto); recuperar as compras e consertar a
    ///    caminhonete (as rodas e as peças tiradas dos carros abandonados da vila). Consertada, a missão acaba e o Capitão
    ///    volta a esperar no bunker.
    /// O save não muda: a missão sai do estado da caminhonete (quebrada longe do bunker, ela volta à conversa com o Capitão).
    /// </summary>
    public partial class LakewoodNaCena
    {
        /// <summary>O grupo do Capitão na identidade (as outras máquinas acham ele por isto, para a conversa).</summary>
        public const ushort GrupoDoCapitao = 64999;

        Sobrevivente _capitao;
        readonly List<Zumbi> _horda = new List<Zumbi>();
        bool _hordaPosta;
        Vector3 _caminhoneteNaCasa;
        float _giroNaCasa, _proximaContaDaMissao, _capitaoMorreuEm = -99f;
        bool _temCaminhoneteNaCasa;

        Carro CaminhoneteDaFamilia() =>
            caminhonete == null ? null : Carro.Todos.FirstOrDefault(c => c != null && c.IsSpawned && c.name.StartsWith(caminhonete.name));

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

        // ------------------------------------------------------------------ a chegada e os 5 anos

        /// <summary>
        /// No anfitrião, na chegada ao bunker (o fim da fuga): o que cada um levava vai para a caçamba da caminhonete (e o que
        /// não couber, para o chão atrás dela). As roupas vestidas ficam.
        /// </summary>
        public void PerderTudoNaFuga()
        {
            var estantes = EstantesDoBunker.Instancia;
            var itens = ItensNoMundo.Instancia;
            var c = _caminhoneteDoDia != null ? _caminhoneteDoDia.GetComponent<Carro>() : CaminhoneteDaFamilia();
            Vector3 atras = c != null ? c.PontoDoPortaMalas - c.transform.forward * 0.8f : (_temCaminhoneteNaCasa ? _caminhoneteNaCasa : Vector3.zero);
            int naCacamba = 0, noChao = 0;
            void Guardar(Item item, int qtd, int desgaste, ulong cores)
            {
                if (item == Item.Nada) return;
                if (estantes != null && estantes.IsSpawned && estantes.GuardarNasEstantes(new[] { CacambaDaFamilia }, item, qtd, desgaste, cores)) { naCacamba++; return; }
                if (itens == null || atras == Vector3.zero) return;
                itens.Criar(item, atras + new Vector3(Random.Range(-0.9f, 0.9f), 0.3f, Random.Range(-0.9f, 0.9f)), Random.Range(0f, 360f), qtd, desgaste, cores);
                noChao++;
            }
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || j.Inventario == null) continue;
                var tudo = j.Inventario.TirarTudo(out var mochila, out int pneu);
                foreach (var it in tudo) Guardar(it.item, it.qtd, it.desgaste, it.cores);
                if (mochila != Item.Nada) Guardar(mochila, 0, 0, 0);
                if (pneu != InventarioJogador.SemPneu) Guardar(Item.Pneu, pneu, 0, 0);
            }
            Debug.Log($"[Missão] Perdemos tudo na loucura da fuga: {naCacamba} coisas na caçamba da caminhonete e {noChao} no chão atrás dela.");
        }

        // no anfitrião, nos 5 anos depois da fuga: a caminhonete na casa da família, sem as rodas e as peças do motor (a caçamba
        // é a mesma estante: as compras ficam)
        void DeixarACaminhoneteNaCasa()
        {
            if (caminhonete == null || !_temCaminhoneteNaCasa)
            {
                LevarACaminhoneteAoBunker();
                return;
            }
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
            int naCacamba = EstantesDoBunker.Instancia != null ? EstantesDoBunker.Instancia.Quantos(CacambaDaFamilia) : 0;
            Debug.Log($"[Missão] 5 anos depois: a caminhonete ficou na casa da família ({SetorDe(_caminhoneteNaCasa)}), sem as rodas e as peças do motor, com {naCacamba} coisas na caçamba.");
        }

        // ------------------------------------------------------------------ a missão (a cada quadro)

        void AcompanharAMissao()
        {
            var m = MundoDeLakewood.Instancia;
            if (m == null || !m.IsSpawned) return;
            if (GetComponent<MissaoNaTela>() == null) gameObject.AddComponent<MissaoNaTela>();
            PorAConversaNoCapitao();
            var rede = NetworkManager.Singleton;
            if (rede == null || !rede.IsServer || _antes || _palco != null) return;
            var etapa = m.Missao;
            var c = CaminhoneteDaFamilia();
            if (etapa == EtapaDaMissao.Nenhuma)
            {
                // a caminhonete quebrada longe do bunker (a da casa, também depois de um save): a missão começa
                if (!m.PrimeiraMissaoCumprida && c != null && !Consertada(c) && PortaoDoBunker(out var portao) && (c.transform.position - portao).sqrMagnitude > 150f * 150f)
                    m.PorAMissao(EtapaDaMissao.FaleComOCapitao);
                return;
            }
            if (etapa == EtapaDaMissao.Cumprida)
            {
                if (_capitao != null && _capitao.escolta) _capitao.escolta = false;
                return;
            }
            PorOCapitao();
            if (c == null || Time.time < _proximaContaDaMissao) return;
            _proximaContaDaMissao = Time.time + 0.5f;
            int naCacamba = EstantesDoBunker.Instancia != null ? EstantesDoBunker.Instancia.Quantos(CacambaDaFamilia) : 0;
            if (Consertada(c))
            {
                m.NumerosDaMissao(0, 0, naCacamba);
                m.PorAMissao(EtapaDaMissao.Cumprida);
                if (_capitao != null) _capitao.escolta = false;
                return;
            }
            var casa = c.transform.position;
            bool alguemPerto = false;
            foreach (var j in JogadorRede.Todos)
                if (j != null && j.Criado && (j.transform.position - casa).sqrMagnitude < 80f * 80f) { alguemPerto = true; break; }
            int vivos = _horda.Count(z => z != null && z.isActiveAndEnabled && !z.Morto);
            switch (etapa)
            {
                case EtapaDaMissao.VaParaACasa:
                    if (alguemPerto)
                    {
                        PorAHorda(casa);
                        m.PorAMissao(EtapaDaMissao.RetomeARua);
                    }
                    break;
                case EtapaDaMissao.RetomeARua:
                    if (!_hordaPosta && alguemPerto) PorAHorda(casa);
                    if (alguemPerto && vivos <= 2) m.PorAMissao(EtapaDaMissao.ConserteACaminhonete);
                    break;
            }
            m.NumerosDaMissao(vivos, QuantasFaltam(c), naCacamba);
        }

        // no anfitrião: os Ocos e os Corredores em volta da casa (a rua para retomar), entre 14 e 48 m dela
        void PorAHorda(Vector3 casa)
        {
            var zm = ZombieManager.Instancia;
            if (zm == null || !zm.IsSpawned) return;
            _hordaPosta = true;
            _horda.Clear();
            var rnd = new System.Random(9091);
            for (int i = 0; i < 14; i++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, dist = 14f + (float)rnd.NextDouble() * 34f;
                var p = casa + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
                if (NavMesh.SamplePosition(p, out var hit, 5f, NavMesh.AllAreas)) p = hit.position;
                var z = zm.NascerEspecial(i < 2 ? TipoDeZumbi.Corredor : TipoDeZumbi.Oco, p, (float)rnd.NextDouble() * 360f);
                if (z != null) _horda.Add(z);
            }
            Debug.Log($"[Missão] A rua da casa da família: {_horda.Count} zumbis para retomar.");
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
            Debug.Log($"[Missão] O Capitão espera no pátio do bunker ({SetorDe(ponto)}).");
        }

        /// <summary>No anfitrião (a conversa, <see cref="MundoDeLakewood.EscoltaRpc"/>): o Capitão vem junto ou espera.</summary>
        public void MudarAEscolta(bool vem)
        {
            var m = MundoDeLakewood.Instancia;
            if (m == null || _capitao == null) return;
            _capitao.escolta = vem;
            if (vem && m.Missao == EtapaDaMissao.FaleComOCapitao) m.PorAMissao(EtapaDaMissao.VaParaACasa);
            Debug.Log($"[Missão] O Capitão {(vem ? "vem junto" : "espera onde está")}.");
            if (!vem) _capitao.posto = _capitao.transform.position;
        }

        // em cada máquina: a conversa no Capitão (ele é achado pela identidade)
        void PorAConversaNoCapitao()
        {
            if (Time.frameCount % 30 != 0) return;
            foreach (var s in FindObjectsByType<Sobrevivente>(FindObjectsSortMode.None))
            {
                if (s == null || !s.IsSpawned || s.Identidade.Value.grupo != GrupoDoCapitao || s.GetComponent<ConversaComNpc>() != null) continue;
                var conversa = s.gameObject.AddComponent<ConversaDoCapitao>();
                conversa.capitao = s;
                ConversaComNpc.Por(s.gameObject, conversa);
            }
        }
    }
}
