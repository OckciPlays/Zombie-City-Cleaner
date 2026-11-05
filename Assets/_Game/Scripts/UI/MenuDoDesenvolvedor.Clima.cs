using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O painel de teste do clima (02/11/2026, World/CicloDoDia.Tempo.cs e World/ClimaNaTela.cs), no lugar do painel dos
    /// zumbis, ao lado do Oco (as abas "Zumbis", "Clima", "Gente" e "Corpo" na faixa logo acima). Embaixo do Oco ou do
    /// carro ele passava da altura da tela e os botões não apareciam. Mostra o tempo agora e o próximo evento do
    /// calendário e onde quem joga está (fora, dentro, no carro, debaixo da terra). Os botões forçam cada nível, chamam o tornado, derrubam um raio
    /// perto ou longe e enchem as poças; os pedidos vão para o anfitrião (quem manda no tempo).
    /// O fogo (05/11/2026: a chuva apaga a tocha e a fogueira, e os zumbis ficam longe delas): uma fogueira acesa na
    /// frente, a tocha acesa na mão e uma horda de 6 caçando quem pediu; no texto, os fogos, quem rodeia cada um (e quanto
    /// falta para a horda criar coragem), a fogueira mais perto (coberta ou no aberto, o quanto molhou) e a tocha molhando.
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        static readonly (string nome, int acao)[] TestesDoClima =
        {
            ("Sol", 0), ("Chuvisco", 1), ("Chuva", 2), ("Tempestade", 3), ("Com raios", 4), ("Calendário", 5),
            ("Tornado agora", 6), ("Acabar o tornado", 7), ("Raio perto", 8), ("Raio longe", 9), ("Só o clarão", 12), ("Encher as poças", 10),
            ("Próximo evento", 11), ("Fogueira aqui", 13), ("Tocha acesa na mão", 14), ("Horda de 6 caçando", 15),
            ("Long da fogueira já", 16), ("Brutamonte enterrado", 17),
        };

        bool _abaDoClima;
        static readonly GUIContent _conteudoDoClima = new GUIContent();

        static readonly string[] NomesDasAbas = { "Zumbis", "Clima", "Gente", "Corpo" };

        /// <summary>A faixa das abas acima do painel do canto (o <see cref="AbasDoCanto"/>), para a Area pegar os cliques nela.</summary>
        static float AlturaDasAbas(float u) => 24 * u;

        // as abas numa faixa logo acima do painel do canto (05/11/2026: com a quarta, "Corpo", a linha dentro do painel
        // cobria os títulos "ZUMBIS (TESTE)" e "SOBREVIVENTES"); a aba ativa em branco, as outras a 55%
        void AbasDoCanto(float u, Rect painel)
        {
            float vao = 4 * u, h = 20 * u, y = painel.y - AlturaDasAbas(u);
            float w = (painel.width - vao * (NomesDasAbas.Length - 1)) / NomesDasAbas.Length;
            int ativa = _abaDoCorpo ? 3 : _abaDosSobreviventes ? 2 : _abaDoClima ? 1 : 0;
            var antes = GUI.color;
            for (int i = 0; i < NomesDasAbas.Length; i++)
            {
                GUI.color = i == ativa ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                if (GUI.Button(new Rect(painel.x + i * (w + vao), y, w, h), NomesDasAbas[i], _botao)) MostrarAba(i);
            }
            GUI.color = antes;
        }

        // 0: os zumbis; 1: o clima; 2: os sobreviventes (MenuDoDesenvolvedor.Sobreviventes.cs); 3: o corpo
        // (MenuDoDesenvolvedor.Corpo.cs)
        void MostrarAba(int qual)
        {
            _abaDoClima = qual == 1;
            _abaDosSobreviventes = qual == 2;
            _abaDoCorpo = qual == 3;
        }

        Rect PainelDoClima(float u, Rect aoLado)
        {
            float x0 = aoLado.xMax + 8 * u, largura = 250 * u;
            float x = x0 + 12 * u, w = largura - 24 * u;
            float meio = (w - 6 * u) / 2f, x2 = x + meio + 6 * u;
            // o texto primeiro: a altura do painel acompanha (as linhas do tornado e do próximo evento vêm e vão)
            var ciclo = CicloDoDia.Instancia;
            string texto = ciclo != null ? ciclo.ResumoDoTempo() : "sem o ciclo do dia nesta cena";
            if (ClimaNaTela.Instancia != null) texto += "\n" + ClimaNaTela.Instancia.Resumo();
            texto += "\n" + ResumoDoFogo();
            _conteudoDoClima.text = texto;
            float alturaDoTexto = _texto.CalcHeight(_conteudoDoClima, w);
            int linhas = (TestesDoClima.Length + 1) / 2;
            float altura = 8 * u + 28 * u + alturaDoTexto + 8 * u + linhas * 30 * u + 6 * u;
            var area = new Rect(x0, aoLado.y, largura, altura);

            var antes = GUI.color;
            GUI.color = new Color(0.07f, 0.08f, 0.09f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.35f, 0.55f, 0.75f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "CLIMA", _titulo);
            y += 28 * u;
            GUI.Label(new Rect(x, y, w, alturaDoTexto), _conteudoDoClima, _texto);
            y += alturaDoTexto + 8 * u;
            for (int k = 0; k < TestesDoClima.Length; k++)
            {
                var r = new Rect(k % 2 == 0 ? x : x2, y + (k / 2) * 30 * u, meio, 26 * u);
                if (!GUI.Button(r, TestesDoClima[k].nome, _botao)) continue;
                if (TestesDoClima[k].acao >= 13) TesteDoFogoRpc((byte)TestesDoClima[k].acao);
                else TesteDoClimaRpc((byte)TestesDoClima[k].acao);
            }
            return area;
        }

        // o fogo no texto do painel (os números ficam no anfitrião)
        string ResumoDoFogo()
        {
            if (!IsServer) return "fogo: os números ficam no anfitrião";
            var zm = Zombies.ZombieManager.Instancia;
            string s = zm != null ? zm.ResumoDosFogos(transform.position) + zm.ResumoDoLongNaFogueira(transform.position) + zm.ResumoDoEnterrado(transform.position) : "fogos: sem os zumbis";
            var c = Loot.ConstrucoesNoMundo.Instancia;
            if (c == null) return s;
            if (c.FogueiraPerto(transform.position, 25f, out var f)) s += $"\nfogueira perto: {c.ResumoDaFogueira(f)}";
            float m = c.TochaMolhada(OwnerClientId);
            if (m > 0f) s += $"\ntocha: molhou {m * 100f:0}%";
            return s;
        }

        // 13: uma fogueira acesa 2,5 m à frente; 14: uma tocha acesa na mão; 15: uma horda de 6 caçando quem pediu (14 m à frente);
        // 16: o Long da fogueira mais perto, já; 17: o Brutamonte enterrado 15-18 m à frente (chegue a 10 m dele)
        [Rpc(SendTo.Server)]
        void TesteDoFogoRpc(byte acao, RpcParams parametros = default)
        {
            if (!Aceita(parametros)) return;
            var rede = GetComponent<Player.JogadorRede>();
            Vector3 frente = transform.forward;
            frente.y = 0f;
            frente = frente.sqrMagnitude > 0.01f ? frente.normalized : Vector3.forward;
            switch (acao)
            {
                case 13:
                    Loot.ConstrucoesNoMundo.Instancia?.FogueiraDeTeste(transform.position + frente * 2.5f, transform.eulerAngles.y,
                        rede != null && rede.Chave.Value != 0 ? rede.Chave.Value : OwnerClientId);
                    break;
                case 14:
                {
                    var inv = GetComponent<Player.InventarioJogador>();
                    if (inv == null || !inv.Receber(Loot.Item.Tocha)) break;
                    ushort id = 0;
                    foreach (var it in inv.Mochila)
                        if (it.item == Loot.Item.Tocha && it.qtd != Loot.Itens.TochaApagada && it.id > id) id = it.id;
                    if (id != 0) inv.PorNaMao(id);
                    break;
                }
                case 15:
                    if (rede != null) Zombies.ZombieManager.Instancia?.CriarHordaPerseguindo(rede, transform.position + frente * 14f, 6);
                    break;
                case 16:
                {
                    string feito = Zombies.ZombieManager.Instancia?.LongDaFogueiraAgora(transform.position);
                    if (!string.IsNullOrEmpty(feito)) AvisoDoCarroRpc(feito);
                    break;
                }
                case 17:
                {
                    string feito = Zombies.ZombieManager.Instancia?.EnterrarBrutamonteNaFrente(rede);
                    if (!string.IsNullOrEmpty(feito)) AvisoDoCarroRpc(feito);
                    break;
                }
            }
        }

        [Rpc(SendTo.Server)]
        void TesteDoClimaRpc(byte acao)
        {
            var ciclo = CicloDoDia.Instancia;
            if (ciclo == null) return;
            switch (acao)
            {
                case <= 4: ciclo.ForcarTempo(acao); break;
                case 5: ciclo.ForcarTempo(-1); break;
                case 6:
                    if (ciclo.TempoForcado < (int)NivelDoTempo.Raios) ciclo.ForcarTempo((int)NivelDoTempo.Raios);
                    ciclo.ComecarTornado(300f);
                    break;
                case 7: ciclo.AcabarTornado(); break;
                case 8: ciclo.RaioPerto(); break;
                case 9: ciclo.RaioLonge(); break;
                case 10: ciclo.EncherPocas(); break;
                case 11: ciclo.ProximoEventoAgora(); break;
                case 12: ciclo.RaioLonge(true); break;
            }
        }
    }
}
