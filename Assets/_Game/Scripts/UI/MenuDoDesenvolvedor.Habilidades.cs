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
            const int linhas = 19;   // (16: a proteção de sucata, 11/11/2026; 18: o fogo; 19: as armadilhas)
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
            // as melhorias das armas de sucata (10/11/2026, a etapa 2): os nós do Armeiro de sucata no máximo, as peças
            // prontas (uma de cada) e os materiais das receitas delas; o painel abre na aba Criar (perto de uma bancada)
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Melhorias: nós", _botao)) TesteDasHabilidadesRpc(21);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Melhorias: peças", _botao)) TesteDasHabilidadesRpc(22);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Mat. melhorias", _botao)) TesteDasHabilidadesRpc(23);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Painel melhorias", _botao)) GetComponent<TelaDoInventario>()?.AbrirNasMelhorias();
            // a proteção de sucata (11/11/2026, a etapa 3): os nós no máximo, as 4 peças, os materiais delas (com o pneu e 2 kits
            // de reparo) e o desgaste das vestidas a 95% (para testar o conserto)
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Proteção: nós", _botao)) TesteDasHabilidadesRpc(24);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Proteção: peças", _botao)) TesteDasHabilidadesRpc(25);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Mat. proteção", _botao)) TesteDasHabilidadesRpc(26);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Gastar proteção", _botao)) TesteDasHabilidadesRpc(27);
            // o fogo (11/11/2026, a etapa 4): os nós no máximo, as peças (o lampião, 3 molotovs e o kit), os materiais das receitas
            // (com o álcool e um galão) e uma área de fogo do molotov a 6 m na frente
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Fogo: nós", _botao)) TesteDasHabilidadesRpc(28);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Fogo: peças", _botao)) TesteDasHabilidadesRpc(29);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Mat. fogo", _botao)) TesteDasHabilidadesRpc(30);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Molotov na frente", _botao)) TesteDasHabilidadesRpc(31);
            // as armadilhas (11/11/2026, a etapa 5a; a Barricada de tábuas e a Fortaleza, a 5b): os nós no máximo, as peças (2
            // alarmes, 2 tapetes, as estacas, 2 cercas, 2 tábuas para barricada e a barricada solta) e os materiais das receitas
            // (com a ferramenta, 4 álcoois e o do conserto); "Barricar a porta": a porta de casa mais perto, do seu lado
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Armadilhas: nós", _botao)) TesteDasHabilidadesRpc(32);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Armadilhas: peças", _botao)) TesteDasHabilidadesRpc(33);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Mat. armadilhas", _botao)) TesteDasHabilidadesRpc(34);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Barricar a porta", _botao)) TesteDasHabilidadesRpc(35);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Barricar a janela", _botao)) TesteDasHabilidadesRpc(36);
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
                else if (acao == 21)
                {
                    // os nós do Armeiro de sucata no máximo (sem conferir os pedidos: é o teste), e o XP do nível máximo
                    var p = inv.Progresso.Value;
                    p.xp = Mathf.Max(p.xp, Habilidades.XpAte(Habilidades.NivelMaximo));
                    for (var h = Habilidade.PistolaDeSucata; h <= Habilidade.MestreArmeiro; h++) p.PorPontos(h, Habilidades.Maximo(h));
                    inv.Progresso.Value = p;
                    aviso10 = "O Armeiro de sucata inteiro: as 3 armas, o cano reforçado III, as miras, o carregador, a luneta, a mola, os silenciadores e o Mestre armeiro.";
                }
                else if (acao == 22)
                {
                    for (int i = 0; i < Combat.Melhorias.Quantas; i++) Dar(Combat.Melhorias.ItemDa((Combat.PecaDaMelhoria)i), 1);
                    aviso10 = "Uma peça de cada melhoria (os 2 silenciadores, as 3 miras, o carregador e os 3 canos): Criar > Melhorias, na bancada.";
                }
                else if (acao == 23)
                {
                    Dar(Loot.Item.Sucata, 20); Dar(Loot.Item.Sucata, 20); Dar(Loot.Item.FitaAdesiva, 5); Dar(Loot.Item.FitaAdesiva, 5);
                    Dar(Loot.Item.RefrigeranteGarrafa, 2); Dar(Loot.Item.Pilhas, 2); Dar(Loot.Item.PecasDeMetal, 4);
                    aviso10 = "40 sucatas, 10 fitas, 2 garrafas de refrigerante, 2 pares de pilhas e 4 peças de metal: as receitas das melhorias.";
                }
                else if (acao == 24)
                {
                    var p = inv.Progresso.Value;
                    p.xp = Mathf.Max(p.xp, Habilidades.XpAte(Habilidades.NivelMaximo));
                    for (var h = Habilidade.CapaceteDePanela; h <= Habilidade.Couraca; h++) p.PorPontos(h, Habilidades.Maximo(h));
                    inv.Progresso.Value = p;
                    aviso10 = "A Proteção de sucata inteira: o capacete, as braçadeiras e as caneleiras, o colete, o Remendo de chapa 2 e a Couraça.";
                }
                else if (acao == 25)
                {
                    for (int k = 0; k < Protecao.Quantos; k++) Dar(Protecao.ItemDo((LugarDaProtecao)k), 1);
                    aviso10 = "As 4 peças da proteção de sucata: vista no grupo PROTEÇÃO (TAB, embaixo do retrato).";
                }
                else if (acao == 26)
                {
                    Dar(Loot.Item.Sucata, 20); Dar(Loot.Item.FitaAdesiva, 5); Dar(Loot.Item.FitaAdesiva, 5); Dar(Loot.Item.Corda, 1);
                    Dar(Loot.Item.Pneu, 1); Dar(Loot.Item.KitDeReparo, 1); Dar(Loot.Item.KitDeReparo, 1);
                    aviso10 = "20 sucatas, 10 fitas, 1 corda, 1 pneu e 2 kits de reparo: as receitas da proteção (o colete, na mesa de oficina).";
                }
                else if (acao == 27)
                {
                    var pv = inv.Protecao.Value;
                    for (int k = 0; k < Protecao.Quantos; k++)
                        if (pv.Peca((LugarDaProtecao)k) != Loot.Item.Nada) pv.Por((LugarDaProtecao)k, pv.Peca((LugarDaProtecao)k), 95);
                    inv.Protecao.Value = pv;
                    aviso10 = "As peças vestidas ficaram com 5%: conserte pelo botão direito (com o kit de reparo).";
                }
                else if (acao == 28)
                {
                    var p = inv.Progresso.Value;
                    p.xp = Mathf.Max(p.xp, Habilidades.XpAte(Habilidades.NivelMaximo));
                    for (var h = Habilidade.TochaDuradoura; h <= Habilidade.MuralhaDeFogo; h++) p.PorPontos(h, Habilidades.Maximo(h));
                    p.PorPontos(Habilidade.NervosDeAco, Mathf.Max(1, p.PontosEm(Habilidade.NervosDeAco)));   // (o Calor do fogo pede)
                    inv.Progresso.Value = p;
                    aviso10 = "O Fogo inteiro: a Tocha duradoura 2, o lampião, o molotov, a fogueira portátil, o Fogo que pega 2, o Calor do fogo 2 e a Muralha de fogo.";
                }
                else if (acao == 29)
                {
                    Dar(Loot.Item.Lampiao, 1); Dar(Loot.Item.Molotov, 1); Dar(Loot.Item.Molotov, 1); Dar(Loot.Item.Molotov, 1); Dar(Loot.Item.KitFogueira, 1);
                    aviso10 = "O lampião, 3 coquetéis molotov e o kit da fogueira portátil (ponha no atalho e aperte o clique).";
                }
                else if (acao == 30)
                {
                    Dar(Loot.Item.Sucata, 6); Dar(Loot.Item.RefrigeranteGarrafa, 2); Dar(Loot.Item.Desinfetante, 2); Dar(Loot.Item.Pano, 2);
                    Dar(Loot.Item.Madeira, 3); Dar(Loot.Item.Corda, 1);
                    if (!inv.Receber(Loot.Item.Galao)) Loot.ItensNoMundo.Instancia?.Criar(Loot.Item.Galao, transform.position + transform.forward, transform.eulerAngles.y);
                    aviso10 = "6 sucatas, 2 garrafas de refrigerante, 2 álcoois, 2 panos, 3 madeiras, 1 corda e um galão cheio: as receitas do fogo.";
                }
                else if (acao == 32)
                {
                    var p = inv.Progresso.Value;
                    p.xp = Mathf.Max(p.xp, Habilidades.XpAte(Habilidades.NivelMaximo));
                    foreach (var h in new[] { Habilidade.AlarmeDeLatas, Habilidade.BarricadaDeTabuas, Habilidade.TapeteDePregos, Habilidade.Estacas, Habilidade.Armadilheiro, Habilidade.CercaDeFogo, Habilidade.Fortaleza })
                        p.PorPontos(h, Habilidades.Maximo(h));
                    // (as Estacas pedem o Artesão, que pede o Faz-tudo e o Sucateiro)
                    foreach (var h in new[] { Habilidade.Sucateiro, Habilidade.FazTudo, Habilidade.Artesao }) p.PorPontos(h, Mathf.Max(1, p.PontosEm(h)));
                    inv.Progresso.Value = p;
                    aviso10 = "As Armadilhas inteiras: o alarme de latas, a Barricada de tábuas 2, o tapete de pregos, as estacas, o Armadilheiro 2, a cerca de fogo e a Fortaleza.";
                }
                else if (acao == 33)
                {
                    Dar(Loot.Item.AlarmeLatas, 1); Dar(Loot.Item.AlarmeLatas, 1); Dar(Loot.Item.TapetePregos, 1); Dar(Loot.Item.TapetePregos, 1);
                    Dar(Loot.Item.Estacas, 1); Dar(Loot.Item.CercaFogo, 1); Dar(Loot.Item.CercaFogo, 1);
                    Dar(Loot.Item.TabuasBarricada, 1); Dar(Loot.Item.TabuasBarricada, 1); Dar(Loot.Item.Barricada, 1);
                    aviso10 = "2 alarmes de latas, 2 tapetes de pregos, as estacas, 2 cercas de fogo, 2 tábuas para barricada e a barricada solta (botão direito: Montar no chão; as tábuas: mire uma porta ou uma janela de casa e segure E).";
                }
                else if (acao == 34)
                {
                    Dar(Loot.Item.Sucata, 2); Dar(Loot.Item.Corda, 2); Dar(Loot.Item.Madeira, 10); Dar(Loot.Item.Pregos, 20); Dar(Loot.Item.Ferramenta, 1);
                    Dar(Loot.Item.Desinfetante, 2); Dar(Loot.Item.Pano, 1);
                    aviso10 = "2 sucatas, 2 cordas, 10 madeiras, 20 pregos, a ferramenta, 2 álcoois e 1 pano: as 5 receitas das armadilhas (na bancada) e um conserto da barricada de tábuas.";
                }
                else if (acao == 35 || acao == 36)
                {
                    // a barricada de tábuas na porta (ou na janela) de casa mais perto (11/11/2026, a etapa 5b), do lado do jogador,
                    // como a dele (o nível, o Armadilheiro e a Fortaleza), sem gastar as tábuas
                    var portas = ZombieCityCleaner.World.PortasDoMundo.Instancia;
                    bool janela = acao == 36;
                    ulong chave = portas != null ? portas.MaisPerto(transform.position + Vector3.up, 3.4f, janela, out _) : 0;
                    var rede = GetComponent<JogadorRede>();
                    aviso10 = chave == 0 ? (janela ? "Nenhuma janela de casa a menos de 3,4 m." : "Nenhuma porta de casa a menos de 3,4 m.")
                        : portas.Pregar(inv, rede != null ? rede.OwnerClientId : 0, chave, true) ? (janela ? "A janela mais perto barricada do seu lado." : "A porta mais perto barricada do seu lado.")
                        : "Não deu: já está barricada (ou longe).";
                }
                else if (acao == 31)
                {
                    var rede = GetComponent<JogadorRede>();
                    Loot.ConstrucoesNoMundo.Instancia?.FogoDeTeste(transform.position + transform.forward * 6f, rede);
                    aviso10 = "Uma área de fogo do molotov a 6 m na frente (como a sua: com o Fogo que pega e a Muralha, se tiver).";
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
