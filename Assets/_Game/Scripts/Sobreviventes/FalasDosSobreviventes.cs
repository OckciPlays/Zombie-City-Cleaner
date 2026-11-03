using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>O que um sobrevivente fala (cada uma com umas variações).</summary>
    public enum Frase : byte { Contato, Atirando, Recarregando, Zumbis, CaiuUm, Fugindo, Sumiu, ParaAi, AbaixaArma, Ola, Traidor,
        FicaAi, GuardaArma, Estepe, PneuRuim, Valeu, Pronto, Obrigado, EuAvisei }

    /// <summary>
    /// As falas dos sobreviventes (02/11/2026, a pedido: "por hora sem voz, mas textos seria interessante com algum som
    /// saindo das letras escritas"): a frase aparece em cima da cabeça dele, letra a letra, sem caixa (a sombra embaixo), e
    /// cada letra solta um bipe curto, no lugar dele, com o tom de cada um (pela semente). Só para quem está a até 35 m e
    /// olhando para o lado dele.
    /// </summary>
    public class FalasDosSobreviventes : MonoBehaviour
    {
        static readonly Dictionary<Frase, string[]> Textos = new Dictionary<Frase, string[]>
        {
            [Frase.Contato] = new[] { "Ali! Um deles!", "Contato!", "Tem gente ali!", "Atirem!" },
            [Frase.Atirando] = new[] { "Cobre a esquerda!", "Fogo!", "Não deixa ele sair!" },
            [Frase.Recarregando] = new[] { "Recarregando!", "Me cobre, tô sem bala!" },
            [Frase.Zumbis] = new[] { "Zumbis!", "Eles tão vindo!", "Tem mais deles!" },
            [Frase.CaiuUm] = new[] { "Caiu um!", "Pegaram ele!", "Perdemos um!" },
            [Frase.Fugindo] = new[] { "Eu tô fora!", "Recua! Recua!", "Não dá mais!" },
            [Frase.Sumiu] = new[] { "Perdi ele de vista.", "Sumiu...", "Fiquem atentos." },
            [Frase.ParaAi] = new[] { "Para aí!", "Nem mais um passo!" },
            // o acampamento (02/11/2026)
            [Frase.AbaixaArma] = new[] { "Abaixa essa arma.", "Ei! Abaixa isso!", "Aponta isso pra lá." },
            [Frase.Ola] = new[] { "Precisa de alguma coisa?", "Dá uma olhada.", "Tenho o que você precisa." },
            [Frase.Traidor] = new[] { "Ele atirou! Derruba ele!", "Traidor!", "Todo mundo, nele!" },
            // o carro quebrado (02/11/2026)
            [Frase.FicaAi] = new[] { "Ei! Fica aí!", "Parado aí!", "Não chega mais perto!" },
            [Frase.GuardaArma] = new[] { "Guarda essa arma!", "Mão longe da arma!", "Guarda isso aí, amigo!" },
            [Frase.Estepe] = new[] { "O estepe furou também. Tem um pneu aí?", "Furou o pneu e o estepe. Você tem um pneu?" },
            [Frase.PneuRuim] = new[] { "Esse pneu não aguenta. Tem um melhor?", "Esse aí tá pior que o nosso." },
            [Frase.Valeu] = new[] { "Valeu! Fica de olho, o barulho atrai eles.", "Beleza! Cobre a gente enquanto eu troco." },
            [Frase.Pronto] = new[] { "Pronto! Valeu mesmo. Toma, é seu.", "Tá feito. Toma aqui, pela ajuda." },
            [Frase.Obrigado] = new[] { "Valeu pela ajuda.", "A gente te deve essa.", "Se cuida na estrada." },
            [Frase.EuAvisei] = new[] { "Eu avisei!", "Ele vai atirar!", "Derruba ele!" },
        };

        struct Fala
        {
            public Sobrevivente quem;
            public string texto;
            public float desde;
            public int letrasTocadas;
            public float tom;
        }

        static FalasDosSobreviventes _instancia;
        readonly List<Fala> _falas = new List<Fala>();
        GUIStyle _estilo, _sombra;
        float _u;

        const float LetrasPorSegundo = 26f, Dura = 2.6f, Alcance = 35f;

        /// <summary>A última fala mostrada nesta máquina e de quem (o papel), para os testes.</summary>
        public static string Ultima { get; private set; } = "";

        /// <summary>Nesta máquina: a frase em cima da cabeça dele.</summary>
        public static void Mostrar(Sobrevivente quem, byte frase)
        {
            if (quem == null) return;
            if (_instancia == null)
            {
                var go = new GameObject("Falas dos sobreviventes");
                _instancia = go.AddComponent<FalasDosSobreviventes>();
            }
            var lista = Textos.TryGetValue((Frase)frase, out var t) ? t : new[] { "..." };
            int semente = quem.Identidade.Value.semente;
            _instancia._falas.RemoveAll(f => f.quem == quem);
            string texto = lista[Random.Range(0, lista.Length)];
            Ultima = $"{quem.Identidade.Value.papel}: {texto}";
            _instancia._falas.Add(new Fala
            {
                quem = quem, texto = texto, desde = Time.time,
                tom = 0.85f + (Mathf.Abs(semente) % 7) * 0.06f,
            });
        }

        void OnDestroy()
        {
            if (_instancia == this) _instancia = null;
        }

        // o bipe: 35 ms de um tom com um pouco de ruído, que some depressa; um por tom de voz (7, pela semente de cada um)
        readonly Dictionary<int, AudioClip> _bipes = new Dictionary<int, AudioClip>();

        AudioClip Bipe(float tom)
        {
            int k = Mathf.RoundToInt(tom * 100f);
            if (_bipes.TryGetValue(k, out var pronto)) return pronto;
            int taxa = 22050, n = Mathf.RoundToInt(taxa * 0.035f);
            var dados = new float[n];
            var r = new System.Random(k);
            float hz = 560f * tom;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)taxa;
                float env = Mathf.Exp(-t * 90f) * Mathf.Min(1f, i / 40f);
                dados[i] = (Mathf.Sin(2f * Mathf.PI * hz * t) * 0.8f + ((float)r.NextDouble() * 2f - 1f) * 0.08f) * env * 0.5f;
            }
            var clip = AudioClip.Create("Bipe das falas " + k, n, 1, taxa, false);
            clip.SetData(dados, 0);
            _bipes[k] = clip;
            return clip;
        }

        void Update()
        {
            for (int i = _falas.Count - 1; i >= 0; i--)
            {
                var f = _falas[i];
                if (f.quem == null || Time.time - f.desde > Dura + f.texto.Length / LetrasPorSegundo)
                {
                    _falas.RemoveAt(i);
                    continue;
                }
                // o som das letras: um bipe por letra nova (sem os espaços e a pontuação)
                int letras = Mathf.Min(f.texto.Length, Mathf.FloorToInt((Time.time - f.desde) * LetrasPorSegundo));
                var j = JogadorRede.Local;
                bool perto = j != null && (j.transform.position - f.quem.transform.position).sqrMagnitude < Alcance * Alcance;
                while (f.letrasTocadas < letras)
                {
                    char c = f.texto[f.letrasTocadas++];
                    if (perto && char.IsLetter(c) && f.letrasTocadas % 2 == 1)
                        Combat.Efeitos.Som(Bipe(f.tom), f.quem.transform.position + Vector3.up * 1.7f, 0.5f, true, Alcance);
                }
                _falas[i] = f;
            }
        }

        void OnGUI()
        {
            if (_falas.Count == 0 || Event.current.type != EventType.Repaint || MenuOnline.Aberto) return;
            var j = JogadorRede.Local;
            var cam = j != null && j.cameraMundo != null ? j.cameraMundo : Camera.main;
            if (cam == null) return;
            float u = Screen.height / 720f;
            if (_estilo == null || !Mathf.Approximately(u, _u))
            {
                _u = u;
                _estilo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter };
                _estilo.normal.textColor = new Color(1f, 0.93f, 0.78f);
                _sombra = new GUIStyle(_estilo);
                _sombra.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            }
            foreach (var f in _falas)
            {
                if (f.quem == null) continue;
                Vector3 cabeca = f.quem.transform.position + Vector3.up * 2.15f;
                if ((cabeca - cam.transform.position).sqrMagnitude > Alcance * Alcance) continue;
                Vector3 tela = cam.WorldToScreenPoint(cabeca);
                if (tela.z <= 0f) continue;
                int letras = Mathf.Clamp(Mathf.FloorToInt((Time.time - f.desde) * LetrasPorSegundo), 0, f.texto.Length);
                string texto = f.texto.Substring(0, letras);
                float fim = f.desde + f.texto.Length / LetrasPorSegundo + Dura;
                float alfa = Mathf.Clamp01((fim - Time.time) / 0.5f);
                var r = new Rect(tela.x - 200 * u, Screen.height - tela.y - 30 * u, 400 * u, 28 * u);
                var antes = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alfa);
                GUI.Label(new Rect(r.x + 2 * u, r.y + 2 * u, r.width, r.height), texto, _sombra);
                GUI.Label(r, texto, _estilo);
                GUI.color = antes;
            }
        }
    }
}
