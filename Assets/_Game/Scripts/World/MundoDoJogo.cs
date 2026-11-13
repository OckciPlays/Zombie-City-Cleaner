using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O mundo da partida, para os sistemas do jogo que não ligam para qual mapa é (12/11/2026, o Plano_Lakewood, "O jogo do
    /// mundo sorteado no Lakewood", a parte 1): o mundo sorteado (o <see cref="MundoDaPartida"/>, com os dados do gerador) ou
    /// o mapa fixo de Lakewood (o <see cref="LakewoodNaCena"/>, com o <see cref="MundoDeLakewood"/> na rede). É o chão dos
    /// zumbis (<see cref="IChaoDosZumbis"/>) e mais: o pronto da partida, a semente dela, a borda do mapa, as estradas, as
    /// cidades e o que as partes seguintes pedirem (a fauna, os sobreviventes, os carros...).
    /// <see cref="MundoDoJogo.Atual"/> é o da cena aberta.
    /// </summary>
    public interface IMundoDoJogo : IChaoDosZumbis
    {
        /// <summary>A partida começou e o mundo está pronto para os sistemas (os bichos, os grupos, os acampamentos).</summary>
        bool PartidaPronta { get; }
        /// <summary>No Dia 0 do mundo sorteado (o começo na cidade normal): os sistemas do mundo ficam desligados.</summary>
        bool NoDiaZero { get; }
        /// <summary>
        /// A semente da partida: o que é sorteado a cada partida (os bichos, os grupos, o que os comerciantes vendem). No
        /// mundo sorteado é a do mundo; no Lakewood, uma por partida (o anfitrião sorteia ao abrir).
        /// </summary>
        int SementeDaPartida { get; }
        /// <summary>O retângulo do mapa (x e z do mundo).</summary>
        Rect Area { get; }
        /// <summary>O ponto está dentro do mapa, a <paramref name="margem"/> da borda (e fora da serra, no sorteado).</summary>
        bool Dentro(float x, float z, float margem);
        /// <summary>O ponto está numa estrada ou numa rua asfaltada (os passos, o desgaste do carro).</summary>
        bool NaEstrada(float x, float z);

        // ------------------------------------------------------------------ as cidades e os lugares (as partes 5 e 6)

        /// <summary>Quantas cidades o mapa tem (os números de <see cref="CidadeEm"/>; no Lakewood, as do mapa, com ou sem lotes).</summary>
        int QuantasCidades { get; }
        Vector2 MeioDaCidade(int cidade);
        /// <summary>A cidade grande (no sorteado; no Lakewood, a principal).</summary>
        bool CidadeGrande(int cidade);
        /// <summary>Quantos grupos de sobreviventes a cidade tem ('naGrande': os da cidade grande, até 8).</summary>
        int GruposDaCidade(int cidade, int naGrande);
        /// <summary>A cidade do ponto (-1 fora de uma; no Lakewood, só as que têm os lotes).</summary>
        int CidadeEm(float x, float z);
        /// <summary>As bases militares (os militares e a construção da base).</summary>
        IReadOnlyList<Lugar> BasesMilitares { get; }
        /// <summary>O giro da base (graus): o portão para a estrada que chega nela.</summary>
        float GiroDaBase(Lugar b);
        /// <summary>Os acampamentos dos comerciantes (o chão deles já plano, na altura do <see cref="Acampamento.Altura"/>).</summary>
        IReadOnlyList<Acampamento> Acampamentos { get; }
    }

    public static class MundoDoJogo
    {
        static readonly DoMundoSorteado _sorteado = new DoMundoSorteado();

        /// <summary>O mundo da cena aberta: o sorteado da partida ou o Lakewood (null sem nenhum dos dois).</summary>
        public static IMundoDoJogo Atual
        {
            get
            {
                var m = MundoDaPartida.Instancia;
                if (m != null && m.Dados != null)
                {
                    _sorteado.Mundo = m;
                    return _sorteado;
                }
                var l = LakewoodNaCena.Instancia;
                return l != null && l.Chao != null ? l : null;
            }
        }

        /// <summary>O mundo da cena, pronto para os sistemas da partida (null antes disso, no Dia 0 e sem mundo).</summary>
        public static IMundoDoJogo Pronto
        {
            get
            {
                var a = Atual;
                return a != null && a.PartidaPronta && !a.NoDiaZero ? a : null;
            }
        }

        /// <summary>
        /// Um número que muda quando o mundo muda (outro mundo sorteado, outra partida no Lakewood): para os sistemas que
        /// guardam o que montaram do mundo (as plantas das bases e dos acampamentos) saberem quando refazer. 0 sem mundo.
        /// </summary>
        public static int Chave(IMundoDoJogo m)
        {
            if (m == null) return 0;
            object dono = m is DoMundoSorteado s ? s.Mundo.Dados : m is LakewoodNaCena l ? l.Chao : m;
            return System.HashCode.Combine(dono, m.SementeDaPartida);
        }

        /// <summary>O mundo sorteado como o mundo do jogo (os dados do gerador).</summary>
        sealed class DoMundoSorteado : IMundoDoJogo
        {
            public MundoDaPartida Mundo;
            DadosDoMundo Dados => Mundo.Dados;

            public int Semente => Dados.Semente;
            public bool Ligado => TerrenoDoMundo.Instancia != null && TerrenoDoMundo.Instancia.Ligado;
            public float Altura(float x, float z) => Dados.Altura(x, z);
            public bool NaCidade(float x, float z) => Dados.CidadeEm(x, z) != null;
            public bool TemHospital => Dados.TemHospital;
            public bool Pronto(Vector3 centro, float raio) => TerrenoDoMundo.Instancia != null && TerrenoDoMundo.Instancia.Pronto(centro, raio);
            public string QueFalta(Vector3 centro, float raio) => TerrenoDoMundo.Instancia == null ? "sem o terreno" : TerrenoDoMundo.Instancia.QueFalta(centro, raio);

            public bool PodeTerAgua(Bounds caixa)
            {
                var lago = Dados.Lago;
                if (lago == null) return false;
                float a = lago.Raio * 1.6f;
                return caixa.max.x > lago.Centro.x - a && caixa.min.x < lago.Centro.x + a && caixa.max.z > lago.Centro.y - a && caixa.min.z < lago.Centro.y + a;
            }

            public float FunduraDaAgua(float x, float z, out float nivel)
            {
                var lago = Dados.Lago;
                nivel = lago != null ? lago.Nivel : 0f;
                return lago != null ? Dados.FunduraDaAgua(x, z) : 0f;
            }

            public bool PartidaPronta => Mundo.Pronto;
            public bool NoDiaZero => Mundo.NoDiaZero;
            public int SementeDaPartida => Dados.Semente;
            public Rect Area => Rect.MinMaxRect(-Dados.Metade, -Dados.Metade, Dados.Metade, Dados.Metade);
            public bool Dentro(float x, float z, float margem) => Mathf.Abs(x) < Dados.Metade - margem && Mathf.Abs(z) < Dados.Metade - margem;

            public bool NaEstrada(float x, float z)
            {
                // as ruas das cidades (o asfalto não tem colisor próprio: o pé fica no terreno, 5 cm abaixo)
                if (Dados.OcupadoPelaCidade(new Vector2(x, z), 0f)) return true;
                var rede = Dados.Rede;
                return rede != null && rede.Acostado(x, z, out bool deTerra) > 0.5f && !deTerra;
            }

            public int QuantasCidades => Dados.Plano != null ? Dados.Plano.Cidades.Count : 0;
            public Vector2 MeioDaCidade(int cidade) => Dados.Plano.Cidades[cidade].Centro;

            public bool CidadeGrande(int cidade)
            {
                var c = Dados.Plano?.Cidades;
                return c != null && cidade >= 0 && cidade < c.Count && c[cidade].Tipo == TipoDeLugar.CidadeGrande;
            }

            public int GruposDaCidade(int cidade, int naGrande) => CidadeGrande(cidade) ? Mathf.Clamp(naGrande, 1, 8) : 1;

            public int CidadeEm(float x, float z)
            {
                var t = Dados.CidadeEm(x, z);
                var c = Dados.Plano?.Cidades;
                if (t == null || c == null) return -1;
                for (int i = 0; i < c.Count; i++) if (c[i].Tracado == t) return i;
                return -1;
            }

            List<Lugar> _bases;
            DadosDoMundo _dasBases;

            public IReadOnlyList<Lugar> BasesMilitares
            {
                get
                {
                    if (_bases == null || _dasBases != Dados)
                    {
                        _dasBases = Dados;
                        _bases = new List<Lugar>();
                        if (Dados.Plano != null)
                            foreach (var l in Dados.Plano.Lugares) if (l.Tipo == TipoDeLugar.BaseMilitar) _bases.Add(l);
                    }
                    return _bases;
                }
            }

            // o portão para a estrada que sai do meio do lugar (a de terra que liga a base à rede); sem ela, o do lugar
            public float GiroDaBase(Lugar l)
            {
                int indice = Dados.Plano.Lugares.IndexOf(l);
                foreach (var e in Dados.Plano.Estradas)
                {
                    if (e.De != indice || e.Pontos.Count < 2) continue;
                    foreach (var p in e.Pontos)
                    {
                        var para = p - l.Centro;
                        if (para.magnitude < 20f + 8f) continue;
                        return Mathf.Atan2(para.x, para.y) * Mathf.Rad2Deg;
                    }
                }
                return l.Giro;
            }

            static readonly List<Acampamento> Nenhum = new List<Acampamento>();
            public IReadOnlyList<Acampamento> Acampamentos => Dados.Plano != null ? Dados.Plano.Acampamentos : Nenhum;
        }
    }
}
