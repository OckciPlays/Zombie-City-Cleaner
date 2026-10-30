using UnityEngine;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O painel do Oco de teste, ao lado dos outros do F1 (a pedido em 27/10/2026): pôr um Oco 4 m à frente, tirar todos e
    /// escolher a animação. Desde 28/10/2026, também a versão (uma das versões de ~5 mil triângulos,
    /// <see cref="OcoDeTeste.Versoes"/>; o antigo, de ~125 mil, saiu) e "Pôr todas lado a lado", para comparar. Só nesta máquina (sem rede) e sem dano
    /// (<see cref="OcoDeTeste"/>).
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        static string[] _nomesDasVersoes;

        Rect PainelDoOco(float u, Rect aoLado)
        {
            if (_nomesDasVersoes == null)
            {
                _nomesDasVersoes = new string[OcoDeTeste.Versoes.Length];
                for (int i = 0; i < _nomesDasVersoes.Length; i++) _nomesDasVersoes[i] = OcoDeTeste.Versoes[i].nome;
            }
            int linhasVersao = (_nomesDasVersoes.Length + 1) / 2;
            int linhasModo = (OcoDeTeste.NomesDosModos.Length + 1) / 2;
            float altura = 8 + 26 + 24 + 22 + linhasVersao * 26 + 8 + 34 + 34 + 34 + 22 + linhasModo * 30 + 10;
            var area = new Rect(aoLado.xMax + 8 * u, aoLado.y, 250 * u, altura * u);
            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.72f, 0.58f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float x = area.x + 12 * u, w = area.width - 24 * u, y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "O OCO (TESTE)", _titulo);
            y += 26 * u;
            GUI.Label(new Rect(x, y, w, 36 * u), "Só nesta máquina. Não tira vida.", _texto);
            y += 24 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "Versão", _texto);
            y += 22 * u;
            OcoDeTeste.VersaoEscolhida = GUI.SelectionGrid(new Rect(x, y, w, linhasVersao * 26 * u), OcoDeTeste.VersaoEscolhida, _nomesDasVersoes, 2, _botao);
            y += linhasVersao * 26 * u + 8 * u;
            if (GUI.Button(new Rect(x, y, w, 28 * u), "Pôr um Oco na minha frente", _botao)) PorOco();
            y += 34 * u;
            if (GUI.Button(new Rect(x, y, w, 28 * u), "Pôr todas lado a lado", _botao)) PorTodasAsVersoes();
            y += 34 * u;
            if (GUI.Button(new Rect(x, y, w, 26 * u), $"Tirar os Ocos ({OcoDeTeste.Todos.Count})", _botao)) OcoDeTeste.TirarTodos();
            y += 34 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "Animação (todos)", _texto);
            y += 22 * u;
            int atual = (int)OcoDeTeste.ModoDeTodos;
            int escolhido = GUI.SelectionGrid(new Rect(x, y, w, linhasModo * 30 * u), atual, OcoDeTeste.NomesDosModos, 2, _botao);
            if (escolhido != atual) OcoDeTeste.MudarTodos((OcoDeTeste.Modo)escolhido);
            return area;
        }

        // o ponto no chão (um raio de cima) a "frente" metros à frente e "lado" metros à direita do jogador
        Vector3 ChaoNaFrente(Vector3 frente, float aFrente, float aoLado)
        {
            Vector3 direita = Vector3.Cross(Vector3.up, frente);
            Vector3 pe = transform.position + frente * aFrente + direita * aoLado;
            if (Physics.Raycast(pe + Vector3.up * 1.5f, Vector3.down, out var chao, 6f, ~((1 << 2) | (1 << Player.JogadorRede.CamadaJogador)), QueryTriggerInteraction.Ignore))
                pe = chao.point;
            return pe;
        }

        Vector3 FrenteNoChao()
        {
            Vector3 frente = transform.forward;
            frente.y = 0f;
            return frente.sqrMagnitude > 0.0001f ? frente.normalized : Vector3.forward;
        }

        // 4 m à frente, no chão, olhando para o jogador
        void PorOco()
        {
            Vector3 frente = FrenteNoChao();
            var versao = OcoDeTeste.Versoes[OcoDeTeste.VersaoEscolhida];
            if (OcoDeTeste.Criar(ChaoNaFrente(frente, 4f, 0f), -frente) == null)
                Avisar("Esta versão ainda não foi montada: menu Zombie City Cleaner > Zumbis > Montar as versões do Oco (teste).");
            else Avisar($"Oco \"{versao.nome}\" posto 4 m à frente.");
        }

        // as versões numa fila a 5 m, 1,1 m uma da outra, todas olhando para o jogador
        void PorTodasAsVersoes()
        {
            Vector3 frente = FrenteNoChao();
            int total = OcoDeTeste.Versoes.Length, postas = 0;
            for (int i = 0; i < total; i++)
            {
                float lado = (i - (total - 1) * 0.5f) * 1.1f;
                if (OcoDeTeste.Criar(ChaoNaFrente(frente, 5f, lado), -frente, i) != null) postas++;
            }
            if (postas == 0) Avisar("As versões ainda não foram montadas: menu Zombie City Cleaner > Zumbis > Montar as versões do Oco (teste).");
            else Avisar($"{postas} versões postas lado a lado, 5 m à frente (da esquerda: Original 5k, 2k, 1k, 01 ... 11).");
        }
    }
}
