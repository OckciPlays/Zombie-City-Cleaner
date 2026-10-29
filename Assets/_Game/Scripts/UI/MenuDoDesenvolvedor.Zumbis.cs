using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.World;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O painel de teste dos zumbis (a pedido em 28/10/2026), ao lado do painel do Oco: criar um zumbi na frente (um por
    /// um, normal ou já de pedra), petrificar os de perto, soltar os de pedra, pular para o amanhecer (5h, com o relógio
    /// andando: os zumbis procuram abrigo antes de o sol vir), uma horda de 8 vindo atrás de você (o cerco), ligar e
    /// desligar o povoamento do mundo gerado, os rastejantes em todos os carros abandonados (em vez de 1 em 10) e um
    /// rastejante agora no carro mais perto (a etapa 5). O dia e a noite são os botões "Sempre de
    /// dia" e "Sempre de noite" do topo do F1 (a pedra só sobe de dia, no sol; as estátuas se soltam de 30 a 60 s depois
    /// de escurecer). Os pedidos vão para o anfitrião.
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        Rect PainelDosZumbis(float u, Rect aoLado)
        {
            var area = new Rect(aoLado.xMax + 8 * u, aoLado.y, 250 * u, 380 * u);
            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.72f, 0.58f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float x = area.x + 12 * u, w = area.width - 24 * u, y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "ZUMBIS (TESTE)", _titulo);
            y += 24 * u;
            var zm = ZombieManager.Instancia;
            string sol = zm == null ? "sem zumbis nesta cena" : zm.EDia ? "dia: o sol petrifica"
                : zm.Amanhecendo ? "amanhecendo: procuram abrigo" : "noite: as estátuas se soltam";
            GUI.Label(new Rect(x, y, w, 36 * u), $"{CicloDoDia.Relogio()} · {sol}\nde pé: {(zm != null ? zm.Vivos : 0)} · de pedra: {(zm != null ? zm.DePedra : 0)}", _texto);
            y += 40 * u;
            // (em pares, lado a lado, para caber o painel da trilha embaixo)
            float meio = (w - 6 * u) / 2f, x2 = x + meio + 6 * u;
            if (GUI.Button(new Rect(x, y, meio, 26 * u), "Criar 1 zumbi", _botao))
            {
                CriarZumbiNaFrenteRpc(false);
                Avisar("Um zumbi 5 m à frente.");
            }
            if (GUI.Button(new Rect(x2, y, meio, 26 * u), "Criar 1 de pedra", _botao))
            {
                CriarZumbiNaFrenteRpc(true);
                Avisar("Um zumbi de pedra 5 m à frente.");
            }
            y += 30 * u;
            if (GUI.Button(new Rect(x, y, meio, 26 * u), "Petrificar (30 m)", _botao))
            {
                PedraRpc(true);
                Avisar("Os zumbis até 30 m viraram pedra.");
            }
            if (GUI.Button(new Rect(x2, y, meio, 26 * u), "Soltar as pedras", _botao))
            {
                PedraRpc(false);
                Avisar("As estátuas se soltaram (no sol, a pedra volta a subir).");
            }
            y += 30 * u;
            if (GUI.Button(new Rect(x, y, w, 26 * u), "Amanhecer (5h, o relógio anda)", _botao))
            {
                AmanhecerRpc();
                Avisar("5h: os zumbis procuram abrigo; o sol petrifica a partir das ~6h30 (1 hora = 1 minuto).");
            }
            y += 30 * u;
            if (GUI.Button(new Rect(x, y, w, 26 * u), "Horda de 8 vindo atrás de mim", _botao))
            {
                HordaRpc(8);
                Avisar("8 zumbis a 14 m à frente, vindo atrás de você (eles cercam).");
            }
            y += 30 * u;
            // a etapa 5: o mundo povoado e os rastejantes embaixo dos carros
            bool povoar = zm == null || zm.PovoarOMundo.Value, todos = zm != null && zm.RastejanteEmTodos.Value;
            if (GUI.Button(new Rect(x, y, w, 26 * u), povoar ? "Povoar o mundo: LIGADO" : "Povoar o mundo: DESLIGADO", _botao))
            {
                PovoarRpc(!povoar);
                Avisar(!povoar ? "O mundo volta a se povoar em volta (de 22 a 52 m)." : "Nenhuma célula nova se povoa (os que já estão ficam).");
            }
            y += 30 * u;
            if (GUI.Button(new Rect(x, y, w, 26 * u), todos ? "Rastejantes: em TODOS os carros" : "Rastejantes: 1 em cada 10 carros", _botao))
            {
                RastejantesRpc(!todos);
                Avisar(!todos ? "Todo carro abandonado tem um rastejante: abra o capô, ponha o macaco ou tire uma roda."
                              : "Volta ao normal: um em cada 10 carros.");
            }
            y += 30 * u;
            if (GUI.Button(new Rect(x, y, w, 26 * u), "Rastejante agora no carro mais perto", _botao))
            {
                RastejanteAgoraRpc();
                Avisar("Sai um rastejante de baixo do carro abandonado mais perto (até 25 m).");
            }
            y += 30 * u;
            // a etapa 6: quantos nascem, de dia e à noite (o padrão: ×2 e ×3), e povoar tudo de novo para ver
            for (int i = 0; i < 2; i++)
            {
                bool dia = i == 0;
                float atual = zm == null ? (dia ? 2f : 3f) : dia ? zm.DensidadeDeDia.Value : zm.DensidadeDeNoite.Value;
                if (GUI.Button(new Rect(x, y, w, 26 * u), $"Zumbis {(dia ? "de dia" : "à noite")}: ×{atual:0.#}", _botao))
                {
                    float proxima = ProximaDensidade(atual);
                    DensidadeRpc(dia, proxima);
                    Avisar($"{(dia ? "De dia" : "À noite")}, ×{proxima:0.#} (o padrão é ×{(dia ? 2 : 3)}). Vale para o que se povoar daqui em diante; \"Repovoar em volta\" refaz o de perto.");
                }
                y += 30 * u;
            }
            if (GUI.Button(new Rect(x, y, w, 26 * u), "Repovoar em volta agora", _botao))
            {
                RepovoarRpc();
                Avisar("Os de pé (menos quem está atrás de alguém) somem, e tudo em volta se povoa de novo, a partir de 22 m.");
            }
            return area;
        }

        [Rpc(SendTo.Server)]
        void CriarZumbiNaFrenteRpc(bool dePedra, RpcParams parametros = default)
        {
            if (!Aceita(parametros) || ZombieManager.Instancia == null) return;
            Vector3 frente = transform.forward;
            frente.y = 0f;
            frente.Normalize();
            // olhando para o jogador
            ZombieManager.Instancia.CriarUm(transform.position + frente * 5f, Quaternion.LookRotation(-frente).eulerAngles.y, dePedra);
        }

        // o amanhecer: 5h com o relógio andando (o sol petrifica a partir do meio do amanhecer, ~6h30)
        [Rpc(SendTo.Server)]
        void AmanhecerRpc(RpcParams parametros = default)
        {
            if (!Aceita(parametros) || CicloDoDia.Instancia == null) return;
            CicloDoDia.Instancia.SempreDeDia(false);
            CicloDoDia.Instancia.SempreDeNoite(false);
            CicloDoDia.MudarHora(5f);
        }

        // a horda: nasce junta 14 m à frente e já vem atrás do jogador (o cerco os espalha em volta)
        [Rpc(SendTo.Server)]
        void HordaRpc(byte quantos, RpcParams parametros = default)
        {
            if (!Aceita(parametros) || ZombieManager.Instancia == null) return;
            Vector3 frente = transform.forward;
            frente.y = 0f;
            frente.Normalize();
            ZombieManager.Instancia.CriarHordaPerseguindo(GetComponent<Player.JogadorRede>(), transform.position + frente * 14f, quantos);
        }

        [Rpc(SendTo.Server)]
        void PovoarRpc(bool ligado, RpcParams parametros = default)
        {
            if (Aceita(parametros) && ZombieManager.Instancia != null) ZombieManager.Instancia.PovoarOMundo.Value = ligado;
        }

        [Rpc(SendTo.Server)]
        void RastejantesRpc(bool emTodos, RpcParams parametros = default)
        {
            if (Aceita(parametros) && ZombieManager.Instancia != null) ZombieManager.Instancia.RastejanteEmTodos.Value = emTodos;
        }

        // a roda de densidades do F1
        static readonly float[] Densidades = { 0.5f, 1f, 1.5f, 2f, 3f, 4f };

        static float ProximaDensidade(float atual)
        {
            foreach (float d in Densidades)
                if (d > atual + 0.01f) return d;
            return Densidades[0];
        }

        [Rpc(SendTo.Server)]
        void DensidadeRpc(bool dia, float densidade, RpcParams parametros = default)
        {
            var zm = ZombieManager.Instancia;
            if (!Aceita(parametros) || zm == null) return;
            densidade = Mathf.Clamp(densidade, 0.25f, 6f);
            if (dia) zm.DensidadeDeDia.Value = densidade;
            else zm.DensidadeDeNoite.Value = densidade;
        }

        [Rpc(SendTo.Server)]
        void RepovoarRpc(RpcParams parametros = default)
        {
            if (Aceita(parametros) && ZombieManager.Instancia != null) ZombieManager.Instancia.RepovoarEmVolta();
        }

        [Rpc(SendTo.Server)]
        void RastejanteAgoraRpc(RpcParams parametros = default)
        {
            if (Aceita(parametros) && ZombieManager.Instancia != null)
                ZombieManager.Instancia.RastejanteNoCarroMaisPerto(GetComponent<Player.JogadorRede>());
        }

        [Rpc(SendTo.Server)]
        void PedraRpc(bool petrificar, RpcParams parametros = default)
        {
            if (!Aceita(parametros) || ZombieManager.Instancia == null) return;
            if (petrificar) ZombieManager.Instancia.PetrificarPerto(transform.position, 30f);
            else ZombieManager.Instancia.SoltarTodos();
        }
    }
}
