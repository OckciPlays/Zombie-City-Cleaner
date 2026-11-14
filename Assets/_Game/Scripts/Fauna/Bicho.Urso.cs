using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// A mente do urso (a etapa 2, 12/11/2026: o urso-negro e o urso-pardo), no anfitrião. Sozinho, o macho; com 1 ou 2
    /// filhotes, a mãe. Fuça o chão, anda devagar e dorme à noite. Ataca quando é provocado (ferido), quando é assustado de
    /// perto (alguém a menos de 12 m sem ele ter notado antes) e quando alguém chega perto dos filhotes; de resto, ou vai
    /// embora (o negro, mais) ou fica olhando (o pardo, mais), e quem insiste chegando perto leva o aviso.
    /// - o aviso: fica de pé, bufa e bate os dentes (de 1,5 a 3,5 s). Quem se afasta (a mais de 25 m) acalma o urso; quem
    ///   chega a 5 m ou sai correndo (70%) faz ele vir;
    /// - às vezes o falso ataque (o negro, 50%; o pardo, 30%; perto do fogo, sempre que o fogo o segura): corre e para a 3–5 m,
    ///   e volta a avisar;
    /// - o ataque: a patada e a mordida (forte; o pardo às vezes quebra um osso), o empurrão que derruba, de 2 a 4 golpes, e
    ///   ele vai embora. Ferido demais (abaixo de 30% da vida), foge.
    /// O fogo segura o urso menos que os lobos: metade das vezes.
    /// </summary>
    public sealed partial class Bicho
    {
        float _avisoAte, _proximaPatada, _ultimaGente = -999f, _provocadoAte, _distanciaAntes, _fimDoAtaque;
        int _patadas, _patadasMax;
        bool _falso, _decidiu, _decidiuAtaque, _vaiEmbora;
        JogadorRede _alvoDoUrso;

        bool Negro => Especie == Especie.UrsoNegro;

        void PensarUrso(FaunaDoMundo f, float agora, float dt)
        {
            Vector3 p = transform.position;
            // os zumbis (e o que ele teme): se afasta
            if (f.Perceber(this, out var de, out _, out _, true))
            {
                _perigo = de;
                _perigoAte = agora + 6f;
            }
            bool surpresa = false;
            if (agora >= _distraidoAte && f.PerceberGente(this, out var deGente, out var quem))
            {
                surpresa = agora - _ultimaGente > 20f && Vector3.Distance(p, deGente) < 12f;
                _ultimaGente = agora;
                if (_alvoDoUrso == null || Estado != EstadoDoBicho.Investindo) _alvoDoUrso = quem;
            }
            if (Variante == Variante.Filhote) { PensarFilhoteDeUrso(f, agora); return; }
            if (Estado == EstadoDoBicho.Sentado && _peixeNaBoca == null) Estado = EstadoDoBicho.Andando;   // (largou o peixe)

            switch (Estado)
            {
                case EstadoDoBicho.Investindo when !_pescando:
                    PensarAtaqueDoUrso(f, agora);
                    return;
                case EstadoDoBicho.Fugindo:
                    if (agora < _fugaAte) { Fugir(agora, false); return; }
                    Estado = EstadoDoBicho.Andando;
                    _decidiu = false;
                    break;
                case EstadoDoBicho.Alerta when _avisoAte > 0f:
                    PensarAviso(f, agora);
                    return;
            }
            if (agora < _perigoAte && Vector3.Distance(p, _perigo) < 18f)
            {
                LargarPesca();
                Afastar(_perigo, 40f);
                return;
            }

            var j = _alvoDoUrso;
            if (j != null && j.AlvoValido && agora - _ultimaGente < 10f)
            {
                LargarPesca();
                Vector3 pj = j.transform.position;
                float d = Vector3.Distance(p, pj);
                if (Vida < Especies.Pontos(Especie, Variante) * 0.3f)
                {
                    _perigo = pj;
                    _fugaAte = agora + 20f;
                    Fugir(agora, true);
                    return;
                }
                bool filhotes = PertoDosFilhotes(pj, 20f);
                float chance = agora < _provocadoAte ? (Negro ? 0.5f : 0.85f)
                    : filhotes ? (Negro ? 0.6f : 0.9f)
                    : surpresa ? (Negro ? 0.35f : 0.7f) : 0f;
                // um sorteio por encontro (a surpresa, os filhotes, o tiro que levou)
                if (chance > 0f && !_decidiuAtaque)
                {
                    _decidiuAtaque = true;
                    if (Random.value < chance) { ComecarAviso(agora); return; }
                    _decidiu = true;
                    _vaiEmbora = true;
                }
                if (!_decidiu)
                {
                    _decidiu = true;
                    _vaiEmbora = Random.value < (Negro ? 0.65f : 0.35f);
                }
                if (_vaiEmbora)
                {
                    Afastar(pj, 50f);
                    return;
                }
                // fica olhando (de pé às vezes); quem insiste chegando perto leva o aviso
                Estado = EstadoDoBicho.Alerta;
                _temDestino = false;
                _velAlvo = 0f;
                Olhar(pj);
                if (d < 15f && Random.value < 0.35f * Mathf.Max(0.3f, dt)) ComecarAviso(agora);
                return;
            }
            _decidiu = _decidiuAtaque = false;
            _vaiEmbora = false;
            if (Estado == EstadoDoBicho.Alerta) Estado = EstadoDoBicho.Pastando;

            // calmo: fuça o chão (a mãe na frente, os filhotes atrás) e dorme à noite
            if (Estado == EstadoDoBicho.Deitado)
            {
                if (agora < _deitadoAte) return;
                Estado = EstadoDoBicho.Pastando;
            }
            else if (World.CicloDoDia.Noite > 0.7f && Random.value < 0.04f * dt)
            {
                Estado = EstadoDoBicho.Deitado;
                _deitadoAte = agora + Random.Range(60f, 240f);
                _temDestino = false;
                _velAlvo = 0f;
                // os filhotes deitam junto
                if (Grupo != null)
                    foreach (var b in Grupo.membros)
                        if (b != null && !b.Morto && b != this && b.Variante == Variante.Filhote)
                        {
                            b.Estado = EstadoDoBicho.Deitado;
                            b._deitadoAte = _deitadoAte;
                            b._temDestino = false;
                            b._velAlvo = 0f;
                        }
                return;
            }
            _olhar = Mathf.MoveTowards(_olhar, 0f, 40f * dt);
            if (!_pescando && CuidarDaCaverna(f, agora)) return;   // (a etapa 5: o urso dorme na caverna)
            if (Pescar(f, agora)) return;   // (a etapa 3: o urso pescando o salmão)
            Pastar(f, agora);
        }

        bool PertoDosFilhotes(Vector3 ponto, float raio)
        {
            if (Grupo == null) return false;
            foreach (var b in Grupo.membros)
                if (b != null && !b.Morto && b.Variante == Variante.Filhote && (b.transform.position - ponto).sqrMagnitude < raio * raio) return true;
            return false;
        }

        void Afastar(Vector3 de, float quanto)
        {
            Estado = EstadoDoBicho.Andando;
            Vector3 longe = Plano(transform.position - de);
            if (longe.sqrMagnitude < 0.01f) longe = -transform.forward;
            Ir(transform.position + longe.normalized * quanto, Dados.trotar);
        }

        // ------------------------------------------------------------------ o aviso, o falso ataque e o ataque

        void ComecarAviso(float agora)
        {
            Estado = EstadoDoBicho.Alerta;
            _avisoAte = agora + Random.Range(1.5f, 3.5f);
            _temDestino = false;
            _velAlvo = 0f;
            _distanciaAntes = _alvoDoUrso != null ? Vector3.Distance(transform.position, _alvoDoUrso.transform.position) : 99f;
            Tocar(Random.value < 0.5f ? SomDoBicho.BufoDoUrso : SomDoBicho.Dentes);
            if (_alvoDoUrso != null && _alvoDoUrso.Vida != null) _alvoDoUrso.Vida.AbalarAgora(Negro ? 4f : 6f, MotivoDaMente.Susto);
        }

        void PensarAviso(FaunaDoMundo f, float agora)
        {
            var j = _alvoDoUrso;
            if (j == null || !j.AlvoValido || j.Vida == null || j.Vida.Caido.Value)
            {
                Acalmar();
                return;
            }
            Vector3 pj = j.transform.position;
            float d = Vector3.Distance(transform.position, pj);
            Olhar(pj);
            bool fugindo = j.Velocidade > 3.5f && d > _distanciaAntes + 0.2f;
            _distanciaAntes = d;
            if (d > 25f)
            {
                // se afastou: o urso acalma e vai embora
                Acalmar();
                _decidiu = true;
                _vaiEmbora = true;
                return;
            }
            if (Random.value < 0.25f) Tocar(Random.value < 0.5f ? SomDoBicho.BufoDoUrso : SomDoBicho.Dentes);
            bool correuDele = fugindo && Random.value < 0.7f;
            if (d < 5f || correuDele || agora > _avisoAte)
            {
                // quem corre é perseguido de verdade; perto do fogo, metade das vezes ele só finge
                bool fogoSegura = f.NoFogo(pj, out _, out _) && Random.value < 0.5f;
                _falso = !correuDele && (fogoSegura || Random.value < (Negro ? 0.5f : 0.3f));
                ComecarAtaqueDoUrso(agora);
            }
        }

        void Acalmar()
        {
            _avisoAte = 0f;
            _falso = false;
            Estado = EstadoDoBicho.Andando;
        }

        void ComecarAtaqueDoUrso(float agora)
        {
            _avisoAte = 0f;
            Estado = EstadoDoBicho.Investindo;
            _fimDoAtaque = agora + 12f;
            _patadas = 0;
            _patadasMax = Random.Range(2, 5);
            _proximaPatada = 0f;
            Tocar(SomDoBicho.Rugido);
            if (!_falso && _alvoDoUrso != null && _alvoDoUrso.Vida != null) _alvoDoUrso.Vida.AbalarAgora(Negro ? 5f : 8f, MotivoDaMente.Susto);
        }

        void PensarAtaqueDoUrso(FaunaDoMundo f, float agora)
        {
            var j = _alvoDoUrso;
            if (j == null || !j.AlvoValido || j.Vida == null || j.Vida.Caido.Value || agora > _fimDoAtaque || (j.Passageiro != null && j.Passageiro.NoCarro))
            {
                Acalmar();
                if (j != null) Afastar(j.transform.position, 50f);
                _decidiu = true;
                _vaiEmbora = true;
                return;
            }
            Vector3 pj = j.transform.position;
            float d = Vector3.Distance(Plano(transform.position), Plano(pj));
            Olhar(pj);
            // (13/11) lá em cima, fora do alcance: vem correndo como sempre e, perto (a até 6 m), espera embaixo, de pé e
            // bufando; a paciência só conta dali (com o tempo, desiste e vai embora)
            if (d < 6f && ForaDoAlcance(j, agora))
            {
                if (PerdeuAPaciencia(agora))
                {
                    _semAlcanceDesde = -1f;
                    Acalmar();
                    Afastar(pj, 50f);
                    _decidiu = true;
                    _vaiEmbora = true;
                    return;
                }
                _fimDoAtaque = Mathf.Max(_fimDoAtaque, agora + 2f);
                if (d > 2.5f) { Ir(pj, Dados.trotar); return; }
                _temDestino = false;
                _velAlvo = 0f;
                if (Random.value < 0.1f) Tocar(SomDoBicho.BufoDoUrso);
                return;
            }
            if (_falso)
            {
                // o falso ataque: para a 3–5 m e volta a avisar
                if (d < Random.Range(3f, 5f))
                {
                    _falso = false;
                    Estado = EstadoDoBicho.Alerta;
                    _avisoAte = agora + Random.Range(2f, 4f);
                    _temDestino = false;
                    _velAlvo = 0f;
                    Tocar(SomDoBicho.BufoDoUrso);
                    return;
                }
                Ir(pj, Dados.correr * 0.8f);
                return;
            }
            if (d < 1.6f + Dados.comprimento * Escala * 0.35f)
            {
                _temDestino = false;
                _velAlvo = 0f;
                if (agora > _proximaPatada)
                {
                    _proximaPatada = agora + Random.Range(1.1f, 1.7f);
                    f.Golpear(this, j);
                    _patadas++;
                    if (_patadas >= _patadasMax)
                    {
                        // satisfeito: vai embora
                        Acalmar();
                        Afastar(pj, 60f);
                        _decidiu = true;
                        _vaiEmbora = true;
                    }
                }
                return;
            }
            if (d > 45f)
            {
                Acalmar();
                return;
            }
            Ir(pj, Dados.correr * 0.85f);
        }

        // o filhote: segue a mãe; com perigo perto, corre para trás dela; sem a mãe, foge
        void PensarFilhoteDeUrso(FaunaDoMundo f, float agora)
        {
            Bicho mae = null;
            if (Grupo != null)
                foreach (var b in Grupo.membros)
                    if (b != null && !b.Morto && b.Variante != Variante.Filhote) { mae = b; break; }
            Vector3 perigo = _alvoDoUrso != null && agora - _ultimaGente < 10f ? _alvoDoUrso.transform.position : agora < _perigoAte ? _perigo : Vector3.positiveInfinity;
            bool temPerigo = !float.IsInfinity(perigo.x) && Vector3.Distance(transform.position, perigo) < 30f;
            if (mae == null)
            {
                if (temPerigo)
                {
                    _perigo = perigo;
                    Fugir(agora, true);
                }
                else Pastar(f, agora);
                return;
            }
            // (13/11) a patada na água da mãe pescando usa a pose do ataque: não é perigo (o filhote corria para dentro da água)
            if (temPerigo || (!mae._pescando && (mae.Estado == EstadoDoBicho.Investindo || mae.Estado == EstadoDoBicho.Alerta)))
            {
                Vector3 de = temPerigo ? perigo : mae.transform.position + mae.transform.forward * 10f;
                Vector3 atras = mae.transform.position + Plano(mae.transform.position - de).normalized * 3f + LugarNoGrupo * 0.3f;
                if (FaunaDoMundo.FunduraEm(atras) > 0.15f) atras = transform.position;   // (atrás dela é água: fica onde está, olhando)
                Estado = EstadoDoBicho.Andando;
                float dist = Vector3.Distance(Plano(transform.position), Plano(atras));
                if (dist > 0.8f) Ir(atras, dist > 6f ? Dados.correr * 0.6f : Dados.trotar);
                else { _temDestino = false; _velAlvo = 0f; Estado = EstadoDoBicho.Alerta; }
                Olhar(de);
                return;
            }
            // (5a) a mãe sentada comendo o salmão: o filhote senta do lado dela e come junto
            if (mae.ComendoOSalmao)
            {
                Vector3 frente = mae.transform.forward, ladoDela = mae.transform.right * (LugarNoGrupo.x >= 0f ? 1f : -1f);
                Vector3 lugar = mae.transform.position + frente * mae.Dados.comprimento * mae.Escala * 0.4f + ladoDela * 0.85f;
                float dl = Vector3.Distance(Plano(transform.position), Plano(lugar));
                if (dl > 0.6f)
                {
                    Estado = EstadoDoBicho.Andando;
                    Ir(lugar, dl > 6f ? Dados.trotar : Dados.andar);
                    return;
                }
                _temDestino = false;
                _velAlvo = 0f;
                Estado = EstadoDoBicho.Sentado;
                Vector3 peixe = mae.transform.position + frente * mae.Dados.comprimento * mae.Escala * 0.2f;
                Vector3 para = Plano(peixe - transform.position);
                if (para.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(para);
                return;
            }
            if (Estado == EstadoDoBicho.Sentado) Estado = EstadoDoBicho.Pastando;
            if (Estado == EstadoDoBicho.Deitado && agora < _deitadoAte) return;
            // (13/11) a mãe longe (pescando, indo para a caverna): o filhote vai atrás dela
            float ateAMae = Vector3.Distance(Plano(transform.position), Plano(mae.transform.position));
            if (ateAMae > 12f)
            {
                Vector3 junto = mae.transform.position + LugarNoGrupo.normalized * 3f;
                if (FaunaDoMundo.FunduraEm(junto) > 0.25f)
                {
                    // a mãe na água (pescando): o filhote espera na beira, no ponto seco mais perto dela
                    Vector3 seco = transform.position;
                    for (int k = 1; k <= 16; k++)
                    {
                        Vector3 q = Vector3.Lerp(transform.position, mae.transform.position, k / 16f);
                        if (FaunaDoMundo.FunduraEm(q) > 0.15f) break;
                        seco = q;
                    }
                    junto = seco;
                }
                if (Vector3.Distance(Plano(transform.position), Plano(junto)) > 1.5f)
                {
                    Estado = EstadoDoBicho.Andando;
                    Ir(junto, ateAMae > 30f ? Dados.trotar : Dados.andar * 1.3f);
                    return;
                }
                Estado = EstadoDoBicho.Alerta;   // na beira, olhando a mãe
                _temDestino = false;
                _velAlvo = 0f;
                Olhar(mae.transform.position);
                return;
            }
            Pastar(f, agora);
        }

        // ------------------------------------------------------------------ o tiro e o ferimento

        void TiroNoUrso(Vector3 ponto, bool perto, float agora)
        {
            if (Variante == Variante.Filhote) { _perigo = ponto; _perigoAte = agora + 10f; return; }
            if (Estado == EstadoDoBicho.Investindo)
            {
                // atacando: o negro às vezes desiste com o tiro; o pardo, quase nunca
                if (Random.value < (Negro ? 0.3f : 0.08f))
                {
                    Acalmar();
                    _perigo = ponto;
                    _fugaAte = agora + 15f;
                    Fugir(agora, true);
                }
                return;
            }
            if (!perto) return;
            if (Negro && Random.value < 0.7f)
            {
                _perigo = ponto;
                _fugaAte = agora + Random.Range(10f, 18f);
                Fugir(agora, true);
            }
            else
            {
                Estado = EstadoDoBicho.Alerta;
                _temDestino = false;
                _velAlvo = 0f;
                Olhar(ponto);
            }
        }

        void FeridoUrso(JogadorRede autor, float agora)
        {
            LargarPesca();
            if (Variante == Variante.Filhote)
            {
                Fugir(agora, true);
                return;
            }
            _provocadoAte = agora + 60f;
            if (autor != null) _alvoDoUrso = autor;
            _ultimaGente = agora;
            if (Vida < Especies.Pontos(Especie, Variante) * 0.3f)
            {
                Acalmar();
                _fugaAte = agora + 25f;
                Fugir(agora, true);
                return;
            }
            if (Estado == EstadoDoBicho.Investindo) return;
            if (_alvoDoUrso != null && Random.value < (Negro ? 0.5f : 0.85f))
            {
                _falso = false;
                ComecarAtaqueDoUrso(agora);   // provocado: vem direto, sem aviso
            }
            else
            {
                _fugaAte = agora + Random.Range(12f, 20f);
                Fugir(agora, true);
            }
        }
    }
}
