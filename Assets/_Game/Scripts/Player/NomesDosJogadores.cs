using UnityEngine;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O nome em cima da cabeça dos outros jogadores (a parte 3 do Plano_Personagem.md da pasta de arte, 31/10/2026; a
    /// decisão 5): só no dono, na tela dele, em cima de cada outro jogador que já criou o personagem (o dele não), até 30 m
    /// da câmera e só quando dá para ver a cabeça (não atravessa as paredes nem o chão; os carros não tapam: quem vai
    /// dentro aparece pelas janelas). Menor e mais apagado
    /// com a distância; some com o menu, a tela de criar e o mapa abertos. Posto pelo <see cref="JogadorRede"/> no dono.
    /// </summary>
    public class NomesDosJogadores : MonoBehaviour
    {
        public const float Alcance = 30f;
        // acima do osso da cabeça (a base dele fica no queixo): o nome flutua uns 15 cm acima do alto da cabeça
        const float AcimaDaCabeca = 0.42f;
        // o que tapa a vista: tudo menos os jogadores, os braços, o retrato, os zumbis (7 e 8), os carros, o Ignore Raycast e
        // os gatilhos
        static readonly int MascaraDaVista = ~((1 << 2) | (1 << JogadorRede.CamadaBracos) | (1 << 7) | (1 << 8) | (1 << JogadorRede.CamadaJogador)
                                               | (1 << JogadorRede.CamadaRetrato) | (1 << Vehicles.Carro.CamadaVeiculo));

        JogadorRede _rede;
        GUIStyle _estilo, _sombra;
        float _uDoEstilo;

        void Awake() => _rede = GetComponent<JogadorRede>();

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || _rede == null || !_rede.IsOwner) return;
            if (MenuOnline.Aberto || TelaDeCriarPersonagem.Aberta || ModoVideo.Ligado || GravacaoNoMundo.SemHud) return;
            var cam = _rede.cameraMundo;
            if (cam == null || !cam.isActiveAndEnabled) return;
            GUI.depth = 20;   // por baixo do HUD e das telas
            float u = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            var olho = cam.transform.position;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || j == _rede || !j.IsSpawned || !j.Criado || j.ossoCabeca == null) continue;
                if (Alucinacoes.Escondido(j)) continue;   // (06/11/2026: no colapso, ele é visto como zumbi)
                var topo = j.ossoCabeca.position + Vector3.up * AcimaDaCabeca;
                float d = Vector3.Distance(olho, topo);
                if (d > Alcance) continue;
                var tela = cam.WorldToScreenPoint(topo);
                if (tela.z <= 0f) continue;
                // a vista até a cabeça (o meio dela): uma parede ou o chão no caminho tapa
                var cabeca = j.ossoCabeca.position + Vector3.up * 0.12f;
                if (Physics.Linecast(olho, cabeca, MascaraDaVista, QueryTriggerInteraction.Ignore)) continue;
                string nome = j.Aparencia.Value.nome.ToString();
                if (string.IsNullOrEmpty(nome)) continue;
                Desenhar(nome, new Vector2(tela.x, Screen.height - tela.y), d, u);
            }
            // os moradores do bunker (06/11/2026, a parte 6 do Plano_NPCs_Bunker.md da pasta de arte): o nome (ou o apelido)
            // em cima deles também, num tom mais quente
            foreach (var m in Moradores.CorpoDoMorador.Todos)
            {
                if (m == null || !m.IsSpawned || m.Cabeca == null) continue;
                var topo = m.Cabeca.position + Vector3.up * AcimaDaCabeca;
                float d = Vector3.Distance(olho, topo);
                if (d > Alcance) continue;
                var tela = cam.WorldToScreenPoint(topo);
                if (tela.z <= 0f) continue;
                if (Physics.Linecast(olho, m.Cabeca.position + Vector3.up * 0.12f, MascaraDaVista, QueryTriggerInteraction.Ignore)) continue;
                string nome = m.Nome;
                if (!string.IsNullOrEmpty(nome)) Desenhar(nome, new Vector2(tela.x, Screen.height - tela.y), d, u, true);
            }
        }

        static readonly Color CorDosMoradores = new Color(0.98f, 0.86f, 0.62f);

        void Desenhar(string nome, Vector2 p, float d, float u, bool morador = false)
        {
            // de 17 px perto a 11 px a 30 m; os últimos 6 m somem aos poucos
            int tamanho = Mathf.RoundToInt(Mathf.Lerp(17f, 11f, d / Alcance) * u);
            if (_estilo == null || !Mathf.Approximately(_uDoEstilo, u)) Estilos(u);
            _estilo.fontSize = _sombra.fontSize = Mathf.Max(8, tamanho);
            float alfa = Mathf.Clamp01((Alcance - d) / 6f);
            var conteudo = new GUIContent(nome);
            var t = _estilo.CalcSize(conteudo);
            var r = new Rect(p.x - t.x / 2f, p.y - t.y, t.x, t.y);
            var antes = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alfa);
            GUI.Label(new Rect(r.x + 1f * u, r.y + 1f * u, r.width, r.height), conteudo, _sombra);
            var cor = _estilo.normal.textColor;
            if (morador) _estilo.normal.textColor = CorDosMoradores;
            GUI.Label(r, conteudo, _estilo);
            _estilo.normal.textColor = cor;
            GUI.color = antes;
        }

        void Estilos(float u)
        {
            _uDoEstilo = u;
            _estilo = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
            _estilo.normal.textColor = new Color(0.95f, 0.93f, 0.86f);
            _sombra = new GUIStyle(_estilo);
            _sombra.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        }
    }
}
