using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>A população de uma região do mapa (512 m), no save do mundo: 1 cheia, até o piso (nunca zera).</summary>
    [System.Serializable]
    public struct NivelDaFauna
    {
        public long chave;
        public float nivel;
    }

    /// <summary>
    /// O povoamento (no anfitrião): em volta de cada jogador, os grupos de cada espécie nascem fora da vista (de 95 a
    /// 190 m), numa mancha do mapa onde ela vive, e somem a mais de 270 m de todos. A caça baixa a população da região
    /// (o mapa em quadrados de 512 m), e a chance de nascer cai junto; ela volta sozinha, devagar (decisão do Pedro em
    /// 12/11/2026: "depois de algum tempo volta a ter spawn dos animais, não quero que morram para sempre"): nunca abaixo de
    /// 15%, e cheia de novo em ~1 dia do jogo. As regiões que não estão cheias vão no save do mundo.
    /// </summary>
    public sealed partial class FaunaDoMundo
    {
        const float NascerMin = 95f, NascerMax = 190f, RaioDoPovo = 230f, DistanciaDeRecolher = 270f;
        const float TamanhoDaRegiao = 512f, PisoDoNivel = 0.15f, VoltaPorHora = 0.035f;

        /// <summary>As espécies que nascem sozinhas no mundo (as com <see cref="DadosDaEspecie.noMundo"/>; as outras pelo F1).</summary>
        static Especie[] _noMundo;
        static Especie[] NoMundo
        {
            get
            {
                if (_noMundo != null) return _noMundo;
                var l = new List<Especie>();
                for (int i = 0; i < Especies.Quantas; i++) if (Especies.De((Especie)i).noMundo) l.Add((Especie)i);
                return _noMundo = l.ToArray();
            }
        }

        readonly Dictionary<long, float> _nivel = new Dictionary<long, float>();
        double _ultimaHora;

        /// <summary>As regiões guardadas (o save carregou antes da fauna existir, ou a fauna foi recriada).</summary>
        public static List<NivelDaFauna> NiveisGuardados;

        static long Chave(Vector3 p) => ((long)Mathf.FloorToInt(p.x / TamanhoDaRegiao) << 32) ^ (uint)Mathf.FloorToInt(p.z / TamanhoDaRegiao);

        /// <summary>A população da região do ponto (1: cheia).</summary>
        public float Nivel(Vector3 p) => _nivel.TryGetValue(Chave(p), out var n) ? n : 1f;

        void BaixarNivel(Vector3 p, Bicho b)
        {
            float tira = b.Dados.pontos >= 60f ? (b.Variante == Variante.Filhote ? 0.06f : 0.12f) : 0.03f;
            _nivel[Chave(p)] = Mathf.Max(PisoDoNivel, Nivel(p) - tira);
        }

        void Recuperar()
        {
            double agora = HorasDoJogo;
            double dh = agora - _ultimaHora;
            _ultimaHora = agora;
            if (dh <= 0.0 || _nivel.Count == 0) return;
            var chaves = new List<long>(_nivel.Keys);
            foreach (var k in chaves)
            {
                float n = _nivel[k] + VoltaPorHora * (float)dh;
                if (n >= 1f) _nivel.Remove(k);
                else _nivel[k] = n;
            }
        }

        /// <summary>As regiões que não estão cheias (o save do mundo).</summary>
        public static List<NivelDaFauna> NiveisParaSalvar()
        {
            var f = Instancia;
            if (f == null || !f._servidor) return NiveisGuardados != null ? new List<NivelDaFauna>(NiveisGuardados) : new List<NivelDaFauna>();
            var lista = new List<NivelDaFauna>();
            foreach (var kv in f._nivel) lista.Add(new NivelDaFauna { chave = kv.Key, nivel = kv.Value });
            return lista;
        }

        /// <summary>O save carregou: as regiões voltam (agora, ou quando a fauna nascer).</summary>
        public static void Restaurar(List<NivelDaFauna> niveis)
        {
            NiveisGuardados = niveis != null ? new List<NivelDaFauna>(niveis) : null;
            if (Instancia != null && Instancia._servidor) Instancia.RestaurarNiveis();
        }

        void RestaurarNiveis()
        {
            _nivel.Clear();
            if (NiveisGuardados == null) return;
            foreach (var n in NiveisGuardados) _nivel[n.chave] = Mathf.Clamp(n.nivel, PisoDoNivel, 1f);
        }

        // ------------------------------------------------------------------ nascer em volta dos jogadores

        void Povoar(float agora)
        {
            var dados = MundoDaPartida.Instancia != null ? MundoDaPartida.Instancia.Dados : null;
            if (dados == null || dados.Floresta == null) return;
            foreach (var g in Pessoas)
            {
                foreach (var e in NoMundo)
                {
                    var d = Especies.De(e);
                    if (GruposPerto(e, g.pes, RaioDoPovo) >= d.gruposPorJogador) continue;
                    if (Random.value > d.raridade) continue;   // os raros (os predadores) demoram a aparecer
                    if (!NasceAgora(d)) continue;   // (a etapa 4) os da noite só nascem à noite
                    // (a etapa 3) os da água: no lago (o raso, a toca, o céu dele), se ele estiver no anel
                    if (LugarNaAgua(e, g.pes, NascerMin, NascerMax, out var naAgua, out bool achou))
                    {
                        if (achou && MaisPerto(naAgua) >= NascerMin * 0.8f && Random.value <= Nivel(naAgua)) NascerGrupo(e, naAgua, false);
                        continue;
                    }
                    for (int tentativa = 0; tentativa < 10; tentativa++)
                    {
                        float ang = Random.Range(0f, Mathf.PI * 2f), dist = Random.Range(NascerMin, NascerMax);
                        Vector3 p = g.pes + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
                        if (!LugarBom(p, e, dados) || MaisPerto(p) < NascerMin) continue;
                        if (Random.value > Nivel(p)) break;   // a região caçada demais: menos chance (e tenta de novo depois)
                        NascerGrupo(e, p, false);
                        break;
                    }
                }
            }
        }

        int GruposPerto(Especie e, Vector3 p, float raio)
        {
            int n = 0;
            foreach (var g in Grupos)
                if (g.especie == e && (g.Centro - p).sqrMagnitude < raio * raio) n++;
            return n;
        }

        static bool LugarBom(Vector3 p, Especie e, DadosDoMundo dados)
        {
            float lim = dados.Metade - 60f;
            if (Mathf.Abs(p.x) > lim || Mathf.Abs(p.z) > lim) return false;
            if (dados.FunduraDaAgua(p.x, p.z) > 0.05f) return false;
            var m = dados.Floresta.ManchaEm(p.x, p.z, out _);
            foreach (var h in Especies.De(e).habitats) if (h == m) return true;
            return false;
        }

        /// <summary>Um pasto novo para o rebanho, na mesma mancha (ou perto do antigo, se não achar).</summary>
        internal Vector3 NovoPasto(Vector3 antes, Especie e)
        {
            if (PastoNaAgua(antes, e, out var naAgua)) return naAgua;
            var dados = MundoDaPartida.Instancia != null ? MundoDaPartida.Instancia.Dados : null;
            for (int k = 0; k < 8; k++)
            {
                // os predadores andam mais (de 40 a 110 m); os outros pastam por perto
                bool longe = Especies.De(e).papel != Papel.Presa;
                float ang = Random.Range(0f, Mathf.PI * 2f), dist = longe ? Random.Range(40f, 110f) : Random.Range(15f, 35f);
                Vector3 p = antes + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
                if (dados == null || LugarBom(p, e, dados)) return p;
            }
            return antes;
        }

        /// <summary>
        /// Um grupo da espécie em volta do ponto. O rebanho de veados: o macho alfa (65% das vezes), as fêmeas e um terço de
        /// filhotes. 'teste': o F1 (todos os tipos, sem a mancha). Devolve quantos nasceram.
        /// </summary>
        /// <summary>O grupo do F1 chega distraído por estes segundos (a 18 m, o veado fugiria na hora: ele foge a 40 m).</summary>
        const float SegundosDistraido = 10f;

        internal int NascerGrupo(Especie e, Vector3 centro, bool teste)
        {
            var d = Especies.De(e);
            int n = Random.Range(d.grupoMin, d.grupoMax + 1);
            var g = new Grupo { especie = e, pasto = centro, trocarPastoEm = Time.time + Random.Range(10f, 40f) };
            Grupos.Add(g);
            var variantes = Composicao(e, n, teste);
            float raio = Mathf.Clamp(2f + n * 0.8f, 2f, 9f) * Mathf.Max(0.3f, d.altura);
            // (a etapa 3) os da água ficam na água; o castor, na toca; os gansos, às vezes com o ninho na margem
            bool daAgua = e == Especie.Salmao || e == Especie.Pato || e == Especie.Ganso || e == Especie.Castor;
            if (daAgua) raio = Mathf.Max(raio, 2.5f);
            if (e == Especie.Castor) { g.temToca = true; g.toca = centro; }
            if (e == Especie.Ganso && (teste || Random.value < 0.5f) && MargemNaDirecao(centro, 1.5f, out var ninho))
            {
                g.temNinho = true;
                g.ninho = ninho;
            }
            int nasceram = 0;
            for (int i = 0; i < variantes.Count; i++)
            {
                Vector2 c = Random.insideUnitCircle * raio;
                var lugar = new Vector3(c.x, 0f, c.y);
                if (daAgua && FunduraEm(centro + lugar) < (e == Especie.Salmao ? 0.55f : 0.35f)) lugar *= 0.2f;
                var b = Nascer(e, variantes[i], centro + lugar, Random.Range(0f, 360f), g);
                if (b == null) continue;
                nasceram++;
                b.LugarNoGrupo = lugar;
                if (teste) b.Distrair(Time.time + SegundosDistraido);
                if (d.meio == Meio.Ar) b.PorNoAr(Random.Range(30f, 45f));
                // o ganso no ninho: a fêmea já choca
                if (g.temNinho && variantes[i] == Variante.Femea) b.transform.position = g.ninho;
                // e o macho de guarda, ali do lado (o resto do bando boia no lago)
                else if (g.temNinho && variantes[i] == Variante.Macho)
                {
                    Vector3 lado = lugar.sqrMagnitude > 0.01f ? lugar.normalized : Vector3.forward;
                    b.transform.position = g.ninho + lado * 2f;
                }
                if (i == 0 && variantes[i] == Variante.Macho && (d.vida == Vida.Rebanho || d.vida == Vida.Matilha))
                {
                    b.Alfa = true;
                    g.lider = b;
                }
            }
            if (nasceram == 0) Grupos.Remove(g);
            else if (g.lider == null) ArrumarGrupos();
            return nasceram;
        }

        static List<Variante> Composicao(Especie e, int n, bool teste)
        {
            var lista = new List<Variante>();
            switch (e)
            {
                case Especie.Alce:
                    // o alce: o macho sozinho, ou a mãe com o filhote (às vezes dois)
                    if (n <= 1 && !teste) lista.Add(Variante.Macho);
                    else
                    {
                        if (teste) lista.Add(Variante.Macho);
                        lista.Add(Variante.Femea);
                        lista.Add(Variante.Filhote);
                        if (Random.value < 0.25f) lista.Add(Variante.Filhote);
                    }
                    break;
                case Especie.Ganso:
                    lista.Add(Variante.Macho);
                    lista.Add(Variante.Femea);
                    while (lista.Count < n) lista.Add(Variante.Adulto);
                    break;
                case Especie.Veado:
                    if (teste || Random.value < 0.65f) lista.Add(Variante.Macho);
                    int filhotes = Random.Range(e == Especie.Alce ? 1 : 0, n / 3 + 1);
                    if (teste) filhotes = Mathf.Max(1, filhotes);
                    while (lista.Count < n - filhotes) lista.Add(Variante.Femea);
                    while (lista.Count < n) lista.Add(Variante.Filhote);
                    break;
                case Especie.UrsoNegro:
                case Especie.UrsoPardo:
                    // sozinho, o macho; com filhotes (1 ou 2), a mãe
                    if (n <= 1) lista.Add(Variante.Macho);
                    else
                    {
                        lista.Add(Variante.Femea);
                        while (lista.Count < n) lista.Add(Variante.Filhote);
                    }
                    break;
                case Especie.Coiote:
                    lista.Add(Variante.Macho);
                    while (lista.Count < n) lista.Add(Variante.Adulto);
                    break;
                case Especie.Lobo:
                    lista.Add(Variante.Macho);
                    lista.Add(Variante.Femea);
                    while (lista.Count < n) lista.Add(lista.Count >= n - 1 && Random.value < 0.5f ? Variante.Filhote : Variante.Adulto);
                    break;
                default:
                    for (int i = 0; i < n; i++) lista.Add(Variante.Adulto);
                    break;
            }
            return lista;
        }
    }
}
