using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Sentado no chão perto da fogueira (18/11/2026, a etapa 1 da fogueira): a mesma pose de quem vai sentado no piso da
    /// caçamba (uma perna esticada e a outra dobrada, as mãos na coxa e no joelho), com o quadril a uns 14 cm do chão e o
    /// tronco um pouco para a frente, para o fogo. Tocando (ou com o violão na mão), de pernas cruzadas (19/11/2026: o
    /// instrumento no colo, sem o joelho dobrado no caminho). Em todas as máquinas, pelo <see cref="JogadorRede.SentadoNoChao"/>
    /// (fora do carro: o LateUpdate daqui só cuida dela quando ele está a pé).
    /// </summary>
    public partial class PassageiroDoCarro
    {
        const float QuadrilNoChao = 0.14f;
        bool _noChaoAntes;

        void SentadoNoChao()
        {
            bool sentado = _rede != null && _rede.IsSpawned && _rede.SentadoNoChao.Value && _rede.animadorCorpo != null;
            if (!sentado)
            {
                if (_noChaoAntes)
                {
                    _noChaoAntes = false;
                    if (_rede != null && _rede.animadorCorpo != null) _rede.animadorCorpo.enabled = true;
                }
                return;
            }
            _noChaoAntes = true;
            PoseNoChao(_rede.Tocando.Value.instrumento != 0 || SegurandoOViolao);
        }

        // o violão na mão, parado (sem tocar): ele já fica no corpo, na posição de tocar
        bool SegurandoOViolao => _rede != null && _rede.IsSpawned && _rede.NaMao == Item.Violao && !_rede.NaMaoEscondida
                                 && (_rede.Vida == null || !_rede.Vida.Caido.Value);

        void PoseNoChao(bool cruzadas)
        {
            var animador = _rede.animadorCorpo;
            if (_repouso.Count == 0) return;
            if (animador.enabled) animador.enabled = false;
            foreach (var (osso, pos, rot) in _repouso) osso.SetLocalPositionAndRotation(pos, rot);
            Vector3 frente = transform.forward, cima = transform.up, direita = transform.right;
            // o quadril desce até o chão (a raiz fica em pé: é a cápsula do jogador)
            Vector3 assento = transform.position + cima * QuadrilNoChao;
            if (_ossos.TryGetValue("hips", out var quadril))
            {
                Vector3 doRepouso = transform.TransformPoint(_quadrilNaRaiz);
                quadril.position += assento - doRepouso;
            }
            if (_ossos.TryGetValue("spine", out var coluna))
                coluna.rotation = Quaternion.AngleAxis(cruzadas ? 4f : 8f, direita) * coluna.rotation;   // (um pouco para a frente, para o fogo)
            if (cruzadas)
            {
                // de pernas cruzadas: os joelhos baixos, para os lados; as canelas cruzadas na frente, a direita por cima; o pé
                // de cada uma embaixo do joelho da outra, deitado de lado
                Vector3 tornozeloD = assento + frente * 0.30f - direita * 0.11f - cima * (QuadrilNoChao - 0.085f);
                DoisOssos("thigh.R", "shin.R", tornozeloD, direita * 0.9f + cima * 0.35f + frente * 0.15f);
                PeNoPlano(".R", -direita * 0.8f + frente * 0.35f - cima * 0.3f);
                Vector3 tornozeloE = assento + frente * 0.20f + direita * 0.13f - cima * (QuadrilNoChao - 0.055f);
                DoisOssos("thigh.L", "shin.L", tornozeloE, -direita * 0.9f + cima * 0.35f + frente * 0.15f);
                PeNoPlano(".L", direita * 0.8f + frente * 0.35f - cima * 0.3f);
                // as mãos nos joelhos (o instrumento as leva depois)
                if (_ossos.TryGetValue("shin.R", out var joelhoDireito))
                {
                    DoisOssos("upper_arm.R", "forearm.R", joelhoDireito.position + cima * 0.06f - direita * 0.05f, -cima * 0.5f + direita * 0.5f - frente * 0.3f);
                    Palma(".R", -cima);
                }
                if (_ossos.TryGetValue("shin.L", out var joelhoEsquerdo))
                {
                    DoisOssos("upper_arm.L", "forearm.L", joelhoEsquerdo.position + cima * 0.06f + direita * 0.05f, -cima * 0.5f - direita * 0.5f - frente * 0.3f);
                    Palma(".L", -cima);
                }
            }
            else
            {
                // a direita esticada, o calcanhar no chão
                Vector3 tornozeloD = assento + frente * 0.80f + direita * 0.13f - cima * (QuadrilNoChao - 0.06f);
                DoisOssos("thigh.R", "shin.R", tornozeloD, cima + frente * 0.2f);
                PeNoPlano(".R", frente * 0.35f + cima * 0.93f);
                // a esquerda dobrada: o pé no chão, o joelho para cima
                Vector3 tornozeloE = assento + frente * 0.52f - direita * 0.13f - cima * (QuadrilNoChao - 0.07f);
                DoisOssos("thigh.L", "shin.L", tornozeloE, cima * 0.95f - direita * 0.15f);
                PeNoPlano(".L", frente * 0.95f - cima * 0.1f);
                // as mãos: a direita na coxa esticada, a esquerda com o antebraço no joelho
                if (_ossos.TryGetValue("thigh.R", out var coxaD) && _ossos.TryGetValue("shin.R", out var joelhoD))
                {
                    DoisOssos("upper_arm.R", "forearm.R", Vector3.Lerp(coxaD.position, joelhoD.position, 0.55f) + cima * 0.08f,
                              -cima * 0.6f - frente * 0.5f + direita * 0.3f);
                    Palma(".R", -cima);
                }
                if (_ossos.TryGetValue("shin.L", out var joelhoE))
                {
                    DoisOssos("upper_arm.L", "forearm.L", joelhoE.position + frente * 0.08f + cima * 0.02f, -cima * 0.3f - frente * 0.3f - direita * 0.6f);
                    Palma(".L", -cima);
                }
            }
            // (18/11/2026) o corpo fica parado: o olhar vira o peito um pouco e a cabeça (o giro pela rede) e inclina a
            // cabeça para cima e para baixo (a pose de repouso apagou o que o JogadorRede tinha posto)
            float giro = Mathf.Clamp(_rede.GiroSentado.Value, -MovimentoPrimeiraPessoa.GiroSentadoMaximo, MovimentoPrimeiraPessoa.GiroSentadoMaximo);
            var peito = _rede.ossoPeito;
            if (peito != null) peito.rotation = Quaternion.AngleAxis(giro * 0.35f, cima) * peito.rotation;
            if (_ossos.TryGetValue("head", out var cabeca))
            {
                cabeca.rotation = Quaternion.AngleAxis(Mathf.Clamp(giro * 0.65f, -GiroDaCabeca, GiroDaCabeca), cima) * cabeca.rotation;
                Vector3 lateral = Quaternion.AngleAxis(giro, cima) * direita;
                cabeca.rotation = Quaternion.AngleAxis(Mathf.Clamp(_rede.Pitch.Value, -60f, 60f) * 0.7f, lateral) * cabeca.rotation;
            }
        }
    }

    /// <summary>
    /// A pose de quem toca (18/11/2026, a etapa 4 da fogueira; 19/11/2026, refeita com calma para o vídeo da fogueira: "fazer
    /// ficar bem legal e dar uma melhorada nos detalhes... com cuidado e realismo com cada instrumento"). Em todas as
    /// máquinas, depois da animação (e da pose de sentado no chão), pelo <see cref="JogadorRede.Tocando"/>: o instrumento sai
    /// da mão (o lugar da lata) e vai para o corpo, os braços vão até ele com o IK de dois ossos daqui, e cada mão dobra o
    /// pulso, fecha os dedos e o polegar (o corpo tem um osso para os quatro dedos e outro para o polegar) no tempo da música
    /// (<see cref="MusicaDoJogador.Batida"/>); o tronco balança um pouco e a cabeça acompanha:
    /// - o violão no colo (em pé, na barriga), o braço dele para a esquerda, subindo e indo para a frente; a esquerda com o
    ///   braço do violão entre o polegar (atrás) e os dedos (na frente, nas cordas), trocando de acorde a cada compasso (os
    ///   dedos levantam, a mão desliza e eles apertam de novo); a direita com o antebraço no bojo, batendo as cordas em
    ///   colcheias (desce no tempo, sobe no contratempo) com a palheta; no dedilhado, os dedos e o polegar; a cabeça olha o
    ///   braço do violão;
    /// - o pandeiro na esquerda, na frente do peito, a pele para a direita e para a frente: o polegar na beira, os dedos atrás
    ///   da pele, virando de leve (o "abre e fecha"); a direita bate em semicolcheias: o polegar perto da beira, a ponta dos
    ///   dedos no meio, a base da mão perto da beira e a ponta de novo, balançando a mão;
    /// - a gaita na boca: a esquerda a segura pela metade esquerda (o polegar embaixo, o indicador em cima) e a direita fica
    ///   em concha por fora, abrindo e fechando no tempo; ela corre de um lado para o outro devagar;
    /// - a flauta doce 45° para baixo da boca: cada mão do seu lado, a esquerda em cima, os dedos dando a volta na frente até
    ///   os furos e o polegar atrás, subindo e descendo.
    /// A flauta e a gaita vão com a cabeça (a boca e o rosto no espaço dela). O dono, em primeira pessoa, vê o próprio corpo
    /// tocando (os braços de primeira pessoa somem, como na moto).
    /// </summary>
    public partial class PassageiroDoCarro
    {
        Transform _instrumento;
        MeshRenderer _rInstrumento;
        Item _instrumentoMontado;
        bool _tocandoAntes;
        GameObject _maoEscondidaPeloInstrumento;
        bool _escondiACabeca;
        // (19/11/2026) o repouso das mãos, dos dedos e dos polegares, e a boca e os eixos do rosto no espaço da cabeça (do
        // prefab: LerOssos)
        readonly Dictionary<Transform, Quaternion> _maosNoRepouso = new Dictionary<Transform, Quaternion>();
        Vector3 _bocaNaCabeca, _frenteNaCabeca, _cimaNaCabeca;
        bool _temRosto;

        const float EscalaDoViolao = 0.85f, EscalaDoPandeiro = 1.05f;
        // o meio da mão esquerda no braço do violão (no comprimento dele) em cada acorde da sequência (sol, dó, ré e mi menor:
        // entre a primeira e a terceira casa)
        static readonly float[] AcordesDoViolao = { 0.44f, 0.475f, 0.455f, 0.48f };

        static Item ItemDoInstrumento(int i) => i switch { 1 => Item.Violao, 2 => Item.Pandeiro, 3 => Item.Gaita, 4 => Item.Flauta, _ => Item.Nada };

        void PoseDoInstrumento()
        {
            var t = _rede != null && _rede.IsSpawned ? _rede.Tocando.Value : default;
            // (18/11/2026, a pedido: "quando pegamos o violão na mão sem tocar, o violão fica deitado, acho que já deveria
            // ficar na posição de tocar, isso ajuda a limpar a visão do jogador") o violão na mão, parado, já fica no corpo,
            // na posição de tocar, sem mexer as mãos
            bool segurando = t.instrumento == 0 && SegurandoOViolao;
            bool tocando = (t.instrumento != 0 || segurando) && _rede.animadorCorpo != null && _ossos.Count > 0;
            if (!tocando)
            {
                if (_tocandoAntes) PararDeTocar();
                return;
            }
            var item = segurando ? Item.Violao : ItemDoInstrumento(t.instrumento);
            if (!_tocandoAntes || item != _instrumentoMontado) ComecarATocar(item);
            _tocandoAntes = true;
            // o que está na mão (o lugar da lata) some: o instrumento está no corpo
            int m = Itens.MaoDe(item);
            if (_rede.naMaoCorpo != null && m >= 0 && m < _rede.naMaoCorpo.Length && _rede.naMaoCorpo[m] != null && _rede.naMaoCorpo[m].activeSelf)
            {
                _rede.naMaoCorpo[m].SetActive(false);
                _maoEscondidaPeloInstrumento = _rede.naMaoCorpo[m];
            }
            // o jogador desta máquina, com a câmera nos olhos, vê o próprio corpo (sem a cabeça) e não os braços de primeira
            // pessoa; trocando para a terceira pessoa no meio, a cabeça volta
            if (CameraNosOlhos)
            {
                _rede.CorpoNoVeiculo(true);
                _escondiACabeca = true;
                if (_rede.cameraBracos != null && _rede.cameraBracos.enabled) _rede.cameraBracos.enabled = false;
            }
            else if (_escondiACabeca) VoltarACabeca();
            // o instrumento como a lança do corpo: escondido, ou só a sombra, como o corpo
            var pele = _rede.renderersCorpo != null && _rede.renderersCorpo.Length > 0 ? _rede.renderersCorpo[0] : null;
            if (pele != null)
            {
                _rInstrumento.shadowCastingMode = pele.shadowCastingMode;
                _rInstrumento.renderingLayerMask = pele.renderingLayerMask;
                _rInstrumento.enabled = pele.enabled;
            }
            // parado (só segurando): a batida em -1, as mãos sem tocar
            float batida = segurando ? -1f : MusicaDoJogador.Batida(t);
            switch (item)
            {
                case Item.Violao: PoseDoViolao(batida, t.musica); break;
                case Item.Pandeiro: PoseDoPandeiro(batida); break;
                case Item.Gaita: PoseDaGaita(batida); break;
                case Item.Flauta: PoseDaFlauta(batida); break;
            }
        }

        void ComecarATocar(Item item)
        {
            if (_instrumento == null)
            {
                var go = new GameObject("Instrumento tocando");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>();
                _rInstrumento = go.AddComponent<MeshRenderer>();
                _instrumento = go.transform;
            }
            _instrumento.gameObject.SetActive(true);
            _instrumento.GetComponent<MeshFilter>().sharedMesh = ItensDaCaca.MalhaDoItem(item);
            var itens = ItensNoMundo.Instancia;
            if (itens != null) _rInstrumento.sharedMaterial = itens.MaterialDe(item);
            if (_rede.animadorCorpo != null) _instrumento.gameObject.layer = _rede.animadorCorpo.gameObject.layer;
            _instrumentoMontado = item;
        }

        void PararDeTocar()
        {
            _tocandoAntes = false;
            if (_instrumento != null) _instrumento.gameObject.SetActive(false);
            // as mãos e os dedos voltam ao repouso (a animação e a pose sentada põem o resto)
            foreach (var (osso, rot) in _maosNoRepouso)
                if (osso != null) osso.localRotation = rot;
            // o que estava na mão volta (se ainda é o que está nela)
            if (_maoEscondidaPeloInstrumento != null && !_rede.NaMaoEscondida && _rede.Inventario != null && MusicaDoJogador.EhInstrumento(_rede.Inventario.NaMao))
                _maoEscondidaPeloInstrumento.SetActive(true);
            _maoEscondidaPeloInstrumento = null;
            if (_escondiACabeca) VoltarACabeca();
        }

        /// <summary>A câmera nos olhos de quem toca: o jogador desta máquina, na primeira pessoa.</summary>
        bool CameraNosOlhos => _rede.IsOwner && !_rede.EmTerceiraPessoa;

        // a cabeça (e os braços de primeira pessoa) de volta, só se foi o instrumento que os escondeu (fora do carro: lá, quem
        // cuida é ele)
        void VoltarACabeca()
        {
            _escondiACabeca = false;
            if (_fase != Fase.Fora) return;
            _rede.CorpoNoVeiculo(false);
            if (_rede.cameraBracos != null && !_rede.EmTerceiraPessoa && _rede.IsOwner) _rede.cameraBracos.enabled = true;
        }

        // ------------------------------------------------------------------ o corpo, o rosto, as mãos e os dedos

        Transform CabecaDeQuemToca => _ossos.TryGetValue("head", out var c) ? c : _rede.ossoCabeca;
        // o peito, a boca e os eixos do rosto (o rosto vai com a cabeça: a flauta e a gaita a acompanham)
        Vector3 PeitoDeQuemToca => _rede.ossoPeito != null ? _rede.ossoPeito.position : transform.position + transform.up * 1.35f;
        Vector3 BocaDeQuemToca
        {
            get
            {
                var cabeca = CabecaDeQuemToca;
                if (cabeca == null) return transform.position + transform.up * 1.6f + transform.forward * 0.11f;
                return _temRosto ? cabeca.position + cabeca.rotation * _bocaNaCabeca : cabeca.position + transform.up * 0.03f + transform.forward * 0.11f;
            }
        }
        Vector3 FrenteDoRosto => _temRosto && CabecaDeQuemToca != null ? CabecaDeQuemToca.rotation * _frenteNaCabeca : transform.forward;
        Vector3 CimaDoRosto => _temRosto && CabecaDeQuemToca != null ? CabecaDeQuemToca.rotation * _cimaNaCabeca : transform.up;

        /// <summary>
        /// O corpo de quem toca: o tronco inclina <paramref name="inclina"/> graus para a frente e balança
        /// <paramref name="balanco"/> graus de um lado para o outro (a cada 4 tempos); a cabeça olha <paramref name="baixo"/> graus
        /// para baixo e <paramref name="esquerda"/> graus para a esquerda e acena <paramref name="aceno"/> graus em cada tempo.
        /// </summary>
        void CorpoNaMusica(float batida, float inclina, float balanco, float baixo, float esquerda, float aceno)
        {
            Vector3 cima = transform.up, direita = transform.right, frente = transform.forward;
            bool parado = batida < 0f;
            if (_ossos.TryGetValue("spine", out var coluna))
            {
                float lado = parado ? 0f : balanco * Mathf.Sin(batida * Mathf.PI * 0.5f);
                coluna.rotation = Quaternion.AngleAxis(lado, frente) * Quaternion.AngleAxis(inclina, direita) * coluna.rotation;
            }
            var cabeca = CabecaDeQuemToca;
            if (cabeca != null && (baixo != 0f || esquerda != 0f || aceno != 0f))
            {
                float acena = parado ? 0f : aceno * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(batida * Mathf.PI * 2f)), 4f);
                cabeca.rotation = Quaternion.AngleAxis(baixo + acena, direita) * Quaternion.AngleAxis(-esquerda, cima) * cabeca.rotation;
            }
        }

        // põe o instrumento: o ponto 'local' da malha vai em 'onde', com os eixos X e Y dela em 'x' e 'y' (no mundo)
        Quaternion PorOInstrumento(Vector3 local, Vector3 onde, Vector3 x, Vector3 y, float escala = 1f)
        {
            y = Vector3.ProjectOnPlane(y, x).normalized;
            x.Normalize();
            var rot = Quaternion.LookRotation(Vector3.Cross(x, y), y);
            _instrumento.localScale = Vector3.one * escala;
            _instrumento.SetPositionAndRotation(onde - rot * (local * escala), rot);
            return rot;
        }

        Vector3 NoInstrumento(Vector3 local) => _instrumento.TransformPoint(local);

        void Braco(string l, Vector3 pulso, Vector3 cotovelo, Vector3 palma)
        {
            DoisOssos("upper_arm" + l, "forearm" + l, pulso, cotovelo);
            Palma(l, palma);
        }

        /// <summary>
        /// A mão (o pulso): os dedos para <paramref name="dedos"/> e a palma para <paramref name="palma"/> (no mundo), a partir
        /// da mão reta no antebraço, sem dobrar mais do que <paramref name="maximo"/> graus.
        /// </summary>
        void Mao(string l, Vector3 dedos, Vector3 palma, float maximo = 65f)
        {
            if (!_ossos.TryGetValue("hand" + l, out var mao)) return;
            if (_maosNoRepouso.TryGetValue(mao, out var reta)) mao.localRotation = reta;
            var quero = GiroDaMao(mao, dedos, palma);
            float angulo = Quaternion.Angle(mao.rotation, quero);
            mao.rotation = angulo > maximo ? Quaternion.Slerp(mao.rotation, quero, maximo / angulo) : quero;
        }

        static readonly string[] NomesDosDedos = { "index", "middle", "ring", "pinky" };

        /// <summary>
        /// Os dedos fecham, em graus, para o lado da palma (a partir do repouso): os quatro nas duas juntas (a de cima 90% da
        /// de baixo) e o polegar. <paramref name="leque"/>: graus a mais em cada dedo, do indicador para o mínimo (negativo: o
        /// indicador fecha mais, como quem segura a palheta).
        /// </summary>
        void Dedos(string l, float dedos, float polegar, float leque = 0f)
        {
            if (!_ossos.TryGetValue("hand" + l, out var mao) || !_palma.TryGetValue(mao, out var local)) return;
            Vector3 palma = mao.rotation * local;
            for (int i = 0; i < 4; i++)
            {
                float graus = dedos + leque * (i - 1.5f);
                Fechar(NomesDosDedos[i] + "1" + l, palma, graus);
                Fechar(NomesDosDedos[i] + "2" + l, palma, graus * 0.9f);
            }
            Fechar("thumb" + l, palma, polegar * 0.6f);
            Fechar("thumb2" + l, palma, polegar * 0.5f);
        }

        void Fechar(string nome, Vector3 palma, float graus)
        {
            if (!_ossos.TryGetValue(nome, out var osso)) return;
            if (_maosNoRepouso.TryGetValue(osso, out var repouso)) osso.localRotation = repouso;
            Vector3 eixo = Vector3.Cross(osso.up, palma);   // (o osso vai ao longo do eixo Y dele)
            if (eixo.sqrMagnitude < 1e-6f) return;
            osso.rotation = Quaternion.AngleAxis(graus, eixo.normalized) * osso.rotation;
        }

        // ------------------------------------------------------------------ o violão

        void PoseDoViolao(float batida, int musica)
        {
            Vector3 frente = transform.forward, cima = transform.up, direita = transform.right;
            bool parado = batida < 0f, sentado = _rede.SentadoNoChao.Value;
            // inclinado sobre o violão, a cabeça olhando o braço dele (para baixo e para a esquerda), acenando no tempo
            CorpoNaMusica(batida, sentado ? 4f : 6f, 1.5f, parado ? 8f : 14f, 12f, parado ? 0f : 3f);
            // o violão: a boca do lado direito da barriga (sentado, mais alta: o bojo de baixo na coxa direita), encostado no
            // corpo; o braço para a esquerda, subindo 18° e indo 22° para a frente (a esquerda alcança com o cotovelo dobrado,
            // perto do corpo); o tampo para a frente e um pouco para cima (quem toca vê as cordas)
            float sobe = 18f * Mathf.Deg2Rad, avanca = 22f * Mathf.Deg2Rad;
            Vector3 eixo = -direita * (Mathf.Cos(sobe) * Mathf.Cos(avanca)) + frente * (Mathf.Cos(sobe) * Mathf.Sin(avanca)) + cima * Mathf.Sin(sobe);
            Vector3 lugar = PeitoDeQuemToca + direita * 0.07f - cima * (sentado ? 0.03f : 0.16f) + frente * 0.13f;
            var rot = PorOInstrumento(new Vector3(-0.025f, 0f, 0f), lugar, eixo, frente + cima * 0.3f, EscalaDoViolao);
            Vector3 tampo = rot * Vector3.up, largura = rot * Vector3.forward, comprimento = rot * Vector3.right;

            // a direita: o antebraço no bojo de baixo, a mão sobre as cordas entre a boca e o cavalete, a palma para elas e os
            // dedos para baixo (segurando a palheta). A batida em colcheias: em cima no tempo, desce até embaixo no meio dele e
            // sobe no contratempo, rápida no meio e firme nas pontas; o pulso acompanha. No dedilhado, a mão fica e os dedos
            // e o polegar mexem
            bool dedilhado = musica == 2;
            float f = parado ? 0f : batida - Mathf.Floor(batida);
            float onda = Mathf.Cos(f * Mathf.PI * 2f);
            float batidaDaMao = parado ? 0.4f : dedilhado ? 0.25f : Mathf.Sign(onda) * Mathf.Pow(Mathf.Abs(onda), 0.45f);
            Vector3 cordas = NoInstrumento(new Vector3(-0.13f, 0.105f, 0f));
            Vector3 pulsoD = cordas + tampo * 0.05f + largura * (0.085f + 0.05f * batidaDaMao) + comprimento * 0.015f;
            Vector3 palmaD = -tampo, dedosD = -largura - comprimento * 0.25f;
            Braco(".R", pulsoD, direita * 0.6f - cima * 0.25f - frente * 0.35f, palmaD);
            var giroD = Quaternion.AngleAxis(parado || dedilhado ? 0f : 14f * batidaDaMao, comprimento);
            Mao(".R", giroD * dedosD, giroD * palmaD, 70f);
            if (dedilhado && !parado)
                Dedos(".R", 25f + 18f * Mathf.Max(0f, Mathf.Sin(batida * Mathf.PI * 4f)), 10f + 25f * Mathf.Pow(Mathf.Max(0f, onda), 6f));
            else
                Dedos(".R", 42f, 35f, -8f);   // (a palheta entre o polegar e o indicador, os outros soltos)

            // a esquerda: o pulso embaixo e atrás do braço do violão, a palma para cima e para trás, os dedos dando a volta pela
            // frente até as cordas e o polegar atrás. O acorde de cada compasso: no fim dele, os dedos levantam, a mão desliza
            // até a casa do próximo e eles apertam de novo
            int tempos = musica == 1 ? 3 : 4;
            float noCompasso = parado ? 0f : batida / tempos;
            int compasso = Mathf.FloorToInt(noCompasso);
            float troca = parado ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.86f, 1f, noCompasso - compasso));
            float casa = Mathf.Lerp(AcordesDoViolao[compasso % 4], AcordesDoViolao[(compasso + 1) % 4], troca);
            float levanta = Mathf.Sin(troca * Mathf.PI);
            Vector3 pulsoE = NoInstrumento(new Vector3(casa, -0.005f, -0.085f)) - tampo * (0.015f * levanta);
            Vector3 dedosE = tampo * 0.85f + largura * 0.5f, palmaE = largura * 0.85f - tampo * 0.5f;
            Braco(".L", pulsoE, -cima * 0.85f - direita * 0.2f + frente * 0.1f, palmaE);
            Mao(".L", dedosE, palmaE, 75f);
            Dedos(".L", parado ? 50f : 80f - 35f * levanta, parado ? 30f : 45f, 4f);
        }

        // ------------------------------------------------------------------ o pandeiro

        void PoseDoPandeiro(float batida)
        {
            Vector3 frente = transform.forward, cima = transform.up, direita = transform.right;
            bool parado = batida < 0f;
            float b = parado ? 0f : batida;
            CorpoNaMusica(batida, 3f, 2.5f, 10f, 8f, parado ? 0f : 3f);
            // em pé na frente do peito, um pouco à esquerda, a pele para a direita e para a frente (para a mão que bate); a
            // esquerda o vira de leve para lá e para cá (o "abre e fecha") e ele chacoalha
            Vector3 normal = (direita * 0.65f + frente * 0.62f + cima * 0.25f).normalized;
            Vector3 sobe = Vector3.ProjectOnPlane(cima, normal).normalized;
            normal = Quaternion.AngleAxis(9f * Mathf.Sin(b * Mathf.PI * 0.5f), sobe) * normal;
            sobe = Quaternion.AngleAxis(2.5f * Mathf.Sin(b * Mathf.PI * 4f), normal) * Vector3.ProjectOnPlane(sobe, normal).normalized;
            Vector3 meio = PeitoDeQuemToca + frente * 0.30f - direita * 0.05f + cima * 0.03f;
            var rot = PorOInstrumento(new Vector3(0f, 0.031f, 0f), meio, sobe, normal, EscalaDoPandeiro);
            normal = rot * Vector3.up;
            sobe = rot * Vector3.right;
            Vector3 paraDireita = rot * Vector3.forward;   // (na pele, para o lado de quem toca)

            // a esquerda segura o aro embaixo, à esquerda (no vão das platinelas): o antebraço vem de baixo, quase em pé (de
            // lado, ele ficava quase na direção da pele e a palma não virava para ela: a mão ficava aberta embaixo, como quem
            // segura uma bandeja); a palma para a pele, por trás dela, os dedos abertos atrás e o polegar por cima da beira
            float ag = 221f * Mathf.Deg2Rad;
            Vector3 fora = (sobe * Mathf.Cos(ag) + paraDireita * Mathf.Sin(ag)).normalized;
            Vector3 aro = NoInstrumento(new Vector3(0.124f * Mathf.Cos(ag), 0.031f, 0.124f * Mathf.Sin(ag)));
            Vector3 pulsoE = aro + fora * 0.02f - normal * 0.04f;
            Braco(".L", pulsoE, -cima * 0.95f - frente * 0.25f - direita * 0.1f, normal);
            Mao(".L", -fora + normal * 0.15f, normal, 85f);
            Dedos(".L", 20f, 55f, 3f);

            // a direita bate na pele em semicolcheias: o polegar perto da beira, a ponta dos dedos no meio, a base da mão perto
            // da beira e a ponta de novo; entre uma e outra, a mão sobe um pouco e vai para o lugar da próxima, balançando (a
            // ponta desce, a base desce, o polegar vira a mão de lado)
            Vector3 centro = NoInstrumento(new Vector3(0f, 0.06f, 0f));
            Vector3 dedosD = (-paraDireita * 0.6f + sobe * 0.8f).normalized;
            Vector3 beira = centro + paraDireita * 0.07f - sobe * 0.055f;
            Vector3 lateral = Vector3.Cross(dedosD, normal).normalized;
            float q = b * 4f;
            int k = Mathf.FloorToInt(q);
            float fq = q - k;
            int k0 = parado ? 1 : k, k1 = parado ? 1 : k + 1;
            float s = parado ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((fq - 0.35f) / 0.65f));
            Vector3 alvo = Vector3.Lerp(LugarDaBatida(k0, beira, centro, dedosD), LugarDaBatida(k1, beira, centro, dedosD), s);
            float altura = parado ? 0.06f : 0.03f + 0.04f * Mathf.Pow(Mathf.Sin(fq * Mathf.PI), 0.8f);
            float inclina = Mathf.Lerp(InclinaDaBatida[k0 % 4], InclinaDaBatida[k1 % 4], s);
            float rola = Mathf.Lerp(k0 % 4 == 0 ? 22f : 0f, k1 % 4 == 0 ? 22f : 0f, s);
            var giro = Quaternion.AngleAxis(rola, dedosD) * Quaternion.AngleAxis(inclina, lateral);
            Braco(".R", alvo + normal * altura, direita * 0.75f - cima * 0.55f - frente * 0.15f, -normal);
            Mao(".R", giro * dedosD, giro * -normal, 65f);
            Dedos(".R", 8f, k0 % 4 == 0 && !parado ? 0f : 18f);
        }

        // o pulso direito em cada batida do pandeiro (0: o polegar, 1 e 3: a ponta dos dedos no meio, 2: a base da mão)
        static Vector3 LugarDaBatida(int k, Vector3 beira, Vector3 centro, Vector3 dedos) => (k % 4) switch
        {
            0 => beira - dedos * 0.05f,
            2 => beira,
            _ => centro - dedos * 0.15f,
        };

        // o quanto os dedos da direita sobem da pele (graus) em cada batida: a ponta desce, a base sobe
        static readonly float[] InclinaDaBatida = { 0f, -14f, 16f, -14f };

        // ------------------------------------------------------------------ a gaita

        void PoseDaGaita(float batida)
        {
            Vector3 cima = transform.up, direita = transform.right;
            bool parado = batida < 0f;
            float b = parado ? 0f : batida;
            CorpoNaMusica(batida, 2f, 1.5f, 0f, 0f, 0f);
            Vector3 frente = FrenteDoRosto, alto = CimaDoRosto, lado = Vector3.Cross(alto, frente).normalized;
            // na boca, correndo de um lado para o outro devagar (a cada 2 compassos)
            Vector3 boca = BocaDeQuemToca + frente * 0.035f + lado * (0.025f * Mathf.Sin(b * Mathf.PI * 0.25f));
            PorOInstrumento(new Vector3(0f, 0.0125f, 0f), boca, lado, alto);
            // a esquerda a segura pela metade esquerda: o polegar embaixo, o indicador em cima, a palma para a boca
            Vector3 pulsoE = boca - lado * 0.055f - alto * 0.07f + frente * 0.015f;
            Vector3 palmaE = lado * 0.75f - frente * 0.6f;
            Braco(".L", pulsoE, -cima * 0.8f - direita * 0.4f, palmaE);
            Mao(".L", alto * 0.85f + lado * 0.3f + frente * 0.2f, palmaE, 65f);
            Dedos(".L", 55f, 40f);
            // a direita em concha por fora da esquerda, abrindo e fechando no tempo (o "uá" da gaita)
            float concha = parado ? 0f : Mathf.Pow(Mathf.Max(0f, Mathf.Sin(b * Mathf.PI)), 2f);
            Vector3 pulsoD = boca + lado * 0.04f - alto * 0.075f + frente * (0.055f + 0.01f * concha);
            Vector3 palmaD = -frente * 0.7f - lado * 0.5f;
            var abre = Quaternion.AngleAxis(-12f * concha, alto);
            Braco(".R", pulsoD, -cima * 0.8f + direita * 0.5f, palmaD);
            Mao(".R", abre * (alto * 0.8f - lado * 0.55f + frente * 0.15f), abre * palmaD, 65f);
            Dedos(".R", 50f - 25f * concha, 25f);
        }

        // ------------------------------------------------------------------ a flauta doce

        void PoseDaFlauta(float batida)
        {
            Vector3 cima = transform.up, direita = transform.right;
            bool parado = batida < 0f;
            float b = parado ? 0f : batida;
            CorpoNaMusica(batida, 2f, 1.5f, 0f, 0f, 0f);
            Vector3 frente = FrenteDoRosto, alto = CimaDoRosto, lado = Vector3.Cross(alto, frente).normalized;
            // na boca, 45° para baixo do rosto (como a flauta doce), balançando de leve com a frase
            float inclina = (45f + 3f * Mathf.Sin(b * Mathf.PI * 0.5f)) * Mathf.Deg2Rad;
            Vector3 saindo = (frente * Mathf.Cos(inclina) - alto * Mathf.Sin(inclina)).normalized;
            Vector3 furos = (alto * Mathf.Cos(inclina) + frente * Mathf.Sin(inclina)).normalized;
            var rot = PorOInstrumento(new Vector3(0.26f, 0.016f, 0f), BocaDeQuemToca + saindo * 0.005f, -saindo, furos);
            Vector3 frenteDela = rot * Vector3.up;
            Vector3 aDireita = Vector3.ProjectOnPlane(lado, saindo).normalized;
            // cada mão do seu lado (a esquerda em cima, perto da boca): a palma para a flauta, os dedos para a frente, dando a
            // volta até os furos, o polegar atrás; os dedos sobem e descem com as notas
            float notas = parado ? 0f : Mathf.Sin(b * Mathf.PI * 2f);
            Vector3 pulsoE = NoInstrumento(new Vector3(0.04f, 0.012f, 0f)) - aDireita * 0.03f - frenteDela * 0.075f;
            Vector3 pulsoD = NoInstrumento(new Vector3(-0.09f, 0.012f, 0f)) + aDireita * 0.03f - frenteDela * 0.075f;
            Braco(".L", pulsoE, -cima * 0.85f - direita * 0.45f, aDireita);
            Mao(".L", frenteDela, aDireita, 70f);
            Dedos(".L", 72f + 8f * notas, 35f);
            Braco(".R", pulsoD, -cima * 0.85f + direita * 0.45f, -aDireita);
            Mao(".R", frenteDela, -aDireita, 70f);
            Dedos(".R", 72f - 8f * notas, 35f);
        }
    }
}
