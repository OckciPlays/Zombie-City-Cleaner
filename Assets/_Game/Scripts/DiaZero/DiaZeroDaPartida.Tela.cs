using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// O que o Dia 0 desenha na tela: a carteira e o carrinho no canto (no Dia 0), o aviso curto embaixo da mira, a tela
    /// de carregar a rua e a dos "5 anos depois" (a passagem, por cima da geração do mundo).
    /// </summary>
    public partial class DiaZeroDaPartida
    {
        GUIStyle _grande, _texto, _canto, _cantoPequeno, _aviso, _titulo;
        float _u;

        void Estilos(float u)
        {
            if (_grande != null && Mathf.Approximately(u, _u)) return;
            _u = u;
            var fonte = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Arial" }, 32);
            _grande = new GUIStyle(GUI.skin.label) { font = fonte, fontSize = Mathf.RoundToInt(64 * u), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _grande.normal.textColor = new Color(0.93f, 0.91f, 0.86f);
            _titulo = new GUIStyle(_grande) { fontSize = Mathf.RoundToInt(30 * u) };
            _texto = new GUIStyle(GUI.skin.label) { font = fonte, fontSize = Mathf.RoundToInt(17 * u), alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _texto.normal.textColor = new Color(0.82f, 0.8f, 0.75f);
            _canto = new GUIStyle(GUI.skin.label) { font = fonte, fontSize = Mathf.RoundToInt(22 * u), alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
            _canto.normal.textColor = new Color(0.93f, 0.9f, 0.82f);
            _cantoPequeno = new GUIStyle(_canto) { fontSize = Mathf.RoundToInt(14 * u), fontStyle = FontStyle.Normal };
            _aviso = new GUIStyle(GUI.skin.label) { font = fonte, fontSize = Mathf.RoundToInt(16 * u), alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _aviso.normal.textColor = new Color(0.95f, 0.93f, 0.86f);
        }

        void OnGUI()
        {
            if (!IsSpawned || _mundo == null) return;
            float u = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            Estilos(u);
            if (_passagem)
            {
                GUI.depth = -21;
                DesenharAPassagem(u);
                return;
            }
            if (!_mundo.NoDiaZero) return;
            // carregando a rua (antes de o jogador nascer)
            if (!_ruaPronta || JogadorRede.Local == null)
            {
                GUI.depth = -21;
                Preto(1f);
                GUI.Label(new Rect(0, Screen.height * 0.42f, Screen.width, 50 * u), "UM DIA NORMAL", _titulo);
                GUI.Label(new Rect(0, Screen.height * 0.42f + 54 * u, Screen.width, 30 * u), "Carregando a rua…", _texto);
                return;
            }
            if (MenuOnline.Aberto || ModoVideo.Ligado || MenuOnline.Cinema) return;
            GUI.depth = -5;
            if (TelaDoCaixa.Aberta) TelaDoCaixa.Desenhar(this, u);
            if (!JogadorRede.Local.Criado) return;
            Canto(u);
            DesenharOLobby(u);
            // o aviso do carrinho (embaixo da mira) e a mensagem do Dia 0 (mais embaixo)
            if (Carrinho.Aviso != null && Time.time < Carrinho.AvisoAte)
                Sombra(new Rect(Screen.width / 2f - 400 * u, Screen.height / 2f + 64 * u, 800 * u, 26 * u), Carrinho.Aviso, _aviso);
            if (_mensagem != null && Time.unscaledTime < _mensagemAte)
                Sombra(new Rect(Screen.width / 2f - 420 * u, Screen.height * 0.78f, 840 * u, 50 * u), _mensagem, _aviso);
        }

        // a carteira e o carrinho, no canto de cima à direita
        void Canto(float u)
        {
            ulong eu = NetworkManager.LocalClientId;
            float x = Screen.width - 420 * u, y = 54 * u, w = 400 * u;
            Sombra(new Rect(x, y, w, 30 * u), PrecosDoDiaZero.Dinheiro(Dinheiro(eu)), _canto);
            Sombra(new Rect(x, y + 28 * u, w, 20 * u), "suas economias", _cantoPequeno);
            y += 56 * u;
            int compras = Compras(eu);
            if (compras > 0)
            {
                Sombra(new Rect(x, y, w, 20 * u), $"{compras} {(compras == 1 ? "compra" : "compras")} em casa", _cantoPequeno);
                y += 22 * u;
            }
            if (Carrinho.Loja != null && Carrinho.Quantos > 0)
            {
                y += 6 * u;
                Sombra(new Rect(x, y, w, 24 * u), $"CARRINHO  ·  {Carrinho.Quantos} {(Carrinho.Quantos == 1 ? "item" : "itens")}  ·  {PrecosDoDiaZero.Dinheiro(Carrinho.Total)}",
                       new GUIStyle(_canto) { fontSize = Mathf.RoundToInt(16 * u) });
                Sombra(new Rect(x, y + 22 * u, w, 20 * u), $"{Carrinho.Loja.Nome}: pague no caixa antes de sair", _cantoPequeno);
            }
        }

        // a tela dos 5 anos depois: escurece, o título, a barra da geração do mundo; com o mundo pronto, clareia
        void DesenharAPassagem(float u)
        {
            float t = Time.unscaledTime - _passagemDesde;
            float alfa = Mathf.Clamp01(t / 1.2f);
            float texto = Mathf.Clamp01((t - 1.6f) / 1.4f);
            if (_prontoDesde >= 0f)
            {
                float d = Time.unscaledTime - _prontoDesde;
                alfa = 1f - Mathf.Clamp01((d - 2.2f) / 2.0f);
                texto = Mathf.Min(texto, 1f - Mathf.Clamp01((d - 1.6f) / 1.0f));
            }
            Preto(alfa);
            var antes = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, texto);
            GUI.Label(new Rect(0, Screen.height * 0.38f, Screen.width, 90 * u), "5 ANOS DEPOIS", _grande);
            GUI.Label(new Rect(Screen.width * 0.2f, Screen.height * 0.38f + 96 * u, Screen.width * 0.6f, 50 * u),
                      "A porta do abrigo ficou fechada. Lá fora, o mundo mudou.", _texto);
            if (!string.IsNullOrEmpty(_quemFicouFora))
                GUI.Label(new Rect(Screen.width * 0.2f, Screen.height * 0.38f + 140 * u, Screen.width * 0.6f, 40 * u),
                          $"{_quemFicouFora} ficou de fora quando a porta fechou: as compras ficaram para trás.", _texto);
            if (_prontoDesde < 0f)
            {
                // a barra da geração (fina, embaixo)
                float w = 360 * u, x = (Screen.width - w) / 2f, y = Screen.height * 0.84f;
                GUI.color = new Color(0.2f, 0.2f, 0.19f, texto);
                GUI.DrawTexture(new Rect(x, y, w, 4 * u), Texture2D.whiteTexture);
                GUI.color = new Color(0.72f, 0.58f, 0.2f, texto);
                GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(_mundo.Progresso), 4 * u), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, texto * 0.7f);
                GUI.Label(new Rect(0, y + 8 * u, Screen.width, 22 * u), _mundo.Etapa + "…", _texto);
            }
            GUI.color = antes;
        }

        static void Preto(float alfa)
        {
            if (alfa <= 0f) return;
            var antes = GUI.color;
            GUI.color = new Color(0.02f, 0.02f, 0.02f, alfa);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = antes;
        }

        internal static void Sombra(Rect r, string texto, GUIStyle estilo)
        {
            var cor = estilo.normal.textColor;
            estilo.normal.textColor = new Color(0f, 0f, 0f, 0.75f * cor.a);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), texto, estilo);
            estilo.normal.textColor = cor;
            GUI.Label(r, texto, estilo);
        }
    }

    /// <summary>
    /// A tela do caixa (o Dia 0): o que está no carrinho, com o "–" para tirar um, as roupas provadas, o total, o dinheiro e
    /// o pagar. Abre com E no caixa; o mouse fica solto (uma tela do jogo, <see cref="MenuOnline.Tela"/>); ESC fecha.
    /// </summary>
    public static class TelaDoCaixa
    {
        static readonly object Dona = new object();
        static LojaDoDiaZero _loja;
        static Vector2 _rolar;
        static GUIStyle _titulo, _linha, _botao, _total, _pequeno;
        static float _u;

        public static bool Aberta => _loja != null;

        public static void Abrir(LojaDoDiaZero loja)
        {
            if (_loja != null) return;
            _loja = loja;
            MenuOnline.Tela(Dona, true);
            MenuOnline.FecharTelas += Fechar;
        }

        public static void Fechar()
        {
            if (_loja == null) return;
            _loja = null;
            MenuOnline.Tela(Dona, false);
            MenuOnline.FecharTelas -= Fechar;
        }

        static void Estilos(float u)
        {
            if (_titulo != null && Mathf.Approximately(u, _u)) return;
            _u = u;
            _titulo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * u), fontStyle = FontStyle.Bold };
            _titulo.normal.textColor = new Color(0.93f, 0.9f, 0.82f);
            _linha = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(16 * u) };
            _linha.normal.textColor = new Color(0.86f, 0.84f, 0.78f);
            _pequeno = new GUIStyle(_linha) { fontSize = Mathf.RoundToInt(13 * u) };
            _total = new GUIStyle(_titulo) { fontSize = Mathf.RoundToInt(20 * u), alignment = TextAnchor.MiddleRight };
            _botao = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(16 * u) };
        }

        public static void Desenhar(DiaZeroDaPartida dia, float u)
        {
            if (_loja == null) return;
            if (Carrinho.Loja != _loja || Carrinho.Quantos == 0)
            {
                Fechar();
                return;
            }
            Estilos(u);
            float w = 520 * u, h = 470 * u, x = (Screen.width - w) / 2f, y = (Screen.height - h) / 2f;
            var antes = GUI.color;
            GUI.color = new Color(0.07f, 0.07f, 0.065f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = new Color(0.72f, 0.58f, 0.2f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, w, 3 * u), Texture2D.whiteTexture);
            GUI.color = antes;
            float px = x + 20 * u, pw = w - 40 * u, py = y + 14 * u;
            GUI.Label(new Rect(px, py, pw, 30 * u), $"CAIXA  ·  {_loja.Nome.ToUpperInvariant()}", _titulo);
            py += 40 * u;

            // as linhas: cada produto (com a quantidade) e cada roupa provada
            var contagem = new SortedDictionary<int, int>();
            foreach (int i in Carrinho.Produtos) contagem[i] = contagem.TryGetValue(i, out int n) ? n + 1 : 1;
            var roupas = Carrinho.Roupas();
            float alturaDaLista = h - 190 * u;
            var vista = new Rect(px, py, pw, alturaDaLista);
            float conteudo = (contagem.Count + roupas.Count) * 32 * u;
            _rolar = GUI.BeginScrollView(vista, _rolar, new Rect(0, 0, pw - 18 * u, Mathf.Max(conteudo, alturaDaLista - 4)));
            float ly = 0f;
            foreach (var kv in contagem)
            {
                var p = PrecosDoDiaZero.DaLoja(_loja.Tipo, kv.Key);
                if (!p.HasValue) continue;
                GUI.Label(new Rect(0, ly, pw - 200 * u, 28 * u), $"{kv.Value} × {Carrinho.Primeira(p.Value.Nome)}", _linha);
                GUI.Label(new Rect(pw - 210 * u, ly, 110 * u, 28 * u), PrecosDoDiaZero.Dinheiro(p.Value.preco * kv.Value), _linha);
                if (GUI.Button(new Rect(pw - 90 * u, ly + 2 * u, 34 * u, 24 * u), "–", _botao)) Carrinho.Tirar(kv.Key);
                if (GUI.Button(new Rect(pw - 52 * u, ly + 2 * u, 34 * u, 24 * u), "+", _botao)) Carrinho.Produtos.Add(kv.Key);
                ly += 32 * u;
            }
            foreach (var (lugar, valor) in roupas)
            {
                string peca = AparenciaDoJogador.LugaresDaLoja[lugar].pecas[valor - 1];
                GUI.Label(new Rect(0, ly, pw - 200 * u, 28 * u), $"{Carrinho.Primeira(Carrinho.Nome(peca))} (vestindo)", _linha);
                GUI.Label(new Rect(pw - 210 * u, ly, 110 * u, 28 * u), PrecosDoDiaZero.Dinheiro(PrecosDoDiaZero.PrecoDaRoupa(peca)), _linha);
                if (GUI.Button(new Rect(pw - 90 * u, ly + 2 * u, 72 * u, 24 * u), "Tirar", _botao)) Carrinho.Desprovar(lugar);
                ly += 32 * u;
            }
            GUI.EndScrollView();
            py += alturaDaLista + 10 * u;

            int total = Carrinho.Total, tenho = dia.Dinheiro(dia.NetworkManager.LocalClientId);
            GUI.Label(new Rect(px, py, pw, 28 * u), $"Total  {PrecosDoDiaZero.Dinheiro(total)}", _total);
            py += 28 * u;
            var cor = _pequeno.normal.textColor;
            if (total > tenho) _pequeno.normal.textColor = new Color(1f, 0.5f, 0.4f);
            GUI.Label(new Rect(px, py, pw, 22 * u),
                      total > tenho ? $"Você tem {PrecosDoDiaZero.Dinheiro(tenho)}: faltam {PrecosDoDiaZero.Dinheiro(total - tenho)}. Tire alguma coisa."
                                    : $"Você tem {PrecosDoDiaZero.Dinheiro(tenho)}; sobram {PrecosDoDiaZero.Dinheiro(tenho - total)}. As compras vão direto para a sua casa.", _pequeno);
            _pequeno.normal.textColor = cor;
            py += 34 * u;
            GUI.enabled = total <= tenho && !dia.Pagando;
            if (GUI.Button(new Rect(px, py, pw * 0.62f, 40 * u), dia.Pagando ? "Pagando…" : $"Pagar {PrecosDoDiaZero.Dinheiro(total)}", _botao)) dia.Pagar();
            GUI.enabled = true;
            if (GUI.Button(new Rect(px + pw * 0.66f, py, pw * 0.34f, 40 * u), "Voltar (ESC)", _botao)) Fechar();
        }
    }
}
