using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Os Ocos são fotossensíveis (a pedido em 28/10/2026). Só a luz do sol os petrifica; a lanterna, o farol e a tocha
    /// só os afastam (outra etapa).
    ///  - <b>No sol</b> (um raio do zumbi até o sol sem nada no caminho, <see cref="ZombieManager.NoSol"/>), a pedra
    ///    (<see cref="Pedra"/>, de 0 a 1) sobe dos pés à cabeça em <c>tempoParaPetrificar</c> (10 s). Ele entra em desespero
    ///    e foge procurando sombra (<see cref="Estado.Fugindo"/>), cada vez mais lento: a velocidade e a animação caem até
    ///    <c>lentidaoNaPedra</c>. Quem está perseguindo ou atacando (ameaçado) não foge: continua atrás do jogador,
    ///    virando pedra. Na sombra, a pedra recua devagar (<c>tempoParaRecuar</c>).
    ///  - <b>Pedra cheia:</b> <see cref="Estado.Petrificado"/>. Fica na última pose (o Animator para), sem agente, sem
    ///    reagir, até a noite. A casca protege: o tiro tira só uma fração e a cabeça não explode; o carro (acima de 3 m/s)
    ///    e a espingarda de perto a quebram de uma vez. Quebrada, cai como estátua (o corpo todo de pedra, duro).
    ///  - <b>À noite</b>, cada estátua se solta num momento sorteado de 30 a 60 s depois de escurecer: a casca quebra
    ///    (a pedra desce em <c>tempoDeQuebrar</c>), com a poeira só em quem está sendo visto.
    ///  - <b>Longe</b> de todos (o zumbi virtual), nada muda; ao chegar perto de dia depois de muito tempo longe, se está no
    ///    sol, já aparece de pedra (ninguém viu a pedra subir).
    /// A pedra vai pela rede (um byte); a tela mostra pelo shader do Oco (_Petrificacao), por MaterialPropertyBlock.
    /// </summary>
    public partial class Zumbi
    {
        static readonly int PetrificacaoId = Shader.PropertyToID("_Petrificacao");
        static readonly int AlturaDosPesId = Shader.PropertyToID("_AlturaDosPes");

        /// <summary>A pedra do sol: 0 nada, 1 estátua. Vai pela rede.</summary>
        public float Pedra { get; private set; }
        public bool Petrificado => Atual == Estado.Petrificado;
        /// <summary>A lentidão da pedra (a velocidade e a animação).</summary>
        float FatorDaPedra => _m != null ? Mathf.Lerp(1f, _m.ajustes.lentidaoNaPedra, Pedra) : 1f;

        float _pedraVista, _proximoSol, _soltarEm = -1f, _quebrandoAte, _proximaSombra, _proximaAgonia, _longeDesde, _soltouEm = -100f;
        bool _noSol, _comPedra;
        MaterialPropertyBlock _blocoDaPedra;

        void ZerarPedra()
        {
            Pedra = 0f;
            _pedraVista = 0f;
            _soltarEm = -1f;
            _quebrandoAte = 0f;
            _noSol = false;
            if (animador != null) animador.speed = 1f;
            if (_comPedra)
            {
                foreach (var r in renderers) r.SetPropertyBlock(null);
                _comPedra = false;
            }
        }

        /// <summary>A pedra na tela (o shader) e a animação mais lenta. Nos níveis Perto e Médio, em todas as máquinas.</summary>
        void MostrarPedra(float dt)
        {
            // a cópia recebe a pedra 3 a 10 vezes por segundo: a da tela anda até ela
            _pedraVista = Copia ? Mathf.MoveTowards(_pedraVista, Pedra, dt * 1.5f) : Pedra;
            // a pedra subindo prende o corpo (a animação fica lenta); quebrando a casca ao se soltar, não: ele se debate
            // na velocidade normal (a câmera lenta enquanto a pedra descia parecia um movimento errado)
            bool soltando = _m != null && Time.time - _soltouEm < _m.ajustes.tempoDeQuebrar;
            if (animador.enabled) animador.speed = _m != null && !soltando ? Mathf.Lerp(1f, _m.ajustes.lentidaoNaPedra, _pedraVista) : 1f;
            if (_pedraVista <= 0.001f)
            {
                if (_comPedra)
                {
                    foreach (var r in renderers) r.SetPropertyBlock(null);   // de volta ao SRP Batcher
                    _comPedra = false;
                }
                return;
            }
            _blocoDaPedra ??= new MaterialPropertyBlock();
            _blocoDaPedra.SetFloat(PetrificacaoId, _pedraVista);
            _blocoDaPedra.SetFloat(AlturaDosPesId, transform.position.y);
            foreach (var r in renderers) r.SetPropertyBlock(_blocoDaPedra);
            _comPedra = true;
        }

        /// <summary>
        /// Virou estátua (o Animator para na pose) ou se soltou. Com efeito, ao se soltar, a casca quebra em poeira (só
        /// para quem está vendo: fora da tela, nada). Posar: virou pedra sem ninguém ver a pedra subir (já nasceu de
        /// pedra, ou estava longe): ele ganha uma pose do andar antes de parar (senão ficava na pose de repouso do modelo,
        /// com os braços abertos para baixo).
        /// </summary>
        void MudouAPedra(bool comEfeito, bool posar = false)
        {
            if (Petrificado)
            {
                if (posar) PosarParaAPedra();
                if (animador.enabled) animador.enabled = false;
                Velocidade = 0f;
                if (voz != null) voz.Stop();   // a estátua não geme (e corta o gemido que estava no meio)
                if (_somDaPedra != null) _somDaPedra.Stop();
                // a pedra fechando (o som do Oco, Zumbi.Voz.cs; sem ele, o estalo antigo, só para quem está vendo)
                var sons = Sons;
                if (comEfeito && sons != null && sons.petrificou != null)
                {
                    if (_m.PodeOuvir(transform.position, 35f)) Efeitos.Som(sons.petrificou, transform.position + Vector3.up, 0.9f * ZombieManager.VolumeDosZumbis, true, 35f);
                }
                else if (comEfeito && renderers.Length > 0 && renderers[0].isVisible)
                    Efeitos.Som(Efeitos.SocoParede, transform.position + Vector3.up, 0.7f * ZombieManager.VolumeDosZumbis, true, 20f);
                return;
            }
            _soltouEm = Time.time;
            if (NivelAtual != Nivel.Longe && !Morto)
            {
                animador.enabled = true;
                animador.speed = 1f;
                MostrarReacao();   // se debate, quebrando a casca
                var sons = Sons;
                if (comEfeito && sons != null) Falar(sons.voz, Fala.Chamado, 1f, 0.85f);   // e ruge, livre
            }
            else if (!Morto)
            {
                // de longe: se está sendo desenhado (dentro da distância de visão), volta a respirar, parado
                animador.speed = 1f;
                AtualizarAnimador();
            }
            if (comEfeito && renderers.Length > 0 && renderers[0].isVisible) Quebrar();
            else if (comEfeito && Sons != null && Sons.soltando != null && _m.PodeOuvir(transform.position))
                Efeitos.Som(Sons.soltando, transform.position + Vector3.up, 0.9f * ZombieManager.VolumeDosZumbis, true, 30f);   // fora da tela: só o som
        }

        /// <summary>Uma pose natural (um quadro sorteado do andar, ou do mancar e do rastejar) para a estátua.</summary>
        void PosarParaAPedra()
        {
            var corte = animador.cullingMode;
            animador.enabled = true;
            animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animador.speed = 1f;
            animador.SetFloat(SpeedId, Random.Range(0.2f, 1.1f));
            animador.SetInteger(PernasId, _pernas);
            string estado = _pernas == 1 ? "Mancando sem a esquerda" : _pernas == 2 ? "Mancando sem a direita" : _pernas >= 3 ? "Rastejando" : "Andar";
            animador.Play(estado, 0, Random.value);
            PosarAAgonia();   // o tronco numa pose da agonia (Zumbi.Voz.cs), se ele tem uma
            animador.Update(0f);
            animador.cullingMode = corte;
        }

        /// <summary>A casca quebrando: poeira e lascas saindo do corpo, e o estalo.</summary>
        void Quebrar()
        {
            if (partes == null) return;
            foreach (var p in partes)
                if (p != null) Efeitos.Impacto(p.position, (p.position - (transform.position + Vector3.up)).normalized + Vector3.up * 0.3f);
            var sons = Sons;
            Efeitos.Som(sons != null && sons.soltando != null ? sons.soltando : Efeitos.SocoParede, transform.position + Vector3.up, ZombieManager.VolumeDosZumbis, true, 30f);
        }

        // ------------------------------------------------------------------ anfitrião

        /// <summary>Chegou perto de alguém depois de um tempo longe: de dia e no sol, já está de pedra (ninguém viu subir).</summary>
        void ChegouDeLonge()
        {
            if (Morto || Petrificado || _m == null || Time.time - _longeDesde < 3f || !_m.NoSol(transform.position)) return;
            Pedra = 1f;
            _pedraVista = 1f;
            Petrificar(true);
        }

        /// <summary>A cada quadro, no anfitrião, para todos os vivos (também os de longe: o relógio de soltar à noite).</summary>
        internal void SolNoServidor(float agora, float dt)
        {
            if (Morto || _m == null) return;
            var a = _m.ajustes;
            if (!_m.EDia)
            {
                _noSol = false;
                if (Petrificado)
                {
                    // à noite: cada um se solta num momento sorteado
                    if (_soltarEm < 0f) _soltarEm = agora + Random.Range(a.soltarANoite.x, a.soltarANoite.y);
                    else if (agora >= _soltarEm) Soltar(agora);
                    return;
                }
                if (Pedra > 0f) MudarPedra(Pedra - dt / (agora < _quebrandoAte ? a.tempoDeQuebrar : a.tempoParaRecuar));
                if (Atual == Estado.Fugindo) Pausar(agora);   // sem sol, a fuga acaba
                // ao amanhecer: quem está onde o sol vai bater procura abrigo (quem não achar vira estátua quando o sol vier)
                if (_m.Amanhecendo && NivelAtual != Nivel.Longe && agora >= _proximoSol && !Engajado
                    && (Atual == Estado.Parado || Atual == Estado.Vagando || Atual == Estado.Procurando || Atual == Estado.Investigando))
                {
                    _proximoSol = agora + Random.Range(2f, 4f);
                    if (_m.Exposto(transform.position)) ComecarAbrigo(a);
                }
                return;
            }
            _soltarEm = -1f;
            if (Petrificado || NivelAtual == Nivel.Longe) return;
            if (agora >= _proximoSol)
            {
                _proximoSol = agora + Random.Range(0.3f, 0.5f);
                _noSol = _m.NoSol(transform.position);
            }
            if (_noSol)
            {
                MudarPedra(Pedra + dt / Mathf.Max(0.5f, a.tempoParaPetrificar));
                if (Pedra >= 1f)
                {
                    Petrificar();
                    return;
                }
                // em desespero: foge procurando sombra. Só quem foi ferido (tiro, soco) continua atrás do jogador, virando
                // pedra; quem saiu só pelo barulho volta para o abrigo (a pedido em 28/10/2026)
                if (!Ferido && Atual != Estado.Fugindo) ComecarFuga(agora);
            }
            else if (Pedra > 0f) MudarPedra(Pedra - dt / Mathf.Max(0.5f, agora < _quebrandoAte ? a.tempoDeQuebrar : a.tempoParaRecuar));
        }

        void MudarPedra(float p)
        {
            Pedra = Mathf.Clamp01(p);
            // a velocidade do agente acompanha a lentidão
            if (_velocidadeBase >= 0f && agente.enabled && agente.isOnNavMesh) agente.speed = _velocidadeBase * FatorDasPernas * FatorDaPedra * _ritmo;
        }

        void ComecarFuga(float agora)
        {
            if (!NoNavMesh) return;
            Atual = Estado.Fugindo;
            _alvo = null;
            _ameacadoAte = 0f;   // o medo do sol vence a ameaça: na sombra, ele não sai de novo atrás de quem fez barulho
            _proximaSombra = 0f;
            _proximaAgonia = agora + Random.Range(0.2f, 0.8f);
            if (Sons == null) Gemer(1.25f);   // o Oco grita pela agonia (Zumbi.Voz.cs)
            FugirDoSol(agora, _m.ajustes);
        }

        /// <summary>
        /// Fugindo do sol: corre para a sombra mais perto que achar (mantém o destino até chegar; refaz a busca se chegou e
        /// ainda está no sol, ou depois de 4 s) e se debate (a reação ao tiro, no tronco). Na sombra, fica encolhido até a
        /// pedra recuar.
        /// </summary>
        void FugirDoSol(float agora, AjustesDeZumbi a)
        {
            // quem não tem uma agonia própria (o tipo 0, "como está hoje") se debate (a reação ao tiro, no tronco)
            if (agora >= _proximaAgonia && _noSol && (TipoDeAgonia == 0 || !TemAgonia()))
            {
                _proximaAgonia = agora + Random.Range(0.9f, 1.6f);
                MostrarReacao();
                Reacoes++;
            }
            if (!_noSol)
            {
                if (agente.hasPath) agente.ResetPath();
                if (Pedra <= 0f) Pausar(agora);
                return;
            }
            // ainda indo para a sombra escolhida (ou o caminho ainda sendo calculado): mantém o destino até chegar. Trocando
            // de destino, ele ziguezagueava e virava pedra no caminho. Só procura outra se chegou e ainda está no sol, ou se
            // ficou 4 s sem caminho
            if (agente.pathPending || (agente.hasPath && !Chegou() && agora < _proximaSombra)) return;
            _proximaSombra = agora + 4f;
            if (AcharSombra(a, out var sombra)) Ir(sombra, a.velocidadeDeFuga, 0.3f);
            else
            {
                // nenhuma sombra por perto: corre para qualquer lado, longe dali
                Vector2 r = Random.insideUnitCircle.normalized * 8f;
                if (NavMesh.SamplePosition(transform.position + new Vector3(r.x, 0f, r.y), out var hit, 3f, NavMesh.AllAreas))
                    Ir(hit.position, a.velocidadeDeFuga, 0.3f);
            }
        }

        /// <summary>A sombra mais perto: 10 pontos do NavMesh em volta, de 3 m até o raio da sombra.</summary>
        bool AcharSombra(AjustesDeZumbi a, out Vector3 ponto)
        {
            ponto = default;
            float melhor = float.MaxValue;
            bool achou = false;
            for (int t = 0; t < 10; t++)
            {
                float raio = Mathf.Lerp(3f, a.raioDaSombra, t / 9f);
                Vector2 r = Random.insideUnitCircle.normalized * raio;
                if (!NavMesh.SamplePosition(transform.position + new Vector3(r.x, 0f, r.y), out var hit, 2f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(hit.position.y - transform.position.y) > 3f || _m.Exposto(hit.position)) continue;
                float d = (hit.position - transform.position).sqrMagnitude;
                if (d < melhor)
                {
                    melhor = d;
                    ponto = hit.position;
                    achou = true;
                }
            }
            return achou;
        }

        /// <summary>Ao amanhecer: vai (andando, sem pressa) para o abrigo que achar.</summary>
        void ComecarAbrigo(AjustesDeZumbi a)
        {
            if (!NoNavMesh || !AcharAbrigo(a, out var abrigo)) return;   // sem abrigo por perto: tenta de novo daqui a pouco
            Ir(abrigo, a.velocidadeInvestigando, 0.5f);
            Atual = Estado.Abrigando;
            _alvo = null;
            _casa = abrigo;
        }

        /// <summary>
        /// Um abrigo do sol: 14 pontos do NavMesh em volta, de 4 m até <c>raioDoAbrigo</c>, onde o sol do dia não bate
        /// (atrás das janelas, não). O que tem teto (dentro de uma casa ou de um prédio) vale como 15 m mais perto que a
        /// sombra aberta (de um prédio, das árvores): ele prefere entrar.
        /// </summary>
        bool AcharAbrigo(AjustesDeZumbi a, out Vector3 ponto)
        {
            ponto = default;
            float melhor = float.MaxValue;
            bool achou = false;
            for (int t = 0; t < 14; t++)
            {
                float raio = Mathf.Lerp(4f, a.raioDoAbrigo, t / 13f);
                Vector2 r = Random.insideUnitCircle.normalized * raio;
                if (!NavMesh.SamplePosition(transform.position + new Vector3(r.x, 0f, r.y), out var hit, 2.5f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(hit.position.y - transform.position.y) > 4f || _m.Exposto(hit.position)) continue;
                float nota = Vector3.Distance(hit.position, transform.position) + (_m.Coberto(hit.position) ? 0f : 15f);
                if (nota < melhor)
                {
                    melhor = nota;
                    ponto = hit.position;
                    achou = true;
                }
            }
            return achou;
        }

        /// <summary>Vira estátua: para na pose de agora (ou numa do andar, se ninguém viu), sem agente, sem alvo, até a noite.</summary>
        void Petrificar(bool semVer = false)
        {
            Pedra = 1f;
            Atual = Estado.Petrificado;
            _alvo = null;
            _golpeEm = 0f;
            _alerta = 0f;
            _soltarEm = -1f;
            if (agente.enabled)
            {
                if (agente.isOnNavMesh) agente.ResetPath();
                agente.enabled = false;
            }
            Velocidade = 0f;
            MudouAPedra(true, semVer);
        }

        /// <summary>Se solta da pedra: a casca quebra (a pedra desce em tempoDeQuebrar) e ele volta a vagar dali.</summary>
        void Soltar(float agora)
        {
            if (!Petrificado) return;
            var a = _m.ajustes;
            _soltarEm = -1f;
            _quebrandoAte = agora + a.tempoDeQuebrar;
            Atual = Estado.Parado;
            _casa = transform.position;
            _fimDaPausa = agora + a.tempoDeQuebrar + Random.Range(0.5f, 2f);
            if (NivelAtual != Nivel.Longe && !agente.enabled)
            {
                if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas)) transform.position = hit.position;
                agente.enabled = true;
            }
            if (NivelAtual == Nivel.Longe) Pedra = 0f;   // longe de todos: sem a quebra
            MudouAPedra(true);
            if (NivelAtual != Nivel.Longe) Gemer(0.85f);
        }

        // ------------------------------------------------------------------ o menu de teste (F1)

        /// <summary>Teste: vira estátua agora (pedra cheia).</summary>
        internal void PetrificarJa()
        {
            if (Morto || Petrificado) return;
            Pedra = 1f;
            _pedraVista = 1f;
            Petrificar(true);
        }

        /// <summary>Teste: solta agora (mesmo de dia: no sol, a pedra volta a subir).</summary>
        internal void SoltarJa(float agora)
        {
            Soltar(agora);
            if (_m.EDia) MudarPedra(0f);   // de dia, sem a quebra: senão, no sol, a pedra cheia virava estátua de novo na hora
        }
    }
}
