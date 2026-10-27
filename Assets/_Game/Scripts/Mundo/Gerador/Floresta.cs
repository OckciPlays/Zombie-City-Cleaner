using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>As plantas (e as pedras) da floresta, na ordem das malhas do Cidade_Arvores.fbx (<see cref="Floresta.Nomes"/>).</summary>
    public enum Especie : byte
    {
        AbetoA, AbetoB, AbetoC, PinheiroBrancoA, PinheiroBrancoB, PinheiroVermelhoA, PinheiroVermelhoB, BordoVerde,
        BordoVermelho, BordoLaranja, CarvalhoA, CarvalhoB, BetulaA, BetulaB, Salgueiro, MortaA, MortaB, TocoA, TocoB,
        TroncoCaidoA, TroncoCaidoB, ArbustoA, ArbustoB, ArbustoC, SamambaiaA, SamambaiaB, CapimA, CapimB, GramaA, GramaB,
        Taboa, PedraA, PedraB, PedraC,
        // as lavouras das fazendas (Parte M9d)
        MilhoA, MilhoB, TrigoA, TrigoB, Fardo,
    }

    /// <summary>Uma planta da floresta: a espécie, o pé no chão, o giro em y (graus) e a escala.</summary>
    public struct PlantaDaFloresta
    {
        public Especie Especie;
        public Vector3 Posicao;
        public float Giro, Escala;
    }

    /// <summary>
    /// A mancha do mapa num ponto: o campo (o pasto abandonado, de capim alto, com as mudas e umas árvores soltas), a
    /// rebrota (o campo virando mata: as mudas, as bétulas e os arbustos), a mata mista (os bordos, os carvalhos, as
    /// bétulas e os pinheiros), a mata de coníferas, a serra (os abetos e as pedras), a beira do lago (os salgueiros, as
    /// bétulas e a taboa), os plantios (o pinheiral e a fazenda de árvores de Natal, em fileiras) e, nas cidades, os
    /// baldios tomados pelo mato e os fundos dos quintais; a lavoura das fazendas (Parte M9d: o milho e o trigo, em
    /// faixas). Nada = onde não nasce planta (as estradas, as ruas e as construções, a água, os lugares do mapa e o pátio
    /// das fazendas, a cerca).
    /// </summary>
    public enum Mancha : byte { Nada, Campo, Rebrota, Mista, Coniferas, Serra, Beira, Pinheiral, Viveiro, Baldio, Quintal, Lavoura }

    /// <summary>
    /// A floresta do mundo gerado (Plano_Mapa, Parte M9a): anos depois do apocalipse, a mata tomou conta do que fica entre
    /// as cidades. É a mata do norte da América do Norte (a pedido: o jogo não se passa no Brasil): as coníferas (o abeto,
    /// o pinheiro-branco e o pinheiro-vermelho), os bordos (uns no outono), os carvalhos, as bétulas e os salgueiros. As
    /// plantas de cada bloco de 64 m saem na hora (<see cref="DoBloco"/> e <see cref="GramaDoBloco"/>), sempre as mesmas
    /// para a mesma semente, em qualquer máquina e em qualquer ordem (cada célula tem o seu sorteio); nada vai pela rede
    /// nem é guardado.
    ///  - As manchas: um ruído largo (uns 320 m) separa a mata, a rebrota e o campo, com mais mata na beira das estradas (a
    ///    pedido: as estradas fechadas pela mata) e a mata indo até a cidade; outro (uns 450 m) separa a mata mista da de
    ///    coníferas (as coníferas na maior parte, a pedido); a serra da borda e a beira do lago têm as suas. Nas cidades, os
    ///    baldios tomados pelo mato e os fundos dos quintais (atrás das casas) também têm árvores.
    ///  - Os plantios: retângulos girados, sorteados no começo (uns 22% das células de 360 m, longe das cidades e das
    ///    serras), com as árvores em fileiras (umas faltando, uns tocos e umas mortas) e o capim entre elas: o pinheiral
    ///    (pinheiros-vermelhos) e a fazenda de árvores de Natal abandonada (abetos novos, de tamanhos desencontrados).
    ///  - As lavouras das fazendas (Parte M9d, só o visual): no giro de cada fazenda, as faixas de 13 m do mapa pintado,
    ///    uma do milharal velho (as touceiras de milho seco em fileiras de 1 m, muitas faltando e umas tombadas) e outra do
    ///    trigal tomado (o trigo que nasceu sozinho, sem fileira, mais ralo em uns trechos, acamado em outros e com o capim
    ///    no meio), os fardos de feno esquecidos em fila e umas mudas; o chão da faixa do milho é de terra
    ///    (<see cref="TerraDaLavoura"/>). O pátio da casa fica vazio (as construções especiais vêm depois).
    ///  - As árvores numa grade de ~4,9 m, um candidato sorteado em cada célula, que fica com a chance da mancha; o chão da
    ///    mata (os arbustos, as samambaias, o capim alto, a taboa, os tocos, os troncos caídos e as pedras) numa grade de
    ///    3,2 m; a grama baixa, à parte, seis tufos em cada célula de 3,2 m (só os blocos perto do jogador a pedem).
    ///  - Onde não nasce: nas estradas e no acostamento (as árvores chegam a 0,3 m dele), nos pátios dos postos, perto dos
    ///    objetos da beira das estradas e das ruas (os carros, as placas, as barreiras, os postes: 2,5 m), nas ruas, nas
    ///    calçadas e nas construções das cidades (com 1,5 m de folga), nos lotes da cidade que não são baldio nem o fundo de
    ///    um quintal, na água e na beira dela, nos lugares do mapa (com 8 m de folga; nas fazendas, só o pátio), na faixa da
    ///    cerca da quarentena e nas encostas mais em pé que 45° (só as pedras).
    /// <see cref="Folhagem"/> diz o quanto o chão é coberto de folhas (o shader do chão põe as folhas mortas embaixo da mata).
    /// Seguro em várias threads (só lê).
    /// </summary>
    public sealed class Floresta
    {
        public static readonly string[] Nomes =
        {
            "Abeto_A", "Abeto_B", "Abeto_C", "PinheiroBranco_A", "PinheiroBranco_B", "PinheiroVermelho_A", "PinheiroVermelho_B",
            "Bordo_Verde", "Bordo_Vermelho", "Bordo_Laranja", "Carvalho_A", "Carvalho_B", "Betula_A", "Betula_B", "Salgueiro",
            "Morta_A", "Morta_B", "Toco_A", "Toco_B", "TroncoCaido_A", "TroncoCaido_B", "Arbusto_A", "Arbusto_B", "Arbusto_C",
            "Samambaia_A", "Samambaia_B", "Capim_A", "Capim_B", "Grama_A", "Grama_B", "Taboa", "Pedra_A", "Pedra_B", "Pedra_C",
            "Milho_A", "Milho_B", "Trigo_A", "Trigo_B", "Fardo",
        };
        public static int Quantas => Nomes.Length;

        /// <summary>
        /// A colisão de cada espécie na escala 1 (as medidas do cidade_arvores.py): o raio e a altura do tronco (uma cápsula
        /// em pé); raio 0 = sem colisão (os arbustos, as samambaias, o capim, a grama, a taboa, a pedra pequena, o milho e o
        /// trigo). O tronco caído e o fardo são uma cápsula deitada (o comprimento na altura).
        /// </summary>
        public static readonly float[] RaioDoTronco =
        {
            0.35f, 0.29f, 0.19f, 0.36f, 0.36f, 0.30f, 0.30f, 0.30f, 0.30f, 0.30f, 0.46f, 0.46f, 0.30f, 0.26f, 0.50f, 0.30f, 0.30f,
            0.36f, 0.30f, 0.32f, 0.28f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0.55f, 1.05f,
            0f, 0f, 0f, 0f, 0.72f,
        };
        public static readonly float[] AlturaDoTronco =
        {
            14f, 10f, 4f, 15f, 12f, 14f, 11f, 4f, 4f, 4f, 3f, 3f, 6f, 5f, 3f, 4f, 10f, 0.45f, 1.1f, 6f, 4.5f,
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1.0f, 1.9f,
            0f, 0f, 0f, 0f, 1.25f,
        };

        public static bool Deitada(Especie e) => e == Especie.TroncoCaidoA || e == Especie.TroncoCaidoB || e == Especie.Fardo;

        /// <summary>O que é árvore (tem copa alta): o resto é o chão da mata.</summary>
        public static bool EhArvore(Especie e) => e <= Especie.MortaB;

        /// <summary>A grama baixa (só perto do jogador, de <see cref="GramaDoBloco"/>).</summary>
        public static bool EhGrama(Especie e) => e == Especie.GramaA || e == Especie.GramaB;

        /// <summary>O milho e o trigo das lavouras das fazendas (Parte M9d).</summary>
        public static bool EhLavoura(Especie e) => e >= Especie.MilhoA && e <= Especie.TrigoB;

        /// <summary>
        /// O quanto a espécie dobra quando alguém passa (Parte M9d; o _Dobra do shader da vegetação): a grama, o capim e o
        /// trigo inteiros, a taboa e as samambaias quase, o milho pela metade, os arbustos pouco; as árvores, os tocos, as
        /// pedras e o fardo, nada.
        /// </summary>
        public static float Dobra(Especie e) => e switch
        {
            Especie.GramaA or Especie.GramaB or Especie.CapimA or Especie.CapimB or Especie.TrigoA or Especie.TrigoB => 1f,
            Especie.Taboa => 0.8f,
            Especie.MilhoA or Especie.MilhoB => 0.45f,
            Especie.SamambaiaA or Especie.SamambaiaB => 0.6f,
            Especie.ArbustoA or Especie.ArbustoB or Especie.ArbustoC => 0.25f,
            _ => 0f,
        };

        sealed class Plantio
        {
            public Mancha Tipo;
            public Vector2 Centro, Meia;
            public float Cos, Sen, NaFileira, EntreFileiras;
            public Rect Caixa;

            // o ponto nas coordenadas do plantio (u ao longo das fileiras, v de uma fileira à outra)
            public Vector2 Local(float x, float z)
            {
                float dx = x - Centro.x, dz = z - Centro.y;
                return new Vector2(dx * Cos + dz * Sen, -dx * Sen + dz * Cos);
            }

            public Vector2 Mundo(float u, float v) => new Vector2(Centro.x + u * Cos - v * Sen, Centro.y + u * Sen + v * Cos);

            public bool Dentro(float x, float z)
            {
                if (!Caixa.Contains(new Vector2(x, z))) return false;
                var l = Local(x, z);
                return Mathf.Abs(l.x) <= Meia.x && Mathf.Abs(l.y) <= Meia.y;
            }
        }

        // a fazenda (Parte M9d): o lugar do mapa no giro dele (u, v como no PintorDoMapa) e os fardos (x, z, giro, escala)
        sealed class Fazenda
        {
            public Vector2 Centro;
            public float Raio, Cos, Sen;
            public Vector4[] Fardos;

            public Vector2 Local(float x, float z)
            {
                float dx = x - Centro.x, dz = z - Centro.y;
                return new Vector2(dx * Cos + dz * Sen, -dx * Sen + dz * Cos);
            }

            public Vector2 Mundo(float u, float v) => new Vector2(Centro.x + u * Cos - v * Sen, Centro.y + u * Sen + v * Cos);
        }

        // a grade das árvores (13 células por bloco, ~4,9 m) e a do chão da mata (20 por bloco, 3,2 m)
        const int CelulasDasArvores = 13, CelulasDoChao = 20;
        /// <summary>As faixas das lavouras (m; as do mapa pintado: o trigo onde Repeat(u, 26) &lt; 13, o milho no resto).</summary>
        public const float FaixaDaLavoura = 13f;
        /// <summary>O pátio da casa da fazenda, vazio (a meia largura em u e em v, no giro dela).</summary>
        public static readonly Vector2 MeioPatio = new Vector2(16f, 12f);
        // a beira da lavoura: depois do raio da fazenda, a faixa só de capim e grama, sem árvore
        const float BeiraDaLavoura = 4f;

        readonly DadosDoMundo _d;
        readonly Ruido _mata, _tipo, _coniferas;
        readonly List<Plantio> _plantios = new List<Plantio>();
        readonly List<Fazenda> _fazendas = new List<Fazenda>();
        // por bloco, os lugares do mapa (fora as cidades) e os plantios que encostam nele; por lugar, a fazenda dele (ou -1)
        readonly int[][] _lugaresDoBloco, _plantiosDoBloco;
        readonly int[] _fazendaDoLugar;

        public int Plantios => _plantios.Count;
        public int Fazendas => _fazendas.Count;

        /// <summary>A faixa da lavoura no ponto u (no giro da fazenda) é de milho (senão, de trigo).</summary>
        public static bool FaixaDeMilho(float u) => Mathf.Repeat(u, FaixaDaLavoura * 2f) >= FaixaDaLavoura;

        public Floresta(DadosDoMundo d)
        {
            _d = d;
            _mata = new Ruido(d.Semente, "floresta");
            _tipo = new Ruido(d.Semente, "floresta-tipo");
            _coniferas = new Ruido(d.Semente, "floresta-coniferas");
            int n = d.BlocosPorLado * d.BlocosPorLado;
            _lugaresDoBloco = new int[n][];
            var listas = new List<int>[n];
            _fazendaDoLugar = new int[d.Plano?.Lugares.Count ?? 0];
            if (d.Plano != null)
                for (int i = 0; i < d.Plano.Lugares.Count; i++)
                {
                    var l = d.Plano.Lugares[i];
                    _fazendaDoLugar[i] = -1;
                    if (l.EhCidade) continue;
                    Marcar(listas, l.Centro, l.Raio + 10f, i);
                    if (l.Tipo == TipoDeLugar.Fazenda)
                    {
                        _fazendaDoLugar[i] = _fazendas.Count;
                        _fazendas.Add(NovaFazenda(l, i));
                    }
                }
            for (int k = 0; k < n; k++) _lugaresDoBloco[k] = listas[k]?.ToArray();

            SortearPlantios();
            listas = new List<int>[n];
            for (int i = 0; i < _plantios.Count; i++)
            {
                var p = _plantios[i];
                Marcar(listas, p.Caixa.center, Mathf.Max(p.Caixa.width, p.Caixa.height) * 0.5f, i);
            }
            _plantiosDoBloco = new int[n][];
            for (int k = 0; k < n; k++) _plantiosDoBloco[k] = listas[k]?.ToArray();
        }

        // a fazenda no giro do lugar, com os fardos que ficaram no campo: uma ou duas filas de 3 a 5, como a enfardadeira
        // deixou (longe do pátio)
        Fazenda NovaFazenda(Lugar l, int indice)
        {
            float giro = l.Giro * Mathf.Deg2Rad;
            var f = new Fazenda { Centro = l.Centro, Raio = l.Raio, Cos = Mathf.Cos(giro), Sen = Mathf.Sin(giro) };
            var s = Sorteio.Da(_d.Semente, "fardos-" + indice);
            var fardos = new List<Vector4>();
            int filas = s.Entre(1, 3);
            for (int k = 0; k < filas; k++)
            {
                var ini = new Vector2(s.Entre(-0.7f, 0.7f), s.Entre(-0.7f, 0.7f)) * l.Raio;
                float rumo = s.Entre(0f, Mathf.PI * 2f);
                var passo = new Vector2(Mathf.Cos(rumo), Mathf.Sin(rumo)) * s.Entre(4f, 7f);
                int quantos = s.Entre(3, 6);
                for (int i = 0; i < quantos; i++)
                {
                    var uv = ini + passo * i + new Vector2(s.Entre(-0.8f, 0.8f), s.Entre(-0.8f, 0.8f));
                    float giroDoFardo = s.Entre(0f, 360f), escala = s.Entre(0.9f, 1.1f);
                    if (uv.sqrMagnitude > (l.Raio - 3f) * (l.Raio - 3f)) continue;
                    if (Mathf.Abs(uv.x) < MeioPatio.x + 4f && Mathf.Abs(uv.y) < MeioPatio.y + 4f) continue;
                    var w = f.Mundo(uv.x, uv.y);
                    fardos.Add(new Vector4(w.x, w.y, giroDoFardo, escala));
                }
            }
            f.Fardos = fardos.ToArray();
            return f;
        }

        // a fazenda do ponto (até a beira da lavoura), com o ponto no giro dela
        Fazenda FazendaEm(float x, float z, out Vector2 local)
        {
            local = default;
            if (_fazendas.Count == 0) return null;
            var b = _d.BlocoDe(x, z);
            int k = b.y * _d.BlocosPorLado + b.x;
            var lugares = k >= 0 && k < _lugaresDoBloco.Length ? _lugaresDoBloco[k] : null;
            if (lugares == null) return null;
            foreach (int i in lugares)
            {
                int fi = _fazendaDoLugar[i];
                if (fi < 0) continue;
                var f = _fazendas[fi];
                float dx = x - f.Centro.x, dz = z - f.Centro.y, r = f.Raio + BeiraDaLavoura;
                if (dx * dx + dz * dz >= r * r) continue;
                local = f.Local(x, z);
                return f;
            }
            return null;
        }

        static bool NoPatio(Vector2 local) => Mathf.Abs(local.x) < MeioPatio.x && Mathf.Abs(local.y) < MeioPatio.y;

        void Marcar(List<int>[] listas, Vector2 centro, float raio, int indice)
        {
            var min = _d.BlocoDe(centro.x - raio, centro.y - raio);
            var max = _d.BlocoDe(centro.x + raio, centro.y + raio);
            for (int bz = Mathf.Max(0, min.y); bz <= Mathf.Min(_d.BlocosPorLado - 1, max.y); bz++)
            for (int bx = Mathf.Max(0, min.x); bx <= Mathf.Min(_d.BlocosPorLado - 1, max.x); bx++)
                (listas[bz * _d.BlocosPorLado + bx] ??= new List<int>()).Add(indice);
        }

        // os pinheirais e as fazendas de árvores de Natal: um sorteio por célula de 360 m dentro do mapa
        void SortearPlantios()
        {
            const float celula = 360f;
            var s = Sorteio.Da(_d.Semente, "plantios");
            int n = Mathf.CeilToInt(_d.Lado / celula);
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                // sempre os mesmos sorteios por célula, aceita ou não
                bool tem = s.Chance(0.22f);
                var centro = new Vector2(-_d.Metade + (i + s.Entre(0.25f, 0.75f)) * celula, -_d.Metade + (j + s.Entre(0.25f, 0.75f)) * celula);
                var meia = new Vector2(s.Entre(45f, 105f), s.Entre(32f, 75f));
                float giro = s.Entre(0f, 180f) * Mathf.Deg2Rad;
                var tipo = s.Chance(0.6f) ? Mancha.Pinheiral : Mancha.Viveiro;
                if (!tem || _d.Borda(centro.x, centro.y) > 0.3f) continue;
                if (_d.Lago != null && _d.Lago.Q(centro.x, centro.y) < 1.8f) continue;
                if (tipo == Mancha.Viveiro) meia *= 0.6f;
                if (PertoDeCidade(centro, Mathf.Max(meia.x, meia.y) + 70f)) continue;
                var p = new Plantio
                {
                    Tipo = tipo, Centro = centro, Meia = meia, Cos = Mathf.Cos(giro), Sen = Mathf.Sin(giro),
                    NaFileira = tipo == Mancha.Pinheiral ? 2.8f : 2.7f, EntreFileiras = tipo == Mancha.Pinheiral ? 3.2f : 3.0f,
                };
                float ex = Mathf.Abs(meia.x * p.Cos) + Mathf.Abs(meia.y * p.Sen), ez = Mathf.Abs(meia.x * p.Sen) + Mathf.Abs(meia.y * p.Cos);
                p.Caixa = Rect.MinMaxRect(centro.x - ex, centro.y - ez, centro.x + ex, centro.y + ez);
                _plantios.Add(p);
            }
        }

        bool PertoDeCidade(Vector2 p, float folga)
        {
            if (_d.Plano == null) return false;
            foreach (var t in _d.Plano.Tracados)
                if (Vector2.Distance(p, t.Centro) < t.Alcance + folga) return true;
            return false;
        }

        // ------------------------------------------------------------------ onde nasce

        /// <summary>
        /// O ponto pode ter planta: fora das estradas e do acostamento, dos pátios, das cidades, da água, dos lugares do
        /// mapa e da cerca da quarentena, e dentro da grade do terreno.
        /// </summary>
        public bool Livre(float x, float z)
        {
            float borda = _d.MetadeDaGrade - 4f;
            if (Mathf.Abs(x) > borda || Mathf.Abs(z) > borda) return false;
            // a faixa do alambrado da quarentena
            float fora = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            if (Mathf.Abs(fora - _d.Cerca) < 4f) return false;
            if (_d.Lago != null && _d.Lago.Q(x, z) < 1.08f) return false;
            var cidade = _d.CidadeNoChao(x, z, out float peso);
            if (cidade != null && peso > 0.02f && !LivreNaCidade(cidade, new Vector2(x, z))) return false;
            var b = _d.BlocoDe(x, z);
            int k = b.y * _d.BlocosPorLado + b.x;
            var lugares = k >= 0 && k < _lugaresDoBloco.Length ? _lugaresDoBloco[k] : null;
            if (lugares != null)
                foreach (int i in lugares)
                {
                    var l = _d.Plano.Lugares[i];
                    float dx = x - l.Centro.x, dz = z - l.Centro.y, r = l.Raio + 8f;
                    if (dx * dx + dz * dz >= r * r) continue;
                    // nas fazendas (Parte M9d), só o pátio da casa fica vazio
                    int fi = _fazendaDoLugar[i];
                    if (fi >= 0 && !NoPatio(_fazendas[fi].Local(x, z))) continue;
                    return false;
                }
            if (_d.ComPlato)
            {
                float dx = x - _d.Partida.x, dz = z - _d.Partida.z;
                if (dx * dx + dz * dz < DadosDoMundo.RaioDoPlato * DadosDoMundo.RaioDoPlato) return false;
            }
            if (_d.Rede != null)
            {
                int e = _d.Rede.MaisPerto(x, z, out float dist, out _);
                if (e >= 0 && dist < RedeDeEstradas.MeiaLargura(_d.Plano.Estradas[e].Tipo) + RedeDeEstradas.Acostamento + 0.3f) return false;
                // os pátios dos postos
                var trechos = _d.Rede.DoBloco(b.x, b.y);
                if (trechos != null)
                    foreach (int r in trechos)
                    {
                        if (r >= 0) continue;
                        var pl = _d.Rede.Plataformas[-r - 1];
                        float dx = x - pl.Centro.x, dz = z - pl.Centro.y;
                        float u = Mathf.Abs(dx * pl.Cos + dz * pl.Sen) - pl.Meia.x, v = Mathf.Abs(-dx * pl.Sen + dz * pl.Cos) - pl.Meia.y;
                        if (Mathf.Max(u, v) < 2f) return false;
                    }
            }
            // os objetos da beira das estradas e das ruas (os carros, as placas, as barreiras, os postes)
            var objetos = _d.Objetos?.DoBloco(b.x, b.y);
            if (objetos != null)
                foreach (int i in objetos)
                {
                    var o = _d.Objetos.Todos[i].Posicao;
                    float dx = x - o.x, dz = z - o.y;
                    if (dx * dx + dz * dz < 2.5f * 2.5f) return false;
                }
            return true;
        }

        // no chão de uma cidade: fora das ruas, das calçadas e das construções, e só nos baldios, nos fundos dos quintais e
        // fora dos quarteirões
        static bool LivreNaCidade(TracadoDaCidade t, Vector2 p)
        {
            if (t.Ocupado(p, 1.5f)) return false;
            var lote = t.LoteEm(p);
            return lote == null || lote.Tipo == TipoDeLote.Baldio || NoFundoDoQuintal(t, lote, p);
        }

        // atrás da casa (e do patamar dela), a 1,5 m dela e a 0,8 m da beira do lote: longe da entrada do carro e da porta
        static bool NoFundoDoQuintal(TracadoDaCidade t, Lote lote, Vector2 p)
        {
            if (lote.Tipo != TipoDeLote.Casa || lote.Casa == null) return false;
            var l = t.Local(p);
            if (l.x < lote.U0 + 0.8f || l.x > lote.U1 - 0.8f || l.y < lote.V0 + 0.8f || l.y > lote.V1 - 0.8f) return false;
            var r = lote.RetDaCasa;
            var f = lote.FundoDaCasa;
            if (f.y > 0.5f) return l.y > Mathf.Max(r.yMax, lote.RetDoPatamar.yMax) + 1.5f;
            if (f.y < -0.5f) return l.y < Mathf.Min(r.yMin, lote.RetDoPatamar.yMin) - 1.5f;
            if (f.x > 0.5f) return l.x > Mathf.Max(r.xMax, lote.RetDoPatamar.xMax) + 1.5f;
            return l.x < Mathf.Min(r.xMin, lote.RetDoPatamar.xMin) - 1.5f;
        }

        /// <summary>A mancha do ponto e a chance de uma árvore numa célula de ~5 m (0 a 1).</summary>
        public Mancha ManchaEm(float x, float z, out float chance)
        {
            chance = 0f;
            if (!Livre(x, z)) return Mancha.Nada;
            // a lavoura (o milho e o trigo vão à parte, no Lavoura; aqui, só umas mudas) e a beira dela, de capim
            var fazenda = FazendaEm(x, z, out var local);
            if (fazenda != null)
            {
                if (local.sqrMagnitude < fazenda.Raio * fazenda.Raio)
                {
                    chance = 0.025f;
                    return Mancha.Lavoura;
                }
                return Mancha.Campo;
            }
            var plantio = PlantioEm(x, z);
            if (plantio != null) return plantio.Tipo; // as árvores dos plantios vão em fileiras
            var cidade = _d.CidadeNoChao(x, z, out float peso);
            if (cidade != null && peso > 0.02f)
            {
                var lote = cidade.LoteEm(new Vector2(x, z));
                if (lote != null)
                {
                    // (o Livre já deixou só o baldio e o fundo do quintal)
                    chance = lote.Tipo == TipoDeLote.Baldio ? 0.5f : 0.35f;
                    return lote.Tipo == TipoDeLote.Baldio ? Mancha.Baldio : Mancha.Quintal;
                }
            }
            if (_d.Lago != null && _d.Lago.Q(x, z) < 1.45f)
            {
                chance = 0.45f;
                return Mancha.Beira;
            }
            float serra = Geo.Suave(0.3f, 0.6f, _d.Borda(x, z));
            float v = ValorDaMata(x, z);
            if (serra > 0.5f)
            {
                chance = 0.55f + 0.25f * Geo.Suave(-0.4f, -0.05f, v);
                return Mancha.Serra;
            }
            if (v > -0.08f)
            {
                chance = 0.7f + 0.22f * Geo.Suave(-0.08f, 0.2f, v);
                return _coniferas.Amostra01(x / 450f + 3.7f, z / 450f - 8.1f) > 0.4f ? Mancha.Coniferas : Mancha.Mista;
            }
            if (v > -0.22f)
            {
                chance = 0.42f;
                return Mancha.Rebrota;
            }
            chance = 0.12f;
            return Mancha.Campo;
        }

        // o ruído largo da mata, mais fechado na beira das estradas (a mata cobre a estrada dos dois lados)
        float ValorDaMata(float x, float z)
        {
            float v = _mata.Fbm(x / 320f + 11.3f, z / 320f - 7.7f, 3);
            if (_d.Rede != null && _d.Rede.MaisPerto(x, z, out float estrada, out _) >= 0 && estrada < 40f)
                v += 0.16f * Geo.Suave(40f, 12f, estrada);
            return v;
        }

        Plantio PlantioEm(float x, float z)
        {
            var b = _d.BlocoDe(x, z);
            int k = b.y * _d.BlocosPorLado + b.x;
            var lista = k >= 0 && k < _plantiosDoBloco.Length ? _plantiosDoBloco[k] : null;
            if (lista == null) return null;
            foreach (int i in lista)
                if (_plantios[i].Dentro(x, z)) return _plantios[i];
            return null;
        }

        /// <summary>
        /// O quanto o chão é coberto de folhas no ponto (0 a 1): embaixo da mata, da serra e dos plantios, para o shader do
        /// chão. Não confere as estradas nem as cidades (quem chama já tira as folhas de lá).
        /// </summary>
        public float Folhagem(float x, float z)
        {
            if (_d.Lago != null && _d.Lago.Q(x, z) < 1.2f) return 0f;
            if (FazendaEm(x, z, out _) != null) return 0f;
            var plantio = PlantioEm(x, z);
            if (plantio != null) return plantio.Tipo == Mancha.Pinheiral ? 0.85f : 0.4f;
            float v = ValorDaMata(x, z);
            float serra = Geo.Suave(0.3f, 0.6f, _d.Borda(x, z));
            float mata = Geo.Suave(-0.12f, 0.12f, v) * 0.8f;
            // no chão da cidade, menos (os lotes e os quintais têm a grama)
            _d.CidadeNoChao(x, z, out float cidade);
            return Mathf.Lerp(mata, 0.35f * Geo.Suave(-0.45f, -0.1f, v), serra) * (1f - cidade);
        }

        /// <summary>
        /// O quanto o chão das fazendas é de terra no ponto (0 a 1; Parte M9d), para o shader do chão: a terra entre as
        /// fileiras do milho, um pouco no trigal e a terra batida do pátio; sumindo na beira da lavoura.
        /// </summary>
        public float TerraDaLavoura(float x, float z)
        {
            var f = FazendaEm(x, z, out var local);
            if (f == null) return 0f;
            if (NoPatio(local)) return 0.55f;
            float beira = Geo.Suave(f.Raio + 2f, f.Raio - 5f, local.magnitude);
            return beira * (FaixaDeMilho(local.x) ? 0.8f : 0.25f);
        }

        // ------------------------------------------------------------------ as plantas de um bloco

        /// <summary>As árvores e o chão da mata do bloco (bx, bz), na saída (limpa antes); sem a grama. Seguro em várias threads.</summary>
        public void DoBloco(int bx, int bz, List<PlantaDaFloresta> saida)
        {
            saida.Clear();
            var canto = _d.CantoDoBloco(bx, bz);
            ulong semente = Semente(bx, bz);

            // as árvores, uma por célula no máximo
            float passo = DadosDoMundo.TamanhoDoBloco / CelulasDasArvores;
            for (int j = 0; j < CelulasDasArvores; j++)
            for (int i = 0; i < CelulasDasArvores; i++)
            {
                var dado = new Dado(semente ^ (ulong)(j * 64 + i + 1) * 0x9E3779B97F4A7C15UL);
                float x = canto.x + (i + 0.5f + dado.Entre(-0.35f, 0.35f)) * passo;
                float z = canto.y + (j + 0.5f + dado.Entre(-0.35f, 0.35f)) * passo;
                float sorte = dado.Fracao();
                var mancha = ManchaEm(x, z, out float chance);
                if (sorte >= chance || !Plano(x, z)) continue;
                var e = EspecieDaArvore(mancha, ref dado, x, z);
                // a rebrota, o campo e a lavoura têm as árvores novas (as mudas)
                float escala = mancha == Mancha.Rebrota || mancha == Mancha.Baldio || mancha == Mancha.Lavoura ? dado.Entre(0.45f, 0.95f)
                    : mancha == Mancha.Campo && (e == Especie.AbetoC || e == Especie.BetulaB) ? dado.Entre(0.35f, 0.8f)
                    : dado.Entre(0.82f, 1.22f);
                Por(saida, e, x, z, dado.Entre(0f, 360f), escala);
            }

            // as fileiras dos plantios que encostam no bloco
            int kb = bz * _d.BlocosPorLado + bx;
            var plantios = kb >= 0 && kb < _plantiosDoBloco.Length ? _plantiosDoBloco[kb] : null;
            if (plantios != null)
            {
                var area = new Rect(canto, Vector2.one * DadosDoMundo.TamanhoDoBloco);
                foreach (int pi in plantios)
                {
                    var p = _plantios[pi];
                    int nu = Mathf.FloorToInt(p.Meia.x / p.NaFileira), nv = Mathf.FloorToInt(p.Meia.y / p.EntreFileiras);
                    for (int v = -nv; v <= nv; v++)
                    for (int u = -nu; u <= nu; u++)
                    {
                        var w = p.Mundo(u * p.NaFileira, v * p.EntreFileiras);
                        if (!area.Contains(w)) continue;
                        var dado = new Dado(Sorteio.Misturar((ulong)(uint)_d.Semente * 31UL + (ulong)pi * 7919UL) ^ (ulong)((v + 512) * 1024 + u + 512) * 0x9E3779B97F4A7C15UL);
                        float x = w.x + dado.Entre(-0.3f, 0.3f), z = w.y + dado.Entre(-0.3f, 0.3f);
                        float r = dado.Fracao();
                        // as que faltam (caíram, foram cortadas; na fazenda de árvores de Natal, as vendidas no último ano)
                        if (r < (p.Tipo == Mancha.Viveiro ? 0.3f : 0.2f) || !Livre(x, z) || !Plano(x, z) || FazendaEm(x, z, out _) != null) continue;
                        Especie e;
                        float escala;
                        if (r < 0.24f) { e = dado.Fracao() < 0.5f ? Especie.TocoA : Especie.TocoB; escala = dado.Entre(0.6f, 0.85f); }
                        else if (p.Tipo == Mancha.Pinheiral)
                        {
                            if (r < 0.28f) { e = Especie.MortaB; escala = dado.Entre(0.8f, 1f); }
                            else { e = dado.Fracao() < 0.5f ? Especie.PinheiroVermelhoA : Especie.PinheiroVermelhoB; escala = dado.Entre(0.85f, 1.12f); }
                        }
                        else
                        {
                            // as árvores de Natal que ninguém cortou: cresceram cada uma de um jeito
                            e = r > 0.92f ? Especie.AbetoB : Especie.AbetoC;
                            escala = e == Especie.AbetoB ? dado.Entre(0.5f, 0.7f) : dado.Entre(0.55f, 1.25f);
                        }
                        Por(saida, e, x, z, dado.Entre(0f, 360f), escala);
                    }
                }
            }

            // as lavouras das fazendas que encostam no bloco
            var lugares = kb >= 0 && kb < _lugaresDoBloco.Length ? _lugaresDoBloco[kb] : null;
            if (lugares != null)
                foreach (int li in lugares)
                    if (_fazendaDoLugar[li] >= 0) Lavoura(saida, _fazendas[_fazendaDoLugar[li]], _fazendaDoLugar[li], canto);

            // o chão da mata
            float passoDoChao = DadosDoMundo.TamanhoDoBloco / CelulasDoChao;
            for (int j = 0; j < CelulasDoChao; j++)
            for (int i = 0; i < CelulasDoChao; i++)
            {
                var dado = new Dado(semente ^ (ulong)(j * 64 + i + 4097) * 0xD1B54A32D192ED03UL);
                float x = canto.x + (i + 0.5f + dado.Entre(-0.4f, 0.4f)) * passoDoChao;
                float z = canto.y + (j + 0.5f + dado.Entre(-0.4f, 0.4f)) * passoDoChao;
                float r = dado.Fracao();
                var mancha = ManchaEm(x, z, out _);
                if (mancha == Mancha.Nada) continue;
                if (!EspecieDoChao(mancha, r, out var e)) continue;
                if (e == Especie.Taboa && (_d.Lago == null || _d.Lago.Q(x, z) > 1.25f)) e = Especie.CapimA;
                if (e != Especie.PedraA && e != Especie.PedraB && e != Especie.PedraC && !Plano(x, z)) continue;
                float escala = e == Especie.CapimA || e == Especie.CapimB || e == Especie.Taboa ? dado.Entre(0.8f, 1.35f) : dado.Entre(0.75f, 1.25f);
                Por(saida, e, x, z, dado.Entre(0f, 360f), escala);
            }
        }

        // a parte da lavoura da fazenda que cai no bloco: os fardos, as fileiras do milho (de 1 m, uma touceira a cada 1,3 m)
        // e o trigo tomado (sem fileira, numa grade de 1 m bagunçada), cada um na faixa dele
        void Lavoura(List<PlantaDaFloresta> saida, Fazenda f, int fi, Vector2 canto)
        {
            float tam = DadosDoMundo.TamanhoDoBloco;
            var area = new Rect(canto, Vector2.one * tam);
            foreach (var fd in f.Fardos)
                if (area.Contains(new Vector2(fd.x, fd.y)) && Livre(fd.x, fd.y) && Plano(fd.x, fd.y))
                    Por(saida, Especie.Fardo, fd.x, fd.y, fd.z, fd.w);
            // o bloco no giro da fazenda (a caixa dos quatro cantos), cortado no raio dela
            float u0 = float.MaxValue, u1 = float.MinValue, v0 = float.MaxValue, v1 = float.MinValue;
            for (int c = 0; c < 4; c++)
            {
                var l = f.Local(canto.x + (c & 1) * tam, canto.y + (c >> 1) * tam);
                u0 = Mathf.Min(u0, l.x); u1 = Mathf.Max(u1, l.x);
                v0 = Mathf.Min(v0, l.y); v1 = Mathf.Max(v1, l.y);
            }
            u0 = Mathf.Max(u0, -f.Raio); u1 = Mathf.Min(u1, f.Raio);
            v0 = Mathf.Max(v0, -f.Raio); v1 = Mathf.Min(v1, f.Raio);
            if (u0 > u1 || v0 > v1) return;
            ulong semente = Sorteio.Misturar(((ulong)(uint)_d.Semente << 20) ^ (ulong)(fi + 1) * 0xD1B54A32D192ED03UL ^ 0x4C61766F757261UL);
            for (int iu = Mathf.FloorToInt(u0) - 1; iu <= Mathf.CeilToInt(u1); iu++)
            {
                float u = iu + 0.5f;
                bool milho = FaixaDeMilho(u);
                float passo = milho ? 1.3f : 1f, solto = milho ? 0.12f : 0.4f;
                for (int iv = Mathf.FloorToInt(v0 / passo) - 1; iv <= Mathf.CeilToInt(v1 / passo); iv++)
                {
                    var dado = new Dado(semente ^ (ulong)((iv + 1024) * 2048 + iu + 1024) * 0x9E3779B97F4A7C15UL);
                    float lu = u + dado.Entre(-solto, solto) * (milho ? 0.5f : 1f), lv = (iv + 0.5f) * passo + dado.Entre(-solto, solto);
                    float r = dado.Fracao(), giro = dado.Entre(0f, 360f), escala = dado.Entre(0.85f, 1.15f), beira = f.Raio - dado.Entre(0f, 4f);
                    // (o trigo bagunçado não passa para a faixa do milho)
                    if (FaixaDeMilho(lu) != milho) lu = u;
                    if (lu * lu + lv * lv > beira * beira || NoPatio(new Vector2(lu, lv))) continue;
                    var w = f.Mundo(lu, lv);
                    if (!area.Contains(w) || PertoDeFardo(f, w, 1.5f) || !Livre(w.x, w.y) || !Plano(w.x, w.y)) continue;
                    Especie e;
                    if (milho)
                    {
                        // muitos pés faltando; uns tombados pelo vento
                        if (r < 0.3f) continue;
                        e = r < 0.52f ? Especie.MilhoB : Especie.MilhoA;
                    }
                    else
                    {
                        // o trigal mais ralo em uns trechos, acamado em outros, e o capim tomando conta
                        if (r >= 0.5f + 0.45f * _tipo.Amostra01(w.x / 25f + 31f, w.y / 25f - 17f)) continue;
                        e = r < 0.1f ? Especie.CapimA : _tipo.Amostra01(w.x / 14f - 9f, w.y / 14f + 4f) > 0.62f ? Especie.TrigoB : Especie.TrigoA;
                    }
                    Por(saida, e, w.x, w.y, giro, escala);
                }
            }
        }

        static bool PertoDeFardo(Fazenda f, Vector2 p, float raio)
        {
            foreach (var fd in f.Fardos)
                if ((new Vector2(fd.x, fd.y) - p).sqrMagnitude < raio * raio) return true;
            return false;
        }

        /// <summary>
        /// A grama baixa do bloco (bx, bz), na saída (limpa antes): seis tufos sorteados em cada célula de 3,2 m, com a
        /// chance da mancha (mais nos campos, menos embaixo das coníferas). Seguro em várias threads.
        /// </summary>
        public void GramaDoBloco(int bx, int bz, List<PlantaDaFloresta> saida)
        {
            saida.Clear();
            var canto = _d.CantoDoBloco(bx, bz);
            ulong semente = Semente(bx, bz) ^ 0x6A09E667F3BCC909UL;
            float passo = DadosDoMundo.TamanhoDoBloco / CelulasDoChao;
            for (int j = 0; j < CelulasDoChao; j++)
            for (int i = 0; i < CelulasDoChao; i++)
            {
                float cx = canto.x + (i + 0.5f) * passo, cz = canto.y + (j + 0.5f) * passo;
                var mancha = ManchaEm(cx, cz, out _);
                float chance = mancha switch
                {
                    Mancha.Campo => 0.95f,
                    Mancha.Baldio => 0.95f,
                    Mancha.Lavoura => 0.6f,
                    Mancha.Quintal => 0.9f,
                    Mancha.Rebrota => 0.85f,
                    Mancha.Beira => 0.8f,
                    Mancha.Viveiro => 0.8f,
                    Mancha.Pinheiral => 0.55f,
                    Mancha.Serra => 0.5f,
                    Mancha.Mista => 0.4f,
                    Mancha.Coniferas => 0.25f,
                    _ => 0f,
                };
                if (chance <= 0f) continue;
                // na cidade, cada tufo é conferido (do baldio ou do quintal, um tufo podia cair no lote do lado)
                var cidade = _d.CidadeNoChao(cx, cz, out float peso);
                if (peso <= 0.02f) cidade = null;
                var dado = new Dado(semente ^ (ulong)(j * 64 + i + 1) * 0x9E3779B97F4A7C15UL);
                for (int k = 0; k < 6; k++)
                {
                    float x = cx + (k % 3 - 1) * passo / 3f + dado.Entre(-0.3f, 0.3f);
                    float z = cz + (k / 3 - 0.5f) * passo * 0.5f + dado.Entre(-0.4f, 0.4f);
                    float sorte = dado.Fracao();
                    var e = dado.Fracao() < 0.55f ? Especie.GramaA : Especie.GramaB;
                    float escala = dado.Entre(0.8f, 1.45f), giro = dado.Entre(0f, 360f);
                    if (sorte >= chance) continue;
                    if (cidade != null && !LivreNaCidade(cidade, new Vector2(x, z))) continue;
                    saida.Add(new PlantaDaFloresta { Especie = e, Posicao = new Vector3(x, _d.Altura(x, z) - 0.02f, z), Giro = giro, Escala = escala });
                }
            }
        }

        static readonly float[] Lados = { 1f, -1f };

        /// <summary>
        /// O mato nas frestas das ruas das cidades do bloco (bx, bz) (Parte M9c, o abandono), somado na saída: a grama e o
        /// capim na sarjeta (encostados no meio-fio, na altura do asfalto), na beira de dentro da calçada (encostados no
        /// lote) e uns soltos nas rachaduras do meio dela. Vai com a grama (só perto do jogador). Seguro em várias threads.
        /// </summary>
        public void FrestasDoBloco(int bx, int bz, List<PlantaDaFloresta> saida)
        {
            var lista = _d.CidadesDoBloco(bx, bz);
            if (lista == null || _d.Plano == null) return;
            foreach (int ti in lista)
            {
                var t = _d.Plano.Tracados[ti];
                var doBloco = t.Bloco(bx, bz);
                if (doBloco == null) continue;
                foreach (int k in doBloco.Pecas)
                {
                    var p = t.Pecas[k];
                    if (!t.CalcadaDaPeca(p, out float c0, out float c1, out float a, out float s)) continue;
                    var dado = new Dado(Semente(bx, bz) ^ ((ulong)(uint)(ti * 7919 + k + 1) * 0xBF58476D1CE4E5B9UL));
                    for (float x = c0 + 0.3f; x < c1 - 0.3f; x += 0.6f)
                        foreach (float lado in Lados)
                        {
                            if (dado.Fracao() < 0.5f)
                                Fresta(saida, t, p, x + dado.Entre(-0.25f, 0.25f), lado * (a - dado.Entre(0.06f, 0.2f)), TracadoDaCidade.AlturaDoAsfalto, ref dado, 0.55f, 1.05f);
                            if (dado.Fracao() < 0.4f)
                                Fresta(saida, t, p, x + dado.Entre(-0.25f, 0.25f), lado * (a + s - dado.Entre(0.08f, 0.25f)), TracadoDaCidade.AlturaDaCalcada, ref dado, 0.5f, 1.0f);
                            if (s > 1f && dado.Fracao() < 0.06f)
                                Fresta(saida, t, p, x, lado * (a + dado.Entre(0.4f, s - 0.4f)), TracadoDaCidade.AlturaDaCalcada, ref dado, 0.4f, 0.75f);
                        }
                }
            }
        }

        // um tufo na fresta: o ponto local do pedaço da rua, a altura sobre o chão e a escala sorteada
        void Fresta(List<PlantaDaFloresta> saida, TracadoDaCidade t, TracadoDaCidade.Peca p, float aoLongo, float atravessado, float acima, ref Dado dado,
                    float escalaMin, float escalaMax)
        {
            var w = t.Mundo(t.NaPeca(p, aoLongo, atravessado));
            float r = dado.Fracao();
            var e = r < 0.45f ? Especie.GramaB : r < 0.8f ? Especie.GramaA : Especie.CapimB;
            float giro = dado.Entre(0f, 360f), escala = dado.Entre(escalaMin, escalaMax);
            saida.Add(new PlantaDaFloresta { Especie = e, Posicao = new Vector3(w.x, _d.Altura(w.x, w.y) + acima - 0.02f, w.y), Giro = giro, Escala = escala });
        }

        ulong Semente(int bx, int bz) => Sorteio.Misturar(((ulong)(uint)_d.Semente << 32) ^ ((ulong)(uint)bx << 16) ^ (uint)bz ^ 0x5F0E57A11UL);

        void Por(List<PlantaDaFloresta> saida, Especie e, float x, float z, float giro, float escala)
        {
            // o pé um pouco enterrado (o chão é torto embaixo do tronco)
            float y = _d.Altura(x, z) - (EhArvore(e) ? 0.12f : 0.04f);
            saida.Add(new PlantaDaFloresta { Especie = e, Posicao = new Vector3(x, y, z), Giro = giro, Escala = escala });
        }

        // a encosta não passa de 45° (só as pedras ficam nas mais em pé)
        bool Plano(float x, float z)
        {
            float gx = _d.Altura(x + 1f, z) - _d.Altura(x - 1f, z);
            float gz = _d.Altura(x, z + 1f) - _d.Altura(x, z - 1f);
            return gx * gx + gz * gz < 4f;
        }

        // ------------------------------------------------------------------ as espécies de cada mancha

        // (espécie, peso)
        static readonly (Especie, float)[] DaMista =
        {
            (Especie.BordoVerde, 10), (Especie.BordoVermelho, 5), (Especie.BordoLaranja, 4), (Especie.CarvalhoA, 6),
            (Especie.CarvalhoB, 5), (Especie.BetulaA, 6), (Especie.BetulaB, 5), (Especie.PinheiroBrancoA, 11),
            (Especie.PinheiroBrancoB, 10), (Especie.AbetoA, 10), (Especie.AbetoB, 10), (Especie.PinheiroVermelhoA, 3),
            (Especie.MortaA, 3), (Especie.MortaB, 2),
        };
        static readonly (Especie, float)[] DasConiferas =
        {
            (Especie.AbetoA, 20), (Especie.AbetoB, 18), (Especie.AbetoC, 6), (Especie.PinheiroBrancoA, 12),
            (Especie.PinheiroBrancoB, 10), (Especie.PinheiroVermelhoA, 4), (Especie.PinheiroVermelhoB, 3), (Especie.BetulaA, 4),
            (Especie.BetulaB, 3), (Especie.MortaB, 5), (Especie.BordoVermelho, 2),
        };
        static readonly (Especie, float)[] DaRebrota =
        {
            (Especie.AbetoC, 30), (Especie.AbetoB, 6), (Especie.BetulaA, 8), (Especie.BetulaB, 8), (Especie.BordoVerde, 6),
            (Especie.BordoVermelho, 3), (Especie.PinheiroBrancoB, 12), (Especie.CarvalhoB, 2), (Especie.MortaA, 3),
        };
        static readonly (Especie, float)[] DoCampo =
        {
            (Especie.CarvalhoA, 5), (Especie.CarvalhoB, 4), (Especie.BordoVerde, 3), (Especie.BordoVermelho, 2),
            (Especie.BordoLaranja, 2), (Especie.AbetoC, 18), (Especie.AbetoB, 4), (Especie.BetulaB, 5),
            (Especie.PinheiroBrancoB, 8), (Especie.MortaA, 4),
        };
        static readonly (Especie, float)[] DoBaldio =
        {
            (Especie.AbetoC, 16), (Especie.BetulaA, 12), (Especie.BetulaB, 12), (Especie.BordoVerde, 8),
            (Especie.BordoVermelho, 5), (Especie.PinheiroBrancoB, 8), (Especie.CarvalhoB, 3), (Especie.MortaA, 4),
        };
        static readonly (Especie, float)[] DoQuintal =
        {
            (Especie.BordoVerde, 10), (Especie.BordoVermelho, 6), (Especie.BordoLaranja, 4), (Especie.CarvalhoA, 5),
            (Especie.AbetoB, 8), (Especie.AbetoC, 6), (Especie.BetulaA, 6), (Especie.PinheiroBrancoB, 6), (Especie.MortaA, 3),
        };
        static readonly (Especie, float)[] DaSerra =
        {
            (Especie.AbetoA, 22), (Especie.AbetoB, 20), (Especie.AbetoC, 8), (Especie.PinheiroBrancoA, 10),
            (Especie.PinheiroBrancoB, 8), (Especie.MortaB, 6), (Especie.BetulaB, 4), (Especie.CarvalhoB, 2),
        };
        static readonly (Especie, float)[] DaBeira =
        {
            (Especie.Salgueiro, 18), (Especie.BetulaA, 12), (Especie.BetulaB, 10), (Especie.BordoVerde, 6),
            (Especie.BordoVermelho, 4), (Especie.AbetoB, 8), (Especie.AbetoC, 8), (Especie.MortaA, 3),
        };

        Especie EspecieDaArvore(Mancha m, ref Dado dado, float x, float z)
        {
            var tabela = m switch
            {
                Mancha.Coniferas => DasConiferas,
                Mancha.Rebrota => DaRebrota,
                Mancha.Campo or Mancha.Lavoura => DoCampo,
                Mancha.Serra => DaSerra,
                Mancha.Beira => DaBeira,
                Mancha.Baldio => DoBaldio,
                Mancha.Quintal => DoQuintal,
                _ => DaMista,
            };
            // dois ruídos juntam as espécies em grupos: as bétulas num trecho, os bordos do outono em outro
            float betulas = _tipo.Amostra01(x / 60f, z / 60f);
            float outono = _tipo.Amostra01(x / 90f + 50f, z / 90f - 20f);
            float soma = 0f;
            foreach (var (e, peso) in tabela) soma += Peso(e, peso, betulas, outono);
            float r = dado.Fracao() * soma;
            foreach (var (e, peso) in tabela)
            {
                r -= Peso(e, peso, betulas, outono);
                if (r <= 0f) return e;
            }
            return tabela[tabela.Length - 1].Item1;
        }

        static float Peso(Especie e, float peso, float betulas, float outono) => e switch
        {
            Especie.BetulaA or Especie.BetulaB => peso * (0.2f + 1.8f * betulas),
            Especie.BordoVermelho or Especie.BordoLaranja => peso * (0.3f + 1.6f * outono),
            _ => peso,
        };

        // o chão de cada mancha: (espécie, chance numa célula de 3,2 m)
        static readonly (Especie, float)[] ChaoDaMista =
        {
            (Especie.ArbustoA, 0.07f), (Especie.ArbustoB, 0.05f), (Especie.ArbustoC, 0.04f), (Especie.SamambaiaA, 0.1f),
            (Especie.SamambaiaB, 0.08f), (Especie.CapimB, 0.04f), (Especie.TocoA, 0.005f), (Especie.TocoB, 0.004f),
            (Especie.TroncoCaidoA, 0.005f), (Especie.TroncoCaidoB, 0.008f), (Especie.PedraA, 0.01f), (Especie.PedraB, 0.005f),
        };
        static readonly (Especie, float)[] ChaoDasConiferas =
        {
            (Especie.SamambaiaA, 0.08f), (Especie.SamambaiaB, 0.06f), (Especie.ArbustoA, 0.04f), (Especie.ArbustoB, 0.03f),
            (Especie.CapimB, 0.02f), (Especie.TroncoCaidoB, 0.01f), (Especie.TroncoCaidoA, 0.004f), (Especie.TocoB, 0.006f),
            (Especie.PedraA, 0.012f), (Especie.PedraB, 0.006f),
        };
        static readonly (Especie, float)[] ChaoDaSerra =
        {
            (Especie.PedraA, 0.03f), (Especie.PedraB, 0.025f), (Especie.PedraC, 0.012f), (Especie.ArbustoA, 0.05f),
            (Especie.ArbustoB, 0.04f), (Especie.CapimA, 0.06f), (Especie.CapimB, 0.06f), (Especie.SamambaiaA, 0.03f),
            (Especie.TroncoCaidoB, 0.006f), (Especie.TocoB, 0.004f),
        };
        static readonly (Especie, float)[] ChaoDaRebrota =
        {
            (Especie.ArbustoA, 0.1f), (Especie.ArbustoB, 0.06f), (Especie.ArbustoC, 0.07f), (Especie.CapimA, 0.16f),
            (Especie.CapimB, 0.12f), (Especie.SamambaiaB, 0.03f), (Especie.TocoA, 0.008f), (Especie.PedraA, 0.006f),
        };
        static readonly (Especie, float)[] ChaoDoCampo =
        {
            (Especie.CapimA, 0.22f), (Especie.CapimB, 0.18f), (Especie.ArbustoA, 0.05f), (Especie.ArbustoC, 0.06f),
            (Especie.ArbustoB, 0.03f), (Especie.PedraA, 0.008f), (Especie.TocoA, 0.004f), (Especie.TroncoCaidoB, 0.002f),
        };
        static readonly (Especie, float)[] ChaoDaBeira =
        {
            (Especie.Taboa, 0.3f), (Especie.CapimA, 0.15f), (Especie.CapimB, 0.1f), (Especie.ArbustoA, 0.08f),
            (Especie.ArbustoC, 0.05f), (Especie.SamambaiaA, 0.04f),
        };
        static readonly (Especie, float)[] ChaoDoBaldio =
        {
            (Especie.ArbustoA, 0.1f), (Especie.ArbustoB, 0.06f), (Especie.ArbustoC, 0.06f), (Especie.CapimA, 0.22f),
            (Especie.CapimB, 0.14f), (Especie.TocoA, 0.006f), (Especie.PedraA, 0.006f),
        };
        static readonly (Especie, float)[] ChaoDoQuintal =
        {
            (Especie.ArbustoA, 0.08f), (Especie.ArbustoB, 0.04f), (Especie.ArbustoC, 0.04f), (Especie.CapimA, 0.18f),
            (Especie.CapimB, 0.14f), (Especie.TocoA, 0.004f),
        };
        static readonly (Especie, float)[] ChaoDaLavoura =
        {
            (Especie.CapimA, 0.07f), (Especie.CapimB, 0.06f), (Especie.ArbustoA, 0.015f), (Especie.ArbustoC, 0.02f),
            (Especie.PedraA, 0.004f),
        };
        static readonly (Especie, float)[] ChaoDoPlantio =
        {
            (Especie.CapimA, 0.14f), (Especie.CapimB, 0.12f), (Especie.ArbustoA, 0.05f), (Especie.ArbustoC, 0.05f),
            (Especie.TocoA, 0.015f), (Especie.TroncoCaidoB, 0.004f),
        };

        static bool EspecieDoChao(Mancha m, float r, out Especie e)
        {
            var tabela = m switch
            {
                Mancha.Mista => ChaoDaMista,
                Mancha.Coniferas => ChaoDasConiferas,
                Mancha.Rebrota => ChaoDaRebrota,
                Mancha.Campo => ChaoDoCampo,
                Mancha.Serra => ChaoDaSerra,
                Mancha.Beira => ChaoDaBeira,
                Mancha.Baldio => ChaoDoBaldio,
                Mancha.Quintal => ChaoDoQuintal,
                Mancha.Lavoura => ChaoDaLavoura,
                _ => ChaoDoPlantio,
            };
            foreach (var (esp, chance) in tabela)
            {
                r -= chance;
                if (r < 0f)
                {
                    e = esp;
                    return true;
                }
            }
            e = default;
            return false;
        }

        /// <summary>Um sorteio que não aloca (o do <see cref="Sorteio"/>, num struct): cada célula tem o seu.</summary>
        struct Dado
        {
            ulong _estado;

            public Dado(ulong semente) => _estado = semente;

            public float Fracao()
            {
                _estado += 0x9E3779B97F4A7C15UL;
                return (Sorteio.Misturar(_estado) >> 40) * (1f / 16777216f);
            }

            public float Entre(float min, float max) => min + (max - min) * Fracao();
        }
    }
}
