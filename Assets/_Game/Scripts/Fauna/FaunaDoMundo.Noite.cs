using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// A noite e os detalhes no gerenciador (a etapa 4, 12/11/2026; as mentes no Bicho.Noite.cs):
    /// - os bichos da noite (o sapo, o guaxinim, o gambá) só nascem à noite e, de dia, somem quando ninguém está a 60 m;
    /// - a árvore mais perto (do esquilo e do guaxinim que sobem; do peru que dorme), pela floresta da semente;
    /// - as lixeiras da cidade (DadosDoMundo.Objetos) e o lixo espalhado que o guaxinim deixa (um dia do jogo; vai na
    ///   mensagem das casas);
    /// - o espirro do gambá (o cheiro em quem está atrás dele) e, a cada quadro, o cheiro e o veneno correndo;
    /// - os urubus: em cima dos restos (os corpos dos zumbis que ficam, Zombies/RestosDosCorpos.cs, e as carcaças), um bando
    ///   que chega de longe e roda alto; descem quando não há ninguém (e comem a carcaça).
    /// </summary>
    public sealed partial class FaunaDoMundo
    {
        // ------------------------------------------------------------------ as árvores

        /// <summary>A árvore em pé mais perto do ponto (a até 'raio' m): o pé e o raio do tronco.</summary>
        internal static bool ArvoreMaisPerto(Vector3 p, float raio, out Vector3 pe, out float raioDoTronco) =>
            ArvoresDoMundo.MaisPerto(p, raio, out _, out pe, out raioDoTronco);

        /// <summary>O bicho vivo da espécie mais perto de 'de', a até 'raio' m (a cascavel e o sapo).</summary>
        internal Bicho PresaPerto(Bicho de, Especie e, float raio)
        {
            Bicho melhor = null;
            float m = raio * raio;
            foreach (var b in _ativos)
            {
                if (b == de || b.Morto || b.Especie != e || b.Exposto) continue;
                float d = (b.transform.position - de.transform.position).sqrMagnitude;
                if (d < m) { m = d; melhor = b; }
            }
            return melhor;
        }

        // ------------------------------------------------------------------ as lixeiras

        /// <summary>Quanto o lixo espalhado fica (s reais: um dia do jogo).</summary>
        const float LixoDura = 24f * 60f;
        readonly List<(Vector3 pos, float ate)> _lixos = new List<(Vector3, float)>();

        /// <summary>A lixeira da cidade mais perto (a até 'raio' m) que não está revirada agora.</summary>
        internal bool LixeiraPerto(Vector3 p, float raio, out Vector3 lixeira)
        {
            lixeira = default;
            var d = DadosDoMapa;
            if (d == null) return LixeiraDoLakewood(p, raio, out lixeira);
            if (d.Objetos == null) return false;
            float melhor = raio * raio;
            bool achou = false;
            foreach (var o in d.Objetos.Todos)
            {
                if (o.Tipo != TipoDeObjeto.Lixeira) continue;
                float dd = (o.Posicao - new Vector2(p.x, p.z)).sqrMagnitude;
                if (dd >= melhor) continue;
                var c = new Vector3(o.Posicao.x, d.Altura(o.Posicao.x, o.Posicao.y), o.Posicao.y);
                if (Revirada(c)) continue;
                melhor = dd;
                lixeira = c;
                achou = true;
            }
            return achou;
        }

        // no Lakewood (a parte 2 do "O jogo do mundo sorteado no Lakewood"): a lata de lixo na frente de cada casa, na beira do
        // lote do lado da rua (ela não é desenhada: o guaxinim espalha o lixo ali)
        bool LixeiraDoLakewood(Vector3 p, float raio, out Vector3 lixeira)
        {
            lixeira = default;
            var chao = ChaoDoLakewood;
            if (chao == null) return false;
            float melhor = raio * raio;
            bool achou = false;
            foreach (var l in chao.Lotes)
            {
                if (l.Plano == null || l.Lote.tipo != UsoDoLote.Casa) continue;
                var q = l.Lote.NoMapa(l.Lote.largura * 0.35f, -l.Lote.fundo * 0.5f + 0.8f);
                float dd = (q - new Vector2(p.x, p.z)).sqrMagnitude;
                if (dd >= melhor) continue;
                var c = new Vector3(q.x, chao.Altura(q.x, q.y), q.y);
                if (Revirada(c)) continue;
                melhor = dd;
                lixeira = c;
                achou = true;
            }
            return achou;
        }

        bool Revirada(Vector3 c)
        {
            float agora = Time.time;
            foreach (var l in _lixos) if (l.ate > agora && (l.pos - c).sqrMagnitude < 4f) return true;
            return false;
        }

        /// <summary>O guaxinim derrubou e revirou a lixeira: o lixo fica espalhado (um dia do jogo).</summary>
        internal void ReviraLixeira(Vector3 c)
        {
            float agora = Time.time;
            _lixos.RemoveAll(l => l.ate < agora);
            if (Revirada(c)) return;
            _lixos.Add((c, agora + LixoDura));
            if (_lixos.Count > 30) _lixos.RemoveAt(0);
        }

        // ------------------------------------------------------------------ o gambá e a cascavel (o cheiro e o veneno)

        /// <summary>O espirro do gambá: o cheiro em quem está a até 4,5 m atrás dele (no cone da cauda).</summary>
        internal void EspirroDoGamba(Bicho g)
        {
            Vector3 p = g.transform.position, atras = -g.transform.forward;
            foreach (var gente in Pessoas)
            {
                if (!gente.valido) continue;
                Vector3 para = gente.pes - p;
                para.y = 0f;
                if (para.magnitude > 4.5f || Vector3.Dot(para.normalized, atras) < 0.3f) continue;
                gente.j.ReceberFedor();
            }
        }

        // no anfitrião, a cada quadro: o cheiro e o veneno de cada jogador correm
        void CheiroEVeneno(float dt)
        {
            foreach (var g in Pessoas)
            {
                if (g.j == null) continue;
                g.j.CorrerFedor(dt);
                if (g.j.Vida != null) g.j.Vida.CorrerVeneno(dt);
            }
        }

        // ------------------------------------------------------------------ os bichos da noite

        /// <summary>O bicho da noite não nasce de dia; o que sobrou some de dia, quando ninguém está a 60 m.</summary>
        static bool NasceAgora(DadosDaEspecie d) => !d.noturno || CicloDoDia.Noite > 0.5f;

        void SumirDeDia()
        {
            if (CicloDoDia.Noite > 0.3f) return;
            for (int i = _ativos.Count - 1; i >= 0; i--)
            {
                var b = _ativos[i];
                if (b.Morto || b.Exposto || !b.Dados.noturno || MaisPerto(b.transform.position) < 60f) continue;
                Remover(b);
            }
        }

        // ------------------------------------------------------------------ os urubus

        /// <summary>Os urubus chegam a um resto depois deste tempo (s): o cheiro e a vista do alto demoram.</summary>
        const float UrubusDemoram = 25f;

        /// <summary>O resto do grupo de urubus ainda está lá? (o corpo do zumbi ainda não sumiu; a carcaça, nem carneada nem podre)</summary>
        internal bool RestoValido(Grupo g)
        {
            if (!g.temResto) return false;
            if (g.restoBicho != null) return g.restoBicho.Morto && !g.restoBicho.Carneado;
            return RestosDosCorpos.Existe(g.resto, 3f);
        }

        /// <summary>Um urubu comendo: a carcaça perde um pouco (o corpo do zumbi não muda).</summary>
        internal void Bicar(Grupo g, float dt)
        {
            if (g.restoBicho != null && g.restoBicho.Morto) g.restoBicho.Restante = Mathf.Max(0.25f, g.restoBicho.Restante - 0.0008f * Mathf.Max(0.3f, dt));
        }

        // a cada volta do povoamento: um bando para um resto (perto de alguém, velho o bastante, sem urubus ainda)
        void Urubus(float agora)
        {
            if (Pessoas.Count == 0 || Random.value > 0.35f) return;
            Vector3 lugar = default;
            Bicho carcaca = null;
            bool achou = false;
            // os corpos dos zumbis que ficam (no anfitrião)
            foreach (var r in RestosDosCorpos.Lista)
            {
                if (agora - r.desde < UrubusDemoram || MaisPerto(r.pos) > 170f || TemUrubus(r.pos)) continue;
                lugar = r.pos;
                achou = true;
                break;
            }
            // as carcaças dos bichos
            if (!achou)
                foreach (var b in _ativos)
                {
                    if (!b.Morto || b.Carneado || b.Dados.pontos < 15f || b.Especie == Especie.Urubu) continue;
                    if ((HorasDoJogo - b.MorreuNaHora) * 60.0 < UrubusDemoram || MaisPerto(b.transform.position) > 170f || TemUrubus(b.transform.position)) continue;
                    lugar = b.transform.position;
                    carcaca = b;
                    achou = true;
                    break;
                }
            if (!achou) return;
            // o bando chega de longe, alto
            Vector3 de = lugar + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(70f, 110f);
            int n = Random.Range(3, 7);
            var g = new Grupo { especie = Especie.Urubu, pasto = lugar, temResto = true, resto = lugar, restoBicho = carcaca };
            Grupos.Add(g);
            int nasceram = 0;
            for (int i = 0; i < n; i++)
            {
                var off = new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
                var b = Nascer(Especie.Urubu, Variante.Adulto, de + off, Random.Range(0f, 360f), g);
                if (b == null) continue;
                b.LugarNoGrupo = off;
                b.PorNoAr(Random.Range(35f, 45f));
                nasceram++;
            }
            if (nasceram == 0) Grupos.Remove(g);
        }

        bool TemUrubus(Vector3 p)
        {
            foreach (var g in Grupos) if (g.temResto && (g.resto - p).sqrMagnitude < 20f * 20f) return true;
            return false;
        }

        /// <summary>O F1: urubus agora no resto mais perto (o corpo de um zumbi ou uma carcaça), sem esperar.</summary>
        public string UrubusDeTeste(JogadorRede j)
        {
            if (!_servidor || j == null) return "Só o anfitrião.";
            Vector3 lugar = default;
            Bicho carcaca = null;
            float m = float.MaxValue;
            foreach (var r in RestosDosCorpos.Lista)
            {
                float d = Vector3.Distance(r.pos, j.transform.position);
                if (d < m) { m = d; lugar = r.pos; }
            }
            foreach (var b in _ativos)
            {
                if (!b.Morto || b.Carneado) continue;
                float d = Vector3.Distance(b.transform.position, j.transform.position);
                if (d < m) { m = d; lugar = b.transform.position; carcaca = b; }
            }
            if (m > 200f) return "Nenhum corpo de zumbi nem carcaça a até 200 m (mate um zumbi ou um bicho antes).";
            Vector3 de = lugar + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * 60f;
            var g = new Grupo { especie = Especie.Urubu, pasto = lugar, temResto = true, resto = lugar, restoBicho = carcaca };
            Grupos.Add(g);
            int n = 0;
            for (int i = 0; i < 5; i++)
            {
                var off = new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
                var b = Nascer(Especie.Urubu, Variante.Adulto, de + off, Random.Range(0f, 360f), g);
                if (b == null) continue;
                b.LugarNoGrupo = off;
                b.PorNoAr(35f);
                n++;
            }
            return $"{n} urubus indo para {(carcaca != null ? "a carcaça" : "o corpo")} a {m:0} m (descem quando ninguém está a 30 m).";
        }
    }
}
