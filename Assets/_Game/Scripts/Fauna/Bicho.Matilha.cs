using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// A mente da matilha (a etapa 2, 12/11/2026: o lobo-cinzento e o coiote), no anfitrião:
    /// - vaga atrás do líder (de dia, deita para descansar; ativa no crepúsculo e à noite);
    /// - de tempo em tempo, por sorteio e só perto de alguém (a caça é para ser vista), o líder escolhe uma presa (o veado,
    ///   melhor o filhote ou o ferido; o coiote, o coelho e o filhote): a matilha corre de lado, cerca e morde até derrubar,
    ///   e come na carcaça por uns minutos, deixando menos carne. À noite, o lobo uiva antes;
    /// - a gente: de dia, foge; à noite, só ataca em matilha quem está ferido (sangrando ou com a vida abaixo da metade) ou
    ///   sozinho (ninguém a 30 m), longe do fogo (a tocha, o lampião, a fogueira: esperam na beira, como os zumbis), fora
    ///   do carro e do teto. Cerca numa roda, rosna e um de cada vez dá o bote, pelos lados e por trás (a mordida nas
    ///   pernas e nos braços: o corte, o sangramento e a infecção do sistema dos membros);
    /// - o tiro espanta com chance (o coiote mais), o ferido foge, e matar o alfa desfaz a matilha.
    /// Os zumbis e o urso a espantam (os sentidos do FaunaDoMundo.Sentidos.cs).
    /// </summary>
    public sealed partial class Bicho
    {
        float _boteAte, _proximaMordida, _folego, _gentoAte;
        bool _mordeu;

        bool Coiote => Especie == Especie.Coiote;

        void PensarMatilha(FaunaDoMundo f, float agora, float dt)
        {
            var g = Grupo;
            Vector3 p = transform.position;

            // o que não é gente (os zumbis, o urso, o lobo para o coiote): a matilha larga tudo e foge
            if (f.Perceber(this, out var de, out _, out _, true))
            {
                _perigo = de;
                _perigoAte = agora + 6f;
                if (g != null)
                {
                    g.alvo = null;
                    g.presa = null;
                    g.atacante = null;
                }
                AvisarGrupo(de, agora, true);
            }
            // a gente (o distraído do F1 ainda não nota ninguém)
            if (agora >= _distraidoAte && f.PerceberGente(this, out _, out var quem))
            {
                _alvo = quem;
                _gentoAte = agora + 15f;
                if (g != null) f.MatilhaViuGente(g, quem, agora);
            }

            if (Estado == EstadoDoBicho.Fugindo)
            {
                if (agora < _fugaAte) { Fugir(agora, false); return; }
                if (agora < _perigoAte && Vector3.Distance(p, _perigo) < Dados.fuga) { Fugir(agora, true); return; }
                Estado = EstadoDoBicho.Andando;
                _velAlvo = Dados.trotar;
            }
            // o zumbi ou o urso perto: foge
            if (agora < _perigoAte && Vector3.Distance(p, _perigo) < Dados.visao * 0.6f)
            {
                Fugir(agora, true);
                return;
            }
            if (g != null && g.alvo != null) { Ronda(f, g, agora, dt); return; }

            // a gente sem ser alvo: de dia (ou perto demais) foge; à noite fica de longe, olhando. Caçando, a matilha não para
            // para olhar quem está longe (a caça é para ser vista) e só foge de quem chega bem perto
            bool cacando = g != null && g.presa != null;
            if (_alvo != null && agora < _gentoAte && _alvo.AlvoValido)
            {
                float d = Vector3.Distance(p, _alvo.transform.position);
                bool noite = CicloDoDia.Noite > 0.55f;
                if (d < (noite ? Dados.fuga * 0.5f : Dados.fuga) * (cacando ? 0.45f : 1f))
                {
                    _perigo = _alvo.transform.position;
                    _perigoAte = agora + 8f;
                    AvisarGrupo(_perigo, agora, true);
                    Fugir(agora, true);
                    return;
                }
                if (d < Dados.visao && !cacando)
                {
                    Estado = EstadoDoBicho.Alerta;
                    Olhar(_alvo.transform.position);
                    // à noite, a matilha acompanha de longe (uns 30 m), sem chegar
                    if (noite && d > 38f) Ir(_alvo.transform.position + (p - _alvo.transform.position).normalized * 30f, Dados.andar);
                    else { _temDestino = false; _velAlvo = 0f; }
                    return;
                }
            }

            if (g != null && g.presa != null) { Cacar(f, g, agora); return; }
            if (g != null && g.carcaca != null) { Comer(f, g, agora, dt); return; }
            if (g != null && g.lider == this && agora > g.proximaCaca) f.TentarCaca(g, agora);
            Vagar(f, agora, dt);
        }

        // vaga atrás do líder; de dia, cada um às vezes deita para descansar (ativos à noite)
        float _proximoUivoLivre = -1f;

        void Vagar(FaunaDoMundo f, float agora, float dt)
        {
            _folego = 0f;
            float noite = CicloDoDia.Noite;
            // (a etapa 4, os sons da noite) o líder uiva de vez em quando à noite, mesmo sem caçar
            if (Grupo != null && Grupo.lider == this && noite > 0.6f)
            {
                if (_proximoUivoLivre < 0f) _proximoUivoLivre = agora + Random.Range(30f, 120f);
                else if (agora > _proximoUivoLivre)
                {
                    _proximoUivoLivre = agora + Random.Range(180f, 360f);
                    f.Uivar(this);
                }
            }
            if (Estado == EstadoDoBicho.Deitado)
            {
                if (agora < _deitadoAte && noite < 0.5f) return;
                Estado = EstadoDoBicho.Pastando;
            }
            else if (noite < 0.3f && Random.value < 0.03f * dt)
            {
                Estado = EstadoDoBicho.Deitado;
                _deitadoAte = agora + Random.Range(60f, 200f);
                _temDestino = false;
                _velAlvo = 0f;
                return;
            }
            _olhar = Mathf.MoveTowards(_olhar, 0f, 40f * dt);
            Pastar(f, agora);
        }

        // ------------------------------------------------------------------ a caça (a presa)

        void Cacar(FaunaDoMundo f, Grupo g, float agora)
        {
            var presa = g.presa;
            if (presa == null || presa.Exposto)
            {
                g.presa = null;
                return;
            }
            if (presa.Morto)
            {
                // derrubou: todos para a carcaça
                g.carcaca = presa;
                g.presa = null;
                g.comerAte = agora + Random.Range(60f, 180f);
                return;
            }
            Vector3 p = transform.position, alvo = presa.transform.position;
            float d = Vector3.Distance(Plano(p), Plano(alvo));
            if (agora > g.cacaAte || d > 160f)
            {
                // desistiu: trota e volta a vagar (e tenta de novo bem depois)
                g.presa = null;
                g.proximaCaca = agora + Random.Range(90f, 200f);
                Estado = EstadoDoBicho.Andando;
                _velAlvo = Dados.trotar;
                return;
            }
            if (Variante == Variante.Filhote)
            {
                // o filhote segue de longe
                Ir(alvo, Dados.trotar);
                Estado = EstadoDoBicho.Andando;
                return;
            }
            if (!g.correndo)
            {
                // a espreita: chegam devagar, de cabeça baixa, até uns 32 m (a presa só estranha quem passa bem perto);
                // aí a matilha toda dispara
                if (d > 32f && presa.Estado != EstadoDoBicho.Fugindo)
                {
                    Estado = EstadoDoBicho.Alerta;
                    Ir(alvo, Dados.trotar * 0.6f);
                    Olhar(alvo);
                    return;
                }
                g.correndo = true;
                g.cacaDesde = agora;
                g.cacaAte = agora + Random.Range(20f, 32f);
            }
            Estado = EstadoDoBicho.Investindo;
            if (_folego <= 0f) _folego = Random.Range(0.8f, 0.97f);   // cada um corre do seu jeito (a sorte da presa)
            // cansa: depois de ~15 s correndo, perde fôlego
            float cansaco = Mathf.Clamp01((agora - g.cacaDesde - 15f) / 15f);
            float vel = Dados.correr * _folego * Mathf.Lerp(1f, 0.7f, cansaco);
            // de lado: cada um mira um ponto ao lado e à frente da presa, que fecha conforme chega perto (o cerco)
            g.Adultos(this, out int i);
            float lado = (i % 2 == 0 ? 1f : -1f) * (1 + i / 2) * Mathf.Clamp(d * 0.25f, 0f, 7f);
            Vector3 frente = presa.Velocidade > 0.5f ? presa.transform.forward : (alvo - p).normalized;
            Vector3 ponto = alvo + frente * Mathf.Min(presa.Velocidade * 0.5f, 6f) + Vector3.Cross(Vector3.up, frente) * lado;
            if (d < 1.3f + presa.Dados.comprimento * presa.Escala * 0.5f && agora > _proximaMordida)
            {
                _proximaMordida = agora + Random.Range(0.7f, 1.2f);
                f.MorderPresa(this, presa, agora);
            }
            Ir(ponto, vel);
            Olhar(alvo);
        }

        // come na carcaça (de cabeça baixa, em volta dela); cada um tira um pouco do que sobra
        void Comer(FaunaDoMundo f, Grupo g, float agora, float dt)
        {
            var c = g.carcaca;
            if (c == null || c.Carneado || agora > g.comerAte)
            {
                g.carcaca = null;
                g.proximaCaca = agora + Random.Range(180f, 420f);   // satisfeita
                return;
            }
            int n = g.Adultos(this, out int i);
            float ang = (i * 360f / Mathf.Max(1, n)) + (Variante == Variante.Filhote ? 25f : 0f);
            Vector3 lugar = c.transform.position + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * (c.Dados.comprimento * c.Escala * 0.5f + 0.5f);
            Estado = EstadoDoBicho.Pastando;
            if (Vector3.Distance(Plano(transform.position), Plano(lugar)) > 0.7f) Ir(lugar, Dados.andar * 1.5f);
            else
            {
                _temDestino = false;
                _velAlvo = 0f;
                c.Restante = Mathf.Max(0.3f, c.Restante - 0.0012f * Mathf.Max(0.3f, dt));
            }
            Olhar(c.transform.position);
        }

        // ------------------------------------------------------------------ o cerco do jogador (à noite)

        void Ronda(FaunaDoMundo f, Grupo g, float agora, float dt)
        {
            var j = g.alvo;
            if (!f.PodeContinuarAtaque(g, j, agora))
            {
                // largou: de dia, com o alvo no carro ou caído, sem matilha; volta para longe
                f.LargarAlvo(g, agora, j != null ? j.transform.position : transform.position, false);
                return;
            }
            Vector3 c = j.transform.position;
            bool noFogo = f.NoFogo(c, out var centroDoFogo, out float raioDoFogo);
            if (this == g.atacante && !noFogo) { Bote(f, g, j, agora); return; }
            if (this == g.atacante) g.atacante = null;
            int n = g.Adultos(this, out int i);
            if (Variante == Variante.Filhote) { i = n; n++; }
            float raio = Coiote ? 11f : 9f;
            Vector3 meio = c;
            if (noFogo)
            {
                // o fogo segura: esperam na beira dele, como os zumbis
                meio = centroDoFogo;
                raio = raioDoFogo + 2.5f;
            }
            if (g.lider == this) g.rondaGiro += 14f * Mathf.Max(0.3f, dt);
            float ang = g.rondaGiro + i * 360f / Mathf.Max(1, n) + Mathf.Sin(agora * 0.4f + i) * 12f;
            Vector3 ponto = meio + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * raio;
            Estado = EstadoDoBicho.Alerta;
            float dist = Vector3.Distance(Plano(transform.position), Plano(ponto));
            if (dist > 1f) Ir(ponto, dist > 5f ? Dados.trotar : Dados.andar);
            else { _temDestino = false; _velAlvo = 0f; }
            Olhar(c);
            if (Random.value < 0.06f * Mathf.Max(0.3f, dt)) Tocar(SomDoBicho.Rosnado);
            // o líder manda o próximo bote: o que está mais atrás do jogador (fora da vista dele)
            if (g.lider == this && !noFogo && g.atacante == null && agora > g.proximoBote)
            {
                g.proximoBote = agora + (Coiote ? Random.Range(4f, 8f) : Random.Range(2.5f, 5.5f));
                Bicho melhor = null;
                float atras = float.MaxValue;
                Vector3 olha = j.transform.forward;
                olha.y = 0f;
                foreach (var m in g.membros)
                {
                    if (m == null || m.Morto || m.Variante == Variante.Filhote || m.Estado == EstadoDoBicho.Fugindo) continue;
                    Vector3 para = m.transform.position - c;
                    para.y = 0f;
                    float dot = Vector3.Dot(olha.normalized, para.normalized);
                    if (dot < atras) { atras = dot; melhor = m; }
                }
                if (melhor != null)
                {
                    g.atacante = melhor;
                    melhor._boteAte = agora + 5f;
                    melhor._mordeu = false;
                    melhor.ProximaDecisao = agora;
                }
            }
        }

        void Bote(FaunaDoMundo f, Grupo g, JogadorRede j, float agora)
        {
            Vector3 alvo = j.transform.position;
            float d = Vector3.Distance(Plano(transform.position), Plano(alvo));
            if (_mordeu || agora > _boteAte)
            {
                // de volta para a roda
                g.atacante = null;
                Estado = EstadoDoBicho.Alerta;
                Ir(transform.position + (transform.position - alvo).normalized * 6f, Dados.trotar);
                return;
            }
            Estado = EstadoDoBicho.Investindo;
            Olhar(alvo);
            if (d < 1.5f + Dados.comprimento * Escala * 0.3f)
            {
                _mordeu = true;
                f.Golpear(this, j);
                Ir(transform.position - (alvo - transform.position).normalized * 7f, Dados.trotar * 1.5f);
                return;
            }
            Ir(alvo, Dados.correr * 0.7f);
        }

        // ------------------------------------------------------------------ o tiro e o ferimento

        /// <summary>A matilha largou o alvo: cada um sai de perto (e, se 'fugir', foge do ponto).</summary>
        internal void Espantar(Vector3 de, float agora, bool fugir)
        {
            _perigo = de;
            _perigoAte = agora + (fugir ? 10f : 4f);
            if (fugir)
            {
                _fugaAte = agora + Random.Range(8f, 14f);
                Fugir(agora, true);
            }
            else if (Estado == EstadoDoBicho.Investindo || Estado == EstadoDoBicho.Alerta) Estado = EstadoDoBicho.Andando;
        }

        /// <summary>Um som do bicho (no anfitrião: toca aqui e vai para quem está perto). Um a cada 0,8 s no máximo.</summary>
        internal void Tocar(SomDoBicho som)
        {
            if (Copia || Time.time < _proximoSom) return;
            _proximoSom = Time.time + 0.8f;
            FaunaDoMundo.Instancia?.TocarSom(this, som);
        }

        float _proximoSom;

        void TiroNaMatilha(Vector3 ponto, bool perto, float agora)
        {
            var g = Grupo;
            if (g != null && g.alvo != null)
            {
                // um sorteio por tiro, pela matilha toda: o coiote larga mais
                if (g.ultimoTiro != agora)
                {
                    g.ultimoTiro = agora;
                    if (Random.value < (Coiote ? 0.55f : 0.25f)) FaunaDoMundo.Instancia?.LargarAlvo(g, agora, ponto, true);
                }
                return;
            }
            _perigo = ponto;
            _perigoAte = agora + 12f;
            if (perto)
            {
                AvisarGrupo(ponto, agora, true);
                _fugaAte = agora + Random.Range(10f, 16f);
                ProximaDecisao = Mathf.Min(ProximaDecisao, agora + Random.Range(0.05f, 0.4f));
            }
        }

        void FeridoNaMatilha(JogadorRede autor, float agora)
        {
            var g = Grupo;
            if (g != null && g.alvo != null && Random.value < (Coiote ? 0.6f : 0.25f))
                FaunaDoMundo.Instancia?.LargarAlvo(g, agora, _perigo, true);
            if (g != null && g.atacante == this) g.atacante = null;
            _fugaAte = agora + Random.Range(12f, 20f);
            Fugir(agora, true);
        }
    }
}
