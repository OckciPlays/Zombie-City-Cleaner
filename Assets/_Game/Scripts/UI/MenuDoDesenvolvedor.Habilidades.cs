using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// A aba "Habilidades" do F1 (06/11/2026, a árvore de habilidades): o nível, o XP e os pontos, e os botões para testar
    /// (mais XP, um nível, o nível 10, o nível 30, esquecer as habilidades, tudo do zero). O anfitrião faz
    /// (<see cref="InventarioJogador.ProgressoDeTeste"/>).
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        bool _abaDasHabilidades;
        static readonly GUIContent _conteudoDasHabilidades = new GUIContent();

        Rect PainelDasHabilidades(float u, Rect aoLado)
        {
            float x0 = aoLado.xMax + 8 * u, largura = 250 * u;
            float x = x0 + 12 * u, w = largura - 24 * u;
            float meio = (w - 6 * u) / 2f, x2 = x + meio + 6 * u;
            const float passo = 28f, alto = 24f;
            var inv = GetComponent<InventarioJogador>();
            _conteudoDasHabilidades.text = inv != null && inv.IsSpawned
                ? inv.ResumoDoProgresso() + "\nK abre a aba HABILIDADES da mochila." : "sem o jogador";
            float alturaDoTexto = _texto.CalcHeight(_conteudoDasHabilidades, w);
            const int linhas = 5;
            float altura = 8 * u + 28 * u + alturaDoTexto + 8 * u + linhas * passo * u + 4 * u;
            var area = new Rect(x0, aoLado.y, largura, altura);

            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.98f, 0.82f, 0.36f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "HABILIDADES (TESTE)", _titulo);
            y += 28 * u;
            GUI.Label(new Rect(x, y, w, alturaDoTexto), _conteudoDasHabilidades, _texto);
            y += alturaDoTexto + 8 * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "+50 XP", _botao)) TesteDasHabilidadesRpc(0);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "+1 nível", _botao)) TesteDasHabilidadesRpc(1);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Nível 10", _botao)) TesteDasHabilidadesRpc(2);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Nível 30", _botao)) TesteDasHabilidadesRpc(3);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Esquecer todas", _botao)) TesteDasHabilidadesRpc(5);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Tudo do zero", _botao)) TesteDasHabilidadesRpc(4);
            y += passo * u;
            // os testes do lago e da oficina (06/11/2026)
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Ir ao caiaque", _botao)) IrAoCaiaque();
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Vara + 10 iscas", _botao)) TesteDasHabilidadesRpc(10);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Machado", _botao)) TesteDasHabilidadesRpc(11);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Pólvora + metal", _botao)) TesteDasHabilidadesRpc(12);
            return area;
        }

        // no dono: para a beira, ao lado do caiaque (o primeiro do mundo)
        void IrAoCaiaque()
        {
            var k = Vehicles.Caiaque.Todos.Count > 0 ? Vehicles.Caiaque.Todos[0] : null;
            if (k == null)
            {
                Avisar("Sem caiaque no mundo (o prefab: Zombie City Cleaner > Veículos > Montar o caiaque...).");
                return;
            }
            var mov = GetComponent<MovimentoPrimeiraPessoa>();
            var lado = k.transform.position + k.transform.right * 1.6f;
            lado.y = Agua.Nivel(lado, out float nivel) ? nivel - 1.3f : lado.y;
            mov?.Teleportar(lado, k.transform.eulerAngles.y);
            Avisar("Ao lado do caiaque: E entra (atrás rema, na frente pesca).");
        }

        [Rpc(SendTo.Server)]
        void TesteDasHabilidadesRpc(byte acao, RpcParams parametros = default)
        {
            if (!Aceita(parametros)) return;
            var inv = GetComponent<InventarioJogador>();
            if (inv == null) return;
            if (acao >= 10)
            {
                // os itens do teste: sem lugar na mochila, ficam aos pés
                void Dar(Loot.Item item, int n)
                {
                    bool cabe = Loot.Itens.EmPilha(item) ? inv.ReceberEmPilha(item, n) < n : inv.Receber(item);
                    if (!cabe) Loot.ItensNoMundo.Instancia?.Criar(item, transform.position + transform.forward, transform.eulerAngles.y, n);
                }
                if (acao == 10) { Dar(Loot.Item.VaraDePesca, 1); Dar(Loot.Item.Isca, 10); }
                else if (acao == 11) Dar(Loot.Item.Machado, 1);
                else { Dar(Loot.Item.Polvora, 5); Dar(Loot.Item.PecasDeMetal, 6); Dar(Loot.Item.Ferramenta, 1); }
                AvisoDoTesteRpc(acao == 10 ? "A vara de pesca e 10 iscas: mire na água funda e clique." : acao == 11 ? "Um machado: 5 golpes no tronco derrubam a árvore."
                    : "5 pólvoras, 6 peças de metal e a ferramenta: com o Armeiro, na mesa de oficina do bunker (a garagem).",
                    RpcTarget.Single(parametros.Receive.SenderClientId, RpcTargetUse.Temp));
                return;
            }
            inv.ProgressoDeTeste(acao);
            string aviso = acao switch
            {
                0 => "+50 XP.", 1 => "Mais um nível.", 2 => "Nível 10 (10 pontos).", 3 => "Nível 30 (30 pontos).",
                4 => "Tudo do zero: nível 1, nenhuma habilidade.", _ => "As habilidades foram esquecidas (os pontos voltaram).",
            };
            AvisoDoTesteRpc(aviso, RpcTarget.Single(parametros.Receive.SenderClientId, RpcTargetUse.Temp));
        }
    }
}
