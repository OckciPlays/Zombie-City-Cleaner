using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// Os predadores no gerenciador (a etapa 2, 12/11/2026; a mente no Bicho.Matilha.cs e no Bicho.Urso.cs): quem a matilha
    /// pode atacar, o fogo que a segura (a lista de fogos do ZombieManager: a tocha, o lampião, a fogueira, o molotov), a
    /// caça das presas (o sorteio do líder, a mordida, o uivo), o golpe no jogador e o teste do F1.
    /// </summary>
    public sealed partial class FaunaDoMundo
    {
        /// <summary>À noite (o escuro acima disto) a matilha caça e ataca.</summary>
        public const float NoiteDaMatilha = 0.55f;
        /// <summary>Sozinho: nenhum outro jogador a esta distância.</summary>
        public const float SozinhoA = 30f;

        /// <summary>O ponto está dentro de um fogo que afasta os zumbis (o centro e o raio dele).</summary>
        internal bool NoFogo(Vector3 p, out Vector3 centro, out float raio)
        {
            centro = default;
            raio = 0f;
            var zm = ZombieManager.Instancia;
            if (zm == null) return false;
            foreach (var fogo in zm.Fogos)
            {
                Vector3 d = fogo.centro - p;
                if (Mathf.Abs(d.y) > 3f) continue;
                d.y = 0f;
                if (d.sqrMagnitude > fogo.raio * fogo.raio) continue;
                centro = fogo.centro;
                raio = fogo.raio;
                return true;
            }
            return false;
        }

        /// <summary>Ninguém mais a <see cref="SozinhoA"/> m do jogador.</summary>
        internal static bool Sozinho(JogadorRede j)
        {
            foreach (var o in JogadorRede.Todos)
                if (o != null && o != j && o.AlvoValido && (o.transform.position - j.transform.position).sqrMagnitude < SozinhoA * SozinhoA) return false;
            return true;
        }

        static bool Ferido(JogadorRede j) => j.Vida != null && (j.Vida.Vida.Value < 50f || j.Vida.Corpo.Value.AlgumSangrando);

        static int Adultos(Grupo g)
        {
            int n = 0;
            foreach (var m in g.membros) if (m != null && !m.Morto && m.Variante != Variante.Filhote && m.Estado != EstadoDoBicho.Fugindo) n++;
            return n;
        }

        /// <summary>A matilha pode cercar este jogador agora? (à noite, em matilha, ferido ou sozinho, longe do fogo, fora do carro e do teto)</summary>
        internal bool PodeAtacar(Grupo g, JogadorRede j)
        {
            if (j == null || !j.AlvoValido || j.Vida == null || j.Vida.Caido.Value) return false;
            if (g.desfeita) return false;
            // (20/11/2026, a pedido: "o lobo não está atacando") de dia, o lobo também cerca quem chega perto dele (o líder a
            // menos de 40 m; o lobo foge a 30 m, e é aí que a matilha decide: Bicho.Matilha.cs), sozinho ou ferido; o coiote
            // continua só à noite
            if (CicloDoDia.Noite < NoiteDaMatilha && (g.especie != Especie.Lobo || g.lider == null
                || Vector3.Distance(g.lider.transform.position, j.transform.position) > 40f)) return false;
            if (Adultos(g) < (g.especie == Especie.Coiote ? 3 : 2)) return false;
            if (j.Passageiro != null && j.Passageiro.NoCarro) return false;
            if (j.DebaixoDeTeto.Value || j.Vida.Abrigado()) return false;
            if (NoFogo(j.transform.position, out _, out _)) return false;
            return Ferido(j) || Sozinho(j);
        }

        /// <summary>Um da matilha notou o jogador: o sorteio de cercar (no máximo um a cada 5 s por matilha).</summary>
        internal void MatilhaViuGente(Grupo g, JogadorRede j, float agora, bool agoraMesmo = false)
        {
            // (agoraMesmo: a matilha que ia fugir de quem chegou perto decide na hora, sem esperar os 5 s; os lobos que chegam
            // juntos à beira contam como uma só decisão)
            if (g.alvo != null || agora < g.largaAte || agora - g.pensouNoAlvo < (agoraMesmo ? 1.5f : 5f)) return;
            g.pensouNoAlvo = agora;
            if (!PodeAtacar(g, j)) return;
            // a sorte: o lobo cerca mais que o coiote (o ferido chama mais)
            float chance = (g.especie == Especie.Coiote ? 0.35f : CicloDoDia.Noite < NoiteDaMatilha ? 0.4f : 0.6f) + (Ferido(j) ? 0.2f : 0f);
            if (Random.value > chance) return;
            g.alvo = j;
            g.presa = null;
            g.carcaca = null;
            g.atacante = null;
            g.proximoBote = agora + Random.Range(3f, 6f);
            g.rondaGiro = Random.Range(0f, 360f);
            LadoDosFilhotes(g, j);
            if (g.especie == Especie.Lobo) ChamarAMatilha(g, agora);   // (20/11/2026: o líder uiva chamando todos)
            else if (g.lider != null) g.lider.Tocar(SomDoBicho.Rosnado);
            if (j.Vida != null) j.Vida.AbalarAgora(g.especie == Especie.Coiote ? 4f : 7f, MotivoDaMente.Susto);
        }

        /// <summary>O cerco continua? (o fogo não larga: segura na beira; a manhã, o carro, o teto, a queda e a matilha sem gente largam)</summary>
        internal bool PodeContinuarAtaque(Grupo g, JogadorRede j, float agora)
        {
            if (j == null || !j.AlvoValido || j.Vida == null || j.Vida.Caido.Value) return false;
            // (o lobo também de dia, 20/11/2026; o coiote de dia só no revide)
            if ((!g.cercoDeTeste && agora >= g.revideAte && g.especie != Especie.Lobo && CicloDoDia.Noite < NoiteDaMatilha - 0.1f) || g.desfeita) return false;
            if (Adultos(g) < (agora < g.revideAte ? 1 : 2)) return false;   // (atacada, até o último lobo briga)
            if (j.Passageiro != null && j.Passageiro.NoCarro) return false;
            if (j.DebaixoDeTeto.Value || j.Vida.Abrigado()) return false;
            if (g.lider != null && Vector3.Distance(g.lider.transform.position, j.transform.position) > 90f) return false;
            return true;
        }

        /// <summary>A matilha larga o jogador (e, se 'fugir', foge do ponto: o tiro, o ferido); não volta tão cedo.</summary>
        internal void LargarAlvo(Grupo g, float agora, Vector3 de, bool fugir)
        {
            g.alvo = null;
            g.atacante = null;
            g.largaAte = agora + (fugir ? Random.Range(60f, 120f) : 30f);
            g.cercoDeTeste = false;
            g.revideAte = g.chamadoAte = -1f;
            foreach (var m in g.membros)
                if (m != null && !m.Morto) m.Espantar(de, agora, fugir);
        }

        /// <summary>
        /// (20/11/2026, a pedido: "quando atacamos ele, independe ser de dia ou de noite, se estou ferido ou não, ele e a
        /// alcateia deveria atacar, os filhotes recuados para trás") alguém atacou a matilha de lobos (o tiro ou o golpe num
        /// deles, um deles morto): o líder uiva chamando todos, e a matilha cerca quem atacou por 60 s (cada novo ataque
        /// estica), de dia ou de noite, sem o tiro espantar. O fogo, o carro, o teto e a distância ainda seguram, e o alfa
        /// morto ainda desfaz a matilha. Desde 20/11/2026 o coiote também (o chamado dele é o coro de latidos).
        /// </summary>
        internal void MatilhaAtacada(Grupo g, JogadorRede autor, float agora)
        {
            // (20/11/2026, a pedido: os coiotes também "não atacam quando eu ataco") o lobo e o coiote
            if (g == null || autor == null || g.desfeita || (g.especie != Especie.Lobo && g.especie != Especie.Coiote)) return;
            if (!autor.AlvoValido || autor.Vida == null || autor.Vida.Caido.Value) return;
            g.revideAte = agora + 60f;
            if (g.alvo == autor) return;   // (já cercando quem atacou: só estica o revide)
            g.alvo = autor;
            g.presa = null;
            g.carcaca = null;
            g.atacante = null;
            g.largaAte = 0f;
            g.cercoDeTeste = false;
            g.rondaGiro = Random.Range(0f, 360f);
            LadoDosFilhotes(g, autor);
            ChamarAMatilha(g, agora);
            foreach (var m in g.membros)
                if (m != null && !m.Morto) m.AtenderOChamado(agora);
            if (autor.Vida != null) autor.Vida.AbalarAgora(8f, MotivoDaMente.Susto);
        }

        // o líder (vivo e perto) uiva chamando a matilha: parado uns 4 s; os botes começam depois do uivo
        void ChamarAMatilha(Grupo g, float agora)
        {
            var l = g.lider;
            if (l == null || l.Morto)
            {
                g.proximoBote = Mathf.Max(g.proximoBote, agora + 1.5f);
                return;
            }
            l.ComecarUivo(this, agora, 4f);
            g.chamadoAte = agora + 4f;
            g.proximoBote = Mathf.Max(g.proximoBote, g.chamadoAte + Random.Range(0.5f, 1.5f));
        }

        // os filhotes ficam do lado em que a matilha estava, longe de quem ela cerca
        static void LadoDosFilhotes(Grupo g, JogadorRede j)
        {
            Vector3 lado = g.Centro - j.transform.position;
            lado.y = 0f;
            g.ladoDosFilhotes = lado.sqrMagnitude > 0.5f ? lado.normalized : -j.transform.forward;
        }

        /// <summary>O alfa (ou o líder) da matilha morreu: ela se desfaz (todos fogem e não atacam mais por uns minutos).</summary>
        void AlfaMorreu(Grupo g, float agora, Vector3 de)
        {
            g.desfeita = true;
            LargarAlvo(g, agora, de, true);
            g.largaAte = agora + 300f;
            g.presa = null;
            g.carcaca = null;
        }

        // ------------------------------------------------------------------ a caça das presas

        /// <summary>
        /// O líder pensa em caçar (a cada 15–30 s): só com alguém perto para ver (a caça é pela imersão: os bichos não morrem
        /// de fome), por sorteio (à noite mais), a presa mais fácil a até 100 m. O lobo uiva antes, à noite.
        /// </summary>
        internal void TentarCaca(Grupo g, float agora)
        {
            g.proximaCaca = agora + Random.Range(15f, 30f);
            if (g.alvo != null || g.carcaca != null || g.desfeita) return;
            Vector3 c = g.Centro;
            if (!PertoDeAlguem(c, 160f)) return;
            float noite = CicloDoDia.Noite;
            if (Random.value > (noite > 0.5f ? 0.5f : 0.15f)) return;
            var presa = MelhorPresa(g.especie, c, 100f);
            if (presa == null) return;
            ComecarCaca(g, presa, agora);
        }

        void ComecarCaca(Grupo g, Bicho presa, float agora)
        {
            g.presa = presa;
            g.correndo = false;
            g.cacaDesde = agora;
            g.cacaAte = agora + 60f;   // (a espreita; o disparo conta de novo: Bicho.Matilha.cs)
            if (g.especie == Especie.Lobo && CicloDoDia.Noite > 0.5f && g.lider != null) Uivar(g.lider);
            else if (g.especie == Especie.Coiote && CicloDoDia.Noite > 0.5f && g.lider != null && Random.value < 0.5f) Uivar(g.lider);
        }

        // a presa mais fácil: o filhote e o ferido valem como se estivessem mais perto
        Bicho MelhorPresa(Especie predador, Vector3 c, float raio)
        {
            Bicho melhor = null;
            float nota = float.MaxValue;
            foreach (var b in _ativos)
            {
                if (b.Morto || b.Exposto || !EhPresa(predador, b)) continue;
                float d = Vector3.Distance(c, b.transform.position);
                if (d > raio) continue;
                float n = d * (b.Variante == Variante.Filhote ? 0.6f : 1f) * (b.Ferido ? 0.5f : 1f);
                if (n < nota) { nota = n; melhor = b; }
            }
            return melhor;
        }

        static bool EhPresa(Especie predador, Bicho b) => predador switch
        {
            Especie.Lobo => b.Especie == Especie.Veado || b.Especie == Especie.Coelho || (b.Especie == Especie.Alce && b.Variante == Variante.Filhote),
            Especie.Coiote => b.Especie == Especie.Coelho || (b.Especie == Especie.Veado && b.Variante == Variante.Filhote),
            _ => false,
        };

        /// <summary>A mordida do predador na presa: ela sangra e foge, até cair (a morte natural não baixa a população da região).</summary>
        internal void MorderPresa(Bicho predador, Bicho presa, float agora)
        {
            if (presa.Morto) return;
            float dano = predador.Dados.mordida * (predador.Variante == Variante.Filhote ? 0.4f : 1f) * Random.Range(1.6f, 2.6f);
            presa.Vida -= dano;
            predador.Tocar(SomDoBicho.Mordida);
            if (presa.Vida <= 0f)
            {
                if (presa.Especie == Especie.Coelho) presa.Tocar(SomDoBicho.Guincho);
                Matar(presa, null, false);
            }
            else presa.Ferir(dano, predador.transform.position, agora);
        }

        /// <summary>O uivo (o lobo; o coro do coiote): de longe, à noite, pesa um pouco na mente de quem ouve.</summary>
        internal void Uivar(Bicho b)
        {
            b.Tocar(b.Especie == Especie.Coiote ? SomDoBicho.Latido : SomDoBicho.Uivo);
            if (CicloDoDia.Noite < 0.5f) return;
            foreach (var g in Pessoas)
                if (g.valido && g.j.Vida != null && (g.pes - b.transform.position).sqrMagnitude < 250f * 250f)
                    g.j.Vida.AbalarAgora(b.Especie == Especie.Coiote ? 1.5f : 2.5f, MotivoDaMente.Tensao);
        }

        // ------------------------------------------------------------------ o F1

        /// <summary>
        /// O F1: uma matilha (o lobo ou o coiote) caçando agora. As presas (os veados ou os coelhos) a uns 45 m na frente,
        /// a matilha a uns 70 m, já espreitando a mais fácil (sem o sorteio), e ninguém nota quem pediu por 45 s.
        /// </summary>
        public string CacaDeTeste(JogadorRede j, Especie predador)
        {
            if (!_servidor || j == null) return "Só o anfitrião.";
            if (predador != Especie.Lobo && predador != Especie.Coiote) return "Só o lobo e o coiote caçam.";
            Vector3 frente = j.transform.forward;
            frente.y = 0f;
            frente = frente.sqrMagnitude > 0.01f ? frente.normalized : Vector3.forward;
            Vector3 lado = Vector3.Cross(Vector3.up, frente);
            var presaDe = predador == Especie.Lobo ? Especie.Veado : Especie.Coelho;
            int presas = NascerGrupo(presaDe, j.transform.position + frente * 45f, true);
            var gp = presas > 0 ? Grupos[Grupos.Count - 1] : null;
            int caca = NascerGrupo(predador, j.transform.position + frente * 45f + lado * 55f, true);
            if (presas == 0 || caca == 0) return "Não deu para pôr (sem lugar livre).";
            var g = Grupos[Grupos.Count - 1];
            // (todos distraídos de quem pediu por 45 s: a caça acontece na frente dele)
            foreach (var m in g.membros) if (m != null) m.Distrair(Time.time + 45f);
            foreach (var m in gp.membros) if (m != null) m.Distrair(Time.time + 45f);
            var presa = MelhorPresa(predador, g.Centro, 150f);
            if (presa == null) return "Sem presa perto.";
            ComecarCaca(g, presa, Time.time);
            return $"{caca} {Especies.De(predador).plural} caçando {presas} {Especies.De(presaDe).plural} a uns 45 m na frente (sem notar você por 45 s).";
        }

        /// <summary>O F1: a matilha mais perto cerca o jogador agora (sem esperar a noite e o sorteio).</summary>
        /// <summary>O F1 (20/11/2026): o lobo vivo mais perto uiva agora, com a pose.</summary>
        public string UivoDeTeste(JogadorRede j)
        {
            if (!_servidor || j == null) return "Só o anfitrião.";
            Bicho melhor = null;
            float m = float.MaxValue;
            foreach (var b in _ativos)
            {
                if (b.Morto || b.Especie != Especie.Lobo) continue;
                float d = Vector3.Distance(b.transform.position, j.transform.position);
                if (d < m) { m = d; melhor = b; }
            }
            if (melhor == null) return "Nenhum lobo por perto (ponha lobos antes).";
            melhor.ComecarUivo(this, Time.time);
            return $"{melhor.Dados.NomeDaVariante(melhor.Variante)} a {m:0} m uivando.";
        }

        public string CercarDeTeste(JogadorRede j)
        {
            if (!_servidor || j == null) return "Só o anfitrião.";
            Grupo melhor = null;
            float m = float.MaxValue;
            foreach (var g in Grupos)
            {
                if (g.especie != Especie.Lobo && g.especie != Especie.Coiote) continue;
                float d = Vector3.Distance(g.Centro, j.transform.position);
                if (d < m) { m = d; melhor = g; }
            }
            if (melhor == null) return "Nenhuma matilha por perto (ponha lobos ou coiotes antes).";
            melhor.alvo = j;
            melhor.cercoDeTeste = true;
            melhor.desfeita = false;
            melhor.presa = null;
            melhor.carcaca = null;
            melhor.atacante = null;
            melhor.proximoBote = Time.time + 3f;
            foreach (var b in melhor.membros) if (b != null) b.Distrair(0f);
            return $"{Especies.De(melhor.especie).plural} a {m:0} m cercando você (sem o fogo: eles esperam na beira).";
        }
    }
}
