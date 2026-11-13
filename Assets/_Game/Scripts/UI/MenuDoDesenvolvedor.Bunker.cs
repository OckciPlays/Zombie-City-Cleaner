using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Moradores;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O painel BUNKER do F1 (06/11/2026, a parte 6 do Plano_NPCs_Bunker.md da pasta de arte), na quinta aba do canto: os
    /// moradores (o que cada um faz, onde, se está com o corpo ou no papel, as barras e a cama: a vida é do anfitrião), o
    /// relógio rápido (×1, ×10 e ×60), as barras de todos (cheias, a fome e a sede baixas, o sono baixo), pôr um morador
    /// sorteado e tirar o último, e ir aos andares (o B0 ao B3, na porta da escada). Os pedidos vão para o anfitrião; o ir é
    /// aqui (o movimento é do dono).
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        bool _abaDoBunker;
        static readonly GUIContent _conteudoDoBunker = new GUIContent();
        static readonly float[] Relogios = { 1f, 10f, 60f };

        const byte BunkerEncher = 0, BunkerComFome = 1, BunkerComSono = 2, BunkerPor = 3, BunkerTirar = 4, BunkerRelogio = 5, BunkerEstoque = 6,
                   BunkerHumor = 7, BunkerLazer = 8;

        Rect PainelDoBunker(float u, Rect aoLado)
        {
            float x0 = aoLado.xMax + 8 * u, largura = 330 * u;
            float x = x0 + 12 * u, w = largura - 24 * u;
            float meio = (w - 6 * u) / 2f, terco = (w - 12 * u) / 3f, quarto = (w - 18 * u) / 4f;
            const float passo = 28f, alto = 24f;
            var moradores = MoradoresDoBunker.Instancia;
            string texto;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                texto = $"Os moradores vivem no anfitrião ({CorpoDoMorador.Todos.Count} com o corpo aqui).";
            else if (moradores == null || moradores.Todos.Count == 0)
                texto = "Nenhum morador (o mundo sem as cartas, ou o Dia 0).";
            else
            {
                var s = new System.Text.StringBuilder();
                s.Append($"Dia {CicloDoDia.Dia}, {CicloDoDia.Relogio()} (×{CicloDoDia.Acelerado:0})\n");
                s.Append(moradores.ResumoDoEstoque()).Append('\n');
                foreach (var m in moradores.Todos) s.Append(moradores.Resumo(m)).Append('\n');
                texto = s.ToString().TrimEnd();
            }
            _conteudoDoBunker.text = texto;
            float alturaDoTexto = _texto.CalcHeight(_conteudoDoBunker, w);
            const int linhas = 8;
            float altura = 8 * u + 28 * u + alturaDoTexto + 8 * u + linhas * passo * u + 6 * u;
            var area = new Rect(x0, aoLado.y, largura, altura);

            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.62f, 0.55f, 0.30f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "BUNKER (OS MORADORES)", _titulo);
            y += 28 * u;
            GUI.Label(new Rect(x, y, w, alturaDoTexto), _conteudoDoBunker, _texto);
            y += alturaDoTexto + 8 * u;

            // o relógio rápido (ligado, o botão fica apertado)
            float rotulo = 64 * u, cada = (w - rotulo - 2 * 4 * u) / 3f;
            GUI.Label(new Rect(x, y + 3 * u, rotulo, 20 * u), "Relógio", _texto);
            for (int k = 0; k < Relogios.Length; k++)
            {
                bool ligado = Mathf.Approximately(CicloDoDia.Acelerado, Relogios[k]);
                if (GUI.Toggle(new Rect(x + rotulo + k * (cada + 4 * u), y, cada, alto * u), ligado, $"×{Relogios[k]:0}", _botao) && !ligado)
                    TesteDoBunkerRpc(BunkerRelogio, (byte)k);
            }
            y += passo * u;
            if (GUI.Button(new Rect(x, y, terco, alto * u), "Barras cheias", _botao)) TesteDoBunkerRpc(BunkerEncher, 0);
            if (GUI.Button(new Rect(x + terco + 6 * u, y, terco, alto * u), "Fome e sede 20", _botao)) TesteDoBunkerRpc(BunkerComFome, 0);
            if (GUI.Button(new Rect(x + 2f * (terco + 6 * u), y, terco, alto * u), "Sono 5", _botao)) TesteDoBunkerRpc(BunkerComSono, 0);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Pôr um sorteado", _botao)) TesteDoBunkerRpc(BunkerPor, 0);
            if (GUI.Button(new Rect(x + meio + 6 * u, y, meio, alto * u), "Tirar o último", _botao)) TesteDoBunkerRpc(BunkerTirar, 0);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, w, alto * u), "Encher o estoque (+7 dias de comida e água)", _botao)) TesteDoBunkerRpc(BunkerEstoque, 0);
            y += passo * u;
            // o humor de todos (um motivo de teste por 6 horas): a crise leve vem abaixo de 15 em 2 horas, e a forte abaixo de 5
            if (GUI.Button(new Rect(x, y, terco, alto * u), "Humor 3", _botao)) TesteDoBunkerRpc(BunkerHumor, 0);
            if (GUI.Button(new Rect(x + terco + 6 * u, y, terco, alto * u), "Humor 12", _botao)) TesteDoBunkerRpc(BunkerHumor, 1);
            if (GUI.Button(new Rect(x + 2f * (terco + 6 * u), y, terco, alto * u), "Humor normal", _botao)) TesteDoBunkerRpc(BunkerHumor, 2);
            y += passo * u;
            // o lazer (a parte 8): a vontade de fumar cheia e dois jogando sinuca agora
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Fumo cheio", _botao)) TesteDoBunkerRpc(BunkerLazer, 0);
            if (GUI.Button(new Rect(x + meio + 6 * u, y, meio, alto * u), "Sinuca agora", _botao)) TesteDoBunkerRpc(BunkerLazer, 1);
            y += passo * u;
            GUI.Label(new Rect(x, y + 3 * u, w, 20 * u), "Ir ao andar (na porta da escada)", _texto);
            y += 22 * u;
            for (int f = 0; f < 4; f++)
                if (GUI.Button(new Rect(x + f * (quarto + 6 * u), y, quarto, alto * u), PlantaDeBunker.Codigos[f], _botao)) IrAoAndar(f);
            return area;
        }

        // o jogador vai à porta da escada do andar (do lado do hall, olhando para dentro do andar)
        void IrAoAndar(int f)
        {
            var bunker = BunkerDoMundo.Instancia;
            var mov = GetComponent<MovimentoPrimeiraPessoa>();
            if (bunker == null || bunker.Raiz == null || mov == null) return;
            var p = bunker.Raiz.TransformPoint(LugaresDoBunker.NaEscada(f) + new Vector3(0.6f, 0.1f, 0f));
            mov.Teleportar(p, bunker.Raiz.eulerAngles.y + 90f);
        }

        [Rpc(SendTo.Server)]
        void TesteDoBunkerRpc(byte acao, byte valor, RpcParams parametros = default)
        {
            if (!Aceita(parametros)) return;
            var m = MoradoresDoBunker.Instancia;
            switch (acao)
            {
                case BunkerRelogio:
                    CicloDoDia.Acelerado = Relogios[Mathf.Clamp(valor, 0, Relogios.Length - 1)];
                    break;
                case BunkerEncher: m?.Barras(100f, 100f, 100f); break;
                case BunkerComFome: m?.Barras(20f, 20f, -1f); break;
                case BunkerComSono: m?.Barras(-1f, -1f, 5f); break;
                case BunkerPor:
                {
                    var f = m?.PorUmSorteado();
                    if (f != null) Debug.Log($"[Moradores] F1: {f.NomeCompleto} entrou no bunker.");
                    break;
                }
                case BunkerTirar: m?.TirarOUltimo(); break;
                case BunkerHumor: m?.HumorAte(valor == 0 ? 3f : valor == 1 ? 12f : -1f); break;
                case BunkerLazer:
                    if (valor == 0) m?.FumoCheio();
                    else m?.SinucaAgora();
                    break;
                case BunkerEstoque:
                {
                    int n = m != null ? m.EncherOEstoque() : 0;
                    Debug.Log($"[Moradores] F1: {n} itens de comida e água no estoque.");
                    break;
                }
            }
        }
    }
}
