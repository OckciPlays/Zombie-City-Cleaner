using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// A noite e os detalhes (a etapa 4, 12/11/2026), no anfitrião:
    /// - o sapo: na beira do lago à noite, parado, pulando de vez em quando; coaxa, e o coro cala quando alguém chega a
    ///   12 m (por uns 20 a 35 s); foge para a água;
    /// - a cascavel: enrolada nas pedras e no campo; com gente a 5 m, o chocalho (o aviso); a 1,6 m, o bote (o veneno,
    ///   VidaJogador.Veneno.cs: 25% das picadas são secas); dá o bote no sapo e o engole;
    /// - o guaxinim: à noite, vai às lixeiras da cidade, derruba (o barulho da lata) e revira (o lixo espalhado fica); com
    ///   gente perto, sobe na árvore;
    /// - o gambá (o de listra): com gente a 6 m, o aviso (a cauda levantada, as patas batendo); quem chega a 3 m, ou ainda
    ///   está a menos de 4,5 m quando o aviso acaba, leva o espirro (o cheiro: JogadorRede.Fauna.cs);
    /// - o urubu: roda alto em cima do resto (o corpo do zumbi, a carcaça), desce para comer quando não há ninguém a 30 m e
    ///   sobe quando alguém chega; vai embora quando o resto some;
    /// - o esquilo: no chão perto das árvores; com perigo, corre para o tronco, sobe e chia o alarme;
    /// - o peru: o bando no campo (como a presa) e o glu-glu; à noite, dorme nas árvores.
    /// </summary>
    public sealed partial class Bicho
    {
        float _calarAte, _proximoCoaxar, _proximoChocalho, _proximoBote, _trepadoAte, _semGenteDesde, _proximoGluGlu, _reviraAte;
        bool _trepando, _temArvore, _empoleirado, _avisou;
        Vector3 _peDoTronco;
        float _raioDoTronco;
        Bicho _engolindo;
        float _engolirEm, _proximaSubida, _digerindoAte;
        /// <summary>(a etapa 5) Nesta máquina: quando o sapo pode comer outro vaga-lume (1 minuto depois do último).</summary>
        public float ProximoVagaLume { get; set; }
        TipoDeLixo _lixoDeAgora;

        static bool ENoite => CicloDoDia.Noite > 0.55f;

        // a língua do sapo (só na tela desta máquina): estica até o vaga-lume por um instante
        Transform _lingua;
        float _linguaAte;

        /// <summary>O sapo come o vaga-lume (VagaLumes.cs): a língua estica até ele por um instante.</summary>
        public void Lingua(Vector3 alvo)
        {
            if (_lingua == null) _lingua = ModelosDosBichos.CaixaSolta(transform, Vector3.zero, Vector3.one, new Color(0.85f, 0.4f, 0.45f));
            Vector3 boca = transform.position + transform.forward * 0.05f + Vector3.up * 0.04f;
            Vector3 para = alvo - boca;
            if (para.sqrMagnitude < 0.0001f) return;
            _lingua.SetPositionAndRotation(boca + para * 0.5f, Quaternion.LookRotation(para.normalized));
            _lingua.localScale = new Vector3(0.012f, 0.008f, para.magnitude);
            _lingua.gameObject.SetActive(true);
            _linguaAte = Time.time + 0.15f;
        }

        // ------------------------------------------------------------------ subir na árvore (o esquilo, o guaxinim)

        bool _subidaCalma;

        /// <summary>
        /// Corre para o tronco mais perto e sobe (de 3 a 6 m) por um tempo. Falso: sem árvore perto. 'calmo' (13/11): o esquilo
        /// que sobe por conta própria (sem o chiado do alarme e por menos tempo).
        /// </summary>
        bool Trepar(FaunaDoMundo f, float agora, bool calmo = false)
        {
            if (!_trepando)
            {
                if (!FaunaDoMundo.ArvoreMaisPerto(transform.position, 25f, out _peDoTronco, out _raioDoTronco)) return false;
                _trepando = true;
                _trepadoAte = 0f;
                _subidaCalma = calmo;
            }
            Vector3 p = transform.position;
            Vector3 lado = Plano(p - _peDoTronco);
            lado = lado.sqrMagnitude > 0.01f ? lado.normalized : Vector3.forward;
            Vector3 junto = _peDoTronco + lado * (_raioDoTronco + 0.12f);
            if (_trepadoAte <= 0f)
            {
                if (Vector3.Distance(Plano(p), Plano(junto)) > 1.2f)
                {
                    Estado = EstadoDoBicho.Fugindo;
                    Ir(junto, Dados.correr);
                    return true;
                }
                // chegou: encosta no tronco e sobe, de frente para ele
                transform.SetPositionAndRotation(new Vector3(junto.x, p.y, junto.z), Quaternion.LookRotation(-lado));
                _altitudeAlvo = _subidaCalma ? Random.Range(2f, 5f) : Random.Range(3f, 6f);
                _trepadoAte = agora + (_subidaCalma ? Random.Range(6f, 16f) : Random.Range(18f, 35f));
                if (Especie == Especie.Esquilo && !_subidaCalma) Tocar(SomDoBicho.Chiado);
            }
            Estado = EstadoDoBicho.Alerta;
            _temDestino = false;
            _velAlvo = 0f;
            Velocidade = 0f;
            if (Especie == Especie.Esquilo && Random.value < (_subidaCalma ? 0.05f : 0.25f)) Tocar(SomDoBicho.Chiado);
            if (agora > _trepadoAte)
            {
                _altitudeAlvo = 0f;
                if (_altitude < 0.1f)
                {
                    _trepando = false;
                    Estado = EstadoDoBicho.Andando;
                    Ir(p + lado * 6f, Dados.trotar);
                }
            }
            return true;
        }

        // ------------------------------------------------------------------ o sapo

        void PensarSapo(FaunaDoMundo f, float agora, float dt)
        {
            Vector3 p = transform.position;
            if (agora >= _distraidoAte && f.PerceberGente(this, out var de, out _))
            {
                float d = Vector3.Distance(p, de);
                if (d < 12f) CalarOCoro(agora);
                if (d < Dados.fuga + 1f)
                {
                    // pula para longe (para a água, se tiver perto)
                    _perigo = de;
                    _perigoAte = agora + 6f;
                    Estado = EstadoDoBicho.Fugindo;
                    Vector3 para = FaunaDoMundo.AguaMaisPerto(p, 0.3f, 10f, out var agua) ? agua : p + Plano(p - de).normalized * 4f;
                    Ir(para, Dados.trotar);
                    return;
                }
            }
            if (Estado == EstadoDoBicho.Fugindo && _temDestino) return;
            // (a etapa 5) a cobra perto: às vezes ele percebe e pula para longe (a sorte do sapo)
            var cobra = f.PresaPerto(this, Especie.Cascavel, 1.5f);
            if (cobra != null && Random.value < 0.35f)
            {
                Estado = EstadoDoBicho.Fugindo;
                Ir(p + Plano(p - cobra.transform.position).normalized * Random.Range(2f, 4f), Dados.trotar);
                return;
            }
            // (a etapa 5) à noite, o vaga-lume que ele vê (a até 4 m): pula atrás dele; come com a língua (VagaLumes.cs) e
            // espera 1 minuto para comer outro
            if (ENoite && Time.time >= ProximoVagaLume && VagaLumes.Instancia != null && VagaLumes.Instancia.MaisPerto(p + Vector3.up * 0.3f, 4f, out var luz))
            {
                Vector3 chao = new Vector3(luz.x, p.y, luz.z);
                if (Vector3.Distance(Plano(p), Plano(chao)) > 0.9f)
                {
                    Estado = EstadoDoBicho.Andando;
                    Ir(chao - Plano(chao - p).normalized * 0.6f, Dados.trotar);
                    return;
                }
            }
            // o coro: coaxa de tempo em tempo à noite (calado, não)
            if (ENoite && agora > _calarAte && agora > _proximoCoaxar)
            {
                _proximoCoaxar = agora + Random.Range(4f, 12f);
                Tocar(SomDoBicho.Coaxar);
            }
            // parado; de vez em quando um pulo curto
            Estado = EstadoDoBicho.Pastando;
            if (!_temDestino && Random.value < 0.08f * Mathf.Max(0.3f, dt))
                Ir(p + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(0.6f, 2f), Dados.trotar);
        }

        void CalarOCoro(float agora)
        {
            float ate = agora + Random.Range(20f, 35f);
            _calarAte = Mathf.Max(_calarAte, ate);
            if (Grupo == null) return;
            foreach (var b in Grupo.membros) if (b != null) b._calarAte = Mathf.Max(b._calarAte, ate);
        }

        // ------------------------------------------------------------------ a cascavel

        void PensarCascavel(FaunaDoMundo f, float agora, float dt)
        {
            Vector3 p = transform.position;
            // engolindo o sapo (some uns segundos depois do bote)
            if (_engolindo != null)
            {
                if (agora > _engolirEm) { f.Sumir(_engolindo); _engolindo = null; }
                Estado = EstadoDoBicho.Pastando;
                _temDestino = false;
                _velAlvo = 0f;
                return;
            }
            if (agora >= _distraidoAte && f.PerceberGente(this, out var de, out var quem) && quem != null)
            {
                float d = Vector3.Distance(Plano(p), Plano(de));
                if (d < 5f)
                {
                    // o chocalho e a cabeça virada para quem chega; perto demais, o bote
                    Estado = EstadoDoBicho.Alerta;
                    _temDestino = false;
                    _velAlvo = 0f;
                    Olhar(de);
                    if (agora > _proximoChocalho)
                    {
                        _proximoChocalho = agora + 1.5f;
                        _proximoSom = 0f;
                        Tocar(SomDoBicho.Chocalho);
                    }
                    if (d < 1.6f && agora > _proximoBote && FaunaDoMundo.Alcanca(this, quem))
                    {
                        _proximoBote = agora + 3f;
                        Estado = EstadoDoBicho.Investindo;
                        f.Golpear(this, quem);
                    }
                    return;
                }
            }
            // (a etapa 5, 13/11/2026, a pedido: "toda vez que uma cobra ver um sapo, ela vai caçar o sapo") a cobra sente o
            // sapo a até 10 m (o calor e o tremor do chão) e vai atrás, devagar; a 0,8 m, o bote; engole (uns 8 s) e fica de 2
            // a 4 minutos digerindo, sem caçar
            if (agora > _digerindoAte)
            {
                var sapo = f.PresaPerto(this, Especie.Sapo, 10f);
                if (sapo != null)
                {
                    Vector3 ps = sapo.transform.position;
                    float ds = Vector3.Distance(Plano(p), Plano(ps));
                    if (ds < 0.8f)
                    {
                        Estado = EstadoDoBicho.Investindo;
                        _proximoSom = 0f;
                        Tocar(SomDoBicho.Mordida);
                        f.Matar(sapo, null, false);
                        _engolindo = sapo;
                        _engolirEm = agora + 8f;
                        _digerindoAte = agora + Random.Range(120f, 240f);
                        return;
                    }
                    Estado = EstadoDoBicho.Andando;
                    Ir(ps, ds > 3f ? Dados.trotar : Dados.andar);
                    return;
                }
            }
            // enrolada; às vezes rasteja um pouco
            Estado = EstadoDoBicho.Pastando;
            if (!_temDestino && Random.value < 0.02f * Mathf.Max(0.3f, dt))
                Ir(p + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(1.5f, 5f), Dados.andar);
        }

        // ------------------------------------------------------------------ o guaxinim

        void PensarGuaxinim(FaunaDoMundo f, float agora, float dt)
        {
            Vector3 p = transform.position;
            if (_trepando && Trepar(f, agora)) return;
            if (agora >= _distraidoAte && f.Perceber(this, out var de, out _, out _) && Vector3.Distance(p, de) < Dados.fuga)
            {
                _perigo = de;
                _perigoAte = agora + 10f;
                _reviraAte = 0f;
                if (Trepar(f, agora)) return;
                Fugir(agora, true);
                return;
            }
            if (Estado == EstadoDoBicho.Fugindo && agora < _fugaAte) { Fugir(agora, false); return; }
            // revirando o lixo: de cabeça baixa, remexendo (a sacola, as latinhas), com barulho de vez em quando
            if (agora < _reviraAte)
            {
                Estado = EstadoDoBicho.Pastando;
                _temDestino = false;
                _velAlvo = 0f;
                if (Random.value < 0.4f) Tocar(_lixoDeAgora == TipoDeLixo.Sacolas ? SomDoBicho.Sacola : Random.value < 0.5f ? SomDoBicho.Sacola : SomDoBicho.Remexer);
                return;
            }
            // à noite (e, na cidade, de dia também: 13/11), o lixo mais perto (a até 60 m) que ninguém revirou faz pouco
            if ((ENoite || FaunaDoMundo.NaCidade(p)) && f.LixeiraPerto(p, 60f, out var lixeira, out var tipo))
            {
                float d = Vector3.Distance(Plano(p), Plano(lixeira));
                if (d > (tipo == TipoDeLixo.Cacamba ? 1.6f : 1.2f))
                {
                    Estado = EstadoDoBicho.Andando;
                    Ir(lixeira, d > 15f ? Dados.trotar : Dados.andar);
                    return;
                }
                // derruba a lata (o barulho), rasga a sacola ou bate a tampa da caçamba; o lixo fica espalhado
                var ponto = tipo == TipoDeLixo.Cacamba ? lixeira + Quaternion.LookRotation(Plano(p - lixeira).sqrMagnitude > 0.01f ? Plano(p - lixeira) : Vector3.forward) * Vector3.right * 1.6f : lixeira;
                f.ReviraLixeira(ponto, (byte)tipo);
                f.BarulhoNoLixo(lixeira, tipo);
                _lixoDeAgora = tipo;
                _proximoSom = 0f;
                Tocar(tipo == TipoDeLixo.Lata ? SomDoBicho.Lata : tipo == TipoDeLixo.Cacamba ? SomDoBicho.Cacamba : SomDoBicho.Sacola);
                _reviraAte = agora + Random.Range(15f, 35f);
                return;
            }
            Pastar(f, agora);
        }

        // ------------------------------------------------------------------ o gambá

        void PensarGamba(FaunaDoMundo f, float agora, float dt)
        {
            Vector3 p = transform.position;
            JogadorRede j = null;
            float d = 999f;
            if (agora >= _distraidoAte && f.PerceberGente(this, out var de, out var quem) && quem != null)
            {
                j = quem;
                d = Vector3.Distance(Plano(p), Plano(de));
                _alvo = quem;
            }
            else if (_alvo != null && _alvo.AlvoValido && Estado == EstadoDoBicho.Alerta)
            {
                j = _alvo;
                d = Vector3.Distance(Plano(p), Plano(j.transform.position));
            }
            if (Estado == EstadoDoBicho.Alerta && _avisoAte > 0f)
            {
                // o aviso: a cauda em pé e as patas batendo (a animação); quem chega perto leva o espirro
                if (j == null || d > 7f) { _avisoAte = 0f; Estado = EstadoDoBicho.Andando; return; }
                Olhar(j.transform.position);
                if (d < 3f || (agora > _avisoAte && d < 4.5f))
                {
                    Espirrar(f, j, agora);
                    return;
                }
                if (agora > _avisoAte) { _avisoAte = 0f; Estado = EstadoDoBicho.Andando; Ir(p + Plano(p - j.transform.position).normalized * 8f, Dados.andar); }
                return;
            }
            if (j != null && d < 6f)
            {
                Estado = EstadoDoBicho.Alerta;
                _avisoAte = agora + Random.Range(1.5f, 2.5f);
                _temDestino = false;
                _velAlvo = 0f;
                return;
            }
            if (Estado == EstadoDoBicho.Fugindo && agora < _fugaAte) return;
            Pastar(f, agora);
        }

        // o espirro: de costas para quem chegou, o cheiro em quem está a até 4,5 m atrás do gambá
        void Espirrar(FaunaDoMundo f, JogadorRede j, float agora)
        {
            _avisoAte = 0f;
            Vector3 p = transform.position;
            Vector3 para = Plano(j.transform.position - p);
            if (para.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(-para.normalized);
            _proximoSom = 0f;
            Tocar(SomDoBicho.Espirro);
            f.EspirroDoGamba(this);
            Estado = EstadoDoBicho.Fugindo;
            _fugaAte = agora + 8f;
            Ir(p - para.normalized * 10f, Dados.trotar);
        }

        // ------------------------------------------------------------------ o urubu

        void PensarUrubu(FaunaDoMundo f, float agora, float dt)
        {
            var g = Grupo;
            Vector3 p = transform.position;
            bool temResto = g != null && f.RestoValido(g);
            if (!temResto)
            {
                // o resto sumiu: vai embora, alto (o recolhimento tira longe)
                Estado = EstadoDoBicho.Fugindo;
                _altitudeAlvo = 45f;
                if (!_temDestino) Ir(p + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * 400f, Dados.correr * 0.6f);
                return;
            }
            Vector3 resto = g.resto;
            // gente perto: sobe e roda
            bool genteNoResto = FaunaDoMundo.PertoDeAlguem(resto, 30f);
            if (genteNoResto) _semGenteDesde = agora;
            if (_altitude < 1f && agora >= _distraidoAte && f.PerceberGente(this, out var de, out _) && Vector3.Distance(p, de) < Dados.fuga)
            {
                _altitudeAlvo = 18f;
                Estado = EstadoDoBicho.Fugindo;
                Tocar(SomDoBicho.Urubu);
            }
            if (!genteNoResto && agora - _semGenteDesde > 8f)
            {
                // desce para comer: um lugar em volta do resto
                Vector3 lugar = resto + LugarNoGrupo.normalized * Random.Range(1.2f, 2.2f);
                float d = Vector3.Distance(Plano(p), Plano(lugar));
                if (d > 0.8f)
                {
                    Estado = EstadoDoBicho.Andando;
                    Ir(lugar, d > 10f ? Dados.correr * 0.5f : Mathf.Max(1f, d));
                    if (d < 18f) _altitudeAlvo = 0f;
                    return;
                }
                _altitudeAlvo = 0f;
                Estado = EstadoDoBicho.Pastando;
                _temDestino = false;
                _velAlvo = 0f;
                Olhar(resto);
                f.Bicar(g, dt);
                if (Random.value < 0.02f * Mathf.Max(0.3f, dt)) Tocar(SomDoBicho.Urubu);
                return;
            }
            // rodando alto em cima do resto (cada um na sua volta)
            Estado = EstadoDoBicho.Andando;
            _altitudeAlvo = 28f + (Id % 5) * 3f;
            float giro = agora * (10f + Id % 4) + Id * 67f;
            Vector3 ponto = resto + Quaternion.Euler(0f, giro, 0f) * Vector3.forward * (14f + Id % 3 * 5f);
            Ir(ponto, Dados.correr * 0.45f);
        }

        // ------------------------------------------------------------------ o esquilo

        void PensarEsquilo(FaunaDoMundo f, float agora, float dt)
        {
            if (_trepando && Trepar(f, agora)) return;
            if (agora >= _distraidoAte && f.Perceber(this, out var de, out _, out _) && Vector3.Distance(transform.position, de) < Dados.fuga)
            {
                _perigo = de;
                _perigoAte = agora + 8f;
                if (Trepar(f, agora)) return;
                Fugir(agora, true);
                return;
            }
            if (Estado == EstadoDoBicho.Fugindo && agora < _fugaAte) { Fugir(agora, false); return; }
            // (13/11) de vez em quando sobe numa árvore perto, sem susto, e desce depois
            if (agora > _proximaSubida)
            {
                bool primeira = _proximaSubida <= 0f;
                _proximaSubida = agora + Random.Range(20f, 60f);
                if (!primeira && Random.value < 0.55f && FaunaDoMundo.ArvoreMaisPerto(transform.position, 14f, out _, out _) && Trepar(f, agora, true)) return;
            }
            // no chão: corre um pouco e para (fuça)
            Estado = EstadoDoBicho.Pastando;
            if (!_temDestino && Random.value < 0.2f * Mathf.Max(0.3f, dt))
                Ir(transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(1.5f, 5f), Dados.trotar);
        }

        // ------------------------------------------------------------------ o peru

        void PensarPeru(FaunaDoMundo f, float agora, float dt)
        {
            float noite = CicloDoDia.Noite;
            if (noite > 0.6f && Estado != EstadoDoBicho.Fugindo)
            {
                // à noite: cada um vai para uma árvore perto do bando e sobe para dormir
                if (!_temArvore)
                {
                    Vector3 c = Grupo != null ? Grupo.Centro : transform.position;
                    _temArvore = FaunaDoMundo.ArvoreMaisPerto(c + LugarNoGrupo * 3f, 40f, out _peDoTronco, out _raioDoTronco);
                    if (!_temArvore) { Estado = EstadoDoBicho.Deitado; _temDestino = false; _velAlvo = 0f; return; }
                }
                Vector3 junto = _peDoTronco + Plano(transform.position - _peDoTronco).normalized * (_raioDoTronco + 0.3f);
                if (!_empoleirado && Vector3.Distance(Plano(transform.position), Plano(junto)) > 1f)
                {
                    Estado = EstadoDoBicho.Andando;
                    Ir(junto, Dados.trotar);
                    return;
                }
                _empoleirado = true;
                _altitudeAlvo = Random.Range(4.5f, 6.5f);
                Estado = EstadoDoBicho.Deitado;
                _temDestino = false;
                _velAlvo = 0f;
                return;
            }
            if (_empoleirado)
            {
                // amanheceu: desce
                _altitudeAlvo = 0f;
                if (_altitude > 0.1f) return;
                _empoleirado = false;
                _temArvore = false;
                Estado = EstadoDoBicho.Pastando;
            }
            if (Grupo != null && Grupo.lider == this && agora > _proximoGluGlu)
            {
                _proximoGluGlu = agora + Random.Range(20f, 60f);
                Tocar(SomDoBicho.GluGlu);
            }
            PensarPresa(f, agora, dt);
        }
    }
}
