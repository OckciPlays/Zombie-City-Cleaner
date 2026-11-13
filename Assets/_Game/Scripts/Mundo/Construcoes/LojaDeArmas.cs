using System.Collections.Generic;
using UnityEngine;
using Andar = ZombieCityCleaner.Mundo.PlantaDoHospital.Andar;
using Porta = ZombieCityCleaner.Mundo.PlantaDoHospital.Porta;
using Divisa = ZombieCityCleaner.Mundo.PlantaDoHospital.Divisa;
using Borda = ZombieCityCleaner.Mundo.PlantaDoHospital.Borda;
using TipoDePorta = ZombieCityCleaner.Mundo.PlantaDoHospital.TipoDePorta;
using Zona = ZombieCityCleaner.Mundo.PlantaDoHospital.Zona;
using static ZombieCityCleaner.Mundo.PlantaDaLojaDeArmas;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A loja de armas levantada a partir da <see cref="PlantaDaLojaDeArmas"/> (o Plano_LojaDeArmas.md da pasta de arte), como o
    /// Hospital Principal, em duas partes:
    ///  - <b>a casca</b> (<see cref="Parte.Casca"/>): as paredes de fora (com as vitrines, as grades, as janelas e as portas de
    ///    fora), a laje do chão, a cobertura com a platibanda, a fachada falsa com o frontão, os letreiros ("GUNS &amp; AMMO" e a
    ///    faixa "FIREARMS ★ AMMUNITION ★ SHOOTING RANGE"), as luminárias pescoço de ganso (apagadas), a bandeira e o cervo
    ///    pintados, as faixas vermelhas dos lados, os degraus das portas dos fundos e da saída, os condutores e o telhado
    ///    (LojaDeArmas.Fachada.cs) e as pichações de fora;
    ///  - <b>o lado de dentro</b> (<see cref="Parte.Dentro"/>): as paredes de dentro com as portas (a porta de cofre é um modelo
    ///    da mobília: aqui, só o vão), os pisos, os acabamentos de cada cômodo (o lambri, o azulejo, os painéis acústicos e a
    ///    chapa do estande, o forro rebaixado com as luminárias apagadas e as placas que caíram), o estande (os trilhos dos
    ///    alvos, os defletores do teto, a faixa da linha de tiro, os números das baias, as marcas das jardas e o anteparo do
    ///    fundo), as placas em inglês (LojaDeArmas.Dentro.cs), as marcas do abandono e as pichações de dentro
    ///    (LojaDeArmas.Abandono.cs) e as caixas de colisão dos móveis (<see cref="MoveisDaLojaDeArmas"/>).
    /// A loja não tem energia: nenhuma luz acende. Coordenadas: o meio no centro, a frente em -z, o chão em y = 0.
    /// </summary>
    public sealed partial class LojaDeArmas : Obra
    {
        public enum Parte { Casca, Dentro }

        public readonly Andar an;
        public readonly Variacao v;
        readonly Parte _parte;
        readonly List<Divisa> _divisas = new List<Divisa>();
        readonly List<Borda> _bordas = new List<Borda>();

        /// <summary>As marcas do abandono (o atlas das marcas, <see cref="AtlasDasMarcas"/>) e as pichações (o atlas delas), à parte.</summary>
        public readonly MalhaDePichacao marcas = new MalhaDePichacao(), pichacoes = new MalhaDePichacao();

        static readonly Cor CorDaLaje = Cor.PorNormal("Concreto", "Concreto", "Teto");

        public LojaDeArmas(Medidas medidas, Parte parte, Variacao variacao)
            : base(variacao.semente * 31 + (int)parte * 7 + 3, medidas, TamanhoDaMalha.Media)
        {
            _parte = parte;
            v = variacao;
            an = Planta();
            PlantaDoHospital.Linhas(an, _divisas, _bordas);
        }

        /// <summary>Monta a parte desta obra.</summary>
        public void Montar()
        {
            if (_parte == Parte.Casca) MontarCasca();
            else MontarDentro();
        }

        // ------------------------------------------------------------ as cores pela posição

        protected override object ComodoEm(Vector3 q)
        {
            if (q.y < -0.05f || q.y > PeDireito + 0.05f) return null;
            return an.Em(q.x, q.z);
        }

        // de fora: o tijolo na frente (até a cornija), o concreto pintado em cima e nos lados, o bloco nos fundos e o concreto
        // embaixo do chão; por dentro da platibanda, o concreto
        protected override string CorDeFora(Vector3 q)
        {
            if (q.y < -0.02f) return "Concreto";
            bool dentroDaPegada = q.x > X0 + 0.05f && q.x < X1 - 0.05f && q.z > Z0 + 0.05f && q.z < Z1 - 0.05f;
            if (q.y > PeDireito && dentroDaPegada) return "Concreto";
            if (q.z < Z0 + 0.05f) return q.y < 4.05f ? "TijoloAparente" : "ConcretoClaro";
            if (q.z > Z1 - 0.05f) return "Concreto";
            return "ConcretoClaro";
        }

        protected override string CorDeDentro(object k, Vector3 q) => ((Comodo)k).papel ?? "ParedeInterna";

        static Vector2 Ponta(Divisa d, float s) => d.vertical ? new Vector2(d.c, s) : new Vector2(s, d.c);
        static Vector2 Ponta(Borda b, float s) => b.vertical ? new Vector2(b.c, s) : new Vector2(s, b.c);
        static int LadoDaBorda(Borda b) => b.vertical ? (b.fora > 0 ? 1 : 3) : (b.fora < 0 ? 0 : 2);

        float Largura(Porta p) => p.largura > 0f ? p.largura : p.folhas == 2 ? 2.0f : p.folhas == 0 ? m.passagem : m.porta;
        float Alto(Porta p) => p.alto > 0f ? p.alto : EhACofre(p) ? AltoDoCofre : m.alturaPorta;

        /// <summary>
        /// O ajuste da ponta de uma parede de fora numa quina (as quatro do corpo são de fora): a parede em z vai até a quina
        /// (cresce meia parede) e a em x para antes dela, para as duas não se cobrirem com as faces no mesmo plano.
        /// </summary>
        float AjusteDaQuina(Borda b, float ponta)
        {
            float e = m.esp / 2f;
            Vector2 P = Ponta(b, ponta);
            foreach (var o in _bordas)
            {
                if (o.vertical == b.vertical) continue;
                if ((Ponta(o, o.lo) - P).sqrMagnitude > 1e-4f && (Ponta(o, o.hi) - P).sqrMagnitude > 1e-4f) continue;
                Borda V = b.vertical ? b : o, H = b.vertical ? o : b;
                float dh = Mathf.Abs(H.lo - V.c) < 0.01f ? 1f : -1f;
                if (V.fora * dh >= 0f) return 0f;
                return b.vertical ? -e : e;
            }
            return 0f;
        }

        // ------------------------------------------------------------ os vãos de cada linha

        /// <summary>Um vão numa linha: de 'de' a 'ate' (x ou z da obra) e de 'baixo' a 'alto', com a porta (null numa janela) ou a janela de fora.</summary>
        struct Vao
        {
            public float de, ate, baixo, alto;
            public Porta porta;
            public int janela;
        }

        readonly List<Vao> _vaos = new List<Vao>();

        void VaosDaBorda(Borda b, out float lo, out float hi)
        {
            _vaos.Clear();
            lo = b.lo + AjusteDaQuina(b, b.lo);
            hi = b.hi - AjusteDaQuina(b, b.hi);
            int lado = LadoDaBorda(b);
            foreach (var p in an.portas)
            {
                if (p.b != null || p.a != b.k || p.lado != lado || p.pos < b.lo || p.pos > b.hi) continue;
                float l = Largura(p);
                _vaos.Add(new Vao { de = p.pos - l / 2f, ate = p.pos + l / 2f, alto = Alto(p), porta = p, janela = -1 });
            }
            for (int i = 0; i < Janelas.Length; i++)
            {
                var j = Janelas[i];
                if (j.lado != lado || j.de < b.lo - 0.01f || j.ate > b.hi + 0.01f) continue;
                _vaos.Add(new Vao { de = j.de, ate = j.ate, baixo = j.baixo, alto = j.alto, janela = i });
            }
        }

        bool VaosDaDivisa(Divisa d)
        {
            _vaos.Clear();
            float L = d.hi - d.lo;
            foreach (var p in PlantaDoHospital.PortasEntre(an, d.a, d.b))
            {
                if (p.aberto) return false;
                float l = Largura(p);
                float s = Mathf.Clamp(p.pos - d.lo, l / 2f + 0.15f, L - l / 2f - 0.15f);
                _vaos.Add(new Vao { de = d.lo + s - l / 2f, ate = d.lo + s + l / 2f, alto = Alto(p), porta = p, janela = -1 });
            }
            return true;
        }

        // ------------------------------------------------------------ a casca

        void MontarCasca()
        {
            foreach (var b in _bordas) RegistrarLinha(Ponta(b, b.lo), Ponta(b, b.hi), ChaoDeFora - 0.4f, AltoDaFachada);
            foreach (var b in _bordas) ParedeDeFora(b);
            float e = m.esp / 2f;
            // a laje do chão (embaixo das paredes) e a cobertura, com o capeamento da platibanda
            Laje(X0 - e, X1 + e, Z0 - e, Z1 + e, 0f, 0.4f, "Concreto", true);
            Laje(X0 + e, X1 - e, Z0 + e, Z1 - e, TopoDaCobertura, GrossuraDaCobertura, CorDaLaje, true);
            Fachada();
            Telhado();
            LadosEFundos();
            PichacoesDeFora();
        }

        // uma parede de fora: o pedaço de baixo do chão (a fundação aparente) e a parede do chão até o alto (a fachada falsa
        // na frente; a platibanda nos outros lados), com as portas e as janelas
        void ParedeDeFora(Borda b)
        {
            VaosDaBorda(b, out float lo, out float hi);
            int lado = LadoDaBorda(b);
            bool sobe = b.vertical ? b.fora > 0 : b.fora < 0;
            Vector2 a = Ponta(b, sobe ? lo : hi), c = Ponta(b, sobe ? hi : lo);
            Abertura NaParede(Vao x) => new Abertura(sobe ? x.de - lo : hi - x.ate, x.ate - x.de, x.baixo, x.alto);
            LevantarParede(a, c, new List<Abertura>(), true, "base", ChaoDeFora - 0.4f, -ChaoDeFora + 0.4f, "ConcretoClaro");
            var aberturas = new List<Abertura>();
            foreach (var x in _vaos) aberturas.Add(NaParede(x));
            float topo = lado == 0 ? AltoDaFachada : TopoDaCobertura + Platibanda;
            var w = LevantarParede(a, c, aberturas, true, "fora", 0f, topo, "ConcretoClaro");
            foreach (var x in _vaos)
            {
                var ab = NaParede(x);
                if (x.porta != null) PortaDeFora(w, ab, x.porta);
                else JanelaDeFora(w, ab, Janelas[x.janela]);
            }
        }

        // ------------------------------------------------------------ o lado de dentro

        void MontarDentro()
        {
            foreach (var d in _divisas) RegistrarLinha(Ponta(d, d.lo), Ponta(d, d.hi), 0f, PeDireito);
            foreach (var d in _divisas) ParedeDeDentro(d);
            foreach (var k in an.comodos)
                Laje(k.x0 + 0.1f, k.x1 - 0.1f, k.z0 + 0.1f, k.z1 - 0.1f, 0.006f, 0.006f, k.piso ?? "Piso", false);
            Acabamentos();
            OForro();
            OEstande();
            PlacasDeDentro();
            MarcasDeDentro();
            PichacoesDeDentro();
            MoveisDaLojaDeArmas.Caixas(v, col);
            // a porta de cofre fechada (a sala intacta): a colisão dela, para ninguém passar (o modelo é da cena)
            if (v.sala == EstadoDaSala.Intacta)
                Caixa(PortaDoCofre + new Vector3(0f, AltoDoCofre / 2f, 0f), new Vector3(0.3f, AltoDoCofre, VaoDoCofre), "Metal", colide: true, visual: false);
        }

        // para onde a folha abre: para dentro do cômodo que não é de passagem (a área de vendas e a antessala são)
        static Comodo ParaOndeAbre(Comodo A, Comodo B)
        {
            if (A.tipo == "estande") return A;
            if (B.tipo == "estande") return B;
            bool pa = A.tipo == "vendas" || A.tipo == "antessala", pb = B.tipo == "vendas" || B.tipo == "antessala";
            if (pa != pb) return pa ? B : A;
            return (A.x1 - A.x0) * (A.z1 - A.z0) >= (B.x1 - B.x0) * (B.z1 - B.z0) ? A : B;
        }

        void ParedeDeDentro(Divisa d)
        {
            if (!VaosDaDivisa(d)) return;
            float e0 = Avanco(d, false), e1 = Avanco(d, true);
            var aberturas = new List<Abertura>();
            foreach (var x in _vaos) aberturas.Add(new Abertura(x.de - d.lo + e0, x.ate - x.de, x.baixo, x.alto));
            var w = LevantarParede(Ponta(d, d.lo - e0), Ponta(d, d.hi + e1), aberturas, false, "dentro", 0f, PeDireito, "Batente");
            Comodo ladoN = d.vertical ? d.b : d.a;
            foreach (var x in _vaos)
            {
                var ab = new Abertura(x.de - d.lo + e0, x.ate - x.de, x.baixo, x.alto);
                if (x.porta == null || EhACofre(x.porta)) { MolduraDoCofre(w, ab); continue; }
                int dentro = ParaOndeAbre(d.a, d.b) == ladoN ? 1 : -1;
                PortaDeDentro(w, ab, x.porta, dentro);
            }
        }

        bool ContinuaNaLinha(bool vertical, float c, float ponto, int sentido)
        {
            float s = ponto + sentido * 0.05f;
            foreach (var o in _divisas)
                if (o.vertical == vertical && Mathf.Abs(o.c - c) < 0.01f && s >= o.lo && s <= o.hi) return true;
            foreach (var bd in _bordas)
                if (bd.vertical == vertical && Mathf.Abs(bd.c - c) < 0.01f && s >= bd.lo && s <= bd.hi) return true;
            return false;
        }

        bool NaParedeDeFora(Vector2 ponto)
        {
            foreach (var bd in _bordas)
            {
                float s = bd.vertical ? ponto.y : ponto.x, c = bd.vertical ? ponto.x : ponto.y;
                if (Mathf.Abs(c - bd.c) < 0.01f && s >= bd.lo - 0.01f && s <= bd.hi + 0.01f) return true;
            }
            return false;
        }

        // o quanto a parede de dentro avança além de uma ponta (a regra do hospital: meia parede numa quina solta, nada onde
        // a linha continua ou encosta numa parede de fora; numa quina em L, só a parede em z avança)
        float Avanco(Divisa d, bool fim)
        {
            float ponta = fim ? d.hi : d.lo;
            if (ContinuaNaLinha(d.vertical, d.c, ponta, fim ? 1 : -1) || NaParedeDeFora(Ponta(d, ponta))) return 0f;
            bool antes = ContinuaNaLinha(!d.vertical, ponta, d.c, -1), depois = ContinuaNaLinha(!d.vertical, ponta, d.c, 1);
            if (antes && depois) return 0f;
            if (antes || depois) return d.vertical ? m.esp / 2f : -m.esp / 2f;
            return m.esp / 2f;
        }
    }
}
