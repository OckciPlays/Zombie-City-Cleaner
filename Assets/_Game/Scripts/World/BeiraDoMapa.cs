using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// De onde vêm os objetos da beira das estradas (os carros abandonados com o capô, as rodas e o alarme; no sorteado,
    /// também as placas, as blitz, os postos, a cerca e as coisas das ruas): os dados do gerador do mundo sorteado
    /// (<see cref="DadosDoMundo.Objetos"/>) ou os carros do Lakewood (<see cref="BeiraDeLakewood"/>; 12/11/2026, o
    /// Plano_Lakewood, "O jogo do mundo sorteado no Lakewood", a parte 4). O <see cref="ObjetosDoMundo"/> cria os de perto,
    /// e o <c>PecasDosCarrosDoMundo</c> guarda o que foi mexido em cada carro pelo número dele.
    /// </summary>
    public interface IBeiraDoMapa
    {
        ObjetosDaEstrada Objetos { get; }
        /// <summary>A semente do que sobrou nos carros (as peças, as rodas, o alarme).</summary>
        int Semente { get; }
        /// <summary>Onde o jogador vai nascer (na tela de carregamento, os objetos começam por ali).</summary>
        Vector3 Partida { get; }
        Vector2Int BlocoDe(float x, float z);
        Vector2 CentroDoBloco(int bx, int bz);
        /// <summary>A altura do chão no ponto (no Lakewood, a do asfalto em cima das estradas).</summary>
        float Altura(float x, float z);
        /// <summary>O quanto o objeto da pista fica acima da <see cref="Altura"/> (no sorteado, o asfalto acima do chão).</summary>
        float AcimaDaEstrada { get; }
        /// <summary>Os painéis de metal (a sucata) deste mapa.</summary>
        PaineisDeSucata Paineis { get; }
        /// <summary>A altura do chão do painel pelo mapa (a montagem na cena acerta com um raio para baixo).</summary>
        float AlturaDoPainel(PainelNoMapa p);
    }

    public static class BeiraDoMapa
    {
        static DoSorteado _sorteado;
        static DoLakewood _lakewood;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Limpar()
        {
            _sorteado = null;
            _lakewood = null;
        }

        /// <summary>A beira do mundo da cena (a mesma instância enquanto o mundo for o mesmo), ou null.</summary>
        public static IBeiraDoMapa Atual
        {
            get
            {
                var m = MundoDaPartida.Instancia;
                if (m != null && m.Dados != null)
                {
                    if (_sorteado == null || _sorteado.D != m.Dados) _sorteado = new DoSorteado(m.Dados);
                    return _sorteado;
                }
                var l = LakewoodNaCena.Instancia;
                if (l != null && l.ChaoNoMapa != null)
                {
                    // (no antes do apocalipse, nenhum carro abandonado nem painel de sucata: 22/11/2026)
                    if (LakewoodNaCena.NoAntes) return null;
                    if (_lakewood == null || _lakewood.Chao != l.ChaoNoMapa) _lakewood = new DoLakewood(l.ChaoNoMapa);
                    return _lakewood;
                }
                return null;
            }
        }

        sealed class DoSorteado : IBeiraDoMapa
        {
            public readonly DadosDoMundo D;
            public DoSorteado(DadosDoMundo d) => D = d;
            public ObjetosDaEstrada Objetos => D.Objetos;
            public int Semente => D.Semente;
            public Vector3 Partida => D.Partida;
            public Vector2Int BlocoDe(float x, float z) => D.BlocoDe(x, z);
            public Vector2 CentroDoBloco(int bx, int bz) => D.CentroDoBloco(bx, bz);
            public float Altura(float x, float z) => D.Altura(x, z);
            public float AcimaDaEstrada => RedeDeEstradas.AcimaDoChao;
            public PaineisDeSucata Paineis => PaineisDeSucata.Do(D);
            public float AlturaDoPainel(PainelNoMapa p) => PaineisDeSucata.AlturaDoChao(D, p);
        }

        // (16/11/2026, OrigemDeLakewood: a beira fala no mundo do jogo; os carros e os painéis são feitos no mapa, como
        // antes, e entregues numa cópia no mundo. A grade dos blocos acha o bloco pelo ponto do mundo: os números dos blocos e
        // dos carros são os mesmos de antes)
        sealed class DoLakewood : IBeiraDoMapa
        {
            public readonly ChaoDeLakewood Chao;
            ObjetosDaEstrada _objetosNoMapa, _objetos;
            PaineisDeSucata _paineis;

            public DoLakewood(ChaoDeLakewood chao) => Chao = chao;

            ObjetosDaEstrada ObjetosNoMapa => _objetosNoMapa ??= BeiraDeLakewood.Planejar(Chao);
            public ObjetosDaEstrada Objetos => _objetos ??= NoMundo(ObjetosNoMapa);
            // a semente da partida (cada partida nova, os carros com outras peças; o save guarda a semente)
            public int Semente => MundoDoJogo.Atual != null ? MundoDoJogo.Atual.SementeDaPartida : Chao.Mapa.semente;

            public Vector3 Partida
            {
                get
                {
                    var p = Chao.Mapa.partida;
                    return OrigemDeLakewood.NoMundo(new Vector3(p.x, Chao.Altura(p.x, p.y), p.y));
                }
            }

            public Vector2Int BlocoDe(float x, float z) =>
                new Vector2Int(Mathf.Clamp(Mathf.FloorToInt((x + OrigemDeLakewood.X) / ChaoDeLakewood.TamanhoDoBloco), 0, Chao.BlocosX - 1),
                               Mathf.Clamp(Mathf.FloorToInt((z + OrigemDeLakewood.Z) / ChaoDeLakewood.TamanhoDoBloco), 0, Chao.BlocosZ - 1));

            public Vector2 CentroDoBloco(int bx, int bz) =>
                OrigemDeLakewood.NoMundo(ChaoDeLakewood.CantoDoBloco(bx, bz) + Vector2.one * (ChaoDeLakewood.TamanhoDoBloco * 0.5f));

            public float Altura(float x, float z)
            {
                x += OrigemDeLakewood.X;
                z += OrigemDeLakewood.Z;
                return Chao.EstradaPerto(x, z, 0f, out _, out float asfalto, out _, out _) ? asfalto : Chao.Altura(x, z);
            }

            public float AcimaDaEstrada => 0f;
            public PaineisDeSucata Paineis => _paineis ??= PaineisDeSucata.DoLakewoodNoMundo(Chao, ObjetosNoMapa);
            public float AlturaDoPainel(PainelNoMapa p) => Altura(p.Posicao.x, p.Posicao.y);

            // a cópia no mundo dos objetos (os carros e os quebrados), na mesma ordem (o número de cada carro)
            ObjetosDaEstrada NoMundo(ObjetosDaEstrada m)
            {
                var lista = new List<ObjetoDaEstrada>(m.Todos.Count);
                foreach (var o in m.Todos)
                    lista.Add(new ObjetoDaEstrada
                    {
                        Tipo = o.Tipo, Posicao = OrigemDeLakewood.NoMundo(o.Posicao), Giro = o.Giro, Tombo = o.Tombo, Variante = o.Variante,
                        NaEstrada = o.NaEstrada, NoTalude = o.NoTalude, EmCima = o.EmCima, NaCalcada = o.NaCalcada, NaGaragem = o.NaGaragem,
                        Quebrado = o.Quebrado, Linhas = o.Linhas,
                    });
                var quebrados = new List<CarroQuebrado>(m.Quebrados.Count);
                foreach (var q in m.Quebrados)
                    quebrados.Add(new CarroQuebrado { Indice = q.Indice, Roda = q.Roda, Posicao = OrigemDeLakewood.NoMundo(q.Posicao), Frente = q.Frente, Fora = q.Fora });
                return ObjetosDaEstrada.DeUmMapa(lista, quebrados, BlocoDe, Chao.BlocosX, Chao.BlocosZ);
            }
        }
    }
}
