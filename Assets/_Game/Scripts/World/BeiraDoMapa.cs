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
                if (l != null && l.Chao != null)
                {
                    if (_lakewood == null || _lakewood.Chao != l.Chao) _lakewood = new DoLakewood(l.Chao);
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

        sealed class DoLakewood : IBeiraDoMapa
        {
            public readonly ChaoDeLakewood Chao;
            ObjetosDaEstrada _objetos;

            public DoLakewood(ChaoDeLakewood chao) => Chao = chao;

            public ObjetosDaEstrada Objetos => _objetos ??= BeiraDeLakewood.Planejar(Chao);
            // a semente da partida (cada partida nova, os carros com outras peças; o save guarda a semente)
            public int Semente => MundoDoJogo.Atual != null ? MundoDoJogo.Atual.SementeDaPartida : Chao.Mapa.semente;

            public Vector3 Partida
            {
                get
                {
                    var p = Chao.Mapa.partida;
                    return new Vector3(p.x, Chao.Altura(p.x, p.y), p.y);
                }
            }

            public Vector2Int BlocoDe(float x, float z) =>
                new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(x / ChaoDeLakewood.TamanhoDoBloco), 0, Chao.BlocosX - 1),
                               Mathf.Clamp(Mathf.FloorToInt(z / ChaoDeLakewood.TamanhoDoBloco), 0, Chao.BlocosZ - 1));

            public Vector2 CentroDoBloco(int bx, int bz) => ChaoDeLakewood.CantoDoBloco(bx, bz) + Vector2.one * (ChaoDeLakewood.TamanhoDoBloco * 0.5f);

            public float Altura(float x, float z) =>
                Chao.EstradaPerto(x, z, 0f, out _, out float asfalto, out _, out _) ? asfalto : Chao.Altura(x, z);

            public float AcimaDaEstrada => 0f;
            public PaineisDeSucata Paineis => PaineisDeSucata.DoLakewood(Chao, Objetos);
            public float AlturaDoPainel(PainelNoMapa p) => Altura(p.Posicao.x, p.Posicao.y);
        }
    }
}
