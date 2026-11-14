using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// A água e o ar (a etapa 3, 12/11/2026), no anfitrião:
    /// - a altura de cada um: o peixe uns 30 cm embaixo da superfície (e o pulo fora dela, num arco); a ave da água boiando;
    ///   o bicho anfíbio nadando onde não dá pé (o alce, o castor; o urso que pesca só entra no raso); o castor mergulhado;
    ///   a ave voando (a altura sobe e desce aos poucos);
    /// - o salmão: o cardume no raso, pulando de vez em quando; foge de quem chega perto;
    /// - os patos e os gansos: o bando boia e mergulha a cabeça; com o barulho (o tiro, gente perto, o zumbi, o predador)
    ///   decola grasnando e pousa longe, em outro ponto do lago. O ganso macho do ninho avança grasnando em quem chega
    ///   perto dele. Os gansos em V passam no céu de vez em quando (<see cref="Passagem"/>: só seguem em frente);
    /// - a águia-careca: roda alto sobre o lago, mergulha, sai com o peixe nas garras e volta a rodar.
    /// </summary>
    public sealed partial class Bicho
    {
        /// <summary>De passagem no céu (o V dos gansos): só segue em frente, alto.</summary>
        public bool Passagem { get; internal set; }

        float _altitude, _altitudeAlvo, _puloAte, _proximoPulo, _respingoFim, _mergulhoAte;
        bool _podeMolhar, _temPouso;
        Vector3 _pouso, _destinoDaPassagem;
        const float DuracaoDoPulo = 0.8f;

        /// <summary>Voando (a altura do voo acima do chão ou da água).</summary>
        public bool Voando => _altitude > 0.8f;

        /// <summary>Começa no ar (a águia, o V dos gansos), a esta altura.</summary>
        internal void PorNoAr(float altura)
        {
            _altitude = _altitudeAlvo = altura;
            Vector3 p = transform.position;
            float chao = Chao(p, out float f, out float nivelDaAgua);
            p.y = Mathf.Max(chao, f > 0.01f ? nivelDaAgua : chao) + altura;
            transform.position = p;
        }

        /// <summary>O V dos gansos: voa reto até 'destino', a 'altura' m.</summary>
        internal void Passar(Vector3 destino, float altura)
        {
            Passagem = true;
            _destinoDaPassagem = destino;
            PorNoAr(altura);
            Estado = EstadoDoBicho.Andando;
            Ir(destino, Dados.correr);
            _velAlvo = Dados.correr;
            Velocidade = Dados.correr;
        }

        // ------------------------------------------------------------------ a queda do corpo (13/11/2026, a pedido)

        float _quedaVel;

        /// <summary>
        /// No anfitrião, a cada quadro, o bicho morto (13/11/2026, a pedido: "quando mato algum pato voando o corpo dele fica
        /// lá em cima"): o corpo no ar (a ave abatida voando, o peru no galho, o esquilo no tronco) cai com a gravidade até o
        /// chão; na água, a ave e o peixe boiam e o resto afunda até o meio do corpo. A posição vai pela mensagem de sempre.
        /// </summary>
        internal void Cair(float dt)
        {
            _altitude = _altitudeAlvo = 0f;
            Carregando = false;   // a águia solta o peixe
            Vector3 p = transform.position;
            float chao = Chao(p, out float fundura, out float nivel);
            float pouso = chao;
            if (fundura > 0.01f)
                pouso = Dados.corpo == Corpo.Ave || Dados.meio == Meio.Agua ? nivel - 0.04f : Mathf.Max(chao, nivel - Altura * 0.5f);
            if (p.y <= pouso + 0.005f)
            {
                _quedaVel = 0f;
                if (pouso - p.y > 0.005f && pouso - p.y < 3f) { p.y = pouso; transform.position = p; }
                return;
            }
            _quedaVel = Mathf.Min(_quedaVel + 9.81f * dt, 40f);
            p.y = Mathf.Max(pouso, p.y - _quedaVel * dt);
            transform.position = p;
            if (p.y <= pouso + 0.005f && _quedaVel > 6f && fundura > 0.01f) Tocar(SomDoBicho.Respingo);   // caiu na água
            if (p.y <= pouso + 0.005f) _quedaVel = 0f;
        }

        // ------------------------------------------------------------------ a altura (a cada quadro, no anfitrião)

        float AlturaDe(Vector3 p, float dt, float agora)
        {
            float chao = Chao(p, out float fundura, out float nivelDaAgua);
            float nivel = fundura > 0.01f ? nivelDaAgua : chao;
            float alvo = chao;
            bool liso = false;
            if (Dados.meio == Meio.Agua)
            {
                // o peixe: embaixo da superfície (no raso, acima do fundo); o pulo sai da água num arco
                alvo = fundura > 0.01f ? Mathf.Max(chao + 0.08f, nivel - 0.3f) : chao;
                if (agora < _puloAte)
                {
                    float u = 1f - (_puloAte - agora) / DuracaoDoPulo;
                    alvo = nivel - 0.2f + Mathf.Sin(u * Mathf.PI) * 1.1f;
                    liso = true;
                }
            }
            else if (_altitude > 0.02f || _altitudeAlvo > 0.02f)
            {
                _altitude = Mathf.MoveTowards(_altitude, _altitudeAlvo, (_altitudeAlvo > _altitude ? 5f : 9f) * dt);
                alvo = Mathf.Max(chao, nivel) + _altitude;
                liso = true;
            }
            else if (fundura > 0.01f)
            {
                if (Dados.corpo == Corpo.Ave) alvo = nivel - 0.04f;   // boiando
                else
                {
                    float nado = Altura * 0.8f;   // onde não dá pé, nada (só o lombo e a cabeça fora)
                    if (agora < _mergulhoAte && fundura > 0.7f) alvo = nivel - 0.8f;   // o castor mergulhado
                    else if (fundura > nado) alvo = nivel - nado;
                }
            }
            if (liso) return Mathf.Lerp(p.y, alvo, 1f - Mathf.Exp(-10f * dt));
            return Mathf.Abs(p.y - alvo) > 3f ? alvo : Mathf.Lerp(p.y, alvo, 1f - Mathf.Exp(-14f * dt));
        }

        /// <summary>A água adiante barra o caminho? (o peixe não sai da água; o bicho de terra não entra no fundo)</summary>
        // (13/11, a pedido: "o urso fica parado e o filhote corre dentro da água") 'aqui': a fundura onde o bicho está. O de
        // terra só não vai para onde fica MAIS fundo que o limite e mais fundo que aqui: quem já está na água (o urso que
        // parou de pescar) sempre pode sair para o raso (antes, todo rumo era "fundo demais" e ele ficava parado). O limite
        // é 70% da altura do bicho (o filhote e os pequenos não entram na água acima da barriga), no máximo 0,45 m; o urso
        // pescando, até 1,25 m
        bool AguaBarra(float fundura, float aqui = 0f)
        {
            switch (Dados.meio)
            {
                case Meio.Agua: return fundura < 0.5f;
                case Meio.Terra:
                {
                    float limite = _podeMolhar ? 1.25f : Mathf.Min(0.45f, Altura * 0.7f);
                    return fundura > limite && fundura > aqui + 0.03f;
                }
                default: return false;
            }
        }

        // ------------------------------------------------------------------ o salmão

        void PensarPeixe(FaunaDoMundo f, float agora, float dt)
        {
            if (f.Perceber(this, out var de, out _, out _, agora < _distraidoAte) && Vector3.Distance(transform.position, de) < Dados.fuga * 1.5f)
            {
                _perigo = de;
                _perigoAte = agora + 3f;
                Fugir(agora, true);
                _fugaAte = agora + Random.Range(1.5f, 3f);   // o peixe dispara e logo volta
                return;
            }
            if (Estado == EstadoDoBicho.Fugindo && agora < _fugaAte) return;
            // o pulo: de vez em quando um sai da água (o respingo na saída e na volta)
            if (agora > _proximoPulo)
            {
                _proximoPulo = agora + Random.Range(5f, 22f);
                if (Random.value < 0.6f && Velocidade > 0.2f)
                {
                    _puloAte = agora + DuracaoDoPulo;
                    _respingoFim = _puloAte;
                    Tocar(SomDoBicho.Respingo);
                }
            }
            if (_respingoFim > 0f && agora > _respingoFim)
            {
                _respingoFim = 0f;
                _proximoSom = 0f;
                Tocar(SomDoBicho.Respingo);
            }
            Pastar(f, agora);
            // o cardume não para: nada devagar mesmo pastando
            if (!_temDestino) Ir(transform.position + Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * transform.forward * Random.Range(1.5f, 4f), Dados.andar);
        }

        // ------------------------------------------------------------------ os patos e os gansos

        void PensarAveDaAgua(FaunaDoMundo f, float agora, float dt)
        {
            if (Passagem)
            {
                // o V no céu: segue reto; grasna de vez em quando (o líder)
                Estado = EstadoDoBicho.Andando;
                Ir(_destinoDaPassagem, Dados.correr);
                if (Grupo != null && Grupo.lider == this && Random.value < 0.05f) Tocar(SomDoBicho.Ganso);
                return;
            }
            if (f.Perceber(this, out var de, out var quem, out bool naoGente, agora < _distraidoAte))
            {
                _perigo = de;
                _perigoAte = agora + 10f;
                if (!naoGente) _alvo = quem;
            }
            var g = Grupo;
            // o ninho: o ganso macho avança grasnando em quem chega perto (e a fêmea choca)
            if (Especie == Especie.Ganso && g != null && g.temNinho && Estado != EstadoDoBicho.Fugindo)
            {
                if (Variante == Variante.Femea) { Chocar(g, agora); return; }
                // o macho não foge de quem chega perto: guarda o ninho (só o tiro e o ferimento o fazem voar)
                if (Variante == Variante.Macho)
                {
                    if (!GuardarNinho(f, g, agora)) { Estado = EstadoDoBicho.Alerta; _temDestino = false; _velAlvo = 0f; if (_alvo != null) Olhar(_alvo.transform.position); }
                    return;
                }
            }
            if (Estado == EstadoDoBicho.Fugindo) { Voar(f, agora); return; }
            if (agora < _perigoAte && Vector3.Distance(transform.position, _perigo) < Dados.fuga)
            {
                AvisarGrupo(_perigo, agora, true);
                Decolar(agora);
                return;
            }
            Pastar(f, agora);
        }

        internal void Decolar(float agora)
        {
            Estado = EstadoDoBicho.Fugindo;
            _altitudeAlvo = Random.Range(12f, 24f);
            _fugaAte = agora + Random.Range(9f, 16f);
            _temPouso = false;
            Tocar(Especie == Especie.Pato ? SomDoBicho.Grasnado : SomDoBicho.Ganso);
            Vector3 longe = Plano(transform.position - _perigo);
            if (longe.sqrMagnitude < 0.01f) longe = transform.forward;
            longe = Quaternion.Euler(0f, Random.Range(-30f, 30f), 0f) * longe.normalized;
            if (Grupo != null)
            {
                if (Grupo.lider == this || Grupo.fugaPara == Vector3.zero) Grupo.fugaPara = longe;
                longe = (Grupo.fugaPara * 2f + longe).normalized;
            }
            Ir(transform.position + longe * 200f, Dados.correr);
        }

        // no ar: voa para longe; depois desce num ponto do lago longe do perigo (o bando pousa junto)
        void Voar(FaunaDoMundo f, float agora)
        {
            if (agora < _fugaAte) return;
            var g = Grupo;
            if (!_temPouso)
            {
                if (g != null && agora - g.pousoEm < 40f) { _pouso = g.pouso + LugarNoGrupo * 0.6f; _temPouso = true; }
                else if (FaunaDoMundo.PontoNaAgua(transform.position, 20f, 160f, 0.6f, 99f, out var p) && Vector3.Distance(p, _perigo) > 50f)
                {
                    _pouso = p;
                    _temPouso = true;
                    if (g != null) { g.pouso = p; g.pousoEm = agora; g.pasto = p; }
                }
                else
                {
                    // sem lago perto: segue voando (some longe)
                    _fugaAte = agora + 6f;
                    return;
                }
            }
            float d = Vector3.Distance(Plano(transform.position), Plano(_pouso));
            Ir(_pouso, d > 20f ? Dados.correr * 0.8f : Mathf.Max(2f, d * 0.4f));
            if (d < 15f) _altitudeAlvo = 0f;
            if (_altitude < 0.1f && FaunaDoMundo.FunduraEm(transform.position) > 0.3f)
            {
                Estado = EstadoDoBicho.Pastando;
                _temPouso = false;
                _temDestino = false;
                _velAlvo = 0f;
            }
        }

        // a fêmea choca: deitada no ninho
        void Chocar(Grupo g, float agora)
        {
            float d = Vector3.Distance(Plano(transform.position), Plano(g.ninho));
            if (d > 0.4f)
            {
                Estado = EstadoDoBicho.Andando;
                Ir(g.ninho, Dados.andar);
                return;
            }
            Estado = EstadoDoBicho.Deitado;
            _deitadoAte = agora + 30f;
            _temDestino = false;
            _velAlvo = 0f;
            // gente muito perto (o macho foi e não adiantou): ela decola
            if (agora < _perigoAte && Vector3.Distance(transform.position, _perigo) < 2.5f) Decolar(agora);
        }

        // o macho guarda o ninho: avança grasnando (no chão, de asas abertas) em quem chega a 9 m dele; bica e volta
        bool GuardarNinho(FaunaDoMundo f, Grupo g, float agora)
        {
            var j = _alvo;
            bool perto = j != null && j.AlvoValido && FaunaDoMundo.Alcanca(this, j) && Vector3.Distance(Plano(j.transform.position), Plano(g.ninho)) < 9f;
            // ele vigia o ninho: vê quem chega perto dele mesmo sem os sentidos (o mais perto do ninho)
            if (!perto && Estado != EstadoDoBicho.Investindo)
            {
                float melhor = 9f;
                foreach (var gente in f.Pessoas)
                {
                    if (!gente.valido) continue;
                    float dg = Vector3.Distance(Plano(gente.pes), Plano(g.ninho));
                    if (dg < melhor && FaunaDoMundo.Alcanca(this, gente.j)) { melhor = dg; j = gente.j; }
                }
                if (j != null && melhor < 9f) { _alvo = j; perto = true; }
            }
            if (Estado == EstadoDoBicho.Investindo)
            {
                if (j == null || !j.AlvoValido || !FaunaDoMundo.Alcanca(this, j) || Vector3.Distance(Plano(j.transform.position), Plano(g.ninho)) > 15f || agora > _fimDoAtaque)
                {
                    Estado = EstadoDoBicho.Andando;
                    _altitudeAlvo = 0f;
                    Ir(g.ninho + LugarNoGrupo.normalized * 1.5f, Dados.trotar);
                    return true;
                }
                Vector3 pj = j.transform.position;
                float d = Vector3.Distance(Plano(transform.position), Plano(pj));
                Olhar(pj);
                if (d < 1.2f && agora > _proximaPatada)
                {
                    _proximaPatada = agora + Random.Range(1.5f, 2.5f);
                    f.Golpear(this, j);
                    Ir(transform.position + Plano(transform.position - pj).normalized * 3f, Dados.trotar * 2f);
                    return true;
                }
                if (Random.value < 0.15f) Tocar(SomDoBicho.Ganso);
                Ir(pj, Dados.trotar * 3.2f);
                return true;
            }
            if (perto)
            {
                Estado = EstadoDoBicho.Investindo;
                _fimDoAtaque = agora + 12f;
                _altitudeAlvo = 0.35f;   // os pulinhos de asas abertas
                Tocar(SomDoBicho.Ganso);
                if (j.Vida != null) j.Vida.AbalarAgora(1f, MotivoDaMente.Susto);
                return true;
            }
            _altitudeAlvo = 0f;
            // perto do ninho: fica por ali, na margem
            float dn = Vector3.Distance(Plano(transform.position), Plano(g.ninho));
            if (dn > 4f) { Estado = EstadoDoBicho.Andando; Ir(g.ninho + LugarNoGrupo.normalized * 2f, dn > 12f ? Dados.trotar : Dados.andar); return true; }
            return false;
        }

        // ------------------------------------------------------------------ a águia

        enum FaseDaAguia : byte { Rodando, Mergulhando, Levando }
        FaseDaAguia _fase;
        float _giroDaRoda, _proximoMergulho, _levandoAte;
        Vector3 _centroDaRoda, _alvoDoMergulho;

        void PensarAguia(FaunaDoMundo f, float agora, float dt)
        {
            if (_centroDaRoda == Vector3.zero) { _centroDaRoda = transform.position; _proximoMergulho = agora + Random.Range(15f, 40f); }
            // o tiro perto (OuvirTiro): sobe e vai embora por um tempo
            if (Estado == EstadoDoBicho.Fugindo)
            {
                _altitudeAlvo = 60f;
                if (agora < _fugaAte) return;
                Estado = EstadoDoBicho.Andando;
                _fase = FaseDaAguia.Rodando;
            }
            switch (_fase)
            {
                case FaseDaAguia.Rodando:
                {
                    Estado = EstadoDoBicho.Andando;
                    _altitudeAlvo = 40f;
                    _giroDaRoda += 14f * Mathf.Max(0.3f, dt);
                    Vector3 ponto = _centroDaRoda + Quaternion.Euler(0f, _giroDaRoda, 0f) * Vector3.forward * 35f;
                    Ir(ponto, Dados.correr * 0.55f);
                    if (Random.value < 0.03f * Mathf.Max(0.3f, dt)) Tocar(SomDoBicho.Grito);
                    if (agora > _proximoMergulho && FaunaDoMundo.PontoNaAgua(transform.position, 25f, 55f, 1f, 99f, out _alvoDoMergulho))
                    {
                        _fase = FaseDaAguia.Mergulhando;
                        Estado = EstadoDoBicho.Investindo;
                        _altitudeAlvo = 0.3f;
                        Tocar(SomDoBicho.Grito);
                    }
                    break;
                }
                case FaseDaAguia.Mergulhando:
                {
                    float d = Vector3.Distance(Plano(transform.position), Plano(_alvoDoMergulho));
                    Ir(_alvoDoMergulho, Mathf.Clamp(d * 1.5f, 2f, Dados.correr * 0.75f));
                    if (d < 2.5f && _altitude < 0.8f)
                    {
                        // pegou o peixe: o respingo, e sobe levando
                        _proximoSom = 0f;
                        Tocar(SomDoBicho.Respingo);
                        Carregando = true;
                        _fase = FaseDaAguia.Levando;
                        _levandoAte = agora + Random.Range(15f, 25f);
                        _altitudeAlvo = 25f;
                        Estado = EstadoDoBicho.Andando;
                        Vector3 longe = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                        _destino = _centroDaRoda + longe * Random.Range(80f, 140f);
                        Ir(_destino, Dados.correr * 0.6f);
                    }
                    break;
                }
                default:
                    Ir(_destino, Dados.correr * 0.6f);
                    if (agora > _levandoAte)
                    {
                        Carregando = false;
                        _fase = FaseDaAguia.Rodando;
                        _proximoMergulho = agora + Random.Range(25f, 70f);
                    }
                    break;
            }
        }
    }
}
