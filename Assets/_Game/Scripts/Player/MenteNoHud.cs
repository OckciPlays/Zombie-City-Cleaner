using System.Text;
using UnityEngine;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A mente no HUD (06/11/2026, a sanidade: "um medidor de nível de ansiedade ... como se fosse um cérebro"), só no dono:
    /// o cérebro ao lado das barras da fome e da sede, cheio de baixo para cima com a mente (<see cref="VidaJogador.Sanidade"/>),
    /// na cor de cada faixa: azul-claro (calmo, de 70 para cima), lilás (abalado), roxo (apavorado, abaixo de 40: pulsa com o
    /// coração do <see cref="MonitorCardiaco"/>) e vermelho (o colapso, abaixo de 15: rachado e tremendo). Em cima das
    /// barras, o porquê, como na silhueta do corpo: "ABALADO: ESCURO · ZUMBIS CAÇANDO"; só o que acalma, "ACALMANDO:
    /// FOGUEIRA · AMIGOS PERTO".
    /// </summary>
    public class MenteNoHud : MonoBehaviour
    {
        VidaJogador _vida;
        readonly StringBuilder _texto = new StringBuilder();
        string _linha;
        uint _motivosNaLinha = uint.MaxValue;
        int _faixaNaLinha = -1;
        bool _cheiaNaLinha, _acalmando;
        int _alturaNaLinha, _estadoNaLinha;
        readonly GUIContent _conteudo = new GUIContent();

        static readonly Color Calmo = new Color(0.62f, 0.86f, 1f), Abalado = new Color(0.8f, 0.66f, 1f),
                              Apavorado = new Color(0.86f, 0.36f, 0.96f), Colapso = new Color(0.94f, 0.2f, 0.26f),
                              Acalmando = new Color(0.6f, 0.92f, 0.86f);

        // a ordem do porquê (o mais forte primeiro) e o nome de cada um
        internal static readonly MotivoDaMente[] OrdemDoQueAbala =
        {
            MotivoDaMente.AmigoCaiu, MotivoDaMente.Susto, MotivoDaMente.Zumbis, MotivoDaMente.Matou, MotivoDaMente.Escuro,
            MotivoDaMente.Tensao, MotivoDaMente.Golpe, MotivoDaMente.Dor, MotivoDaMente.Sangue, MotivoDaMente.Febre,
            MotivoDaMente.Sozinho, MotivoDaMente.Tempestade, MotivoDaMente.Fome, MotivoDaMente.Sede,
            MotivoDaMente.Abstinencia, MotivoDaMente.Sono,
        };
        static readonly MotivoDaMente[] OrdemDoQueAcalma =
        {
            MotivoDaMente.Descanso, MotivoDaMente.Calmante, MotivoDaMente.Bunker, MotivoDaMente.Fogueira, MotivoDaMente.CalorDoFogo, MotivoDaMente.Amigos,
            MotivoDaMente.Sinuca, MotivoDaMente.Cigarro, MotivoDaMente.Dia,
        };

        public static string Nome(MotivoDaMente m) => m switch
        {
            MotivoDaMente.Escuro => "ESCURO", MotivoDaMente.Zumbis => "ZUMBIS CAÇANDO", MotivoDaMente.Tensao => "ZUMBIS POR PERTO",
            MotivoDaMente.Susto => "SUSTO", MotivoDaMente.Sozinho => "SOZINHO", MotivoDaMente.Tempestade => "TEMPESTADE",
            MotivoDaMente.Dor => "DOR", MotivoDaMente.Febre => "FEBRE", MotivoDaMente.Sangue => "SANGRANDO",
            MotivoDaMente.Fome => "FOME", MotivoDaMente.Sede => "SEDE", MotivoDaMente.AmigoCaiu => "UM AMIGO CAIU",
            MotivoDaMente.Matou => "MATOU ALGUÉM", MotivoDaMente.Golpe => "FERIDO",
            MotivoDaMente.Dia => "O DIA", MotivoDaMente.Amigos => "AMIGOS PERTO", MotivoDaMente.Fogueira => "FOGUEIRA",
            MotivoDaMente.Bunker => "O BUNKER", MotivoDaMente.Cigarro => "CIGARRO",
            MotivoDaMente.Sono => "SONO", MotivoDaMente.Abstinencia => "ABSTINÊNCIA", MotivoDaMente.Calmante => "CALMANTE",
            MotivoDaMente.Sinuca => "SINUCA", MotivoDaMente.Descanso => "DORMINDO", MotivoDaMente.CalorDoFogo => "CALOR DO FOGO", _ => m.ToString().ToUpperInvariant(),
        };

        /// <summary>A faixa da mente: 0 calmo (70+), 1 abalado, 2 apavorado (abaixo de 40), 3 em colapso (abaixo de 15).</summary>
        public static int Faixa(float mente) => mente >= 70f ? 0 : mente >= 40f ? 1 : mente >= 15f ? 2 : 3;

        public static Color CorDa(float mente) => Faixa(mente) switch { 0 => Calmo, 1 => Abalado, 2 => Apavorado, _ => Colapso };

        TelaDoInventario _tela;

        void Awake()
        {
            _vida = GetComponent<VidaJogador>();
            _tela = GetComponent<TelaDoInventario>();
        }

        /// <summary>
        /// Onde fica o cérebro: o primeiro dos medidores redondos, em cima do monitor cardíaco (12/11/2026; antes, à direita
        /// das barras da fome e da sede), no meio da altura do quadrado (ele é mais largo que alto).
        /// </summary>
        public static Rect Area(float u)
        {
            var m = EstadosNoHud.AreaDoMedidor(0, u);
            float h = m.width * IconesDaMente.Altura / IconesDaMente.Largura;
            return new Rect(m.x, m.y + (m.height - h) / 2f, m.width, h);
        }

        void OnGUI()
        {
            if (_vida == null || !_vida.IsSpawned || !_vida.IsOwner || _vida.Caido.Value) return;
            if (MenuOnline.Aberto || MenuOnline.Cinema || ModoVideo.Ligado || GravacaoNoMundo.SemHud) return;
            // com a mochila aberta, os medidores saem (a tela cobre o canto; 12/11/2026: o cérebro ficava por cima dela)
            if (_tela != null && _tela.Aberta) return;
            float u = Screen.height / 720f;
            float mente = _vida.Sanidade.Value;
            int faixa = Faixa(mente);
            var cor = CorDa(mente);
            var r = Area(u);
            // apavorado, pulsa com o coração; em colapso, treme
            var monitor = MonitorCardiaco.Local;
            if (faixa >= 2 && monitor != null)
            {
                float k = Mathf.Exp(-(Time.time - monitor.UltimaBatida) * 9f) * (faixa == 3 ? 0.1f : 0.07f);
                r = new Rect(r.center.x - r.width * (1f + k) / 2f, r.center.y - r.height * (1f + k) / 2f, r.width * (1f + k), r.height * (1f + k));
            }
            if (faixa == 3) r.position += new Vector2(Mathf.PerlinNoise(Time.time * 18f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, Time.time * 18f) - 0.5f) * 3f * u;
            var antes = GUI.color;
            // o vazio (escuro), o cheio de baixo para cima, as dobras e, no colapso, as rachaduras
            // (o vazio é um cinza que aparece na noite escura; o cheio, a cor da faixa)
            GUI.color = new Color(0.3f, 0.31f, 0.34f, 0.72f);
            GUI.DrawTexture(r, IconesDaMente.Miolo);
            float f = Mathf.Clamp01(mente / VidaJogador.MenteMaxima);
            if (f > 0.001f)
            {
                GUI.color = new Color(cor.r, cor.g, cor.b, 0.92f);
                GUI.DrawTextureWithTexCoords(new Rect(r.x, r.yMax - r.height * f, r.width, r.height * f), IconesDaMente.Miolo, new Rect(0f, 0f, 1f, f));
            }
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.DrawTexture(r, IconesDaMente.Dobras);
            if (faixa == 3)
            {
                GUI.color = new Color(0.08f, 0f, 0f, 0.85f);
                GUI.DrawTexture(r, IconesDaMente.Rachaduras);
            }
            GUI.color = antes;
            // a linha do porquê saiu do HUD (12/11/2026: os símbolos dos estados dizem); ela vira a dica do cérebro, com o
            // mouse solto em cima
            Porque(u, mente, faixa, cor, false);
            var medidor = EstadosNoHud.AreaDoMedidor(0, u);
            if (EstadosNoHud.MouseSolto && medidor.Contains(Event.current.mousePosition))
                EstadosNoHud.Dica(medidor, $"Mente: {Mathf.RoundToInt(mente)}%", string.IsNullOrEmpty(_linha) ? "Calma." : _linha, u);
            // os símbolos dos estados, no canto de baixo à direita (com a mochila aberta, eles vão para a aba CORPO)
            EstadosNoHud.NoHud(_vida, u);
        }

        // a linha do porquê (refeita só quando o motivo ou a faixa mudam); desde 12/11/2026 só a dica do cérebro, sem desenhar
        void Porque(float u, float mente, int faixa, Color cor, bool desenhar = true)
        {
            uint m = _vida.MotivosDaMente.Value;
            bool cheia = mente >= VidaJogador.MenteMaxima - 0.5f;
            var estilo = EstilosDoHud.Rotulo(Mathf.RoundToInt(11 * u), FontStyle.Bold);
            // até a silhueta do corpo (que começa em 232u, com as linhas dela em cima): o que não cabe sai do fim
            float largura = 208f * u;
            // (a etapa 2: a crise de pânico e a fuga mudam o nome do estado)
            int estado = ColapsoDoJogador.Local != null && ColapsoDoJogador.Local.Fugindo ? 2 : CriseDePanico.Local != null && CriseDePanico.Local.Ativa ? 1 : 0;
            // (a etapa 3: dois calmantes seguidos deixam sonolento, escrito na frente)
            if (_vida.TemEfeito(VidaJogador.EfeitoSonolento)) estado += 10;
            if (m != _motivosNaLinha || faixa != _faixaNaLinha || cheia != _cheiaNaLinha || Screen.height != _alturaNaLinha || estado != _estadoNaLinha)
            {
                _motivosNaLinha = m;
                _faixaNaLinha = faixa;
                _cheiaNaLinha = cheia;
                _alturaNaLinha = Screen.height;
                _estadoNaLinha = estado;
                for (int quantos = 3; quantos >= 1; quantos--)
                {
                    _linha = MontarLinha(m, mente, faixa, quantos, out _acalmando);
                    if (_estadoNaLinha >= 10) _linha = _linha == null ? "SONOLENTO" : "SONOLENTO · " + _linha;
                    if (_linha == null) break;
                    _conteudo.text = _linha;
                    if (estilo.CalcSize(_conteudo).x <= largura) break;
                }
            }
            if (string.IsNullOrEmpty(_linha) || !desenhar) return;
            var corDaLinha = _acalmando ? Acalmando : cor;
            var r = new Rect(20f * u, Screen.height - 156f * u, largura, 16f * u);
            estilo.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), _linha, estilo);
            estilo.normal.textColor = corDaLinha;
            GUI.Label(r, _linha, estilo);
        }

        string MontarLinha(uint m, float mente, int faixa, int maximo, out bool acalmando)
        {
            acalmando = false;
            _texto.Clear();
            int n = 0;
            foreach (var x in OrdemDoQueAbala)
                if ((m & VidaJogador.Bit(x)) != 0 && n < maximo)
                {
                    _texto.Append(n == 0 ? "" : " · ").Append(Nome(x));
                    n++;
                }
            int e10 = _estadoNaLinha % 10;
            string forte = e10 == 2 ? "FUGINDO EM PÂNICO" : e10 == 1 ? "CRISE DE PÂNICO" : null;
            if (n > 0)
            {
                string estado = forte ?? faixa switch { 0 => "TENSO", 1 => "ABALADO", 2 => "APAVORADO", _ => "EM COLAPSO" };
                return $"{estado}: {_texto}";
            }
            if (forte != null) return forte;
            if (mente >= VidaJogador.MenteMaxima - 0.5f) return null;
            foreach (var x in OrdemDoQueAcalma)
                if ((m & VidaJogador.Bit(x)) != 0 && n < maximo)
                {
                    _texto.Append(n == 0 ? "" : " · ").Append(Nome(x));
                    n++;
                }
            if (n > 0)
            {
                acalmando = true;
                return $"ACALMANDO: {_texto}";
            }
            return faixa >= 1 ? (faixa == 3 ? "EM COLAPSO" : faixa == 2 ? "APAVORADO" : "ABALADO") : null;
        }
    }
}
