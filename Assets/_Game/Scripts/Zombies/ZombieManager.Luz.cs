using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Os fachos de luz que afastam os Ocos (veja Zumbi.Luz.cs): a lanterna de cada jogador, ligada, e os faróis acesos de
    /// cada carro. Refeitos a cada quadro, no anfitrião (são poucos: um por jogador e um por carro de farol aceso).
    /// E os fogos (05/11/2026, a pedido): a tocha acesa na mão e a fogueira acesa, redondos (<see cref="Fogos"/>), com a
    /// espera dos que rodeiam a beira (<see cref="EsperarNoFogo"/>: a horda que cria coragem).
    /// </summary>
    public partial class ZombieManager
    {
        internal struct Facho
        {
            public Vector3 origem, direcao;
            public float alcance, cosMeio, tanMeio;
            public bool carro;
            /// <summary>Um fogo (a tocha, a fogueira): redondo, sem direção, no plano; o vagar não escolhe ponto nele.</summary>
            public bool fogo;
            public IAlvoDeZumbi dono;   // quem segura a lanterna: o jogador ou o sobrevivente (no farol e no fogo, ninguém)
        }

        internal readonly List<Facho> Fachos = new List<Facho>(8);

        /// <summary>Um fogo que os afasta: o chão no centro (os pés de quem segura a tocha; o chão da fogueira) e o raio.</summary>
        internal struct Fogo
        {
            public Vector3 centro;
            public float raio;
            /// <summary>Quem é (a espera na beira conta por fogo): a fogueira, o id dela; a tocha, <see cref="ChaveDaTocha"/> + o cliente.</summary>
            public int chave;
            /// <summary>Quem segura a tocha (na fogueira, ninguém).</summary>
            public IAlvoDeZumbi dono;
        }

        internal readonly List<Fogo> Fogos = new List<Fogo>(8);
        const int ChaveDaTocha = 100000;
        /// <summary>O zumbi que ainda não esperou em fogo nenhum.</summary>
        internal const int SemEspera = int.MinValue;
        const float AlturaDoFogo = 1f;
        readonly List<(ushort id, Vector3 pos)> _fogueiras = new List<(ushort, Vector3)>(8);

        // quem rodeia cada fogo agora (o zumbi e a última vez que ele esperou ali) e desde quando a horda espera
        sealed class EsperaNoFogo
        {
            public readonly Dictionary<Zumbi, float> quem = new Dictionary<Zumbi, float>();
            public float desde = -1f, abaixoDesde = -1f, coragemAte;
        }

        readonly Dictionary<int, EsperaNoFogo> _esperas = new Dictionary<int, EsperaNoFogo>();
        readonly List<int> _esperasVelhas = new List<int>();
        readonly List<Zumbi> _quemSaiu = new List<Zumbi>();
        float _proximaLimpezaDasEsperas;

        void AtualizarFachos()
        {
            Fachos.Clear();
            var a = ajustes;
            float meioL = a.meioAnguloDaLanterna * Mathf.Deg2Rad, meioF = a.meioAnguloDoFarol * Mathf.Deg2Rad;
            for (int k = 0; k < QuantosJogadores; k++)
            {
                ref readonly var p = ref Jogadores[k];
                if (!p.lanterna || p.olhar.sqrMagnitude < 0.01f) continue;
                Fachos.Add(new Facho
                {
                    origem = p.olho, direcao = p.olhar.normalized, alcance = a.alcanceDaLanterna,
                    cosMeio = Mathf.Cos(meioL), tanMeio = Mathf.Tan(meioL), dono = p.jogador,
                });
            }
            // a lanterna dos sobreviventes (no peito, para a frente) afasta os zumbis como a do jogador (02/11/2026)
            for (int k = 0; k < QuantosNpcs; k++)
            {
                ref readonly var p = ref Npcs[k];
                if (!p.lanterna || p.olhar.sqrMagnitude < 0.01f) continue;
                Fachos.Add(new Facho
                {
                    origem = p.peito, direcao = p.olhar.normalized, alcance = a.alcanceDaLanterna,
                    cosMeio = Mathf.Cos(meioL), tanMeio = Mathf.Tan(meioL), dono = p.quem,
                });
            }
            foreach (var c in Carro.Todos)
            {
                if (c == null || !c.FaroisLigados) continue;
                // a luz sai do meio dos faróis, para a frente do carro (no plano: o facho do farol desce um pouco)
                Vector3 frente = c.transform.forward;
                frente.y = 0f;
                if (frente.sqrMagnitude < 0.01f) continue;
                frente.Normalize();
                Vector3 origem = Vector3.zero;
                int n = 0;
                if (c.farois != null)
                    foreach (var f in c.farois)
                        if (f != null) { origem += f.transform.position; n++; }
                origem = n > 0 ? origem / n : c.transform.position + frente * 2f + Vector3.up * 0.7f;
                Fachos.Add(new Facho
                {
                    origem = origem, direcao = frente, alcance = a.alcanceDoFarol,
                    cosMeio = Mathf.Cos(meioF), tanMeio = Mathf.Tan(meioF), carro = true,
                });
            }
            AtualizarFogos();
        }

        // os fogos: a tocha acesa na mão de cada jogador (fora do carro) e as fogueiras acesas
        void AtualizarFogos()
        {
            Fogos.Clear();
            var a = ajustes;
            for (int k = 0; k < QuantosJogadores; k++)
            {
                ref readonly var p = ref Jogadores[k];
                if (p.jogador == null || !p.jogador.TochaAcesa) continue;
                Fogos.Add(new Fogo { centro = p.pes, raio = a.raioDaTocha, chave = ChaveDaTocha + (int)p.jogador.OwnerClientId, dono = p.jogador });
            }
            var construcoes = ConstrucoesNoMundo.Instancia;
            if (construcoes != null)
            {
                _fogueiras.Clear();
                construcoes.FogueirasAcesas(_fogueiras);
                foreach (var (id, pos) in _fogueiras) Fogos.Add(new Fogo { centro = pos, raio = a.raioDaFogueira, chave = id });
            }
            // no vagar também (Zumbi.cs: ele nunca escolhe um ponto dentro da luz): um facho redondo, sem dono (o Long não o
            // toma pela lanterna, que o petrifica de perto)
            foreach (var f in Fogos)
                Fachos.Add(new Facho { origem = f.centro + Vector3.up * AlturaDoFogo, direcao = Vector3.forward, alcance = f.raio, cosMeio = -2f, fogo = true });
            LimparEsperas();
        }

        /// <summary>O ponto está dentro do fogo (no plano, sem contar outro andar), com uma folga (m) a mais no raio.</summary>
        internal static bool DentroDoFogo(in Fogo f, Vector3 ponto, float folga = 0f)
        {
            Vector3 d = ponto - f.centro;
            if (Mathf.Abs(d.y) > 3f) return false;
            d.y = 0f;
            float r = f.raio + folga;
            return d.sqrMagnitude < r * r;
        }

        /// <summary>O ponto está dentro de algum fogo (a tocha, a fogueira; não a lanterna nem o farol).</summary>
        internal bool NoFogo(Vector3 ponto) => NoOutroFogo(ponto, SemEspera);

        /// <summary>O ponto está dentro de algum fogo além daquele (a chave).</summary>
        internal bool NoOutroFogo(Vector3 ponto, int chave)
        {
            for (int i = 0; i < Fogos.Count; i++)
                if (Fogos[i].chave != chave && DentroDoFogo(Fogos[i], ponto)) return true;
            return false;
        }

        /// <summary>
        /// A chave da espera na beira: a menor entre o fogo e os que se tocam com ele (a tocha de quem está junto da fogueira,
        /// duas tochas lado a lado), para a horda em volta deles contar como uma só.
        /// </summary>
        internal int ChaveDoGrupo(in Fogo fogo)
        {
            int chave = fogo.chave;
            for (int i = 0; i < Fogos.Count; i++)
            {
                var f = Fogos[i];
                Vector3 d = f.centro - fogo.centro;
                if (Mathf.Abs(d.y) > 3f) continue;
                d.y = 0f;
                if (d.sqrMagnitude < (f.raio + fogo.raio) * (f.raio + fogo.raio) && f.chave < chave) chave = f.chave;
            }
            return chave;
        }

        /// <summary>
        /// O fogo que importa para quem está nos pés <paramref name="pes"/>: o mais "por dentro" entre os que chegam até ele
        /// (o raio mais a <paramref name="margem"/>), à vista (sem parede no meio).
        /// </summary>
        internal bool FogoPerto(Vector3 pes, float margem, out Fogo fogo)
        {
            fogo = default;
            float melhor = float.MaxValue;
            bool achou = false;
            for (int i = 0; i < Fogos.Count; i++)
            {
                var f = Fogos[i];
                Vector3 d = pes - f.centro;
                if (Mathf.Abs(d.y) > 3f) continue;
                d.y = 0f;
                float folga = d.magnitude - f.raio;   // negativa: dentro
                if (folga > margem || folga >= melhor) continue;
                if (!Enxerga(f.centro + Vector3.up * AlturaDoFogo, pes + Vector3.up * 1.2f)) continue;
                melhor = folga;
                fogo = f;
                achou = true;
            }
            return achou;
        }

        /// <summary>
        /// Quanto a horda espera na beira até criar coragem (s): <see cref="AjustesDeZumbi.esperaAteACoragem"/>; à noite, numa
        /// fogueira (o grupo com uma fogueira tem a chave dela), <see cref="AjustesDeZumbi.esperaAteACoragemDeNoiteNaFogueira"/>:
        /// o Long da fogueira vem antes (ZombieManager.LongNaFogueira.cs).
        /// </summary>
        internal float EsperaDaCoragem(int chave) =>
            !EDia && chave < ChaveDaTocha ? ajustes.esperaAteACoragemDeNoiteNaFogueira : ajustes.esperaAteACoragem;

        /// <summary>
        /// O zumbi rodeia a beira do fogo, esperando quem está dentro. Devolve true quando a horda que espera ali criou
        /// coragem: com <see cref="AjustesDeZumbi.hordaQueEntraNoFogo"/> ou mais esperando por
        /// <see cref="EsperaDaCoragem"/> s, todos os que esperam entram (os que pensam nos 5 s seguintes).
        /// </summary>
        internal bool EsperarNoFogo(int chave, Zumbi z, float agora, int chaveAntes)
        {
            var a = ajustes;
            if (!_esperas.TryGetValue(chave, out var e)) _esperas[chave] = e = new EsperaNoFogo();
            // a horda mudou de chave (a tocha encostou na fogueira ou saiu dela): o tempo de espera vem junto
            if (chaveAntes != chave && chaveAntes != SemEspera && _esperas.TryGetValue(chaveAntes, out var velha))
            {
                if (velha.desde >= 0f && (e.desde < 0f || velha.desde < e.desde)) e.desde = velha.desde;
                if (velha.coragemAte > e.coragemAte) e.coragemAte = velha.coragemAte;
            }
            e.quem[z] = agora;
            if (agora < e.coragemAte) return true;
            if (QuantosEsperam(e, agora) >= a.hordaQueEntraNoFogo)
            {
                e.abaixoDesde = -1f;
                if (e.desde < 0f) e.desde = agora;
                if (agora - e.desde >= EsperaDaCoragem(chave))
                {
                    e.coragemAte = agora + 5f;
                    e.desde = -1f;
                    return true;
                }
            }
            else if (e.desde >= 0f)
            {
                // um ou outro some por um instante (recuando, dando a volta): a conta só zera depois de 3 s abaixo
                if (e.abaixoDesde < 0f) e.abaixoDesde = agora;
                else if (agora - e.abaixoDesde > 3f)
                {
                    e.desde = -1f;
                    e.abaixoDesde = -1f;
                }
            }
            return false;
        }

        // os que esperaram ali no último segundo e meio (cada um pensa a cada 0,1 a 0,5 s)
        static int QuantosEsperam(EsperaNoFogo e, float agora)
        {
            int n = 0;
            foreach (var t in e.quem.Values)
                if (agora - t < 1.5f) n++;
            return n;
        }

        // a cada 2 s: tira quem não espera mais (morreu, foi embora, entrou) e os fogos sem ninguém
        void LimparEsperas()
        {
            float agora = Time.time;
            if (agora < _proximaLimpezaDasEsperas || _esperas.Count == 0) return;
            _proximaLimpezaDasEsperas = agora + 2f;
            _esperasVelhas.Clear();
            foreach (var kv in _esperas)
            {
                _quemSaiu.Clear();
                foreach (var q in kv.Value.quem)
                    if (q.Key == null || agora - q.Value > 3f) _quemSaiu.Add(q.Key);
                foreach (var z in _quemSaiu) kv.Value.quem.Remove(z);
                if (kv.Value.quem.Count == 0 && agora >= kv.Value.coragemAte) _esperasVelhas.Add(kv.Key);
            }
            foreach (var k in _esperasVelhas) _esperas.Remove(k);
        }

        /// <summary>O F1: os fogos agora e quem espera na beira de cada um (e quanto falta para a coragem).</summary>
        public string ResumoDosFogos(Vector3 perto)
        {
            if (Fogos.Count == 0) return "fogos: nenhum aceso";
            var a = ajustes;
            float agora = Time.time;
            var sb = new System.Text.StringBuilder();
            sb.Append($"fogos: {Fogos.Count}");
            foreach (var f in Fogos)
            {
                if ((f.centro - perto).sqrMagnitude > 60f * 60f) continue;
                string nome = f.chave >= ChaveDaTocha ? "tocha" : $"fogueira {f.chave}";
                int n = 0;
                string coragem = "";
                int grupo = ChaveDoGrupo(f);
                if (_esperas.TryGetValue(grupo, out var e))
                {
                    n = QuantosEsperam(e, agora);
                    coragem = agora < e.coragemAte ? " · CORAGEM" : e.desde >= 0f ? $" · coragem em {Mathf.Max(0f, EsperaDaCoragem(grupo) - (agora - e.desde)):0} s" : "";
                }
                sb.Append($"\n  {nome}: {n} na beira{coragem}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// O facho que bate neste ponto (o peito do zumbi): dentro do alcance e do ângulo, sem parede no meio. De dia, a
        /// luz conta em qualquer sombra (debaixo de um teto, na sombra dos prédios, debaixo das copas); no sol, não (o sol
        /// apaga o facho, e o zumbi no sol já está fugindo). Até 29/10/2026 era só debaixo de um teto, e na cidade grande os
        /// zumbis que passam o dia na sombra dos prédios, na rua, não ligavam para a lanterna (o bug que o Pedro viu).
        /// </summary>
        internal bool NoFacho(Vector3 peito, out Facho facho) => NoFacho(peito, out facho, true);

        /// <summary>O mesmo, com ou sem os fogos (a esquiva de lado é só das lanternas e dos faróis).</summary>
        internal bool NoFacho(Vector3 peito, out Facho facho, bool comFogo)
        {
            facho = default;
            bool dia = EDia;
            for (int i = 0; i < Fachos.Count; i++)
            {
                var f = Fachos[i];
                if (f.fogo && !comFogo) continue;
                Vector3 para = peito - f.origem;
                if (f.fogo && Mathf.Abs(para.y) > 3f) continue;   // outro andar
                if (f.carro || f.fogo) para.y = 0f;
                float d = para.magnitude;
                if (d > f.alcance || d < 0.3f) continue;
                if (Vector3.Dot(para / d, f.direcao) < f.cosMeio) continue;
                if (!Enxerga(f.origem, peito)) continue;
                if (dia && Exposto(peito - Vector3.up * 1.2f)) continue;   // no sol (o ponto é o peito; o Exposto quer os pés)
                facho = f;
                return true;
            }
            return false;
        }
    }
}
