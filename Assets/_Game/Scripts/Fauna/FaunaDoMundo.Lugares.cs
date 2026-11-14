using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>Os lugares dos bichos (a etapa 5): a caverna dos ursos, a toca dos coiotes e o brejo.</summary>
    public enum TipoDeLugarDoBicho : byte { Caverna, Covil, Brejo }

    /// <summary>Um lugar dos bichos: o ponto (y no chão; na caverna, o piso), o giro (a caverna e a toca olham para fora) e o raio.</summary>
    public struct LugarDosBichos
    {
        public TipoDeLugarDoBicho tipo;
        public Vector3 pos;
        public float giro, raio;
        public int semente;

        /// <summary>Um ponto do lugar no mundo (x para a direita, z para dentro da caverna ou da toca).</summary>
        public Vector3 NoMundo(Vector3 local) => pos + Quaternion.Euler(0f, giro, 0f) * local;

        /// <summary>O ponto do mundo no espaço do lugar.</summary>
        public Vector3 NoLugar(Vector3 mundo) => Quaternion.Euler(0f, -giro, 0f) * (mundo - pos);
    }

    /// <summary>
    /// Os lugares dos bichos (a etapa 5, 13/11/2026, a pedido: "pensei em criar umas cavernas em alguns lugares do mapa, onde
    /// pode ter ursos, algumas tocas dos coiotes, também criar um bioma de pântano para os sapos, cobras, vagalumes"). As
    /// decisões do Pedro: o pântano vai por cima do mapa (sem mexer no terreno do Gustavo) e dá para entrar nas cavernas.
    /// Os lugares saem de um sorteio fixo sobre o mapa (a semente da partida), o mesmo em todas as máquinas (nada vai pela
    /// rede e nada vai no save), feito aos poucos nos primeiros segundos (para não travar):
    /// - a caverna (de 3 a 10, pela área do mapa): na serra e na mata, num chão quase plano, longe da água, das estradas, das
    ///   cidades e das árvores: um morro de pedras com o túnel e o salão (dá para entrar: o piso, as paredes e o teto têm
    ///   colisor; por fora, dá para subir nas pedras). Os ursos moram nela: dormem lá dentro no meio do dia e na madrugada;
    /// - a toca dos coiotes (de 4 a 12): no campo, na rebrota e na lavoura: o monte de terra com o buraco; a matilha de coiotes
    ///   descansa em volta dela de dia;
    /// - o brejo (de 3 a 8): os pontos planos na beira da água: as poças, as taboas, as árvores mortas e o barro. Os sapos
    ///   moram nele (de dia também), as cascavéis às vezes, e os vaga-lumes se juntam nele à noite.
    /// Os desenhos ficam em LugaresNaTela.cs.
    /// </summary>
    public sealed partial class FaunaDoMundo
    {
        static readonly List<LugarDosBichos> _lugares = new List<LugarDosBichos>();
        static IMundoDoJogo _lugaresDoMundo;
        static Rect _lugaresDaArea;
        static System.Random _sorteioDosLugares;
        static int _tentativasDosLugares, _metaCavernas, _metaCovis, _metaBrejos;
        static bool _lugaresProntos;

        /// <summary>Os lugares do mapa (enchendo nos primeiros segundos da partida).</summary>
        public static IReadOnlyList<LugarDosBichos> Lugares => _lugares;

        /// <summary>A cada quadro, em cada máquina: o sorteio dos lugares, aos poucos (recomeça se o mapa mudar).</summary>
        internal static void AvancarLugares()
        {
            var m = Mapa;
            if (m == null || !m.PartidaPronta || !TemMapa)
            {
                if (_lugaresDoMundo != null) { _lugares.Clear(); _lugaresDoMundo = null; _lugaresProntos = false; }
                return;
            }
            if (m != _lugaresDoMundo || m.Area != _lugaresDaArea)
            {
                _lugaresDoMundo = m;
                _lugaresDaArea = m.Area;
                _lugares.Clear();
                _lugaresProntos = false;
                _sorteioDosLugares = new System.Random(m.SementeDaPartida ^ 0x5A1713);
                _tentativasDosLugares = 0;
                float km2 = m.Area.width * m.Area.height / 1e6f;
                _metaCavernas = Mathf.Clamp(Mathf.RoundToInt(km2 / 14f), 3, 10);
                _metaCovis = Mathf.Clamp(Mathf.RoundToInt(km2 / 10f), 4, 12);
                _metaBrejos = Mathf.Clamp(Mathf.RoundToInt(km2 / 18f), 3, 8);
            }
            if (_lugaresProntos) return;
            var r = _sorteioDosLugares;
            var a = m.Area;
            for (int k = 0; k < 120; k++)
            {
                if (++_tentativasDosLugares > 30000) { _lugaresProntos = true; return; }
                int cav = 0, cov = 0, bre = 0;
                foreach (var l in _lugares) { if (l.tipo == TipoDeLugarDoBicho.Caverna) cav++; else if (l.tipo == TipoDeLugarDoBicho.Covil) cov++; else bre++; }
                if (cav >= _metaCavernas && cov >= _metaCovis && bre >= _metaBrejos) { _lugaresProntos = true; return; }
                float x = a.xMin + 200f + (float)r.NextDouble() * (a.width - 400f);
                float z = a.yMin + 200f + (float)r.NextDouble() * (a.height - 400f);
                int qual = r.Next(3);
                if (qual == 0 && cav < _metaCavernas) TentarCaverna(m, x, z, r);
                else if (qual == 1 && cov < _metaCovis) TentarCovil(m, x, z, r);
                else if (qual == 2 && bre < _metaBrejos) TentarBrejo(m, x, z, r);
            }
        }

        // a inclinação do chão (o gradiente, m por m) e a direção que desce
        static float Inclinacao(float x, float z, float passo, out Vector3 desce)
        {
            float hx = AlturaDoMapa(x + passo, z) - AlturaDoMapa(x - passo, z);
            float hz = AlturaDoMapa(x, z + passo) - AlturaDoMapa(x, z - passo);
            var g = new Vector2(hx, hz) / (2f * passo);
            desce = g.sqrMagnitude > 1e-6f ? new Vector3(-g.x, 0f, -g.y).normalized : Vector3.forward;
            return g.magnitude;
        }

        static bool LongeDe(TipoDeLugarDoBicho tipo, float x, float z, float distancia)
        {
            foreach (var l in _lugares)
            {
                float d = tipo == l.tipo ? distancia : 120f;   // de outro tipo, pelo menos 120 m
                if ((l.pos.x - x) * (l.pos.x - x) + (l.pos.z - z) * (l.pos.z - z) < d * d) return false;
            }
            return true;
        }

        static bool AguaEmVolta(float x, float z, float raio)
        {
            if (AguaEm(x, z, out _) > 0.01f) return true;
            for (int i = 0; i < 8; i++)
            {
                float ang = i * Mathf.PI / 4f;
                if (AguaEm(x + Mathf.Cos(ang) * raio, z + Mathf.Sin(ang) * raio, out _) > 0.01f) return true;
            }
            return false;
        }

        // (13/11, a pedido: "a caverna dos ursos spawnou no meio de uma pista, coisa que não pode acontecer com nenhuma dessas
        // novas construções") a área toda do lugar, com folga, longe de estrada, de lote, de cidade, de base e de acampamento.
        // Antes só uns pontos em volta do meio eram olhados, e a pista passava entre eles.

        /// <summary>Há estrada (ou lote, cidade, base, acampamento) a menos de 'folga' m de algum ponto do círculo dado.</summary>
        static bool OcupadoEmVolta(IMundoDoJogo m, Vector3 meio, float raio, float folga)
        {
            var chao = ChaoDoLakewood;
            float passo = chao != null ? 6f : 3f;
            for (float dz = -raio; dz <= raio; dz += passo)
                for (float dx = -raio; dx <= raio; dx += passo)
                {
                    if (dx * dx + dz * dz > raio * raio + 0.01f) continue;
                    if (PontoOcupado(m, chao, meio.x + dx, meio.z + dz, folga)) return true;
                }
            return LongeDosLugaresDoMapa(m, meio, raio + 80f) == false;
        }

        /// <summary>A planta da caverna (o retângulo no espaço dela, com folga) longe de estrada, lote e cidade.</summary>
        static bool CavernaOcupada(IMundoDoJogo m, float x, float z, Quaternion rot, float folga)
        {
            var chao = ChaoDoLakewood;
            for (float lz = -9f; lz <= 22f; lz += 3f)
                for (float lx = -9f; lx <= 9f; lx += 3f)
                {
                    Vector3 q = new Vector3(x, 0f, z) + rot * new Vector3(lx, 0f, lz);
                    if (PontoOcupado(m, chao, q.x, q.z, folga)) return true;
                }
            return LongeDosLugaresDoMapa(m, new Vector3(x, 0f, z), 100f) == false;
        }

        static bool PontoOcupado(IMundoDoJogo m, ChaoDeLakewood chao, float x, float z, float folga)
        {
            if (m.CidadeEm(x, z) >= 0) return true;
            if (chao != null)
            {
                if (chao.EstradaPerto(x, z, folga, out _, out _, out _, out _)) return true;
                if (chao.LoteEm(x, z, folga) != null) return true;
                return false;
            }
            if (m.NaEstrada(x, z)) return true;
            for (int i = 0; i < 8; i++)
            {
                float ang = i * Mathf.PI / 4f;
                if (m.NaEstrada(x + Mathf.Cos(ang) * folga, z + Mathf.Sin(ang) * folga)) return true;
            }
            return false;
        }

        // longe das bases militares e dos acampamentos (o raio deles mais a folga)
        static bool LongeDosLugaresDoMapa(IMundoDoJogo m, Vector3 p, float folga)
        {
            var q = new Vector2(p.x, p.z);
            if (m.BasesMilitares != null) foreach (var b in m.BasesMilitares) if (b != null && b.Distancia(q) < b.Raio + folga) return false;
            if (m.Acampamentos != null) foreach (var a in m.Acampamentos) if (a != null && a.Distancia(q) < a.Raio + folga) return false;
            return true;
        }

        static bool ArvoreEmVolta(Vector3 p, float raio) => ArvoresDoMundo.MaisPerto(p, raio, out _, out _, out _);

        static void TentarCaverna(IMundoDoJogo m, float x, float z, System.Random r)
        {
            if (!LongeDe(TipoDeLugarDoBicho.Caverna, x, z, 900f)) return;
            if (Inclinacao(x, z, 8f, out var desce) > 0.14f) return;   // quase plano (o morro de pedras fica em cima do chão)
            if (AguaEmVolta(x, z, 40f)) return;
            var mancha = ManchaEm(x, z);
            if (mancha != Mancha.Serra && mancha != Mancha.Coniferas && mancha != Mancha.Mista) return;
            // a planta da caverna (de -5 a 16 m para dentro, 11 m de largura) sem árvore em cima
            float giro = Quaternion.LookRotation(-desce).eulerAngles.y;   // a boca olha para onde o chão desce
            var rot = Quaternion.Euler(0f, giro, 0f);
            if (CavernaOcupada(m, x, z, rot, 25f)) return;   // nem estrada, nem lote, nem cidade a 25 m da planta
            Vector3 meio = new Vector3(x, 0f, z) + rot * new Vector3(0f, 0f, 7f);
            meio.y = AlturaDoMapa(meio.x, meio.z);
            if (ArvoreEmVolta(meio, 12f)) return;
            // o piso: acima do chão mais alto da planta (o morro não afunda no relevo)
            float piso = float.MinValue;
            for (float lz = -1f; lz <= 16f; lz += 3.4f)
                for (float lx = -5f; lx <= 5f; lx += 2.5f)
                {
                    Vector3 q = new Vector3(x, 0f, z) + rot * new Vector3(lx, 0f, lz);
                    piso = Mathf.Max(piso, AlturaDoMapa(q.x, q.z));
                }
            _lugares.Add(new LugarDosBichos { tipo = TipoDeLugarDoBicho.Caverna, pos = new Vector3(x, piso + 0.12f, z), giro = giro, raio = 16f, semente = r.Next() });
        }

        static void TentarCovil(IMundoDoJogo m, float x, float z, System.Random r)
        {
            if (!LongeDe(TipoDeLugarDoBicho.Covil, x, z, 600f)) return;
            float incl = Inclinacao(x, z, 4f, out var desce);
            if (incl > 0.45f) return;
            if (AguaEmVolta(x, z, 25f)) return;
            var mancha = ManchaEm(x, z);
            if (mancha != Mancha.Campo && mancha != Mancha.Rebrota && mancha != Mancha.Lavoura) return;
            if (OcupadoEmVolta(m, new Vector3(x, 0f, z), 12f, 20f)) return;   // a toca e os coiotes em volta, longe da estrada
            var p = new Vector3(x, AlturaDoMapa(x, z), z);
            if (ArvoreEmVolta(p, 4f)) return;
            float giro = incl > 0.03f ? Quaternion.LookRotation(-desce).eulerAngles.y : (float)r.NextDouble() * 360f;
            _lugares.Add(new LugarDosBichos { tipo = TipoDeLugarDoBicho.Covil, pos = p, giro = giro, raio = 4f, semente = r.Next() });
        }

        static void TentarBrejo(IMundoDoJogo m, float x, float z, System.Random r)
        {
            if (!LongeDe(TipoDeLugarDoBicho.Brejo, x, z, 700f)) return;
            if (AguaEm(x, z, out _) > 0.01f) return;
            if (ManchaEm(x, z) != Mancha.Beira) return;   // a até 35 m da água
            if (Inclinacao(x, z, 12f, out _) > 0.07f) return;   // plano
            float raio = 30f + (float)r.NextDouble() * 16f;
            if (OcupadoEmVolta(m, new Vector3(x, 0f, z), raio, 15f)) return;   // o brejo inteiro longe da estrada
            _lugares.Add(new LugarDosBichos { tipo = TipoDeLugarDoBicho.Brejo, pos = new Vector3(x, AlturaDoMapa(x, z), z), giro = (float)r.NextDouble() * 360f, raio = raio, semente = r.Next() });
        }

        /// <summary>O lugar do tipo mais perto do ponto (a até 'raio' m; -1 sem nenhum).</summary>
        internal static int LugarPerto(TipoDeLugarDoBicho tipo, Vector3 p, float raio)
        {
            int melhor = -1;
            float m = raio * raio;
            for (int i = 0; i < _lugares.Count; i++)
            {
                if (_lugares[i].tipo != tipo) continue;
                float d = (new Vector2(_lugares[i].pos.x - p.x, _lugares[i].pos.z - p.z)).sqrMagnitude;
                if (d < m) { m = d; melhor = i; }
            }
            return melhor;
        }

        /// <summary>O ponto está dentro de um brejo.</summary>
        internal static bool NoBrejo(Vector3 p)
        {
            int i = LugarPerto(TipoDeLugarDoBicho.Brejo, p, 60f);
            return i >= 0 && Vector2.Distance(new Vector2(_lugares[i].pos.x, _lugares[i].pos.z), new Vector2(p.x, p.z)) < _lugares[i].raio;
        }

        /// <summary>Um ponto seco ao acaso dentro do brejo (fora das poças).</summary>
        internal static Vector3 PontoNoBrejo(LugarDosBichos b, float fora = 0.85f)
        {
            for (int t = 0; t < 10; t++)
            {
                Vector2 c = Random.insideUnitCircle * b.raio * fora;
                var q = new Vector3(b.pos.x + c.x, 0f, b.pos.z + c.y);
                if (AguaEm(q.x, q.z, out _) > 0.05f || LugaresNaTela.NaPoca(b, q)) continue;
                q.y = AlturaDoMapa(q.x, q.z);
                return q;
            }
            return b.pos;
        }

        // ------------------------------------------------------------------ quem mora nos lugares (o povoamento, no anfitrião)

        /// <summary>A distância em que os moradores nascem em casa (antes do anel do povoamento: 95 a 190 m).</summary>
        const float PertoDeCasa = 230f;
        readonly Dictionary<int, float> _casaOlhadaAte = new Dictionary<int, float>();

        // o sorteio de um lugar num dia do jogo (o mesmo o dia todo: a caverna vazia hoje continua vazia até amanhã)
        static bool SorteioDoDia(int semente, int quem, float chance)
        {
            int dia = Mathf.FloorToInt((float)(HorasDoJogo / 24.0));
            var r = new System.Random(semente ^ (dia * 7919) ^ (quem * 104729));
            return r.NextDouble() < chance;
        }

        /// <summary>
        /// (13/11, a pedido: "não encontrei os castores spawnados, mesma coisa pra caverna dos ursos, não tem ninguém e dos
        /// coiotes também") Os moradores nascem em casa quando alguém chega a 230 m: o urso no salão da caverna (75% das
        /// cavernas, num dia), os coiotes na toca (80%), os sapos no brejo (sempre) e a cascavel nele (50%), e a família de
        /// castores na toca do lago ou do riacho (85%). Antes eles dependiam do anel de 95 a 190 m e da raridade de cada
        /// espécie (o castor, 5% a cada 2,5 s): quem ia direto até o lugar o achava vazio. Quem se afasta (270 m) leva os
        /// bichos embora; a casa fica livre e o sorteio do dia vale de novo na volta.
        /// </summary>
        void MoradoresDosLugares(Vector3 pes, float agora)
        {
            for (int i = 0; i < _lugares.Count; i++)
            {
                var l = _lugares[i];
                if (Vector2.Distance(new Vector2(l.pos.x, l.pos.z), new Vector2(pes.x, pes.z)) > PertoDeCasa) continue;
                switch (l.tipo)
                {
                    case TipoDeLugarDoBicho.Caverna:
                    {
                        var urso = l.semente % 2 == 0 ? Especie.UrsoNegro : Especie.UrsoPardo;
                        Morador(i, urso, 0.75f, agora, 6f);
                        break;
                    }
                    case TipoDeLugarDoBicho.Covil: Morador(i, Especie.Coiote, 0.8f, agora, 35f); break;
                    default:
                        Morador(i, Especie.Sapo, 1f, agora, 25f, 3);   // (até 3 grupos de sapos no brejo)
                        Morador(i, Especie.Cascavel, 0.5f, agora, 25f);
                        break;
                }
            }
            // as tocas dos castores (do lago e dos riachos)
            for (int i = 0; i < _tocas.Count; i++)
            {
                var t = _tocas[i];
                if (Vector2.Distance(new Vector2(t.x, t.z), new Vector2(pes.x, pes.z)) > PertoDeCasa) continue;
                var toca = new Vector3(t.x, NivelEm(t.x, t.z), t.z);
                bool ocupada = false;
                foreach (var g in Grupos) if (g.especie == Especie.Castor && g.temToca && Vector2.Distance(new Vector2(g.toca.x, g.toca.z), new Vector2(t.x, t.z)) < 3f && g.membros.Count > 0) { ocupada = true; break; }
                if (ocupada) continue;
                int chave = 100000 + i;
                if (_casaOlhadaAte.TryGetValue(chave, out float ate) && agora < ate) continue;
                _casaOlhadaAte[chave] = agora + 30f;
                if (!SorteioDoDia(Mathf.RoundToInt(t.x * 3f + t.z * 7f), 77, 0.85f)) continue;
                NascerGrupo(Especie.Castor, toca, false);   // (saem da toca: nascem em volta dela, na água)
            }
        }

        // um morador do lugar 'i': se a casa está vazia, o sorteio do dia deixa e ninguém está perto demais (quem vê o
        // lugar não vê o bicho aparecer: 'longe' m), nasce
        void Morador(int i, Especie e, float chance, float agora, float longe, int grupos = 1)
        {
            var l = _lugares[i];
            int ja = 0;
            foreach (var g in Grupos) if (g.especie == e && g.temLugar && Vector3.Distance(g.lugar, l.pos) < 1f && g.membros.Count > 0) ja++;
            if (ja >= grupos) return;
            int chave = i * 32 + (int)e + ja * 1000;
            if (_casaOlhadaAte.TryGetValue(chave, out float ate) && agora < ate) return;
            _casaOlhadaAte[chave] = agora + 20f;
            if (!SorteioDoDia(l.semente, (int)e + ja * 31, chance)) return;
            if (MaisPerto(l.pos) < longe) return;
            NascerNoLugar(e, i);
        }

        Grupo GrupoNoLugar(Especie e, LugarDosBichos l)
        {
            foreach (var g in Grupos)
                if (g.especie == e && g.temLugar && Vector3.Distance(g.lugar, l.pos) < 1f && g.membros.Count > 0) return g;
            return null;
        }

        /// <summary>O grupo nasce no lugar 'i' (o urso dormindo no salão, os coiotes deitados em volta da toca, os sapos no brejo).</summary>
        bool NascerNoLugar(Especie e, int i, bool teste = false)
        {
            var l = _lugares[i];
            Vector3 centro = l.tipo == TipoDeLugarDoBicho.Caverna ? l.NoMundo(new Vector3(0f, 0f, 10f))
                           : l.tipo == TipoDeLugarDoBicho.Covil ? l.NoMundo(new Vector3(0f, 0f, -3f))
                           : PontoNoBrejo(l, 0.6f);
            // (13/11) a cascavel do brejo nasce perto de um grupo de sapos (de 5 a 8 m): a caça acontece
            if (l.tipo == TipoDeLugarDoBicho.Brejo && e == Especie.Cascavel)
                foreach (var gs in Grupos)
                    if (gs.especie == Especie.Sapo && gs.temLugar && Vector3.Distance(gs.lugar, l.pos) < 1f && gs.membros.Count > 0 && gs.membros[0] != null)
                    {
                        Vector2 c = Random.insideUnitCircle.normalized * Random.Range(5f, 8f);
                        centro = gs.membros[0].transform.position + new Vector3(c.x, 0f, c.y);
                        break;
                    }
            if (l.tipo != TipoDeLugarDoBicho.Caverna) centro.y = AlturaDoMapa(centro.x, centro.z);
            _ultimoGrupo = null;
            int n = NascerGrupo(e, centro, teste);
            var g = _ultimoGrupo;
            if (n == 0 || g == null || !Grupos.Contains(g)) return false;
            g.temLugar = true;
            g.lugar = l.pos;
            g.lugarIndice = i;
            g.pasto = centro;
            foreach (var b in g.membros)
            {
                if (b == null) continue;
                if (l.tipo == TipoDeLugarDoBicho.Caverna)
                {
                    // no salão, deitados (de dia, dormindo)
                    b.transform.position = l.NoMundo(new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(8f, 12f)));
                    b.DeitarAte(Time.time + Random.Range(60f, 200f));
                }
                else if (l.tipo == TipoDeLugarDoBicho.Covil)
                {
                    Vector2 c = Random.insideUnitCircle.normalized * Random.Range(2.5f, 6f);
                    var q = l.NoMundo(new Vector3(c.x, 0f, c.y - 4f));
                    q.y = AlturaDoMapa(q.x, q.z);
                    b.transform.position = q;
                    if (CicloDoDia.Noite < 0.4f) b.DeitarAte(Time.time + Random.Range(60f, 200f));
                }
                else
                {
                    // juntos, em volta do meio do grupo (na terra)
                    Vector2 c = Random.insideUnitCircle * 2.5f;
                    var q = centro + new Vector3(c.x, 0f, c.y);
                    if (AguaEm(q.x, q.z, out _) > 0.05f) q = centro;
                    q.y = AlturaDoMapa(q.x, q.z);
                    b.transform.position = q;
                }
            }
            return true;
        }

        // ------------------------------------------------------------------ o F1

        /// <summary>O F1, em cada máquina: o lugar do tipo mais perto (a até 8 km): o jogador do lado de fora, de frente para ele.</summary>
        public static bool LugarDeTeste(JogadorRede j, TipoDeLugarDoBicho tipo, out Vector3 onde, out float giro)
        {
            onde = default;
            giro = 0f;
            if (j == null) return false;
            int i = LugarPerto(tipo, j.transform.position, 8000f);
            if (i < 0) return false;
            var l = _lugares[i];
            Vector3 fora = tipo == TipoDeLugarDoBicho.Caverna ? l.NoMundo(new Vector3(0f, 0f, -9f))
                         : tipo == TipoDeLugarDoBicho.Covil ? l.NoMundo(new Vector3(0f, 0f, -12f))
                         : l.pos + Quaternion.Euler(0f, l.giro, 0f) * Vector3.back * (l.raio + 8f);
            fora.y = AlturaDoMapa(fora.x, fora.z);
            Vector3 para = (tipo == TipoDeLugarDoBicho.Brejo ? l.pos : l.NoMundo(new Vector3(0f, 0f, 2f))) - fora;
            para.y = 0f;
            onde = fora;
            giro = Quaternion.LookRotation(para.sqrMagnitude > 0.01f ? para : Vector3.forward).eulerAngles.y;
            return true;
        }

        /// <summary>O F1 (no anfitrião): os bichos do lugar mais perto (a caverna: um urso dormindo; a toca: os coiotes; o brejo: os sapos e uma cascavel).</summary>
        public string BichosDoLugar(JogadorRede j)
        {
            if (!_servidor || j == null) return "Só o anfitrião.";
            Vector3 eu = j.transform.position;
            int melhor = -1;
            float m = 400f * 400f;
            for (int i = 0; i < _lugares.Count; i++)
            {
                float d = (new Vector2(_lugares[i].pos.x - eu.x, _lugares[i].pos.z - eu.z)).sqrMagnitude;
                if (d < m) { m = d; melhor = i; }
            }
            if (melhor < 0) return _lugares.Count == 0 ? "Os lugares ainda não ficaram prontos (ou este mapa não tem)." : "Nenhum lugar a até 400 m (use os \"Ir para\" antes).";
            var l = _lugares[melhor];
            switch (l.tipo)
            {
                case TipoDeLugarDoBicho.Caverna:
                    return NascerNoLugar(Random.value < 0.5f ? Especie.UrsoNegro : Especie.UrsoPardo, melhor, true) ? "Um urso dormindo no salão da caverna." : "Não deu para pôr o urso.";
                case TipoDeLugarDoBicho.Covil:
                    return NascerNoLugar(Especie.Coiote, melhor, true) ? "Os coiotes em volta da toca." : "Não deu para pôr os coiotes.";
                default:
                    bool sapos = NascerNoLugar(Especie.Sapo, melhor, true);
                    bool cobra = NascerNoLugar(Especie.Cascavel, melhor, true);
                    return sapos || cobra ? $"No brejo: {(sapos ? "os sapos" : "")}{(sapos && cobra ? " e " : "")}{(cobra ? "uma cascavel" : "")}." : "Não deu para pôr os bichos do brejo.";
            }
        }

        /// <summary>Um resumo dos lugares (o F1).</summary>
        public static string ResumoDosLugares(Vector3 perto)
        {
            int cav = 0, cov = 0, bre = 0;
            foreach (var l in _lugares) { if (l.tipo == TipoDeLugarDoBicho.Caverna) cav++; else if (l.tipo == TipoDeLugarDoBicho.Covil) cov++; else bre++; }
            return $"{cav} cavernas, {cov} tocas de coiote e {bre} brejos{(_lugaresProntos ? "" : " (ainda sorteando)")}";
        }
    }
}
