using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// A cena de teste dos zumbis especiais (03/11/2026, a pedido: "uma cena de testes para a gente testar os zumbis
    /// especiais, começando pelo brutamonte"; Scenes/Exemplos/ZumbisEspeciais.unity, montada pelo menu Zombie City Cleaner >
    /// Exemplos > Zumbis especiais): os modelos lado a lado (o Brutamonte de hoje e o novo), cada um com o esqueleto e as
    /// animações do jogo (os clipes gravados no esqueleto dele, como os do ZombieManager), tocando a mesma animação ao
    /// mesmo tempo. Sem a rede, a IA nem o jogador.
    /// No Play: ← e → (ou 1 a 9) trocam a animação dos dois; Espaço pausa; − e + mudam a velocidade (de 0,1× a 2×); R
    /// recomeça a animação; F vira os dois de costas (o olho do Brutamonte) e de frente; Tab põe a câmera num deles ou nos dois;
    /// o mouse (o botão esquerdo ou o direito segurado) gira a câmera em volta e a roda aproxima; H esconde o texto.
    /// </summary>
    public class MostruarioDosEspeciais : MonoBehaviour
    {
        [Tooltip("Os modelos, da esquerda para a direita (cada um com o Animator e um estado por animação).")]
        public Animator[] modelos;
        [Tooltip("O nome de cada modelo, embaixo dele.")]
        public string[] nomes;
        [Tooltip("As animações (os estados dos Animators), na ordem das setas.")]
        public string[] animacoes;
        [Tooltip("A câmera que gira em volta.")]
        public Camera cameraDaCena;

        int _atual, _foco = -1;
        float _velocidade = 1f;
        bool _pausado, _deCostas, _semTexto;
        float _giro = 0f, _inclinacao = 8f, _distancia = 6.5f;
        GUIStyle _estilo, _estiloNome;

        void Start()
        {
            Tocar(0);
        }

        void Tocar(int i)
        {
            if (animacoes == null || animacoes.Length == 0) return;
            _atual = (i % animacoes.Length + animacoes.Length) % animacoes.Length;
            foreach (var a in modelos)
                if (a != null) a.Play(animacoes[_atual], 0, 0f);
        }

        void Update()
        {
            var k = Keyboard.current;
            var m = Mouse.current;
            if (k != null)
            {
                if (k.rightArrowKey.wasPressedThisFrame) Tocar(_atual + 1);
                if (k.leftArrowKey.wasPressedThisFrame) Tocar(_atual - 1);
                for (int n = 0; n < 9; n++)
                    if (k[Key.Digit1 + n].wasPressedThisFrame && n < animacoes.Length) Tocar(n);
                if (k.spaceKey.wasPressedThisFrame) _pausado = !_pausado;
                if (k.rKey.wasPressedThisFrame) Tocar(_atual);
                if (k.minusKey.wasPressedThisFrame || k.numpadMinusKey.wasPressedThisFrame) _velocidade = Mathf.Max(0.1f, _velocidade - 0.1f);
                if (k.equalsKey.wasPressedThisFrame || k.numpadPlusKey.wasPressedThisFrame) _velocidade = Mathf.Min(2f, _velocidade + 0.1f);
                if (k.fKey.wasPressedThisFrame)
                {
                    _deCostas = !_deCostas;
                    foreach (var a in modelos)
                        if (a != null) a.transform.rotation = Quaternion.Euler(0f, _deCostas ? 0f : 180f, 0f);
                }
                if (k.tabKey.wasPressedThisFrame) _foco = _foco + 1 >= modelos.Length ? -1 : _foco + 1;
                if (k.hKey.wasPressedThisFrame) _semTexto = !_semTexto;
            }
            foreach (var a in modelos)
                if (a != null) a.speed = _pausado ? 0f : _velocidade;
            if (m != null)
            {
                if (m.leftButton.isPressed || m.rightButton.isPressed)
                {
                    var d = m.delta.ReadValue();
                    _giro += d.x * 0.25f;
                    _inclinacao = Mathf.Clamp(_inclinacao - d.y * 0.2f, -10f, 60f);
                }
                float roda = m.scroll.ReadValue().y;
                if (Mathf.Abs(roda) > 0.01f) _distancia = Mathf.Clamp(_distancia * (roda > 0 ? 0.9f : 1.1f), 1.5f, 20f);
            }
            if (cameraDaCena != null)
            {
                Vector3 alvo = Vector3.up * 1.2f;
                if (_foco >= 0 && _foco < modelos.Length && modelos[_foco] != null) alvo = modelos[_foco].transform.position + Vector3.up * 1.2f;
                else if (modelos.Length > 0)
                {
                    var soma = Vector3.zero;
                    foreach (var a in modelos) soma += a.transform.position;
                    alvo = soma / modelos.Length + Vector3.up * 1.2f;
                }
                var giro = Quaternion.Euler(_inclinacao, _giro, 0f);
                cameraDaCena.transform.SetPositionAndRotation(alvo + giro * new Vector3(0f, 0f, -_distancia), giro);
            }
        }

        void OnGUI()
        {
            if (_semTexto) return;
            float u = Screen.height / 720f;
            _estilo ??= new GUIStyle(GUI.skin.label) { richText = true };
            _estilo.fontSize = Mathf.RoundToInt(15 * u);
            _estiloNome ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _estiloNome.fontSize = Mathf.RoundToInt(17 * u);
            var lista = new System.Text.StringBuilder();
            lista.Append("<b>ZUMBIS ESPECIAIS</b>\n");
            for (int i = 0; i < animacoes.Length; i++)
                lista.Append(i == _atual ? $"<color=#ff5040>▶ {i + 1}. {animacoes[i]}</color>\n" : $"   {i + 1}. {animacoes[i]}\n");
            lista.Append($"\n{(_pausado ? "pausado" : "tocando")} · {_velocidade:0.0}×\n");
            lista.Append("← → (1 a 9) a animação · Espaço pausa · − + a velocidade · R recomeça\n");
            lista.Append("F de frente/de costas · Tab a câmera · o mouse gira · a roda aproxima · H esconde");
            var r = new Rect(16 * u, 16 * u, 640 * u, 700 * u);
            var cor = GUI.color;
            GUI.color = Color.black;
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), lista.ToString().Replace("<color=#ff5040>", "").Replace("</color>", ""), _estilo);
            GUI.color = cor;
            GUI.Label(r, lista.ToString(), _estilo);
            if (cameraDaCena == null || nomes == null) return;
            for (int i = 0; i < modelos.Length && i < nomes.Length; i++)
            {
                if (modelos[i] == null) continue;
                var p = cameraDaCena.WorldToScreenPoint(modelos[i].transform.position + Vector3.up * 2.45f);
                if (p.z <= 0f) continue;
                var caixa = new Rect(p.x - 160 * u, Screen.height - p.y - 14 * u, 320 * u, 28 * u);
                GUI.color = Color.black;
                GUI.Label(new Rect(caixa.x + 1, caixa.y + 1, caixa.width, caixa.height), nomes[i], _estiloNome);
                GUI.color = Color.white;
                GUI.Label(caixa, nomes[i], _estiloNome);
            }
            GUI.color = cor;
        }
    }
}
