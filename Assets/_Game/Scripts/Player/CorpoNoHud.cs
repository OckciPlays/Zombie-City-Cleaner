using UnityEngine;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Vehicles;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O corpo no HUD (05/11/2026, o sistema de vida por membro; só no dono, posto pelo <see cref="VidaJogador"/> no
    /// OnNetworkSpawn): a silhueta das 6 partes embaixo à esquerda, à direita das barras da vida, da fome e da sede, cada
    /// parte na cor da vida dela (verde, amarela, vermelha; a sã apagada, a inutilizada cinza riscada), com os ícones por
    /// cima (<see cref="IconesDoCorpo"/>: a gota vermelha piscando, a faixa, a tala, o osso partido, o anel da entorse) e,
    /// embaixo, o pior estado em palavras ("SANGRANDO", "PERNA ESQ. QUEBRADA", "COM DOR"). Aparece só com algo errado
    /// (<see cref="EstadoDoCorpo.AlgoErrado"/>), devagar; a parte golpeada pisca (<see cref="VidaJogador.Golpeado"/>). Some
    /// com o menu, o cinema, o modo vídeo, caído e com a tela do inventário aberta (a aba CORPO mostra a grande, pelo mesmo
    /// <c>DesenharSilhueta</c>). Com a cabeça a 0, as bordas da tela
    /// escurecem um pouco (a câmera não muda).
    /// <para>A etapa 2 (05/11/2026): em cima da silhueta, o porquê ("MIRA TREMENDO: BRAÇO DIR. COM TALA", "MANCANDO: PERNA
    /// ESQ. QUEBRADA"; o Pedro tremeu e nada na tela disse o motivo); a infecção no pior estado ("SEPSE", "FEBRE", "FERIDA
    /// INFECCIONADA: ..."); o termômetro ao lado da cabeça com a febre; a parte de onde veio a infecção com o contorno
    /// verde-amarelado e o micróbio; o escudinho na ferida protegida e a gotinha azul na limpa.</para>
    /// </summary>
    public class CorpoNoHud : MonoBehaviour
    {
        // a silhueta é feita numa caixa de 52 × 102 (a medida do HUD a 720p) e esticada para a área pedida
        const float CaixaW = 52f, CaixaH = 102f;
        const float TempoDoFade = 0.3f, TempoDoPisca = 1.2f;

        static readonly Color Sombra = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color Inutilizada = new Color(0.3f, 0.3f, 0.29f, 0.95f);
        static readonly Color Realce = new Color(0.98f, 0.82f, 0.36f);
        static readonly Color CorDaGota = new Color(0.92f, 0.12f, 0.1f);
        static readonly Color CorDaFaixa = new Color(0.97f, 0.96f, 0.92f);
        static readonly Color CorDaTala = new Color(0.86f, 0.72f, 0.5f);
        static readonly Color CorDoOsso = new Color(0.96f, 0.93f, 0.84f);
        static readonly Color CorDaEntorse = new Color(1f, 0.62f, 0.25f);
        // a etapa 2: a infecção (o contorno da parte de onde veio e o micróbio), a febre (o termômetro), a proteção e a limpeza
        static readonly Color CorDaInfeccao = new Color(0.74f, 0.86f, 0.22f);
        static readonly Color CorDaFebre = new Color(1f, 0.34f, 0.24f);
        static readonly Color CorDaFebreAliviada = new Color(0.96f, 0.72f, 0.42f);
        static readonly Color CorDoEscudo = new Color(0.55f, 0.9f, 0.7f);
        static readonly Color CorDaLimpeza = new Color(0.45f, 0.75f, 1f);
        /// <summary>As cores da linha do estado: o grave (sangrando, quebrado, inutilizado), o que incomoda (torcido, com dor) e o resto.</summary>
        public static readonly Color LinhaGrave = new Color(0.95f, 0.42f, 0.36f), LinhaMedia = new Color(0.98f, 0.72f, 0.35f), LinhaLeve = new Color(0.86f, 0.84f, 0.76f);

        VidaJogador _vida;
        TelaDoInventario _tela;
        float _alfa, _piscaAte;
        ParteDoCorpo _atingida = PartesDoCorpo.Sortear;
        // a linha do estado, refeita só quando o corpo muda (sem criar texto a cada quadro)
        EstadoDoCorpo _doTexto;
        bool _temTexto;
        string _linha, _porQueTreme, _porQueManca;
        Color _corDaLinha;
        readonly GUIContent _conteudo = new GUIContent();

        void Awake()
        {
            _vida = GetComponent<VidaJogador>();
            _tela = GetComponent<TelaDoInventario>();
        }

        void OnEnable()
        {
            if (_vida != null) _vida.Golpeado += AoGolpe;
        }

        void OnDisable()
        {
            if (_vida != null) _vida.Golpeado -= AoGolpe;
        }

        // a parte golpeada pisca na silhueta
        void AoGolpe(ParteDoCorpo parte, TipoDeGolpe tipo, Lesao nova, bool abriu)
        {
            if (!PartesDoCorpo.Valida(parte)) return;
            _atingida = parte;
            _piscaAte = Time.time + TempoDoPisca;
        }

        void Update()
        {
            if (_vida == null || !_vida.IsSpawned) return;
            bool mostrar = _vida.Corpo.Value.AlgoErrado || Time.time < _piscaAte;
            _alfa = Mathf.MoveTowards(_alfa, mostrar ? 1f : 0f, Time.unscaledDeltaTime / TempoDoFade);
        }

        void OnGUI()
        {
            if (_vida == null || !_vida.IsSpawned || !_vida.IsOwner) return;
            if (MenuOnline.Aberto || MenuOnline.Cinema || ModoVideo.Ligado || _vida.Caido.Value) return;
            if (_tela != null && _tela.Aberta) return;
            if (Event.current.type != EventType.Repaint) return;
            var e = _vida.Corpo.Value;
            float u = Screen.height / 720f;
            // a cabeça a 0: as bordas da tela escurecem
            if (e.Inutil(ParteDoCorpo.Cabeca)) Imagem(new Rect(0f, 0f, Screen.width, Screen.height), IconesDoCorpo.Vinheta, new Color(1f, 1f, 1f, 0.6f), 1f);
            if (_alfa <= 0.01f) return;
            // à direita das barras (a vida vai até 220u), sem chegar na barra de atalhos (que começa em largura/2 - 168u)
            float inicioDosAtalhos = Screen.width / 2f - 176f * u;
            float x = Mathf.Min(232f * u, inicioDosAtalhos - 64f * u);
            var area = new Rect(x, Screen.height - 136f * u, CaixaW * u, CaixaH * u);
            float brilho = 0f;
            if (Time.time < _piscaAte)
            {
                float t = 1f - (_piscaAte - Time.time) / TempoDoPisca;
                brilho = (0.5f + 0.5f * Mathf.Cos(t * Mathf.PI * 6f)) * (1f - t);
            }
            DesenharSilhueta(area, e, brilho > 0f ? _atingida : PartesDoCorpo.Sortear, brilho, _alfa);
            // embaixo, o pior estado; em cima da silhueta, por que a mira treme e por que ele manca (05/11/2026, a pedido: o
            // Pedro tremeu "e nada foi dito na tela o real motivo")
            if (!_temTexto || !e.Equals(_doTexto))
            {
                _doTexto = e;
                _temTexto = true;
                _linha = PiorEstado(e, out _corDaLinha);
                _porQueTreme = LinhaDoTremor(e);
                var manca = PartesDoCorpo.PorQueManca(e);
                _porQueManca = manca != null ? "MANCANDO: " + manca : null;
            }
            if (_porQueTreme != null || _porQueManca != null)
            {
                // de baixo para cima, a partir do alto da silhueta; quebra a linha (para cima) se não couber antes da barra
                // de atalhos, que é o limite à direita
                float larguraDoPorQue = Mathf.Max(60f * u, inicioDosAtalhos - 4f * u - x);
                float yPorQue = area.y - 2f * u;
                var estiloDoPorQue = EstilosDoHud.Rotulo(Mathf.RoundToInt(11 * u), FontStyle.Bold);
                if (_porQueManca != null) yPorQue = LinhaDoPorQue(_porQueManca, x, yPorQue, larguraDoPorQue, estiloDoPorQue, u);
                if (_porQueTreme != null) LinhaDoPorQue(_porQueTreme, x, yPorQue, larguraDoPorQue, estiloDoPorQue, u);
            }
            if (string.IsNullOrEmpty(_linha)) return;
            // a letra de 12 (a do HUD); se não couber antes da barra de atalhos (a tela 5:4), a de 10
            float largura = Mathf.Max(60f * u, inicioDosAtalhos - 8f * u - x);
            var estilo = EstilosDoHud.Rotulo(Mathf.RoundToInt(12 * u), FontStyle.Bold);
            _conteudo.text = _linha;
            if (estilo.CalcSize(_conteudo).x > largura) estilo = EstilosDoHud.Rotulo(Mathf.RoundToInt(10 * u), FontStyle.Bold);
            var cor = _corDaLinha;
            cor.a *= _alfa;
            // a sombra do texto, para ler em cima de qualquer chão
            estilo.normal.textColor = new Color(0f, 0f, 0f, 0.7f * _alfa);
            // (sem passar do começo da barra de atalhos)
            largura = Mathf.Min(Mathf.Max(largura, estilo.CalcSize(_conteudo).x + 4f), Mathf.Max(60f * u, inicioDosAtalhos - 4f * u - x));
            GUI.Label(new Rect(x + 1f, Screen.height - 30f * u + 1f, largura, 18f * u), _linha, estilo);
            estilo.normal.textColor = cor;
            GUI.Label(new Rect(x, Screen.height - 30f * u, largura, 18f * u), _linha, estilo);
        }

        // uma linha do "por quê" (com a sombra), terminando em baixo; devolve o y de cima dela, para a próxima
        float LinhaDoPorQue(string texto, float x, float embaixo, float largura, GUIStyle estilo, float u)
        {
            _conteudo.text = texto;
            float h = Mathf.Max(14f * u, estilo.CalcHeight(_conteudo, largura));
            float y = embaixo - h;
            estilo.normal.textColor = new Color(0f, 0f, 0f, 0.7f * _alfa);
            GUI.Label(new Rect(x + 1f, y + 1f, largura, h), _conteudo, estilo);
            var cor = LinhaMedia;
            cor.a *= _alfa;
            estilo.normal.textColor = cor;
            GUI.Label(new Rect(x, y, largura, h), _conteudo, estilo);
            return y - 1f * u;
        }

        /// <summary>
        /// Por que a mira treme, para o HUD: "MIRA TREMENDO: BRAÇO DIR. COM TALA" (<see cref="PartesDoCorpo.PorQueTreme"/>), com a
        /// febre junto (os calafrios sacodem a vista); null se não treme.
        /// </summary>
        public static string LinhaDoTremor(in EstadoDoCorpo e)
        {
            var braco = PartesDoCorpo.PorQueTreme(e);
            if (e.ComFebre) return braco != null ? $"MIRA TREMENDO: {braco} E {(e.Sepse ? "SEPSE" : "FEBRE")}" : $"MIRA TREMENDO: {(e.Sepse ? "SEPSE" : "FEBRE")} (CALAFRIOS)";
            return braco != null ? "MIRA TREMENDO: " + braco : null;
        }

        // ------------------------------------------------------------------ a silhueta

        /// <summary>
        /// Desenha a silhueta das 6 partes na área (esticada sem deformar, no meio dela): de frente, como quem olha o
        /// personagem (06/11/2026, a pedido: o braço esquerdo dele fica à direita da imagem; era vista de costas). Cada parte
        /// na cor da vida dela; a inutilizada cinza riscada; os ícones
        /// do estado por cima. A parte <paramref name="realce"/> (<see cref="PartesDoCorpo.Sortear"/>: nenhuma) acende com
        /// a moldura amarela, no <paramref name="brilho"/> (0 a 1). A tela do corpo (TelaDoInventario.Corpo.cs) usa a grande.
        /// </summary>
        public static void DesenharSilhueta(Rect area, in EstadoDoCorpo e, ParteDoCorpo realce, float brilho) =>
            DesenharSilhueta(area, e, realce, brilho, 1f);

        /// <summary>A silhueta com a transparência de tudo (<paramref name="alfa"/>, o fade do HUD).</summary>
        public static void DesenharSilhueta(Rect area, in EstadoDoCorpo e, ParteDoCorpo realce, float brilho, float alfa)
        {
            if (Event.current != null && Event.current.type != EventType.Repaint) return;
            float s = Escala(area);
            float borda = Mathf.Max(1f, s);
            // a parte de onde veio a infecção (a etapa 2): o contorno verde-amarelado, pulsando devagar
            var infectada = e.Infectado ? e.OrigemDaInfeccao : PartesDoCorpo.Sortear;
            var corDaInfeccao = CorDaInfeccao;
            corDaInfeccao.a = 0.65f + 0.35f * Mathf.Sin(Time.time * 2.5f);
            // as sombras primeiro (um contorno escuro em volta de tudo), depois as partes
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
            {
                var pp = (ParteDoCorpo)p;
                var r = RetanguloDaParte(area, pp);
                if (pp == ParteDoCorpo.Cabeca) Imagem(Crescer(r, borda), TelaDoInventario.Circulo(), Sombra, alfa);
                else Pintar(Crescer(r, borda), Sombra, alfa);
            }
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
            {
                var pp = (ParteDoCorpo)p;
                var r = RetanguloDaParte(area, pp);
                bool cabeca = pp == ParteDoCorpo.Cabeca;
                Color cor;
                if (e.Inutil(pp)) cor = Inutilizada;
                else
                {
                    int vida = e.Vida(pp);
                    cor = PecasDoMotor.CorDoEstado(vida);
                    // a parte sã fica apagada: o que chama a atenção é o que está machucado
                    cor.a = vida >= 100 ? 0.25f : 0.9f;
                }
                bool acesa = pp == realce && brilho > 0f;
                // a cabeça acesa: o anel amarelo atrás dela (o círculo maior, por baixo); a infecção vinda da cabeça, o verde
                if (cabeca && acesa) Imagem(Crescer(r, borda * 1.8f), TelaDoInventario.Circulo(), new Color(Realce.r, Realce.g, Realce.b, brilho), alfa);
                else if (cabeca && infectada == pp) Imagem(Crescer(r, borda * 1.8f), TelaDoInventario.Circulo(), corDaInfeccao, alfa);
                if (cabeca) Imagem(r, TelaDoInventario.Circulo(), cor, alfa);
                else Pintar(r, cor, alfa);
                if (e.Inutil(pp))
                {
                    if (cabeca) Imagem(r, IconesDoCorpo.RiscasRedondas, Color.white, alfa);
                    else
                    {
                        var antes = GUI.color;
                        GUI.color = new Color(1f, 1f, 1f, alfa);
                        float passo = 10f * s;
                        GUI.DrawTextureWithTexCoords(r, IconesDoCorpo.Riscas, new Rect(0f, 0f, r.width / passo, r.height / passo));
                        GUI.color = antes;
                    }
                }
                if (acesa)
                {
                    var luz = new Color(1f, 1f, 1f, 0.35f * brilho);
                    if (cabeca) Imagem(r, TelaDoInventario.Circulo(), luz, alfa);
                    else
                    {
                        Pintar(r, luz, alfa);
                        var moldura = Realce;
                        moldura.a = brilho;
                        Moldura(Crescer(r, borda), Mathf.Max(1f, Mathf.Round(1.4f * s)), moldura, alfa);
                    }
                }
            }
            // o contorno da infecção depois de todas as partes (a vizinha não pinta por cima); a parte acesa fica com a moldura
            // amarela
            if (PartesDoCorpo.Valida(infectada) && infectada != ParteDoCorpo.Cabeca && !(infectada == realce && brilho > 0f))
                Moldura(Crescer(RetanguloDaParte(area, infectada), borda), Mathf.Max(1f, Mathf.Round(1.4f * s)), corDaInfeccao, alfa);
            // os ícones do estado por cima de tudo (passam da parte fina, o braço)
            for (int p = 0; p < PartesDoCorpo.Quantas; p++) IconesDaParte(area, e, (ParteDoCorpo)p, s, alfa);
            // a febre: o termômetro ao lado da cabeça (vermelho pulsando; com o xarope agindo, apagado)
            if (e.Infeccao >= 2) Imagem(RetanguloDoTermometro(area), IconesDoCorpo.Termometro, CorDoTermometro(e), alfa);
        }

        /// <summary>O lugar do termômetro (a febre), ao lado da cabeça, dentro da caixa da silhueta.</summary>
        public static Rect RetanguloDoTermometro(Rect area)
        {
            float s = Escala(area);
            float x0 = area.x + (area.width - CaixaW * s) / 2f, y0 = area.y + (area.height - CaixaH * s) / 2f;
            return new Rect(x0 + 35f * s, y0 - 1f * s, 16f * s, 16f * s);
        }

        /// <summary>O termômetro da febre no retângulo dado (a imagem da aba CORPO, 11/11/2026); sem febre, nada.</summary>
        public static void Termometro(Rect r, in EstadoDoCorpo e)
        {
            if (e.Infeccao >= 2 && (Event.current == null || Event.current.type == EventType.Repaint)) Imagem(r, IconesDoCorpo.Termometro, CorDoTermometro(e), 1f);
        }

        /// <summary>A cor da parte na silhueta (a da vida; a inutilizada, cinza): a imagem da aba CORPO usa a mesma.</summary>
        public static Color CorDaParte(in EstadoDoCorpo e, ParteDoCorpo p) => e.Inutil(p) ? Inutilizada : PecasDoMotor.CorDoEstado(e.Vida(p));

        /// <summary>O verde-amarelado da infecção, pulsando devagar (o contorno da parte de onde ela veio).</summary>
        public static Color CorDaInfeccaoAgora
        {
            get
            {
                var c = CorDaInfeccao;
                c.a = 0.65f + 0.35f * Mathf.Sin(Time.time * 2.5f);
                return c;
            }
        }

        static Color CorDoTermometro(in EstadoDoCorpo e)
        {
            if (!e.ComFebre) return CorDaFebreAliviada;
            var c = CorDaFebre;
            float v = e.Sepse ? 6f : 3.5f;
            c.a = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * v));
            return c;
        }

        /// <summary>O retângulo da parte na silhueta desenhada na área (a cabeça é o círculo dentro dele).</summary>
        public static Rect RetanguloDaParte(Rect area, ParteDoCorpo p)
        {
            float s = Escala(area);
            float x0 = area.x + (area.width - CaixaW * s) / 2f, y0 = area.y + (area.height - CaixaH * s) / 2f;
            var f = Forma(p);
            return new Rect(x0 + f.x * s, y0 + f.y * s, f.width * s, f.height * s);
        }

        /// <summary>A parte da silhueta embaixo do ponto (com uma folga, para o braço fino), ou false.</summary>
        public static bool ParteEm(Rect area, Vector2 ponto, out ParteDoCorpo parte)
        {
            float folga = 2f * Escala(area);
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
                if (Crescer(RetanguloDaParte(area, (ParteDoCorpo)p), folga).Contains(ponto))
                {
                    parte = (ParteDoCorpo)p;
                    return true;
                }
            parte = PartesDoCorpo.Sortear;
            return false;
        }

        static float Escala(Rect area) => Mathf.Min(area.width / CaixaW, area.height / CaixaH);

        // as partes na caixa de 52 × 102: a cabeça (o círculo de 14), o tronco 20 × 35, os braços 7,5 × 38 e as pernas 9 × 48;
        // de frente: os do lado esquerdo do personagem à direita da imagem
        static Rect Forma(ParteDoCorpo p) => p switch
        {
            ParteDoCorpo.Cabeca => new Rect(19f, 0f, 14f, 14f),
            ParteDoCorpo.Tronco => new Rect(16f, 16f, 20f, 35f),
            ParteDoCorpo.BracoE => new Rect(37.5f, 16f, 7.5f, 38f),
            ParteDoCorpo.BracoD => new Rect(7f, 16f, 7.5f, 38f),
            ParteDoCorpo.PernaE => new Rect(26.5f, 53f, 9f, 48f),
            _ => new Rect(16.5f, 53f, 9f, 48f),
        };

        // os ícones (até 3 no meio da parte, um embaixo do outro; o anel da entorse vai na ponta: o tornozelo, o pulso)
        static readonly Texture2D[] _icones = new Texture2D[4];
        static readonly Color[] _coresDosIcones = new Color[4];

        static void IconesDaParte(Rect area, in EstadoDoCorpo e, ParteDoCorpo p, float s, float alfa)
        {
            var r = RetanguloDaParte(area, p);
            float lado = 12f * s;
            IconesEm(r.center, Vector2.up, new Vector2(r.center.x, r.yMax - 0.225f * lado), lado, e, p, alfa);
        }

        /// <summary>
        /// Os ícones do estado da parte (a gota ou a faixa, com o selo da ferida protegida ou limpa; a tala ou o osso; o
        /// micróbio), empilhados em volta de <paramref name="centro"/> ao longo de <paramref name="eixo"/> (para baixo, na
        /// silhueta), e o anel da entorse em <paramref name="ponta"/>. A imagem do personagem da aba CORPO (11/11/2026) usa
        /// com o eixo do membro.
        /// </summary>
        public static void IconesEm(Vector2 centro, Vector2 eixo, Vector2 ponta, float lado, in EstadoDoCorpo e, ParteDoCorpo p, float alfa)
        {
            if (Event.current != null && Event.current.type != EventType.Repaint) return;
            int n = 0;
            var lesao = e.LesaoDe(p);
            bool tala = e.Tala(p);
            bool exposta = false;
            if (e.Sangra(p))
            {
                _icones[n] = IconesDoCorpo.Gota;
                var c = CorDaGota;
                c.a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 5f));
                _coresDosIcones[n++] = c;
            }
            else if (e.Faixa(p))
            {
                _icones[n] = IconesDoCorpo.Faixa;
                _coresDosIcones[n++] = CorDaFaixa;
            }
            if (tala)
            {
                _icones[n] = IconesDoCorpo.Tala;
                _coresDosIcones[n++] = CorDaTala;
            }
            else if (lesao >= Lesao.Fratura)
            {
                _icones[n] = IconesDoCorpo.Osso;
                _coresDosIcones[n++] = CorDoOsso;
                exposta = lesao == Lesao.Exposta;
            }
            // a etapa 2: o micróbio na parte de onde veio a infecção
            if (e.Infectado && e.OrigemDaInfeccao == p)
            {
                _icones[n] = IconesDoCorpo.Infeccao;
                _coresDosIcones[n++] = CorDaInfeccao;
            }
            // a ferida protegida (o escudinho) ou limpa pelo spray (a gotinha azul): no canto do 1º ícone (a gota ou a faixa)
            var selo = !e.Ferida(p) ? null : e.Protegida(p) ? IconesDoCorpo.Escudo : e.Limpa(p) ? IconesDoCorpo.Gotinha : null;
            var corDoSelo = e.Protegida(p) ? CorDoEscudo : CorDaLimpeza;
            for (int i = 0; i < n; i++)
            {
                var meio = centro + eixo * ((i - (n - 1) * 0.5f) * lado * 1.1f);
                var ri = new Rect(meio.x - lado / 2f, meio.y - lado / 2f, lado, lado);
                Imagem(ri, _icones[i], _coresDosIcones[i], alfa);
                // a fratura exposta: a gotinha junto do osso
                if (exposta && _icones[i] == IconesDoCorpo.Osso)
                    Imagem(new Rect(ri.xMax - lado * 0.35f, ri.y - lado * 0.2f, lado * 0.55f, lado * 0.55f), IconesDoCorpo.Gota, CorDaGota, alfa);
                if (i == 0 && selo != null && (_icones[0] == IconesDoCorpo.Gota || _icones[0] == IconesDoCorpo.Faixa))
                    Imagem(new Rect(ri.xMax - lado * 0.3f, ri.y - lado * 0.25f, lado * 0.6f, lado * 0.6f), selo, corDoSelo, alfa);
                _icones[i] = null;
            }
            // a entorse sem a tala: o anel na ponta do membro
            if (lesao == Lesao.Entorse && !tala)
            {
                float l = lado * 0.9f;
                Imagem(new Rect(ponta.x - l / 2f, ponta.y - l / 2f, l, l), IconesDoCorpo.Entorse, CorDaEntorse, alfa);
            }
        }

        // ------------------------------------------------------------------ o estado em palavras

        static bool Feminino(ParteDoCorpo p) => PartesDoCorpo.NomeComArtigo(p)[0] == 'a';

        // "TORNOZELO ESQ." (a perna), "PULSO DIR." (o braço)
        static string Junta(ParteDoCorpo p) => p switch
        {
            ParteDoCorpo.PernaE => "TORNOZELO ESQ.", ParteDoCorpo.PernaD => "TORNOZELO DIR.",
            ParteDoCorpo.BracoE => "PULSO ESQ.", _ => "PULSO DIR.",
        };

        /// <summary>
        /// O pior estado do corpo, em maiúsculas, para a linha embaixo da silhueta: "SANGRANDO", "FRATURA EXPOSTA: PERNA
        /// ESQ.", "PERNA ESQ. QUEBRADA", "BRAÇO DIR. INUTILIZADO", "TORNOZELO ESQ. TORCIDO", "COM DOR", "PERNA ESQ. COM TALA",
        /// "ENFAIXADO", "MACHUCADO" (null: nada errado). A cor diz o quanto é grave. A etapa 2: "SEPSE" e "FEBRE" antes de tudo,
        /// "FERIDA INFECCIONADA: BRAÇO ESQ." logo depois das fraturas sem a tala, "FEBRE (XAROPE)" com o xarope agindo.
        /// </summary>
        public static string PiorEstado(in EstadoDoCorpo e, out Color cor)
        {
            cor = LinhaGrave;
            if (e.Sepse) return "SEPSE";
            if (e.ComFebre) return "FEBRE";
            if (e.AlgumSangrando) return "SANGRANDO";
            for (int m = (int)ParteDoCorpo.BracoE; m <= (int)ParteDoCorpo.PernaD; m++)
            {
                var p = (ParteDoCorpo)m;
                if (e.LesaoDe(p) == Lesao.Exposta && !e.Tala(p)) return "FRATURA EXPOSTA: " + PartesDoCorpo.NomeCurto(p);
            }
            for (int m = (int)ParteDoCorpo.BracoE; m <= (int)ParteDoCorpo.PernaD; m++)
            {
                var p = (ParteDoCorpo)m;
                if (e.LesaoDe(p) == Lesao.Fratura && !e.Tala(p)) return PartesDoCorpo.NomeCurto(p) + (Feminino(p) ? " QUEBRADA" : " QUEBRADO");
            }
            if (e.Infeccao == 1) return "FERIDA INFECCIONADA: " + PartesDoCorpo.NomeCurto(e.OrigemDaInfeccao);
            for (int k = 0; k < PartesDoCorpo.Quantas; k++)
            {
                var p = (ParteDoCorpo)k;
                if (e.Inutil(p)) return PartesDoCorpo.NomeCurto(p) + (Feminino(p) ? " INUTILIZADA" : " INUTILIZADO");
            }
            cor = LinhaMedia;
            // a febre com o xarope agindo (sem os sintomas, mas a infecção continua)
            if (e.Infeccao == 2) return "FEBRE (XAROPE)";
            for (int m = (int)ParteDoCorpo.BracoE; m <= (int)ParteDoCorpo.PernaD; m++)
            {
                var p = (ParteDoCorpo)m;
                if (e.LesaoDe(p) == Lesao.Entorse && !e.Tala(p)) return Junta(p) + " TORCIDO";
            }
            if (e.ComDor) return "COM DOR";
            cor = LinhaLeve;
            for (int m = (int)ParteDoCorpo.BracoE; m <= (int)ParteDoCorpo.PernaD; m++)
            {
                var p = (ParteDoCorpo)m;
                if (e.Tala(p)) return PartesDoCorpo.NomeCurto(p) + " COM TALA";
            }
            if (e.faixa != 0) return "ENFAIXADO";
            return e.AlgoErrado ? "MACHUCADO" : null;
        }

        /// <summary>
        /// O estado da parte em palavras, para as etiquetas da tela do corpo: "sangrando", "enfaixado: fecha em 2 dias",
        /// "fratura com tala: sara em 14 h", "inutilizado", "machucado", "inteiro"... (juntos com " · "). Na etapa 2, a ferida:
        /// "protegido até o amanhecer", "limpo: enfaixe", "sem antisséptico: pode infeccionar", "coberto pelo antibiótico",
        /// "infeccionado" e o dia dela ("ferida: dia 2 de 3"). Grave: sangrando, quebrado sem a tala, inutilizado ou
        /// infeccionado.
        /// </summary>
        public static string EstadoDaParte(in EstadoDoCorpo e, ParteDoCorpo p, out bool grave)
        {
            bool fem = Feminino(p);
            string o = fem ? "a" : "o";
            grave = false;
            var texto = new System.Text.StringBuilder();
            void Juntar(string s)
            {
                if (texto.Length > 0) texto.Append(" · ");
                texto.Append(s);
            }
            if (e.Inutil(p))
            {
                Juntar("inutilizad" + o);
                grave = true;
            }
            if (e.Sangra(p))
            {
                Juntar("sangrando");
                grave = true;
            }
            else if (e.Faixa(p) && e.FeridaFalta(p) > 0) Juntar($"enfaixad{o}: fecha em {Tempo(e.FeridaFalta(p))}");
            // a etapa 2: a infecção e a limpeza da ferida
            if (e.Infectado && e.OrigemDaInfeccao == p)
            {
                Juntar("infeccionad" + o);
                grave = true;
            }
            else if (e.Ferida(p))
            {
                if (e.Coberta(p)) Juntar($"cobert{o} pelo antibiótico");
                else if (e.Dias(p) < 3)
                {
                    if (e.Protegida(p)) Juntar($"protegid{o} até o amanhecer");
                    else if (e.Limpa(p)) Juntar($"limp{o}: enfaixe");
                    else Juntar("sem antisséptico: pode infeccionar");
                    Juntar(DiaDaFerida(e, p));
                }
            }
            var l = e.LesaoDe(p);
            if (l != Lesao.Nenhuma)
            {
                string nome = PartesDoCorpo.NomeDaLesao(l);
                if (e.Tala(p))
                {
                    // sem o analgésico, sara pela metade da velocidade (o VidaJogador.Corpo)
                    int falta = e.SararFalta(p) * (e.ComAnalgesico ? 1 : 2);
                    Juntar($"{nome} com tala: sara em {Tempo(falta)}");
                }
                else
                {
                    Juntar(l == Lesao.Entorse ? $"{nome}: ponha uma tala" : $"{nome} sem tala");
                    if (l >= Lesao.Fratura) grave = true;
                }
            }
            if (texto.Length == 0) Juntar(e.Dano(p) > 0 ? "machucad" + o : "inteir" + o);
            return texto.ToString();
        }

        /// <summary>
        /// O dia da ferida nos 3 em que ela pode infeccionar (o sorteio de cada amanhecer): "ferida: dia 2 de 3". Antes do 1º
        /// sorteio, o dia 1.
        /// </summary>
        public static string DiaDaFerida(in EstadoDoCorpo e, ParteDoCorpo p) => e.Dias(p) switch
        {
            0 => "ferida: dia 1 de 3", 1 => "ferida: dia 2 de 3", _ => "ferida: dia 3 de 3",
        };

        /// <summary>Os minutos do jogo em texto: "40 min", "14 h", "1 dia", "3 dias".</summary>
        public static string Tempo(int minutos)
        {
            if (minutos >= 24 * 60)
            {
                int d = Mathf.Max(1, Mathf.RoundToInt(minutos / (24f * 60f)));
                return d == 1 ? "1 dia" : $"{d} dias";
            }
            if (minutos >= 60) return $"{Mathf.RoundToInt(minutos / 60f)} h";
            return $"{Mathf.Max(1, minutos)} min";
        }

        // ------------------------------------------------------------------ ajudantes (com a transparência do fade)

        static Rect Crescer(Rect r, float d) => new Rect(r.x - d, r.y - d, r.width + 2f * d, r.height + 2f * d);

        static void Pintar(Rect r, Color c, float alfa)
        {
            var antes = GUI.color;
            c.a *= alfa;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = antes;
        }

        static void Imagem(Rect r, Texture t, Color c, float alfa)
        {
            if (t == null) return;
            var antes = GUI.color;
            c.a *= alfa;
            GUI.color = c;
            GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true);
            GUI.color = antes;
        }

        static void Moldura(Rect r, float b, Color c, float alfa)
        {
            Pintar(new Rect(r.x, r.y, r.width, b), c, alfa);
            Pintar(new Rect(r.x, r.yMax - b, r.width, b), c, alfa);
            Pintar(new Rect(r.x, r.y + b, b, r.height - 2f * b), c, alfa);
            Pintar(new Rect(r.xMax - b, r.y + b, b, r.height - 2f * b), c, alfa);
        }
    }
}
