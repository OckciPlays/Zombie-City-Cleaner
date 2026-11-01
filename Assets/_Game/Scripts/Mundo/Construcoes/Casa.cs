using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Uma casa levantada a partir da Planta, com as receitas das casas da demo (o `cidade_casas.py`): lambri, rodapé,
    /// quinas, molduras, janelas abertas ou com tábuas, telhado de telhas em fileiras com oitões, calha e condutor,
    /// chaminé, varanda, ar-condicionado, arbustos, luminárias e manchas. Os cacos nas janelas, as cortinas, as
    /// persianas e os papéis pelo chão saíram a pedido (26/10/2026).
    ///
    /// O que é novo em relação à demo:
    ///  - as paredes de dentro saem dos cômodos (onde dois cômodos se encostam, há parede; as portas vêm da planta);
    ///  - as janelas são distribuídas pelos cômodos, pelo tamanho e pelo tipo de cada um, desviando das portas;
    ///  - andares (a laje entre eles faz a faixa por fora), escada com vão e guarda-corpo, porão (com as janelinhas
    ///    rente ao chão), a entrada de porão por fora (o fosso com a tampa inclinada) e o telhado nos dois sentidos.
    /// Coordenadas da casa: o meio no centro, a frente em -z. Espelhar a planta espelha a malha no fim (ParaMesh).
    /// </summary>
    public sealed class Casa : Obra
    {
        public readonly Planta p;
        /// <summary>A altura do piso do térreo: 0,15 m; com porão, 0,75 m (a casa fica sobre a fundação, com degraus).</summary>
        public readonly float piso;
        readonly float x0, x1, z0, z1;

        sealed class Interna
        {
            public int andar;
            public Vector2 a, b;
            public bool vertical;
            public float inicio;
            public Comodo menor, maior;
            public Comodo LadoN => vertical ? maior : menor;
        }

        struct PortaFeita { public Parede w; public Abertura ab; public Ligacao l; public Interna i; public int andar; }

        readonly Dictionary<int, List<Rect>> _buracos = new Dictionary<int, List<Rect>>();
        readonly List<Interna> _internas = new List<Interna>();
        readonly List<PortaFeita> _portasDeFora = new List<PortaFeita>();
        readonly List<PortaFeita> _portasDeDentro = new List<PortaFeita>();
        Vector2 _entrada;
        float _varandaXa, _varandaXb, _cumeeira, _xFimDoFosso;

        /// <summary>
        /// A garagem: as paredes vão do chão até 2,7 m, e o telhado de uma água sobe 0,5 m até a parede da casa (fica
        /// embaixo do beiral dela).
        /// </summary>
        const float AlturaDaGaragem = 2.7f, SubidaDaGaragem = 0.5f;
        /// <summary>O "cômodo" da garagem (fora da planta da casa), para a cor das paredes por dentro.</summary>
        Comodo _garagem;
        float _gxa, _gxb, _sPortao;
        Parede _frenteDaGaragem;
        readonly List<(Vector2 pos, float yaw)> _carros = new List<(Vector2 pos, float yaw)>();
        Vector2? _entradaDoCarro;

        /// <summary>Os pontos dos móveis de todos os cômodos (sem espelhar), feitos no fim do <see cref="Montar"/>.</summary>
        public readonly List<PontoDeMovel> moveis = new List<PontoDeMovel>();

        public Casa(Planta planta, Medidas medidas) : base(planta.semente, medidas)
        {
            p = planta;
            piso = PisoDe(p);
            x0 = p.X0; x1 = p.X1; z0 = p.Z0; z1 = p.Z1;
        }

        // ------------------------------------------------------------ o que se sabe só pela planta
        // (o gerador do mapa usa para o tamanho do lote, o patamar, os buracos do porão e o carro, sem montar a casa)

        /// <summary>A altura do piso do térreo: 0,15 m; com porão, 0,75 m.</summary>
        public static float PisoDe(Planta p) => p.porao ? 0.75f : 0.15f;

        /// <summary>O x do fim do fosso da entrada do porão por fora (sem espelhar).</summary>
        static float FimDoFosso(Planta p, Medidas m)
        {
            float xw = (p.poraoDeFora > 0 ? p.X1 : p.X0) + p.poraoDeFora * m.esp / 2;
            int n = Espelhos(-(PisoDe(p) - m.laje - m.alturaPorao));
            return xw + p.poraoDeFora * (1.0f + (n - 1) * 0.25f);
        }

        /// <summary>
        /// A calçada em volta dos buracos do porão, no mundo gerado (Parte M6): o terreno tira as células de 2 m que
        /// encostam no porão e no fosso, e esta faixa de concreto tapa o que sobra delas (uma célula girada passa no máximo
        /// 2,83 m do buraco). Ela conta como parte da casa (nos limites), para o lote ser do tamanho certo.
        /// </summary>
        public const float CalcadaDoPorao = 3f;

        /// <summary>
        /// A largura da escada do fosso da entrada do porão por fora (entre as paredes de lado). As duas folhas da tampa
        /// ficam abertas: com uma fechada, embaixo dela não havia altura no alto da escada, e a faixa ao lado (a metade da
        /// largura, menos o raio do zumbi dos dois lados) era estreita demais para o NavMesh, nem com 2 m (Parte M6).
        /// </summary>
        public const float LarguraDoFosso = 2f;

        /// <summary>
        /// Até onde a casa vai para os lados (com a chaminé, o ar, o fosso, a garagem ou a vaga e, com porão, a calçada em
        /// volta dos buracos), já espelhado.
        /// </summary>
        public static Vector2 Limites(Planta p, Medidas m)
        {
            float a = p.X0 - 1.2f, b = p.X1 + 1.2f;
            if (p.poraoDeFora > 0) b = Mathf.Max(b, FimDoFosso(p, m) + 0.6f);
            if (p.poraoDeFora < 0) a = Mathf.Min(a, FimDoFosso(p, m) - 0.6f);
            var g = p.garagem;
            if (g != null && g.lado > 0) b = Mathf.Max(b, p.X1 + g.largura + 0.6f);
            if (g != null && g.lado < 0) a = Mathf.Min(a, p.X0 - g.largura - 0.6f);
            foreach (var r in BuracosSemEspelho(p, m))
            {
                a = Mathf.Min(a, r.xMin - CalcadaDoPorao);
                b = Mathf.Max(b, r.xMax + CalcadaDoPorao);
            }
            return p.espelhar ? new Vector2(-b, -a) : new Vector2(a, b);
        }

        /// <summary>
        /// De onde até onde a casa vai na frente e atrás (z): a varanda e os degraus da frente, a vaga, as soleiras dos
        /// fundos e, com porão, a calçada em volta dos buracos.
        /// </summary>
        public static Vector2 Profundidade(Planta p, Medidas m)
        {
            float piso = PisoDe(p);
            float degraus = piso < 0.3f ? 0.5f : (Mathf.CeilToInt(piso / 0.19f - 0.05f) - 1) * 0.3f + 0.2f;
            float frente = p.Z0 - m.esp / 2 - (p.varanda ? p.fundoDaVaranda : 1.2f) - degraus;
            float fundo = p.Z1 + m.esp / 2 + 1.0f + degraus;
            if (p.garagem != null) frente = Mathf.Min(frente, p.garagem.z0 - 0.3f);
            foreach (var r in BuracosSemEspelho(p, m))
            {
                frente = Mathf.Min(frente, r.yMin - CalcadaDoPorao);
                fundo = Mathf.Max(fundo, r.yMax + CalcadaDoPorao);
            }
            return new Vector2(frente, fundo);
        }

        /// <summary>
        /// Onde o carro da casa fica: o meio dele e o giro (0 = a frente do carro para o fundo da casa), já espelhado; false
        /// sem lugar de carro. É o mesmo ponto de <see cref="Carros"/>, sem montar a casa.
        /// </summary>
        public static bool LugarDoCarro(Planta p, Medidas m, out Vector2 pos, out float yaw)
        {
            pos = Vector2.zero;
            yaw = 0f;
            var g = p.garagem;
            if (g == null) return false;
            float s = g.lado, xi = s > 0 ? p.X1 : p.X0, xo = xi + s * g.largura;
            float x = g.coberta
                ? (g.portaParaDentro ? xo - s * (m.esp / 2 + 0.25f + 1.05f) : (xi + xo) / 2)
                : (Mathf.Min(xi + s * 0.5f, xo) + Mathf.Max(xi + s * 0.5f, xo)) / 2;
            pos = new Vector2(p.espelhar ? -x : x, (g.z0 + g.z1) / 2);
            return true;
        }

        // o porão (um pouco maior que a casa) e o fosso da entrada dele, sem espelhar
        static List<Rect> BuracosSemEspelho(Planta p, Medidas m)
        {
            var r = new List<Rect>();
            if (p.porao) r.Add(Rect.MinMaxRect(p.X0 - m.esp / 2, p.Z0 - m.esp / 2, p.X1 + m.esp / 2, p.Z1 + m.esp / 2));
            if (p.poraoDeFora != 0)
            {
                float xw = (p.poraoDeFora > 0 ? p.X1 : p.X0) + p.poraoDeFora * m.esp / 2, fim = FimDoFosso(p, m);
                float meia = LarguraDoFosso / 2 + 0.2f;   // com as paredes de lado
                r.Add(Rect.MinMaxRect(Mathf.Min(xw, fim), p.zPoraoDeFora - meia, Mathf.Max(xw, fim), p.zPoraoDeFora + meia));
            }
            return r;
        }

        /// <summary>Onde o chão em volta precisa de buraco (o porão e o fosso da entrada dele), já espelhado, só pela planta.</summary>
        public static List<Rect> BuracosDa(Planta p, Medidas m)
        {
            var r = BuracosSemEspelho(p, m);
            if (p.espelhar)
                for (int i = 0; i < r.Count; i++) r[i] = Rect.MinMaxRect(-r[i].xMax, r[i].yMin, -r[i].xMin, r[i].yMax);
            return r;
        }

        public float Base(int andar) => andar >= 0 ? piso + andar * (m.altura + m.laje) : piso - m.laje - m.alturaPorao;
        float Altura(int andar) => andar >= 0 ? m.altura : m.alturaPorao;
        float Teto(int andar) => Base(andar) + Altura(andar);
        int Topo => p.andares - 1;
        int Primeiro => p.porao ? -1 : 0;
        float TopoDoForro => Teto(Topo) + m.forro;

        Vector2 Esp(Vector2 v) => p.espelhar ? new Vector2(-v.x, v.y) : v;

        /// <summary>O pé da escada da frente (para o caminho até a calçada), já espelhado.</summary>
        public Vector2 Entrada => Esp(_entrada);

        /// <summary>Onde o chão em volta precisa de buraco (o porão e o fosso da entrada dele), já espelhado.</summary>
        public List<Rect> BuracosNoChao => BuracosDa(p, m);

        /// <summary>Até onde a casa vai para os lados (com a chaminé, o ar, o fosso e a garagem ou a vaga), já espelhado.</summary>
        public Vector2 LimitesEmX => Limites(p, m);

        /// <summary>
        /// Onde os carros podem nascer: o meio do carro e o giro (0 = a frente do carro para o fundo da casa, entrando de
        /// frente na garagem ou na vaga), já espelhado. Quem monta o mundo decide se o carro está lá.
        /// </summary>
        public List<(Vector2 pos, float yaw)> Carros => _carros.ConvertAll(c => (Esp(c.pos), p.espelhar ? -c.yaw : c.yaw));

        /// <summary>A boca da garagem (ou a frente da vaga), para o caminho do carro até a rua, já espelhada; null sem lugar de carro.</summary>
        public Vector2? EntradaDoCarro => _entradaDoCarro.HasValue ? Esp(_entradaDoCarro.Value) : (Vector2?)null;

        // ------------------------------------------------------------ cores pela posição

        protected override object ComodoEm(Vector3 q)
        {
            if (q.x < x0 || q.x > x1 || q.z < z0 || q.z > z1)
                return _garagem != null && q.y < AlturaDaGaragem + SubidaDaGaragem + 0.3f && _garagem.Contem(q.x, q.z) ? _garagem : null;
            for (int f = Primeiro; f <= Topo; f++)
            {
                if (q.y < Base(f) - 0.05f || q.y > Teto(f) + 0.05f) continue;
                foreach (var k in p.comodos) if (k.andar == f && k.Contem(q.x, q.z)) return k;
            }
            return null;
        }

        protected override string CorDeFora(Vector3 q) => q.y < piso - 0.05f ? "Concreto" : p.cor;
        protected override string CorDeDentro(object k, Vector3 q) => ((Comodo)k).papel ?? "ParedeInterna";

        // ------------------------------------------------------------ a casa

        public void Montar()
        {
            Preparar();
            for (int f = Primeiro; f <= Topo; f++)
            {
                for (int lado = 0; lado < 4; lado++) RegistrarLinha(LadoA(lado), LadoB(lado), Base(f), Teto(f));
                Internas(f);
            }
            foreach (var i in _internas) RegistrarLinha(i.a, i.b, Base(i.andar), Teto(i.andar));

            Lajes();
            for (int f = Primeiro; f <= Topo; f++)
                for (int lado = 0; lado < 4; lado++) ParedeDeFora(f, lado);
            foreach (var i in _internas) ParedeInterna(i);
            ParedesDaGaragem();
            Escadas();
            Revestimento();
            Aberturas();
            Telhado();
            if (p.chamine != 0) Chamine(p.chamine);
            if (p.varanda) Varanda(); else Entradinha();
            Soleiras();
            CompletarGaragem();
            if (p.ar != 0) ArCondicionado(p.ar);
            if (p.poraoDeFora != 0) PoraoDeFora();
            PorFora();
            PorDentro();
            foreach (var k in p.comodos) MoveisDaObra.Distribuir(this, k, Base(k.andar), "casa", p.semente, moveis);
        }

        /// <summary>Os vãos das escadas, a varanda e o fosso (as paredes precisam saber deles antes).</summary>
        void Preparar()
        {
            for (int f = Primeiro; f <= Topo + 1; f++) _buracos[f] = new List<Rect>();
            foreach (var L in p.lances)
            {
                float b = Base(L.de), t = Base(L.de + 1);
                int n = Espelhos(t - b);
                float r = (t - b) / n, comp = (n - 1) * m.pisada;
                // o vão começa onde a cápsula do jogador passa sem bater a cabeça na borda da laje: 1,8 m dela, mais
                // os 0,3 m que o CharacterController sobe antes de andar (stepOffset), a pele e uma folga (2,6 m em cima
                // do degrau), e 0,4 m antes, porque a cabeça chega na borda antes do meio do corpo
                float inicio = Mathf.Clamp((Altura(L.de) - 2.6f) * m.pisada / r - 0.4f, 0f, comp - 0.3f);
                _buracos[L.de + 1].Add(Rect.MinMaxRect(L.x0, L.zIni + inicio, L.x1, L.zIni + comp));
            }
            if (p.varanda)
            {
                if (p.varandaLarga) { _varandaXa = x0 + 0.4f; _varandaXb = x1 - 0.4f; }
                else
                {
                    float meia = Mathf.Max(1.7f, m.portaFrente / 2 + 1.15f);
                    _varandaXa = Mathf.Max(x0 + 0.3f, p.xPortaFrente - meia);
                    _varandaXb = Mathf.Min(x1 - 0.3f, p.xPortaFrente + meia);
                }
            }
            if (p.poraoDeFora != 0) _xFimDoFosso = FimDoFosso(p, m);
            var g = p.garagem;
            if (g != null)
            {
                float xi = g.lado > 0 ? x1 : x0, xo = xi + g.lado * g.largura;
                _gxa = Mathf.Min(xi, xo); _gxb = Mathf.Max(xi, xo);
                if (g.coberta)
                {
                    _garagem = new Comodo { tipo = "garagem", andar = 0, papel = "Concreto", piso = "ConcretoClaro", x0 = _gxa, x1 = _gxb, z0 = g.z0, z1 = g.z1 };
                    foreach (var (a, b) in LinhasDaGaragem()) RegistrarLinha(a, b, 0f, AlturaDaGaragem + SubidaDaGaragem);
                }
            }
        }

        /// <summary>
        /// As paredes da garagem (a da frente, a de fora e a do fundo), cada uma no sentido em que o lado de fora (n) fica
        /// para fora da garagem. A quarta é a parede da casa.
        /// </summary>
        (Vector2 a, Vector2 b)[] LinhasDaGaragem()
        {
            var g = p.garagem;
            float xo = g.lado > 0 ? _gxb : _gxa;
            return new[]
            {
                (new Vector2(_gxa, g.z0), new Vector2(_gxb, g.z0)),
                g.lado > 0 ? (new Vector2(xo, g.z0), new Vector2(xo, g.z1)) : (new Vector2(xo, g.z1), new Vector2(xo, g.z0)),
                (new Vector2(_gxb, g.z1), new Vector2(_gxa, g.z1)),
            };
        }

        // ------------------------------------------------------------ os lados de fora

        Vector2 LadoA(int lado) => lado switch { 0 => new Vector2(x0, z0), 1 => new Vector2(x1, z0), 2 => new Vector2(x1, z1), _ => new Vector2(x0, z1) };
        Vector2 LadoB(int lado) => lado switch { 0 => new Vector2(x1, z0), 1 => new Vector2(x1, z1), 2 => new Vector2(x0, z1), _ => new Vector2(x0, z0) };
        float S(int lado, float pos) => lado switch { 0 => pos - x0, 1 => pos - z0, 2 => x1 - pos, _ => z1 - pos };

        bool Segmento(Comodo k, int lado, out float s0, out float s1)
        {
            s0 = s1 = 0f;
            switch (lado)
            {
                case 0: if (Mathf.Abs(k.z0 - z0) > 0.01f) return false; s0 = k.x0 - x0; s1 = k.x1 - x0; return true;
                case 1: if (Mathf.Abs(k.x1 - x1) > 0.01f) return false; s0 = k.z0 - z0; s1 = k.z1 - z0; return true;
                case 2: if (Mathf.Abs(k.z1 - z1) > 0.01f) return false; s0 = x1 - k.x1; s1 = x1 - k.x0; return true;
                default: if (Mathf.Abs(k.x0 - x0) > 0.01f) return false; s0 = z1 - k.z1; s1 = z1 - k.z0; return true;
            }
        }

        /// <summary>Um lugar para o meio de uma janela perto de s, entre lo e hi, fora dos intervalos ocupados.</summary>
        static float? Encaixar(float s, float larg, float lo, float hi, List<Vector2> ocupados)
        {
            var livres = new List<Vector2> { new Vector2(lo, hi) };
            foreach (var o in ocupados)
            {
                float a = o.x - larg / 2, b = o.y + larg / 2;
                var novos = new List<Vector2>();
                foreach (var l in livres)
                {
                    if (b <= l.x || a >= l.y) { novos.Add(l); continue; }
                    if (a > l.x) novos.Add(new Vector2(l.x, a));
                    if (b < l.y) novos.Add(new Vector2(b, l.y));
                }
                livres = novos;
            }
            float? melhor = null;
            float dist = float.MaxValue;
            foreach (var l in livres)
            {
                if (l.y < l.x) continue;
                float c = Mathf.Clamp(s, l.x, l.y);
                if (Mathf.Abs(c - s) < dist) { dist = Mathf.Abs(c - s); melhor = c; }
            }
            return melhor;
        }

        int Quantas(Comodo k, int lado, float U, float larg)
        {
            switch (k.tipo)
            {
                case "sala": return U >= 3 * larg + 4.5f ? 3 : U >= 2 * larg + 2.2f ? 2 : U >= larg ? 1 : 0;
                case "quarto": return U >= 2 * larg + 2.2f ? 2 : U >= larg ? 1 : 0;
                case "cozinha": return U >= 2 * larg + 3f ? 2 : U >= larg ? 1 : 0;
                case "banheiro": return U >= larg ? 1 : 0;
                case "corredor": return (lado == 0 || lado == 2) && U >= larg ? 1 : 0;
                case "porao": return Mathf.Clamp(Mathf.FloorToInt(U / 3.5f), 0, 3);
                default: return 0;
            }
        }

        void ParedeDeFora(int f, int lado)
        {
            var aberturas = new List<Abertura>();
            var ocupados = new List<Vector2>();
            var portas = new List<Ligacao>();
            foreach (var l in p.ligacoes)
            {
                if (l.b != null || l.a.andar != f || l.lado != lado) continue;
                bool frente = lado == 0 && f == 0;
                float larg = frente ? m.portaFrente : m.porta, alto = frente ? m.alturaPortaFrente : m.alturaPorta;
                float s = S(lado, l.pos);
                aberturas.Add(new Abertura(s - larg / 2, larg, 0f, alto));
                portas.Add(l);
                ocupados.Add(new Vector2(s - larg / 2 - 0.4f, s + larg / 2 + 0.4f));
            }
            int ladoDoFosso = p.poraoDeFora > 0 ? 1 : 3;
            if (f == -1 && p.poraoDeFora != 0 && lado == ladoDoFosso)
            {
                // a porta da largura das outras: com 1 m e a folha aberta, sobravam uns 18 cm para o NavMesh (o zumbi
                // ora passava, ora não, conforme a grade caía)
                float s = S(lado, p.zPoraoDeFora);
                aberturas.Add(new Abertura(s - m.porta / 2, m.porta, 0f, 2.1f));
                portas.Add(null);
                ocupados.Add(new Vector2(s - LarguraDoFosso / 2 - 0.25f, s + LarguraDoFosso / 2 + 0.25f));
            }
            // o que tapa a parede por fora: a chaminé (em todos os andares), e no porão a varanda e o ar
            int ladoDaChamine = p.chamine > 0 ? 1 : 3;
            if (p.chamine != 0 && lado == ladoDaChamine)
            {
                float s = S(lado, z1 - p.D * 0.525f);
                ocupados.Add(new Vector2(s - 0.7f, s + 0.7f));
            }
            // a parede que dá para dentro da garagem não tem janela (nem a do porão, embaixo do chão dela)
            var g = p.garagem;
            if (g != null && g.coberta && f <= 0 && lado == (g.lado > 0 ? 1 : 3))
            {
                float ga = S(lado, g.z0), gb = S(lado, g.z1);
                ocupados.Add(new Vector2(Mathf.Min(ga, gb) - 0.3f, Mathf.Max(ga, gb) + 0.3f));
            }
            if (f == -1)
            {
                if (lado == 0 && p.varanda) ocupados.Add(new Vector2(_varandaXa - x0 - 0.2f, _varandaXb - x0 + 0.2f));
                if (lado == 0 && !p.varanda) ocupados.Add(new Vector2(p.xPortaFrente - x0 - 1.3f, p.xPortaFrente - x0 + 1.3f));
                if (p.ar != 0 && lado == (p.ar > 0 ? 1 : 3)) { float s = S(lado, z1 - 1.1f); ocupados.Add(new Vector2(s - 0.6f, s + 0.6f)); }
            }
            foreach (var k in p.comodos)
            {
                if (k.andar != f || !Segmento(k, lado, out float s0, out float s1)) continue;
                bool porao = k.tipo == "porao", pequena = k.tipo == "banheiro";
                float larg = porao ? 0.9f : pequena ? m.janelaPequena : m.janela;
                float baixo = porao ? m.alturaPorao - 0.42f : pequena ? m.peitorilPequena : k.tipo == "cozinha" ? m.peitorilDaCozinha : m.peitoril;
                float alto = porao ? m.alturaPorao - 0.1f : m.topoJanela;
                float U = s1 - s0 - 1.2f;
                int n = Quantas(k, lado, U, larg);
                for (int j = 0; j < n; j++)
                {
                    float s = s0 + 0.6f + U * (j + 0.5f) / n;
                    var c = Encaixar(s, larg, s0 + 0.6f + larg / 2, s1 - 0.6f - larg / 2, ocupados);
                    if (c == null) continue;
                    aberturas.Add(new Abertura(c.Value - larg / 2, larg, baixo, alto));
                    ocupados.Add(new Vector2(c.Value - larg / 2 - 0.6f, c.Value + larg / 2 + 0.6f));
                }
            }
            // a parede é cortada onde muda o cômodo de dentro (a cor de cada pedaço por dentro é a do cômodo dele)
            var cortes = new List<float>();
            foreach (var k in p.comodos)
                if (k.andar == f && Segmento(k, lado, out float c0, out float c1)) { cortes.Add(c0); cortes.Add(c1); }
            string nome = f < 0 ? "porao" : lado switch { 0 => "frente", 1 => "direita", 2 => "fundo", _ => "esquerda" };
            var w = LevantarParede(LadoA(lado), LadoB(lado), aberturas, f >= 0, nome, Base(f), Altura(f), cortes: cortes);
            foreach (var ab in w.aberturas)
            {
                if (!ab.EhPorta) continue;
                float meio = ab.ini + ab.larg / 2;
                Ligacao achada = null;
                foreach (var l in portas) if (l != null && Mathf.Abs(S(lado, l.pos) - meio) < 0.05f) achada = l;
                _portasDeFora.Add(new PortaFeita { w = w, ab = ab, l = achada, andar = f });
            }
        }

        // ------------------------------------------------------------ as paredes de dentro (dos cômodos vizinhos)

        void Internas(int f)
        {
            var cs = p.comodos.FindAll(k => k.andar == f);
            for (int i = 0; i < cs.Count; i++)
                for (int j = i + 1; j < cs.Count; j++)
                {
                    Comodo A = cs[i], B = cs[j];
                    Tentar(f, A.x1, B.x0, A.z0, A.z1, B.z0, B.z1, true, A, B);
                    Tentar(f, B.x1, A.x0, B.z0, B.z1, A.z0, A.z1, true, B, A);
                    Tentar(f, A.z1, B.z0, A.x0, A.x1, B.x0, B.x1, false, A, B);
                    Tentar(f, B.z1, A.z0, B.x0, B.x1, A.x0, A.x1, false, B, A);
                }
        }

        void Tentar(int f, float c1, float c2, float a0, float a1, float b0, float b1, bool vertical, Comodo menor, Comodo maior)
        {
            if (Mathf.Abs(c1 - c2) > 0.01f) return;
            float lo = Mathf.Max(a0, b0), hi = Mathf.Min(a1, b1);
            if (hi - lo < 0.05f) return;
            float min = vertical ? z0 : x0, max = vertical ? z1 : x1;
            if (Mathf.Abs(lo - min) < 0.01f) lo += m.esp / 2;
            if (Mathf.Abs(hi - max) < 0.01f) hi -= m.esp / 2;
            _internas.Add(new Interna
            {
                andar = f, vertical = vertical, inicio = lo, menor = menor, maior = maior,
                a = vertical ? new Vector2(c1, lo) : new Vector2(lo, c1),
                b = vertical ? new Vector2(c1, hi) : new Vector2(hi, c1),
            });
        }

        void ParedeInterna(Interna i)
        {
            var aberturas = new List<Abertura>();
            var ligs = new List<Ligacao>();
            float L = (i.b - i.a).magnitude;
            foreach (var l in p.ligacoes)
            {
                if (l.b == null || !((l.a == i.menor && l.b == i.maior) || (l.a == i.maior && l.b == i.menor))) continue;
                float larg = l.passagem ? m.passagem : m.porta;
                float s = Mathf.Clamp(l.pos - i.inicio, larg / 2 + 0.15f, L - larg / 2 - 0.15f);
                aberturas.Add(new Abertura(s - larg / 2, larg, 0f, m.alturaPorta));
                ligs.Add(l);
            }
            var w = LevantarParede(i.a, i.b, aberturas, false, "interna", Base(i.andar), Altura(i.andar));
            foreach (var ab in w.aberturas)
            {
                Ligacao achada = null;
                foreach (var l in ligs)
                {
                    float larg = l.passagem ? m.passagem : m.porta;
                    if (Mathf.Abs(larg - ab.larg) < 0.01f && Mathf.Abs(Mathf.Clamp(l.pos - i.inicio, larg / 2 + 0.15f, L - larg / 2 - 0.15f) - (ab.ini + ab.larg / 2)) < 0.05f) achada = l;
                }
                _portasDeDentro.Add(new PortaFeita { w = w, ab = ab, l = achada, i = i, andar = i.andar });
            }
        }

        // ------------------------------------------------------------ lajes e pisos

        void Lajes()
        {
            if (p.porao)
                Laje(x0 - m.esp / 2, x1 + m.esp / 2, z0 - m.esp / 2, z1 + m.esp / 2, Base(-1), 0.25f, "Concreto", true);
            // o chão do térreo (um pouco maior que a casa), as lajes entre os andares (a faixa de fora) e o forro
            Laje(x0 - 0.2f, x1 + 0.2f, z0 - 0.2f, z1 + 0.2f, piso, m.laje, corDoPiso, true, _buracos[0]);
            for (int f = 1; f <= Topo; f++)
                Laje(x0 - 0.14f, x1 + 0.14f, z0 - 0.14f, z1 + 0.14f, Base(f), m.laje, corDaLaje, true, _buracos[f]);
            Laje(x0 - 0.1f, x1 + 0.1f, z0 - 0.1f, z1 + 0.1f, TopoDoForro, m.forro, corDoForro, true);
            // o piso de cada cômodo
            const float e = 0.1f;
            foreach (var k in p.comodos)
            {
                if (k.tipo == "porao") continue;
                Laje(k.x0 + e, k.x1 - e, k.z0 + e, k.z1 - e, Base(k.andar) + 0.005f, 0.005f, k.piso ?? "Taco", false, _buracos[k.andar]);
            }
        }

        // ------------------------------------------------------------ escadas

        void Escadas()
        {
            foreach (var L in p.lances)
            {
                float b = Base(L.de), t = Base(L.de + 1);
                float r = (t - b) / Espelhos(t - b);
                bool porao = L.de < 0;
                float comp = Lance(new Vector2((L.x0 + L.x1) / 2, L.zIni), new Vector2(0, 1), L.x1 - L.x0, b, t, m.pisada,
                                   porao ? "Concreto" : "Taco", porao ? "Concreto" : "Batente");
                float zFim = L.zIni + comp;
                // o corrimão do lado aberto
                GuardaCorpo(new Vector3(L.x0 + 0.04f, b + r, L.zIni + m.pisada * 0.5f), new Vector3(L.x0 + 0.04f, t - r, zFim - m.pisada * 0.5f), 0.9f, "Batente");
                // em cima, o guarda-corpo em volta do vão (no lado aberto e na ponta de onde ela sai)
                if (!porao)
                {
                    var v = _buracos[L.de + 1].Find(h => Mathf.Abs(h.xMin - L.x0) < 0.01f);
                    GuardaCorpo(new Vector3(L.x0 + 0.04f, t, v.yMin + 0.04f), new Vector3(L.x0 + 0.04f, t, v.yMax), 1.0f, "Batente");
                    GuardaCorpo(new Vector3(L.x0, t, v.yMin + 0.04f), new Vector3(L.x1 - 0.05f, t, v.yMin + 0.04f), 1.0f, "Batente");
                }
                // debaixo do lance, a parede que segue a escada (o vão de baixo fica fechado)
                if (L.fechadoEmbaixo)
                {
                    const float grossura = 0.15f;
                    float zA = L.zIni + grossura * m.pisada / r;
                    float Baixo(float z) => b - grossura + (z - L.zIni) * r / m.pisada;
                    var A = new[] { new Vector3(L.x0 - 0.1f, b, zA), new Vector3(L.x0 - 0.1f, b, zFim), new Vector3(L.x0 - 0.1f, Baixo(zFim), zFim) };
                    var B = new[] { new Vector3(L.x0, b, zA), new Vector3(L.x0, b, zFim), new Vector3(L.x0, Baixo(zFim), zFim) };
                    Solido(A, B, corDeParede, colide: true);
                }
            }
        }

        // ------------------------------------------------------------ por fora

        void Revestimento()
        {
            foreach (var w in paredes)
            {
                if (!w.ext) continue;
                FaixaDoLambri(w,0f, 0.24f, m.esp / 2 + 0.008f, 0.016f);
                for (int k = 0; ; k++)
                {
                    float h = 0.55f + 0.31f * k;
                    if (h > w.altura - 0.04f) break;
                    FaixaDoLambri(w,h, h + 0.03f, m.esp / 2 + 0.006f, 0.012f);
                }
            }
            float topo = Teto(Topo);
            foreach (float cx in new[] { x0, x1 })
                foreach (float cz in new[] { z0, z1 })
                    Caixa(new Vector3(cx + Mathf.Sign(cx) * 0.035f, (piso + topo) / 2, cz + Mathf.Sign(cz) * 0.035f), new Vector3(0.17f, topo - piso, 0.17f), "Batente");
        }

        /// <summary>
        /// Uma faixa do lambri (ou o rodapé) de y0 a y1, nos trechos em que a parede cobre a altura toda dela: os
        /// pedaços juntos e a faixa 5 mm mais curta nas pontas. Com uma placa por pedaço, a ponta ficava no mesmo plano
        /// da face da moldura e as duas brigavam (pontinhos pretos piscando no batente); agora ela acaba dentro da
        /// moldura (ou da quina, na ponta da parede).
        /// </summary>
        void FaixaDoLambri(Parede w, float y0, float y1, float fora, float esp)
        {
            var trechos = new List<Vector2>();
            foreach (var pc in w.pecas) if (pc.z <= y0 + 0.001f && pc.w >= y1 - 0.001f) trechos.Add(new Vector2(pc.x, pc.y));
            trechos.Sort((a, b) => a.x.CompareTo(b.x));
            // a parede da casa dentro da garagem fica sem lambri (até o telhado dela)
            var g = p.garagem;
            float ca = 1f, cb = -1f;
            if (g != null && g.coberta && w.nome == (g.lado > 0 ? "direita" : "esquerda") && Mathf.Abs(w.baseY - Base(0)) < 0.01f &&
                w.baseY + y0 < AlturaDaGaragem + SubidaDaGaragem + 0.25f)
            {
                int lado = g.lado > 0 ? 1 : 3;
                ca = Mathf.Min(S(lado, g.z0), S(lado, g.z1)) - m.esp / 2;
                cb = Mathf.Max(S(lado, g.z0), S(lado, g.z1)) + m.esp / 2;
            }
            void Trecho(float a, float b)
            {
                if (b - a > 0.03f) Placa(w, a + 0.005f, b - 0.005f, y0, y1, fora, esp, p.escura);
            }
            for (int i = 0; i < trechos.Count;)
            {
                float a = trechos[i].x, b = trechos[i].y;
                int j = i + 1;
                while (j < trechos.Count && trechos[j].x <= b + 0.001f) { b = Mathf.Max(b, trechos[j].y); j++; }
                if (cb > ca && b > ca && a < cb) { Trecho(a, ca); Trecho(cb, b); }
                else Trecho(a, b);
                i = j;
            }
        }

        void Aberturas()
        {
            foreach (var w in paredes)
            {
                Molduras(w);
                if (!w.ext && w.nome != "porao") continue;
                foreach (var ab in w.aberturas) if (!ab.EhPorta) TratarJanela(w, ab, p.janelas, p.tabuasPorDentro);
            }
            foreach (var pf in _portasDeFora)
            {
                bool frente = pf.l != null && pf.l.lado == 0 && pf.l.a.andar == 0;
                if (frente)
                {
                    Folha(pf, -1, p.corDaPorta, p.portaDaFrenteAberta ? 95f : 0f);
                    // luminária ao lado da porta (apagada)
                    float sl = pf.ab.ini + pf.ab.larg + 0.35f;
                    Placa(pf.w, sl - 0.06f, sl + 0.06f, pf.ab.alto - 0.25f, pf.ab.alto - 0.03f, m.esp / 2 + 0.06f, 0.08f, "Metal");
                    Placa(pf.w, sl - 0.04f, sl + 0.04f, pf.ab.alto - 0.22f, pf.ab.alto - 0.08f, m.esp / 2 + 0.105f, 0.012f, "Farol");
                }
                // a do porão de fora, um pouco além do ângulo reto: a 85°, a ponta da folha entrava no vão
                else if (pf.w.nome == "porao") Folha(pf, -1, "Madeira", 95f);
                // a da garagem fica aberta: é por ela que se chega no carro com o portão fechado
                else if (pf.l != null && pf.l.paraGaragem) Folha(pf, -1, Um("Madeira", "Batente"), Faixa(80f, 95f));
                else Folha(pf, -1, Um("Madeira", "Batente", p.corDaPorta), Chance(0.5f) ? 0f : Faixa(15f, 40f));
            }
            foreach (var pf in _portasDeDentro)
            {
                if (pf.l == null || pf.l.passagem) continue;
                // abre para dentro do quarto ou do banheiro; nunca para o corredor (a folha aberta tapava a passagem); entre
                // dois cômodos comuns (a sala e a cozinha), para o maior (aberta para dentro da cozinha pequena, a folha
                // tirava o lugar da geladeira e do fogão: a mobília desvia dela)
                Comodo a = pf.l.a, b = pf.l.b;
                Comodo alvo = Privado(b) ? b : Privado(a) ? a : b.tipo == "corredor" ? a : a.tipo == "corredor" ? b
                            : (a.x1 - a.x0) * (a.z1 - a.z0) >= (b.x1 - b.x0) * (b.z1 - b.z0) ? a : b;
                // aberta quase em ângulo reto: com 60°, a folha ficava atravessada na frente da passagem
                Folha(pf, alvo == pf.i.LadoN ? 1 : -1, Um("Batente", "Madeira", "Batente"), Faixa(85f, 100f));
            }
        }

        static bool Privado(Comodo k) => k != null && (k.tipo == "quarto" || k.tipo == "banheiro");

        /// <summary>
        /// A folha da porta, presa do lado em que ela não fica na frente de uma escada: a da frente do sobrado, presa no
        /// fim da abertura, abria em cima do pé da escada e tapava a subida.
        /// </summary>
        void Folha(PortaFeita pf, int dentro, string cor, float abertura)
        {
            bool noInicio = false;
            if (abertura > 1f)
            {
                var (a, b) = PontasDaFolha(pf.w, pf.ab.ini, pf.ab.larg, dentro, abertura, false);
                if (NaFrenteDaEscada(pf.andar, a, b))
                {
                    var (a2, b2) = PontasDaFolha(pf.w, pf.ab.ini, pf.ab.larg, dentro, abertura, true);
                    noInicio = !NaFrenteDaEscada(pf.andar, a2, b2);
                }
            }
            FolhaDePorta(pf.w, pf.ab.ini, pf.ab.larg, pf.ab.alto, dentro, cor, abertura, dobradicaNoInicio: noInicio);
        }

        /// <summary>Se a folha aberta (da dobradiça até a ponta) fica no pé de uma escada ou na chegada dela, naquele andar.</summary>
        bool NaFrenteDaEscada(int andar, Vector2 a, Vector2 b)
        {
            float xa = Mathf.Min(a.x, b.x), xb = Mathf.Max(a.x, b.x), za = Mathf.Min(a.y, b.y), zb = Mathf.Max(a.y, b.y);
            foreach (var L in p.lances)
            {
                float comp = (Espelhos(Base(L.de + 1) - Base(L.de)) - 1) * m.pisada;
                float z0e, z1e;
                if (L.de == andar) { z0e = L.zIni - 1.2f; z1e = L.zIni + comp; }
                else if (L.de + 1 == andar) { z0e = L.zIni + comp - 0.3f; z1e = L.zIni + comp + 1.2f; }
                else continue;
                if (xb > L.x0 - 0.35f && xa < L.x1 + 0.35f && zb > z0e && za < z1e) return true;
            }
            return false;
        }

        void Telhado()
        {
            bool noX = p.cumeeiraNoX;
            float S = noX ? p.D : p.W, L = noX ? p.W : p.D;
            // o quadro do telhado: o vão em x, a cumeeira ao longo de z; com a cumeeira em x, ele gira 90°
            Matrix4x4 Q = noX ? RY(90f) : Matrix4x4.identity;
            Vector3 P(float x, float y, float z) => Q.MultiplyPoint3x4(new Vector3(x, y, z));
            float forro = TopoDoForro, esp = m.esp;
            float hw = S / 2 + 0.1f, ho = S / 2 + 0.45f, C = 0.367f * ho, topo = forro + C;
            _cumeeira = C;
            float YTel(float x) => forro + C * (1 - Mathf.Abs(x) / ho);

            // oitões, na cor da casa por fora, com o lambri e o respiro do sótão
            foreach (var (zc, lado) in new[] { (-L / 2, -1), (L / 2, 1) })
            {
                var pts = new[] { new Vector2(-hw, forro), new Vector2(hw, forro), new Vector2(hw, YTel(hw)), new Vector2(0, topo), new Vector2(-hw, YTel(hw)) };
                var A = new List<Vector3>(); var B = new List<Vector3>();
                foreach (var q in pts) { A.Add(P(q.x, q.y, zc - esp / 2)); B.Add(P(q.x, q.y, zc + esp / 2)); }
                Vector3 fora = Q.MultiplyVector(new Vector3(0, 0, lado));
                Solido(A, B, Cor.Por((n, c) => Vector3.Dot(n, fora) > 0.9f ? p.cor : "Madeira"), colide: true);
                float f = lado * (esp / 2 + 0.006f);
                for (int k = 0; ; k++)
                {
                    float y = forro + 0.3f + 0.31f * k;
                    float meia = ho * (1 - (y + 0.03f - forro) / C) - 0.1f;
                    if (meia < 0.25f) break;
                    meia = Mathf.Min(meia, hw);
                    CaixaM(Q * T(new Vector3(0, y + 0.015f, zc + f)), new Vector3(2 * meia, 0.03f, 0.012f), p.escura);
                }
                float yv = forro + C * 0.42f;
                CaixaM(Q * T(new Vector3(0, yv, zc + lado * (esp / 2 + 0.02f))), new Vector3(0.62f, 0.42f, 0.04f), "Batente");
                CaixaM(Q * T(new Vector3(0, yv, zc + lado * (esp / 2 + 0.035f))), new Vector3(0.48f, 0.3f, 0.03f), "Grade");
            }
            // águas: a laje (o forro do beiral claro embaixo) e as fileiras de telha por cima, um pouco levantadas
            float alfa = Mathf.Atan2(C, ho) * Mathf.Rad2Deg, a = alfa * Mathf.Deg2Rad;
            float Ls = Mathf.Sqrt(ho * ho + C * C), t = 0.14f, prof = L + 0.9f;
            foreach (int s in new[] { 1, -1 })
            {
                var normal = new Vector2(s * Mathf.Sin(a), Mathf.Cos(a));
                var sobe = new Vector2(-s * Mathf.Cos(a), Mathf.Sin(a));
                var beira = new Vector2(s * ho, forro);
                Vector2 meio = beira + sobe * (Ls / 2 + 0.04f) + normal * (t / 2);
                Matrix4x4 giro = RZ(-s * alfa);
                CaixaM(Q * T(new Vector3(meio.x, meio.y, 0)) * giro, new Vector3(Ls + 0.08f, t, prof),
                       Cor.Por((n, c) => n.y < -0.3f ? "Batente" : "Cumeeira"), colide: true);
                int fileiras = (int)(Ls / 0.36f) + 1;
                for (int k = 0; k < fileiras; k++)
                {
                    float d = 0.2f + k * 0.36f;
                    if (d > Ls + 0.02f) break;
                    Vector2 q = beira + sobe * d + normal * (t + 0.022f);
                    CaixaM(Q * T(new Vector3(q.x, q.y, 0)) * RZ(-s * (alfa - 3f)), new Vector3(0.44f, 0.045f, prof + 0.02f), Chance(0.3f) ? p.telhadoEscuro : p.telhado);
                }
                CaixaM(Q * T(new Vector3(s * (ho + 0.02f), forro + 0.03f, 0)), new Vector3(0.04f, 0.22f, prof), "Batente");
                CaixaM(Q * T(new Vector3(s * (ho + 0.1f), forro - 0.03f, 0)), new Vector3(0.12f, 0.1f, prof), "Metal");
                foreach (float zc in new[] { -prof / 2 - 0.02f, prof / 2 + 0.02f })
                    CaixaM(Q * T(new Vector3(meio.x, meio.y + 0.02f, zc)) * giro, new Vector3(Ls + 0.08f, 0.24f, 0.04f), "Batente");
            }
            CaixaM(Q * T(new Vector3(0, topo + 0.12f, 0)) * RZ(45f), new Vector3(0.24f, 0.24f, prof + 0.06f), "Cumeeira");
            // condutor de água numa quina da frente
            float xs = ho + 0.1f, zq = -L / 2 - 0.3f;
            Tubo(P(xs, forro - 0.06f, zq), P(S / 2 + 0.2f, forro - 0.5f, zq), 0.04f, "Metal");
            Tubo(P(S / 2 + 0.2f, forro - 0.5f, zq), P(S / 2 + 0.2f, 0f, zq), 0.04f, "Metal");
        }

        void Chamine(int lado)
        {
            float x = (lado > 0 ? x1 : x0) + lado * (m.esp / 2 + 0.38f);
            float z = z1 - p.D * 0.525f;
            float topo = TopoDoForro + _cumeeira + 0.6f;
            float hb = Teto(Topo) - 0.37f;
            Caixa(new Vector3(x, (hb - 0.02f) / 2, z), new Vector3(0.76f, hb + 0.02f, 1.2f), "Tijolo", colide: true);
            Caixa(new Vector3(x - lado * 0.05f, (hb + topo) / 2, z), new Vector3(0.6f, topo - hb, 0.78f), "Tijolo", colide: true);
            Caixa(new Vector3(x, hb + 0.02f, z), new Vector3(0.8f, 0.06f, 1.24f), "TijoloEscuro");
            Caixa(new Vector3(x - lado * 0.05f, topo + 0.04f, z), new Vector3(0.7f, 0.08f, 0.88f), "Concreto");
            Caixa(new Vector3(x - lado * 0.05f, topo + 0.12f, z), new Vector3(0.3f, 0.12f, 0.3f), "Grade");
        }

        void Varanda()
        {
            float xd = p.xPortaFrente, zf = z0 - m.esp / 2, prof = p.fundoDaVaranda;
            float xa = _varandaXa, xb = _varandaXb, xc = (xa + xb) / 2, larg = xb - xa;
            Caixa(new Vector3(xc, (piso - 0.02f) / 2, zf - prof / 2), new Vector3(larg, piso + 0.02f, prof),
                  Cor.Por((n, c) => n.y > 0.9f ? "MadeiraVelha" : "Madeira"), colide: true);
            for (int k = 1; k < (int)(prof / 0.2f + 0.5f); k++)
                Caixa(new Vector3(xc, piso + 0.002f, zf - k * 0.2f), new Vector3(larg, 0.004f, 0.015f), "Madeira");
            _entrada = Degraus(new Vector2(xd, zf - prof), new Vector2(0, -1), 1.6f, piso);
            float zp = zf - prof + 0.1f;
            float yPilar = piso + m.altura - 0.33f, yViga = piso + m.altura - 0.26f, yTeto = piso + m.altura - 0.09f;
            int vaos = Mathf.Max(1, Mathf.CeilToInt((larg - 0.2f) / 3.6f));
            for (int i = 0; i <= vaos; i++)
            {
                float x = xa + 0.1f + (larg - 0.2f) * i / vaos;
                Caixa(new Vector3(x, (piso + yPilar) / 2, zp), new Vector3(0.12f, yPilar - piso, 0.12f), "Batente", colide: true);
            }
            // guarda-corpo dos lados (da parede até o pilar) e da frente (menos o vão da escada)
            GuardaCorpo(new Vector3(xa + 0.02f, piso, zf), new Vector3(xa + 0.02f, piso, zp), 0.8f, "Batente");
            GuardaCorpo(new Vector3(xb - 0.02f, piso, zf), new Vector3(xb - 0.02f, piso, zp), 0.8f, "Batente");
            if (xd - 0.85f - (xa + 0.1f) > 0.3f) GuardaCorpo(new Vector3(xa + 0.1f, piso, zp), new Vector3(xd - 0.85f, piso, zp), 0.8f, "Batente");
            if (xb - 0.1f - (xd + 0.85f) > 0.3f) GuardaCorpo(new Vector3(xd + 0.85f, piso, zp), new Vector3(xb - 0.1f, piso, zp), 0.8f, "Batente");
            Caixa(new Vector3(xc, yViga, zp), new Vector3(larg + 0.02f, 0.14f, 0.14f), "Batente");
            float incl = -Mathf.Atan2(0.16f, prof + 0.1f) * Mathf.Rad2Deg;
            Caixa(new Vector3(xc, yTeto, zf - (prof + 0.1f) / 2), new Vector3(larg + 0.35f, 0.08f, prof + 0.3f), p.telhado, rot: RX(incl));
        }

        /// <summary>Sem varanda: o degrau de concreto (ou o patamar com degraus, na casa alta).</summary>
        void Entradinha()
        {
            float xd = p.xPortaFrente, zf = z0 - m.esp / 2;
            if (piso < 0.3f)
            {
                Caixa(new Vector3(xd, 0.03f, z0 - 0.6f), new Vector3(1.6f, 0.1f, 0.8f), "Concreto", colide: true);
                _entrada = new Vector2(xd, z0 - 1.0f);
                return;
            }
            Caixa(new Vector3(xd, piso / 2 - 0.01f, zf - 0.6f), new Vector3(1.9f, piso + 0.02f, 1.2f), "Concreto", colide: true);
            _entrada = Degraus(new Vector2(xd, zf - 1.2f), new Vector2(0, -1), 1.6f, piso);
        }

        /// <summary>As outras portas de fora (dos fundos, do lado): o patamar e os degraus.</summary>
        void Soleiras()
        {
            foreach (var pf in _portasDeFora)
            {
                var w = pf.w;
                if (w.nome == "porao" || (pf.l != null && pf.l.lado == 0 && pf.l.a.andar == 0)) continue;
                float s = pf.ab.ini + pf.ab.larg / 2, larg = pf.ab.larg + 0.5f;
                Vector2 c = w.a + w.u * s + w.n * (m.esp / 2 + 0.5f);
                if (piso < 0.3f) { Caixa(new Vector3(c.x, 0.03f, c.y), new Vector3(larg, 0.1f, 1.0f), "Concreto", w.yaw, colide: true); continue; }
                Caixa(new Vector3(c.x, piso / 2 - 0.01f, c.y), new Vector3(larg, piso + 0.02f, 1.0f), "Concreto", w.yaw, colide: true);
                Degraus(w.a + w.u * s + w.n * (m.esp / 2 + 1.0f), w.n, larg - 0.3f, piso);
            }
        }

        /// <summary>
        /// As paredes da garagem: a da frente com o portão (no meio da vaga do carro), a de fora com uma janela às vezes e
        /// a do fundo. Saem antes do lambri e das molduras (Revestimento e Aberturas tratam delas como as da casa).
        /// </summary>
        void ParedesDaGaragem()
        {
            var g = p.garagem;
            if (g == null || !g.coberta) return;
            var linhas = LinhasDaGaragem();
            float s = g.lado, xo = s > 0 ? _gxb : _gxa;
            const float portao = 2.7f, altoPortao = 2.3f;
            // o carro fica no meio; com a porta para dentro (e os degraus dela do lado da casa), encostado na parede de fora
            float xc = g.portaParaDentro ? xo - s * (m.esp / 2 + 0.25f + 1.05f) : (_gxa + _gxb) / 2;
            _sPortao = Mathf.Clamp(xc - _gxa, portao / 2 + 0.3f, _gxb - _gxa - portao / 2 - 0.3f);
            _carros.Add((new Vector2(xc, (g.z0 + g.z1) / 2), 0f));
            _entradaDoCarro = new Vector2(_gxa + _sPortao, g.z0 - m.esp / 2);
            _frenteDaGaragem = LevantarParede(linhas[0].a, linhas[0].b, new List<Abertura> { new Abertura(_sPortao - portao / 2, portao, 0f, altoPortao) },
                                              true, "garagem", 0f, AlturaDaGaragem);
            float comp = g.z1 - g.z0;
            var janelas = new List<Abertura>();
            if (Chance(0.6f)) janelas.Add(new Abertura(comp / 2 - 0.5f, 1.0f, 1.3f, 2.1f));
            LevantarParede(linhas[1].a, linhas[1].b, janelas, true, "garagem", 0f, AlturaDaGaragem);
            LevantarParede(linhas[2].a, linhas[2].b, new List<Abertura>(), true, "garagem", 0f, AlturaDaGaragem);
        }

        /// <summary>
        /// O resto da garagem (ou a vaga ao lado, descoberta): o chão com a mancha de óleo, os oitões, o telhado de uma
        /// água (alto junto da casa, embaixo do beiral dela, e caindo para fora, com calha e condutor), as quinas, a
        /// lâmpada e o portão de placas: fechado, ou recolhido no teto, logo atrás da parede da frente.
        /// </summary>
        void CompletarGaragem()
        {
            var g = p.garagem;
            if (g == null) return;
            float s = g.lado, xi = s > 0 ? x1 : x0, xo = s > 0 ? _gxb : _gxa;
            float zm = (g.z0 + g.z1) / 2, comp = g.z1 - g.z0;
            if (!g.coberta)
            {
                // a vaga: a laje de concreto, a meio metro da parede da casa, com a mancha de óleo
                float va = Mathf.Min(xi + s * 0.5f, xo), vb = Mathf.Max(xi + s * 0.5f, xo), vm = (va + vb) / 2;
                Caixa(new Vector3(vm, -0.12f, zm), new Vector3(vb - va, 0.3f, comp), "Concreto", colide: true);
                Caixa(new Vector3(vm + Faixa(-0.3f, 0.3f), 0.032f, zm + 0.5f), new Vector3(0.9f, 0.004f, 1.4f), "Oleo", Faixa(-20f, 20f));
                _carros.Add((new Vector2(vm, zm), 0f));
                _entradaDoCarro = new Vector2(vm, g.z0);
                return;
            }
            float h = AlturaDaGaragem, alto = h + SubidaDaGaragem, t = 0.14f;
            Laje(_gxa, _gxb, g.z0, g.z1, 0.05f, 0.3f, "ConcretoClaro", true);
            var carro = _carros[_carros.Count - 1].pos;
            Caixa(new Vector3(carro.x + Faixa(-0.2f, 0.2f), 0.054f, zm + 0.5f), new Vector3(0.8f, 0.004f, 1.2f), "Oleo", Faixa(-25f, 25f));
            // os oitões da frente e do fundo, do alto das paredes até o telhado
            foreach (float zw in new[] { g.z0, g.z1 })
            {
                var A = new[] { new Vector3(xi, h, zw - m.esp / 2), new Vector3(xo, h, zw - m.esp / 2), new Vector3(xi, alto, zw - m.esp / 2) };
                var B = new[] { new Vector3(xi, h, zw + m.esp / 2), new Vector3(xo, h, zw + m.esp / 2), new Vector3(xi, alto, zw + m.esp / 2) };
                Solido(A, B, corDeParede, colide: true);
            }
            // o telhado: a laje (o forro do beiral claro embaixo) e as fileiras de telha, com 0,35 m de beiral para fora
            const float beiral = 0.35f;
            var topoAlto = new Vector3(xi, alto, zm);
            var topoBaixo = new Vector3(xo + s * beiral, h - beiral * SubidaDaGaragem / g.largura, zm);
            Vector3 desce = (topoBaixo - topoAlto).normalized;
            float L = (topoBaixo - topoAlto).magnitude;
            CaixaEntre(topoAlto + Vector3.down * (t / 2), topoBaixo + Vector3.down * (t / 2), comp + 0.6f, t,
                       Cor.Por((n, c) => n.y < -0.3f ? "Batente" : "Cumeeira"), colide: true);
            for (float d = 0.2f; d < L - 0.05f; d += 0.36f)
            {
                Vector3 q = topoBaixo - desce * d + Vector3.up * 0.025f;
                CaixaEntre(q - desce * 0.22f, q + desce * 0.22f, comp + 0.62f, 0.045f, Chance(0.3f) ? p.telhadoEscuro : p.telhado);
            }
            Caixa(new Vector3(topoBaixo.x + s * 0.02f, topoBaixo.y - 0.06f, zm), new Vector3(0.04f, 0.22f, comp + 0.6f), "Batente");
            Caixa(new Vector3(topoBaixo.x + s * 0.1f, topoBaixo.y - 0.12f, zm), new Vector3(0.12f, 0.1f, comp + 0.6f), "Metal");
            Tubo(new Vector3(topoBaixo.x + s * 0.1f, topoBaixo.y - 0.15f, g.z0 - 0.25f), new Vector3(xo + s * 0.2f, topoBaixo.y - 0.5f, g.z0 - 0.25f), 0.04f, "Metal");
            Tubo(new Vector3(xo + s * 0.2f, topoBaixo.y - 0.5f, g.z0 - 0.25f), new Vector3(xo + s * 0.2f, 0f, g.z0 - 0.25f), 0.04f, "Metal");
            // as quinas de fora
            foreach (float zq in new[] { g.z0 - 0.035f, g.z1 + 0.035f })
                Caixa(new Vector3(xo + s * 0.035f, h / 2, zq), new Vector3(0.17f, h, 0.17f), "Batente");
            Lampada((_gxa + _gxb) / 2, (h + alto) / 2 - t, zm);
            // o portão de placas
            const float portao = 2.7f, altoPortao = 2.3f;
            string corPortao = Um("Batente", "Metal", "PlacaBranca");
            var w = _frenteDaGaragem;
            if (g.fechada)
            {
                Placa(w, _sPortao - portao / 2 + 0.02f, _sPortao + portao / 2 - 0.02f, 0.02f, altoPortao - 0.02f, 0f, 0.06f, corPortao, colide: true);
                for (float y = 0.48f; y < altoPortao - 0.1f; y += 0.48f)
                    Placa(w, _sPortao - portao / 2 + 0.08f, _sPortao + portao / 2 - 0.08f, y, y + 0.025f, 0.036f, 0.012f, "Grade");
            }
            else
            {
                float xp = _gxa + _sPortao, zp = g.z0 + m.esp / 2 + 1.3f;
                Caixa(new Vector3(xp, altoPortao + 0.14f, zp), new Vector3(portao - 0.04f, 0.05f, 2.5f), corPortao, colide: true);
                foreach (int ld in new[] { -1, 1 })
                    Caixa(new Vector3(xp + ld * (portao / 2 - 0.02f), altoPortao + 0.1f, zp + 0.1f), new Vector3(0.04f, 0.06f, 2.7f), "Metal");
            }
        }

        void ArCondicionado(int lado)
        {
            float x = (lado > 0 ? x1 : x0) + lado * (m.esp / 2 + 0.27f);
            float z = z1 - 1.1f;
            Caixa(new Vector3(x, 0.34f, z), new Vector3(0.5f, 0.68f, 0.8f), "Metal", colide: true);
            Caixa(new Vector3(x + lado * 0.255f, 0.38f, z), new Vector3(0.01f, 0.5f, 0.62f), "Grade");
            Caixa(new Vector3(x, 0f, z), new Vector3(0.6f, 0.06f, 0.9f), "Concreto");
            Tubo(new Vector3(x - lado * 0.25f, 0.55f, z - 0.3f), new Vector3((lado > 0 ? x1 : x0) + lado * m.esp / 2, piso + 0.6f, z - 0.3f), 0.025f, "Metal");
        }

        /// <summary>
        /// A entrada do porão por fora, à americana: o fosso de concreto ao lado da casa, com a escada subindo para
        /// fora, e a tampa inclinada de duas folhas (abertas) por cima. A porta do porão fica no fundo do fosso.
        /// </summary>
        void PoraoDeFora()
        {
            int lado = p.poraoDeFora;
            float zc = p.zPoraoDeFora, larg = LarguraDoFosso, patamar = 1.0f;
            float xw = (lado > 0 ? x1 : x0) + lado * m.esp / 2;
            float yb = Base(-1);
            float xIni = xw + lado * patamar;
            Lance(new Vector2(xIni, zc), new Vector2(lado, 0), larg, yb, 0f, 0.25f, "Concreto", "Concreto");
            float xFim = _xFimDoFosso;
            Caixa(new Vector3((xw + xIni) / 2, yb - 0.1f, zc), new Vector3(patamar, 0.2f, larg), "Concreto", colide: true);
            float xm = (xw + xFim) / 2, comp = Mathf.Abs(xFim - xw);
            float yAlto = Mathf.Min(piso + 0.35f, 1.1f), yBaixo = 0.3f;
            foreach (int sz in new[] { -1, 1 })
            {
                float zp = zc + sz * (larg / 2 + 0.1f);
                Caixa(new Vector3(xm, (yb - 0.2f + 0.25f) / 2, zp), new Vector3(comp, 0.25f - yb + 0.2f, 0.2f), "Concreto", colide: true);
                // as bochechas: de cima da parede do fosso até a tampa
                var A = new[] { new Vector3(xw, 0.25f, zp - 0.1f), new Vector3(xFim, 0.25f, zp - 0.1f), new Vector3(xFim, yBaixo, zp - 0.1f), new Vector3(xw, yAlto, zp - 0.1f) };
                var B = new[] { new Vector3(xw, 0.25f, zp + 0.1f), new Vector3(xFim, 0.25f, zp + 0.1f), new Vector3(xFim, yBaixo, zp + 0.1f), new Vector3(xw, yAlto, zp + 0.1f) };
                Solido(A, B, "Concreto", colide: true);
            }
            // as folhas da tampa: cada uma cobre meia largura, presa na borda de fora; as duas ficam abertas (em pé, uma
            // um pouco mais deitada para fora que a outra)
            string cor = Um("PlacaVerde", "Metal", "Ferrugem", "PersianaVinho");
            foreach (int sz in new[] { -1, 1 })
            {
                Vector3 a = new Vector3(xw, yAlto + 0.03f, zc + sz * larg / 4), b = new Vector3(xFim, yBaixo + 0.03f, zc + sz * larg / 4);
                var M = Matrix4x4.TRS((a + b) / 2, Quaternion.LookRotation(b - a, Vector3.up), Vector3.one);
                var tam = new Vector3(larg / 2 + 0.06f, 0.05f, (b - a).magnitude + 0.06f);
                Vector3 h0 = new Vector3(xw, yAlto, zc + sz * (larg / 2 + 0.05f)), h1 = new Vector3(xFim, yBaixo, zc + sz * (larg / 2 + 0.05f));
                Vector3 eixo = (h1 - h0).normalized;
                // gira para o lado em que o meio da folha sobe
                Matrix4x4 Giro(float g) => T(h0) * Matrix4x4.Rotate(Quaternion.AngleAxis(g, eixo)) * T(-h0);
                float sinal = (Giro(60f) * M).MultiplyPoint3x4(Vector3.zero).y > (Giro(-60f) * M).MultiplyPoint3x4(Vector3.zero).y ? 1f : -1f;
                M = Giro(sinal * Faixa(94f, 108f)) * M;
                CaixaM(M, tam, cor, colide: true);
            }
        }

        /// <summary>Arbustos na frente (fora da varanda e da porta).</summary>
        void PorFora()
        {
            float xd = p.xPortaFrente;
            foreach (float xc in new[] { x0 + p.W * 0.72f, x0 + p.W * 0.08f + 0.6f, x1 - 0.6f, x0 + p.W * 0.45f })
            {
                if (Mathf.Abs(xc - xd) < 2.1f) continue;
                if (p.varanda && xc > _varandaXa - 0.5f && xc < _varandaXb + 0.5f) continue;
                for (int k = 0; k < 2; k++)
                {
                    float r = Faixa(0.32f, 0.5f);
                    var c = new Vector3(xc + Faixa(-0.35f, 0.35f), r * 0.6f, z0 - 0.5f - Faixa(0f, 0.2f));
                    vis.Esfera(T(c) * Matrix4x4.Scale(new Vector3(1f, 0.75f, 1f)), r, Um("Arbusto", "Arbusto", "CopaSeca"), 0.06f, rnd);
                }
            }
        }

        void PorDentro()
        {
            // luminárias no meio de cada cômodo (fora do vão da escada); lâmpadas no porão
            foreach (var k in p.comodos)
            {
                float teto = Teto(k.andar), cx = k.Cx, cz = k.Cz;
                if (k.tipo == "porao")
                {
                    Lampada(x0 + p.W * 0.3f, teto, cz);
                    Lampada(x0 + p.W * 0.7f, teto, cz);
                    continue;
                }
                foreach (var v in _buracos[k.andar + 1])
                    if (v.Contains(new Vector2(cx, cz)) || (cx > v.xMin - 0.3f && cx < v.xMax + 0.3f && cz > v.yMin && cz < v.yMax))
                        cx = (k.x0 + v.xMin) / 2;
                Luminaria(cx, teto, cz);
            }
            // manchas: sangue (altura de gente) e mofo (perto do teto), nas faces de dentro das paredes de fora
            var internas = paredes.FindAll(w => w.ext);
            for (int i = 0; !nova && i < p.sangue + 3 * p.andares; i++)
            {
                var w = internas[rnd.Next(internas.Count)];
                var cheias = new List<Vector4>();
                foreach (var pc in w.pecas) if (pc.z < 0.01f && pc.w > w.altura - 0.01f && pc.y - pc.x > 0.9f) cheias.Add(pc);
                if (cheias.Count == 0) continue;
                var c = cheias[rnd.Next(cheias.Count)];
                float s = Faixa(c.x + 0.4f, c.y - 0.4f);
                float y, larg, alt; string cor;
                if (i < p.sangue) { y = Faixa(0.6f, 1.6f); larg = Faixa(0.25f, 0.7f); alt = Faixa(0.2f, 0.9f); cor = "Sangue"; }
                else { y = Faixa(w.altura - 0.6f, w.altura - 0.3f); larg = Faixa(0.5f, 1.0f); alt = Faixa(0.3f, 0.5f); cor = "Mofo"; }
                Placa(w, s - larg / 2, s + larg / 2, y, Mathf.Min(y + alt, w.altura - 0.02f), -(m.esp / 2 + 0.003f), 0.004f, cor, cor == "Sangue" ? Faixa(-20f, 20f) : 0f);
            }
        }
    }
}
