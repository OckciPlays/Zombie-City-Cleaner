using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// A aba "Chefes" do F1 (15/11/2026, a pedido: os chefes no jogo, com a IA e as habilidades; Zumbi.Chefes.cs): criar
    /// cada um na sua frente (já atrás de você, depois de surgir), a próxima habilidade do mais perto agora (uma de cada vez,
    /// na ordem), tirar um quarto da vida dele (o frenesi do Condutor com a metade e com um quinto) ou matá-lo (o Blindado
    /// se divide nos Agregados, o Alongado vira o Longo), e ligar e desligar o povoamento do mundo (para testar sem os Ocos
    /// em volta). Embaixo, o que o chefe mais perto está fazendo. Os pedidos vão para o anfitrião.
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        bool _abaDosChefes;

        static readonly (QualChefe qual, string nome, string aviso)[] BotoesDosChefes =
        {
            (QualChefe.Blindado, "Blindado", "O Blindado, 15 m à frente: agarra, bate no chão e dá a investida. Morto, vira os Agregados."),
            (QualChefe.Condutor, "Condutor", "O Condutor, 17 m à frente: o grito faz nascer uma horda (Ocos, Corredores, Longs e Brutamontes); o rosto no peito é o ponto fraco."),
            (QualChefe.AgregadoA, "Agregado A", "Um Agregado, 9 m à frente: a mão de carne estica, agarra e puxa."),
            (QualChefe.AgregadoB, "Agregado B", "Um Agregado, 9 m à frente: a mão de carne estica, agarra e puxa."),
            (QualChefe.Alongado, "Alongado", "O Alongado, 17 m à frente: alcança longe e arremessa. Morto, vira dois Longos."),
            (QualChefe.Longo, "Longo", "O Longo, 11 m à frente: o ácido (prende, corrói e envenena) e o bote; o papo é o ponto fraco."),
        };

        Rect PainelDosChefes(float u, Rect aoLado)
        {
            var area = new Rect(aoLado.xMax + 8 * u, aoLado.y, 250 * u, 330 * u);
            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.62f, 0.12f, 0.1f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float x = area.x + 12 * u, w = area.width - 24 * u, y = area.y + 8 * u;
            float meio = (w - 6 * u) / 2f, x2 = x + meio + 6 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "CHEFES (TESTE)", _titulo);
            y += 24 * u;
            var zm = ZombieManager.Instancia;
            for (int i = 0; i < BotoesDosChefes.Length; i++)
            {
                var b = BotoesDosChefes[i];
                if (GUI.Button(new Rect(i % 2 == 0 ? x : x2, y, meio, 26 * u), b.nome, _botao))
                {
                    CriarChefeRpc(b.qual);
                    Avisar(b.aviso);
                }
                if (i % 2 == 1) y += 30 * u;
            }
            if (GUI.Button(new Rect(x, y, w, 26 * u), "Habilidade agora (o mais perto)", _botao))
            {
                HabilidadeDoChefeRpc();
                Avisar("O chefe mais perto (até 70 m) vira para você e faz a próxima habilidade dele (uma de cada vez, na ordem).");
            }
            y += 30 * u;
            if (GUI.Button(new Rect(x, y, meio, 26 * u), "-25% da vida", _botao))
            {
                FerirChefeRpc(0.25f);
                Avisar("O chefe mais perto perdeu um quarto da vida (o Condutor entra em frenesi com a metade e com um quinto).");
            }
            if (GUI.Button(new Rect(x2, y, meio, 26 * u), "Matar o chefe", _botao))
            {
                FerirChefeRpc(1f);
                Avisar("O chefe mais perto morreu (o Blindado se divide nos Agregados; o Alongado vira o Longo).");
            }
            y += 30 * u;
            bool povoar = zm == null || zm.PovoarOMundo.Value;
            if (GUI.Button(new Rect(x, y, w, 26 * u), povoar ? "Povoar o mundo: LIGADO" : "Povoar o mundo: DESLIGADO", _botao))
            {
                PovoarRpc(!povoar);
                Avisar(!povoar ? "O mundo volta a se povoar em volta (de 22 a 52 m)." : "Nenhuma célula nova se povoa (os que já estão ficam).");
            }
            y += 32 * u;
            GUI.Label(new Rect(x, y, w, 60 * u), EstadoDoChefeMaisPerto(zm), _texto);
            return area;
        }

        // o que o chefe mais perto (nesta máquina) está fazendo: a ação, a vida e o frenesi
        string EstadoDoChefeMaisPerto(ZombieManager zm)
        {
            if (zm == null) return "sem zumbis nesta cena";
            Zumbi melhor = null;
            float menor = 120f * 120f;
            foreach (var z in zm.Visiveis())
            {
                if (z == null || !z.EhChefe || z.Morto) continue;
                float d = (z.transform.position - transform.position).sqrMagnitude;
                if (d < menor)
                {
                    menor = d;
                    melhor = z;
                }
            }
            if (melhor == null) return "nenhum chefe a até 120 m";
            return $"{melhor.NomeDoChefe} a {Mathf.Sqrt(menor):0} m\nação: {melhor.AcaoAtual} · vida: {melhor.FracaoDaVida * 100f:0}%{(melhor.Enfurecido ? " · em frenesi" : "")}";
        }

        [Rpc(SendTo.Server)]
        void CriarChefeRpc(QualChefe qual, RpcParams parametros = default)
        {
            if (!Aceita(parametros) || ZombieManager.Instancia == null) return;
            ZombieManager.Instancia.CriarChefePerto(qual, transform.position, transform.forward, GetComponent<Player.JogadorRede>());
        }

        [Rpc(SendTo.Server)]
        void HabilidadeDoChefeRpc(RpcParams parametros = default)
        {
            var zm = ZombieManager.Instancia;
            if (!Aceita(parametros) || zm == null) return;
            var z = zm.ChefeMaisPerto(transform.position, 70f);
            if (z != null) z.HabilidadeAgora(transform.position, GetComponent<Player.JogadorRede>());
        }

        [Rpc(SendTo.Server)]
        void FerirChefeRpc(float fracao, RpcParams parametros = default)
        {
            var zm = ZombieManager.Instancia;
            if (!Aceita(parametros) || zm == null) return;
            var z = zm.ChefeMaisPerto(transform.position, 120f);
            if (z != null) z.TirarVidaDoChefe(Mathf.Clamp01(fracao), GetComponent<Player.JogadorRede>());
        }
    }
}
