using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// A aba "Habilidades" do F1 (06/11/2026, a árvore de habilidades; a árvore nova em 09/11/2026): o nível, o XP e os
    /// pontos, e os botões para testar (mais XP, um nível, 5 níveis, o nível 10, o nível 40, um ponto no primeiro nó que
    /// dá, tirar o último, esquecer a árvore, tudo do zero e um save da árvore velha, para ver a migração). O anfitrião faz
    /// (<see cref="InventarioJogador.ProgressoDeTeste"/>).
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        bool _abaDasHabilidades;
        int _paineisDeTeste;
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
            const int linhas = 12;
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
            if (GUI.Button(new Rect(x, y, meio, alto * u), "+5 níveis", _botao)) TesteDasHabilidadesRpc(9);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), $"Nível {Habilidades.NivelMaximo}", _botao)) TesteDasHabilidadesRpc(3);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Aprender um", _botao)) TesteDasHabilidadesRpc(6);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Tirar um", _botao)) TesteDasHabilidadesRpc(7);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Esquecer todas", _botao)) TesteDasHabilidadesRpc(5);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Tudo do zero", _botao)) TesteDasHabilidadesRpc(4);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Nível 10", _botao)) TesteDasHabilidadesRpc(2);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Save velho", _botao)) TesteDasHabilidadesRpc(8);
            y += passo * u;
            // os testes do lago e da oficina (06/11/2026)
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Ir ao caiaque", _botao)) IrAoCaiaque();
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Vara + 10 iscas", _botao)) TesteDasHabilidadesRpc(10);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Machado", _botao)) TesteDasHabilidadesRpc(11);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Pólvora + metal", _botao)) TesteDasHabilidadesRpc(12);
            y += passo * u;
            // as armas de sucata (09/11/2026): cada uma, 20 de cada munição e 20 de sucata (com a madeira, a fita e a pólvora
            // das receitas)
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Pistola sucata", _botao)) TesteDasHabilidadesRpc(13);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Rifle sucata", _botao)) TesteDasHabilidadesRpc(14);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Submet. sucata", _botao)) TesteDasHabilidadesRpc(15);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "20+20 munições", _botao)) TesteDasHabilidadesRpc(16);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "20 sucatas", _botao)) TesteDasHabilidadesRpc(17);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Materiais sucata", _botao)) TesteDasHabilidadesRpc(18);
            y += passo * u;
            // a sucata, 2ª leva (09/11/2026): um painel de metal na frente (um de teste, fora do save), os itens que se
            // desmontam e ir até o painel do mapa mais perto
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Painel na frente", _botao)) TesteDasHabilidadesRpc(19);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Itens desmontar", _botao)) TesteDasHabilidadesRpc(20);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Ir a um painel", _botao)) IrAoPainel();
            return area;
        }

        // no dono: para perto do painel de metal do mapa mais perto (ainda não desmontado), olhando para ele
        void IrAoPainel()
        {
            var mundo = ZombieCityCleaner.World.MundoDaPartida.Instancia;
            var paineis = mundo != null ? ZombieCityCleaner.Mundo.PaineisDeSucata.Do(mundo.Dados) : null;
            if (paineis == null || paineis.Todos.Count == 0)
            {
                Avisar("Sem painéis de metal neste mapa.");
                return;
            }
            var aqui = new Vector2(transform.position.x, transform.position.z);
            int melhor = -1;
            float menor = float.MaxValue;
            for (int i = 0; i < paineis.Todos.Count; i++)
            {
                if (ZombieCityCleaner.World.PaineisDoMundo.Desmontado(i)) continue;
                float d = (paineis.Todos[i].Posicao - aqui).sqrMagnitude;
                if (d < 9f || d >= menor) continue;   // (o que está aos pés não conta: vai para o próximo)
                menor = d;
                melhor = i;
            }
            if (melhor < 0)
            {
                Avisar("Todos os painéis de metal já foram desmontados.");
                return;
            }
            var p = paineis.Todos[melhor];
            var alvo = new Vector3(p.Posicao.x, ZombieCityCleaner.Mundo.PaineisDeSucata.AlturaDoChao(mundo.Dados, p), p.Posicao.y);
            var de = (new Vector3(transform.position.x, alvo.y, transform.position.z) - alvo);
            de = de.sqrMagnitude > 0.01f ? de.normalized : Vector3.back;
            var pos = alvo + de * 1.8f + Vector3.up * 0.3f;
            GetComponent<MovimentoPrimeiraPessoa>()?.Teleportar(pos, Quaternion.LookRotation(-de).eulerAngles.y);
            Avisar($"Perto do painel {melhor} ({ZombieCityCleaner.Mundo.PaineisDeSucata.NomeDoModelo(p.Modelo)}, a {Mathf.Sqrt(menor):0} m daqui): segure E nele.");
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
                // a munição: uma caixa com n balas
                void DarMunicao(Loot.Item item, int n)
                {
                    if (!inv.Receber(item, n)) Loot.ItensNoMundo.Instancia?.Criar(item, transform.position + transform.forward, transform.eulerAngles.y, n);
                }
                string aviso10;
                if (acao == 10) { Dar(Loot.Item.VaraDePesca, 1); Dar(Loot.Item.Isca, 10); aviso10 = "A vara de pesca e 10 iscas: mire na água funda e clique."; }
                else if (acao == 11) { Dar(Loot.Item.Machado, 1); aviso10 = "Um machado: 5 golpes no tronco derrubam a árvore."; }
                else if (acao == 12)
                {
                    Dar(Loot.Item.Polvora, 5); Dar(Loot.Item.PecasDeMetal, 6); Dar(Loot.Item.Ferramenta, 1);
                    aviso10 = "5 pólvoras, 6 peças de metal e a ferramenta: com o Armeiro, na mesa de oficina do bunker (a garagem).";
                }
                // as armas de sucata (09/11/2026)
                else if (acao <= 15)
                {
                    var arma = acao == 13 ? Loot.Item.PistolaSucata : acao == 14 ? Loot.Item.RifleSucata : Loot.Item.SubmetralhadoraSucata;
                    Dar(arma, 1);
                    aviso10 = $"{TelaDoInventario.Maiuscula(Loot.Itens.NomeComArtigo(arma))}, com o pente cheio.";
                }
                else if (acao == 16)
                {
                    DarMunicao(Loot.Item.MunicaoPrego, 20); DarMunicao(Loot.Item.MunicaoParafuso, 20);
                    aviso10 = "20 de munição de prego e 20 de munição de parafuso.";
                }
                else if (acao == 17) { Dar(Loot.Item.Sucata, 20); aviso10 = "20 pedaços de sucata."; }
                else if (acao == 19)
                {
                    // um painel de teste no chão, 1,3 m na frente, ao alcance da mão (o modelo, um de cada vez)
                    var frente = transform.forward;
                    frente.y = 0f;
                    frente = frente.sqrMagnitude > 0.01f ? frente.normalized : Vector3.forward;
                    var onde = transform.position + frente * 1.3f;
                    if (Physics.Raycast(onde + Vector3.up * 1.5f, Vector3.down, out var chao, 4f, ~0, QueryTriggerInteraction.Ignore)) onde.y = chao.point.y;
                    int modelo = _paineisDeTeste++ % ZombieCityCleaner.Mundo.PaineisDeSucata.Modelos;
                    ZombieCityCleaner.World.MundoDaPartida.Instancia?.PorPainelDeTeste(onde, transform.eulerAngles.y + 90f, modelo);
                    aviso10 = $"Um painel de metal na frente ({ZombieCityCleaner.Mundo.PaineisDeSucata.NomeDoModelo(modelo)}): segure E nele. Os de teste não vão no save.";
                }
                else if (acao == 20)
                {
                    // um de cada coisa que vira sucata (a pistola quebrada, com a ferramenta que ela pede)
                    Dar(Loot.Item.Ferramenta, 1); Dar(Loot.Item.PeDeCabra, 1); Dar(Loot.Item.GarrafaTermica, 1); Dar(Loot.Item.PecasDeMetal, 3);
                    Dar(Loot.Item.Radiador, 1); Dar(Loot.Item.Macaco, 1);
                    if (!inv.Receber(Loot.Item.Pistola, 0, 100)) Loot.ItensNoMundo.Instancia?.Criar(Loot.Item.Pistola, transform.position + transform.forward, transform.eulerAngles.y, 0, 100);
                    aviso10 = "Para desmontar (botão direito na mochila): a ferramenta, o pé de cabra, a garrafa térmica, 3 peças de metal, o radiador, o macaco e uma pistola quebrada.";
                }
                else
                {
                    Dar(Loot.Item.Sucata, 20); Dar(Loot.Item.Madeira, 3); Dar(Loot.Item.FitaAdesiva, 3); Dar(Loot.Item.Polvora, 3);
                    aviso10 = "20 sucatas, 3 madeiras, 3 fitas e 3 pólvoras: as receitas do Armeiro de sucata (a bancada e a mesa de oficina).";
                }
                AvisoDoTesteRpc(aviso10, RpcTarget.Single(parametros.Receive.SenderClientId, RpcTargetUse.Temp));
                return;
            }
            string aviso = inv.ProgressoDeTeste(acao);
            AvisoDoTesteRpc(aviso, RpcTarget.Single(parametros.Receive.SenderClientId, RpcTargetUse.Temp));
        }
    }
}
