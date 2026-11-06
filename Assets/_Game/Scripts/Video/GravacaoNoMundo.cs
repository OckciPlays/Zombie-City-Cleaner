using UnityEngine;

namespace ZombieCityCleaner.Video
{
    /// <summary>
    /// Gravar um vídeo jogando, na partida de sempre (03/11/2026, a pedido: "os próximos vídeos eu mesmo vou andar pelo
    /// mapa e gravar", sem a cena de vídeo): o F8 (ou o painel VÍDEO do F1) pede, e o gravador do Editor
    /// (<c>Editor/GravadorDoVideo</c>, o Unity Recorder) grava a Game view em <c>Recordings/Mundo/</c>. Só no Editor (o
    /// Recorder é dele). Gravando, o HUD some (<see cref="SemHud"/>, <see cref="SemMira"/>) e, na tela em pé, a câmera dos
    /// braços se ajusta para a arma caber (<see cref="BracosEmPe"/>). As escolhas ficam no PlayerPrefs.
    /// </summary>
    public static class GravacaoNoMundo
    {
        /// <summary>O que aparece na tela gravando: nada, só o da mira (a mira, a arma e a munição, a dica e a luneta) ou tudo.</summary>
        public enum Tela { Nada, Mira, Tudo }

        /// <summary>O jogo quer gravar (o F8): o gravador do Editor começa e para por ele.</summary>
        public static bool Pedido { get; set; }
        /// <summary>O gravador está gravando (escrito por ele).</summary>
        public static bool Gravando { get; set; }
        /// <summary>Quando começou (<see cref="Time.unscaledTime"/>), para o tempo no F1.</summary>
        public static float Desde { get; set; }
        /// <summary>O arquivo da última gravação (o caminho inteiro).</summary>
        public static string Arquivo { get; set; }

        /// <summary>Gravando, sem as barras, os atalhos, a linha da partida, o FPS, os nomes, o painel do carro e os números do F3.</summary>
        public static bool SemHud => Gravando && NaTela != Tela.Tudo;
        /// <summary>Gravando, sem a mira também.</summary>
        public static bool SemMira => Gravando && NaTela == Tela.Nada;

        // as escolhas (lidas uma vez e gravadas quando mudam)
        static bool _lidas, _emPe;
        static Tela _naTela;
        static int _quadros;

        /// <summary>Em pé (1080 × 1920, o Reels) ou deitado (1920 × 1080).</summary>
        public static bool EmPe
        {
            get { Ler(); return _emPe; }
            set { Ler(); _emPe = value; PlayerPrefs.SetInt("video.empe", value ? 1 : 0); }
        }

        public static Tela NaTela
        {
            get { Ler(); return _naTela; }
            set { Ler(); _naTela = value; PlayerPrefs.SetInt("video.tela", (int)value); }
        }

        /// <summary>Quadros por segundo do vídeo (60 ou 30). O jogo anda no tempo do vídeo: se o Editor não der conta, fica lento enquanto grava, e o vídeo sai liso.</summary>
        public static int Quadros
        {
            get { Ler(); return _quadros; }
            set { Ler(); _quadros = value; PlayerPrefs.SetInt("video.quadros", value); }
        }

        static void Ler()
        {
            if (_lidas) return;
            _lidas = true;
            _emPe = PlayerPrefs.GetInt("video.empe", 1) == 1;
            _naTela = (Tela)Mathf.Clamp(PlayerPrefs.GetInt("video.tela", 0), 0, 2);
            _quadros = PlayerPrefs.GetInt("video.quadros", 60) == 30 ? 30 : 60;
        }

        // a tela em pé corta a arma de primeira pessoa (ela foi feita para a tela deitada, à direita do meio): a câmera dos
        // braços abre mais (de 55° para 72°) e vai 6 cm para a direita e 2,5 cm para cima, e a arma aparece mais para a
        // esquerda, inteira. Mover a câmera não muda para onde o cano aponta na tela (ele continua na mira); deslocar a
        // imagem dela (a projeção) mudava, e o cano apontava à esquerda e abaixo da mira
        const float CampoDosBracos = 55f, CampoDosBracosEmPe = 72f;
        static readonly Vector3 DesvioDosBracosEmPe = new Vector3(0.06f, 0.025f, 0f);

        /// <summary>Com a tela em pé (a proporção menor que 1), a câmera dos braços com o campo mais aberto e deslocada; deitada, como no jogo.</summary>
        public static void BracosEmPe(Camera bracos) => BracosEmPe(bracos, bracos != null && bracos.aspect < 1f);

        public static void BracosEmPe(Camera bracos, bool emPe)
        {
            if (bracos == null) return;
            bracos.fieldOfView = emPe ? CampoDosBracosEmPe : CampoDosBracos;
            bracos.transform.localPosition = emPe ? DesvioDosBracosEmPe : Vector3.zero;
        }

        // sem Domain Reload, o estado estático sobrevive ao Play
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Zerar()
        {
            Pedido = false;
            Gravando = false;
        }
    }
}
