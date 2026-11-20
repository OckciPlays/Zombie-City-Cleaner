using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Fauna;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O painel de teste da fauna (12/11/2026), na sexta aba do canto ("Fauna"): quantos bichos há de cada espécie, a
    /// população da região e o mais perto; pôr um grupo de cada espécie a ~18 m na frente (os modelos de caixas, e o veado do
    /// Blender), a fila de todos parados (para ver os modelos), matar ou ferir o mais perto, tirar todos, a faca na mochila
    /// (para carnear), a carne crua e os nomes e os estados em cima dos bichos. A etapa 2: a caça na frente (os lobos atrás
    /// dos veados, os coiotes atrás dos coelhos) e a matilha mais perto cercando quem pediu (vale de dia). A etapa 3: ir para
    /// a margem do lago, o V dos gansos agora, o urso pescando o salmão (e, desde 14/11, pegando um na hora) e os castores roendo depressa. Os pedidos vão para o anfitrião.
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        bool _abaDaFauna;
        static readonly GUIContent _conteudoDaFauna = new GUIContent();

        static readonly (Especie e, string rotulo)[] BotoesDaFauna =
        {
            (Especie.Veado, "Veados"), (Especie.Alce, "Alces"), (Especie.Lobo, "Lobos"), (Especie.Coiote, "Coiotes"),
            (Especie.UrsoNegro, "Urso-negro"), (Especie.UrsoPardo, "Urso-pardo"), (Especie.Coelho, "Coelhos"), (Especie.Castor, "Castores"),
            (Especie.Pato, "Patos"), (Especie.Ganso, "Gansos"), (Especie.Aguia, "Águia"), (Especie.Urubu, "Urubus"),
            (Especie.Sapo, "Sapos"), (Especie.Cascavel, "Cascavel"), (Especie.Guaxinim, "Guaxinim"), (Especie.Gamba, "Gambá"),
            (Especie.Esquilo, "Esquilos"), (Especie.Peru, "Perus"), (Especie.Salmao, "Salmões"),
        };

        Rect PainelDaFauna(float u, Rect aoLado)
        {
            float x0 = aoLado.xMax + 8 * u, largura = 250 * u;
            float x = x0 + 12 * u, w = largura - 24 * u;
            float meio = (w - 6 * u) / 2f, x2 = x + meio + 6 * u;
            const float passo = 26f, alto = 22f;
            var f = FaunaDoMundo.Instancia;
            _conteudoDaFauna.text = (f != null ? f.Resumo(transform.position) : "A fauna liga com o mundo gerado pronto.")
                + "\n" + ZombieCityCleaner.Combat.SomDasChamas.Resumo() + "\n" + ZombieCityCleaner.World.SomDaAgua.Resumo()
                + $"\neco do último tiro: {ZombieCityCleaner.Combat.EcoDoTiro.UltimoLugar} (volume {ZombieCityCleaner.Combat.EcoDoTiro.Volume:0.##})";
            float alturaDoTexto = _texto.CalcHeight(_conteudoDaFauna, w);
            int linhas = (BotoesDaFauna.Length + 1) / 2 + 31;
            float altura = 8 * u + 28 * u + alturaDoTexto + 8 * u + 18 * u + linhas * passo * u + 6 * u;
            var area = new Rect(x0, aoLado.y, largura, altura);

            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.55f, 0.78f, 0.42f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "FAUNA (TESTE)", _titulo);
            y += 28 * u;
            GUI.Label(new Rect(x, y, w, alturaDoTexto), _conteudoDaFauna, _texto);
            y += alturaDoTexto + 8 * u;
            GUI.Label(new Rect(x, y, w, 18 * u), "Pôr um grupo a 18 m na frente:", _texto);
            y += 18 * u;
            for (int i = 0; i < BotoesDaFauna.Length; i++)
            {
                var (e, rotulo) = BotoesDaFauna[i];
                if (GUI.Button(new Rect(i % 2 == 0 ? x : x2, y, meio, alto * u), rotulo, _botao)) TesteDaFaunaRpc(0, (byte)e);
                if (i % 2 == 1 || i == BotoesDaFauna.Length - 1) y += passo * u;
            }
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Matar o mais perto", _botao)) TesteDaFaunaRpc(1, 0);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Ferir o mais perto", _botao)) TesteDaFaunaRpc(2, 0);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Faca na mochila", _botao)) TesteDaFaunaRpc(4, 0);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "3 carnes cruas", _botao)) TesteDaFaunaRpc(5, 0);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Tirar todos", _botao)) TesteDaFaunaRpc(3, 0);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), FaunaDoMundo.VerSentidos ? "Esconder nomes" : "Ver os nomes", _botao))
                FaunaDoMundo.VerSentidos = !FaunaDoMundo.VerSentidos;
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Fila de todos", _botao)) TesteDaFaunaRpc(7, 0);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Região a 100%", _botao)) TesteDaFaunaRpc(6, 0);
            // a etapa 2: a caça na frente e a matilha cercando (de dia também)
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Lobos caçando", _botao)) TesteDaFaunaRpc(8, (byte)Especie.Lobo);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Coiotes caçando", _botao)) TesteDaFaunaRpc(8, (byte)Especie.Coiote);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "A matilha me cerca", _botao)) TesteDaFaunaRpc(9, 0);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "O lobo mais perto uiva", _botao)) TesteDaFaunaRpc(30, 0);   // (20/11/2026)
            // a etapa 3: a água (o lago, o V dos gansos, o urso pescando, os castores roendo depressa)
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Ir para o lago", _botao))
            {
                if (f != null && f.LugarNoLago(GetComponent<JogadorRede>(), out var margem, out float giro))
                    GetComponent<MovimentoPrimeiraPessoa>()?.Teleportar(margem + Vector3.up * 0.1f, giro);
                else Avisar("Sem lago neste mundo.");
            }
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Gansos em V", _botao)) TesteDaFaunaRpc(10, 0);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Urso pescando", _botao)) TesteDaFaunaRpc(11, 0);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), FaunaDoMundo.CastorRapido ? "Castor normal" : "Castor rói rápido", _botao)) TesteDaFaunaRpc(12, 0);
            // 14/11: o urso pega o salmão na hora (sozinho, come deitado; com filhotes, dá o peixe a eles)
            y += passo * u;
            if (GUI.Button(new Rect(x, y, w, alto * u), "O urso mais perto pega um salmão", _botao)) TesteDaFaunaRpc(17, 0);
            // a etapa 4: os urubus no resto mais perto (o corpo de um zumbi, a carcaça)
            y += passo * u;
            if (GUI.Button(new Rect(x, y, w, alto * u), "Urubus no corpo mais perto", _botao)) TesteDaFaunaRpc(13, 0);
            // 13/11: os castores no riacho (a represa) e o guaxinim no lixo da cidade
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Ir para o riacho", _botao))
            {
                if (FaunaDoMundo.LugarNoRiacho(GetComponent<JogadorRede>(), out var margem, out float giro))
                    GetComponent<MovimentoPrimeiraPessoa>()?.Teleportar(margem + Vector3.up * 0.1f, giro);
                else Avisar("Sem riacho neste mapa (só no Lakewood).");
            }
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Castores no riacho", _botao)) TesteDaFaunaRpc(14, 0);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Ir para o lixo", _botao))
            {
                if (FaunaDoMundo.LugarNoLixo(GetComponent<JogadorRede>(), out var onde, out float giro))
                    GetComponent<MovimentoPrimeiraPessoa>()?.Teleportar(onde + Vector3.up * 0.1f, giro);
                else Avisar("Sem lixo à vista neste mapa (só na Woodcreek, no Lakewood).");
            }
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Guaxinim no lixo", _botao)) TesteDaFaunaRpc(15, 0);
            // a etapa 5: os lugares dos bichos (a caverna, a toca dos coiotes, o brejo)
            y += passo * u;
            float terco = (w - 12 * u) / 3f;
            if (GUI.Button(new Rect(x, y, terco, alto * u), "Caverna", _botao)) IrParaOLugar(TipoDeLugarDoBicho.Caverna);
            if (GUI.Button(new Rect(x + terco + 6 * u, y, terco, alto * u), "Toca", _botao)) IrParaOLugar(TipoDeLugarDoBicho.Covil);
            if (GUI.Button(new Rect(x + 2 * (terco + 6 * u), y, terco, alto * u), "Brejo", _botao)) IrParaOLugar(TipoDeLugarDoBicho.Brejo);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, w, alto * u), "Os bichos do lugar mais perto", _botao)) TesteDaFaunaRpc(16, 0);
            // a fogueira (18/11/2026): a fogueira acesa na frente e o que se usa nela (a lenha, o espeto, a panela e a comida)
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Fogueira acesa", _botao)) TesteDaFaunaRpc(18, 0);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Itens da fogueira", _botao)) TesteDaFaunaRpc(19, 0);
            // o salmão preso na lança (18/11/2026): a lança na mochila, e o peixe direto na ponta dela (para ver sem o rio)
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Lança na mochila", _botao)) TesteDaFaunaRpc(20, 0);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Salmão na lança", _botao)) TesteDaFaunaRpc(21, 0);
            // os instrumentos (18/11/2026, a etapa 4 da fogueira: os quatro na mochila, ou aos pés)
            y += passo * u;
            if (GUI.Button(new Rect(x, y, w, alto * u), "Os 4 instrumentos na mochila", _botao)) TesteDaFaunaRpc(22, 0);
            // os sons novos (19/11/2026: o Nature Sound FX, e depois os bichos e o ambiente): ouvir cada um aqui (na cabeça,
            // sem 3D), pular de grupo em grupo e qual rio toca
            var sons = SonsCarregados();
            int qual = sons.Count > 0 ? Mathf.Clamp(_somParaOuvir, 0, sons.Count - 1) : 0;
            string nomeDoSom = sons.Count > 0 ? $"{sons[qual].clip.name} ({qual + 1}/{sons.Count})" : "nenhum";
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), (OuvindoNatureza ? "Parar: " : "Ouvir: ") + nomeDoSom, _botao)) OuvirNatureza();
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Próximo som", _botao)) ProximoSomDaNatureza(1);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), sons.Count > 0 ? $"Grupo: {sons[qual].grupo} (trocar)" : "Grupo: -", _botao)) ProximoGrupo();
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Som anterior", _botao)) ProximoSomDaNatureza(-1);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, w, alto * u), $"O rio no jogo: o {SomDaAgua.QualRio + 1} (trocar)", _botao)) SomDaAgua.QualRio = 1 - SomDaAgua.QualRio;
            // o golpe do Mixamo (19/11/2026, um teste: desligado, o golpe de sempre; só nesta máquina): ligar, a amplitude
            // da arma branca na mão (40, 60, 80 ou 100% do caminho do pulso dele: o machado em 80%, a tocha em 60%) e as armas
            // que usam (as cinco brancas: o machado, a tocha, a faca, a lança e o bastão)
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), GolpeDoMixamo.Ligado ? "Golpe Mixamo: LIGADO" : "Golpe Mixamo: desligado", _botao))
                GolpeDoMixamo.Ligado = !GolpeDoMixamo.Ligado;
            var naMaoAgora = GetComponent<InventarioJogador>() != null ? GetComponent<InventarioJogador>().NaMao : Item.Nada;
            var daAmplitude = Itens.EhBranca(naMaoAgora) ? naMaoAgora : Item.Machado;
            string nomeDaBranca = Itens.Nome(daAmplitude);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), $"{char.ToUpper(nomeDaBranca[0])}{nomeDaBranca.Substring(1)} {Mathf.RoundToInt(GolpeDoMixamo.AmplitudeDo(daAmplitude) * 100f)}%", _botao))
                GolpeDoMixamo.GirarAmplitude(daAmplitude);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, w, alto * u), "As 5 armas brancas na mochila", _botao)) TesteDaFaunaRpc(23, 0);
            // a arma perto do rosto na primeira pessoa (19/11/2026, um teste: ArmaPerto.cs; só nesta máquina): ligar e os
            // ajustes ao vivo (o quanto ela chega perto, desce e vai para o lado; a mão esquerda por baixo: atravessada, o
            // antebraço vindo de baixo, a palma e os dedos; e até onde o pulso direito dobra)
            y += passo * u;
            if (GUI.Button(new Rect(x, y, w, alto * u), ArmaPerto.Ligado ? "Arma perto (1ª pessoa): LIGADA" : "Arma perto (1ª pessoa): desligada", _botao))
                ArmaPerto.Ligado = !ArmaPerto.Ligado;
            ArmaPerto.Puxar = Ajuste(x, ref y, w, $"Perto: {Mathf.RoundToInt(ArmaPerto.Puxar * 100f)}%", ArmaPerto.Puxar, 0f, 0.6f, u, passo);
            ArmaPerto.Baixar = Ajuste(x, ref y, w, $"Mais baixa: {ArmaPerto.Baixar * 100f:F0} cm", ArmaPerto.Baixar, -0.1f, 0.15f, u, passo);
            ArmaPerto.Lado = Ajuste(x, ref y, w, $"Para a direita: {ArmaPerto.Lado * 100f:F0} cm", ArmaPerto.Lado, -0.1f, 0.1f, u, passo);
            ArmaPerto.MaoAtravessada = Ajuste(x, ref y, w, $"Mão esquerda atravessada: {ArmaPerto.MaoAtravessada:F0}°", ArmaPerto.MaoAtravessada, 0f, 90f, u, passo);
            ArmaPerto.PulsoDeBaixo = Ajuste(x, ref y, w, $"Antebraço vindo de baixo: {ArmaPerto.PulsoDeBaixo:F0}°", ArmaPerto.PulsoDeBaixo, 0f, 70f, u, passo);
            ArmaPerto.PalmaDeLado = Ajuste(x, ref y, w, $"Palma virada para a esquerda: {ArmaPerto.PalmaDeLado:F0}°", ArmaPerto.PalmaDeLado, 0f, 60f, u, passo);
            ArmaPerto.FolgaDaPalma = Ajuste(x, ref y, w, $"Palma embaixo da arma: {ArmaPerto.FolgaDaPalma * 100f:F1} cm", ArmaPerto.FolgaDaPalma, 0f, 0.05f, u, passo);
            ArmaPerto.DedosFechando = Ajuste(x, ref y, w, $"Dedos da esquerda fechando: {ArmaPerto.DedosFechando:F0}°", ArmaPerto.DedosFechando, 0f, 45f, u, passo);
            ArmaPerto.PulsoDireito = Ajuste(x, ref y, w, $"Pulso direito dobra até: {ArmaPerto.PulsoDireito:F0}°", ArmaPerto.PulsoDireito, 0f, 60f, u, passo);
            return area;
        }

        // os sons novos no F1 (19/11/2026): a lista para ouvir, em grupos (o fogo, os trovões e a água do Nature Sound FX; o
        // ambiente e os bichos dos pacotes de bichos; e, só no Editor, os candidatos que ficaram nas pastas dos pacotes)
        static readonly (string grupo, string caminho)[] SonsParaOuvir =
        {
            ("Fogo", "Fogo/fogueira"), ("Fogo", "Fogo/fogo_medio"), ("Fogo", "Fogo/fogo_grande_1"), ("Fogo", "Fogo/fogo_grande_2"),
            ("Labaredas", "Fogo/labareda_1"), ("Labaredas", "Fogo/labareda_2"), ("Labaredas", "Fogo/labareda_3"), ("Labaredas", "Fogo/labareda_4"),
            ("Labaredas", "Fogo/labareda_5"), ("Labaredas", "Fogo/labareda_6"), ("Labaredas", "Fogo/labareda_grande_1"), ("Labaredas", "Fogo/labareda_grande_2"),
            ("Trovões", "Clima/trovao_perto_4"), ("Trovões", "Clima/trovao_perto_5"), ("Trovões", "Clima/trovao_perto_6"), ("Trovões", "Clima/trovao_medio_4"),
            ("Trovões", "Clima/trovao_medio_5"), ("Trovões", "Clima/trovao_medio_6"), ("Trovões", "Clima/trovao_longe_5"), ("Trovões", "Clima/trovao_longe_6"),
            ("Água", "Agua/Agua_Lago"), ("Água", "Agua/Agua_Rio_1"), ("Água", "Agua/Agua_Rio_2"),
            ("Pássaros (ambiente)", "Ambiente/Passaros/*"), ("Coruja (ambiente)", "Ambiente/Coruja/*"), ("Lobos ao longe (ambiente)", "Ambiente/Lobos/*"),
            ("Galhos (ambiente)", "Ambiente/Galhos/*"), ("Grilos (ambiente, à noite)", "Ambiente/Grilos/*"),   // (20/11/2026: os gravados)
            // (20/11/2026) os do sfxjar que o Pedro mandou e os passos na água
            ("Corvos (ambiente)", "Ambiente/Corvos/*"),
            ("Chuva", "Clima/chuva_fina"), ("Chuva", "Clima/chuva"), ("Chuva", "Clima/chuva_forte"), ("Chuva", "Clima/chuva_telhado"),
            ("Chuva", "Clima/chuva_carro"), ("Poças", "Clima/poca_1"), ("Poças", "Clima/poca_2"), ("Poças", "Clima/poca_3"),
            ("Passos na água", "Passos/Agua/*"), ("Respingo (bichos)", "Bichos/Respingo/*"),
            ("Portas, janelas e móveis", "Portas/*"), ("Comer, beber e a barriga", "Comida/*"), ("Isqueiro", "Fumo/*"),
            ("Arma sem bala e emperrada", "Armas/Manuseio/arma_clique"), ("Arma sem bala e emperrada", "Armas/Manuseio/arma_emperrada"),
            ("Carro: arranque e a outra buzina", "Carro/arranque"), ("Carro: arranque e a outra buzina", "Carro/outra_buzina"),
            ("Molinete", "Pesca/*"),
            // (20/11/2026) os 39 do sfxjar que o Pedro aprovou
            ("Árvore: machado, estalos e tombo", "Arvore/*"), ("Dia 0: tosses, cortador, carro, mangueira", "DiaZero/*"),
            ("Corpo: coração, osso, baques, ofegante, zíper, sussurros, vômito", "Corpo/*"),
            ("Bunker: a TV e o rádio", "Bunker/*"), ("Cliques (lanterna e interruptor)", "Interface/*"),
            ("Armadilhas: martelo, latas, estacas", "Armadilhas/*"), ("Galão estourando", "Fogo/estouro_1"),
            ("Zumbido do raio", "Clima/zumbido"), ("Guaxinim", "Bichos/Guaxinim/*"), ("Peru", "Bichos/GluGlu/*"),
            ("Urubu", "Bichos/Urubu/*"), ("Dentes do urso", "Bichos/Dentes/*"), ("Água: entrar", "Agua/Agua_Entrar"),
            ("Carro: capô, buzina da perua, motor da caminhonete", "Carro/capo_fechar"),
            ("Carro: capô, buzina da perua, motor da caminhonete", "Carro/perua_buzina"),
            ("Carro: capô, buzina da perua, motor da caminhonete", "Carro/motor_c_1_lento"),
            ("Uivo do lobo", "Bichos/Uivo/*"), ("Rosnado do lobo", "Bichos/Rosnado/*"), ("Bufo do urso", "Bichos/BufoDoUrso/*"), ("Rugido do urso", "Bichos/Rugido/*"),
            ("Bufo do veado", "Bichos/Bufo/*"), ("Mugido do alce", "Bichos/Mugido/*"), ("Grito da águia", "Bichos/Grito/*"), ("Sapo", "Bichos/Coaxar/*"),
            ("Cascavel", "Bichos/Chocalho/*"),
            // os que o Pedro achou (19/11/2026, CC0): o coiote, o pato, o ganso, o filhote de urso, o urso comendo, o esquilo e
            // o castor (com a lontra)
            ("Coiote", "Bichos/Latido/*"), ("Pato", "Bichos/Grasnado/*"), ("Ganso", "Bichos/Ganso/*"), ("Filhote de urso", "Bichos/Filhote/*"),
            ("Urso comendo", "Bichos/Comendo/*"), ("Esquilo", "Bichos/Chiado/*"), ("Castor (a lontra)", "Bichos/Estalo/*"),
            // a repaginada (19/11/2026): os tiros, os ecos, o manuseio, os passos, os golpes e as vozes
            ("Pistola", "Armas/Pistola/*"), ("Submetralhadora", "Armas/Submetralhadora/*"), ("Rifle", "Armas/Rifle/*"),
            ("Espingarda", "Armas/Espingarda/*"), ("Sniper", "Armas/Sniper/*"), ("Manuseio das armas", "Armas/Manuseio/*"),
            ("Cápsulas", "Armas/Capsulas/*"), ("Passos no concreto", "Passos/Concreto/*"), ("Passos na madeira", "Passos/Madeira/*"),
            ("Passos na grama", "Passos/Grama/*"), ("Soco: o vento", "Golpes/SocoVento/*"), ("Soco: a pancada", "Golpes/SocoCarne/*"),
            ("Soco: a parede", "Golpes/SocoParede/*"), ("Lâmina", "Golpes/Corte/*"), ("Bastão", "Golpes/Bastao/*"), ("Osso", "Golpes/Osso/*"),
            ("Voz: homem 1", "Vozes/Homem1/*"), ("Voz: homem 2", "Vozes/Homem2/*"), ("Voz: mulher 1", "Vozes/Mulher1/*"),
            ("Voz: mulher 2", "Vozes/Mulher2/*"),
            // a segunda leva (19/11/2026): o carro, o vidro do molotov e o caiaque encalhando
            ("Carro", "Carro/*"), ("Vidro do molotov", "Fogo/vidro_1"), ("Vidro do molotov", "Fogo/vidro_2"), ("Vidro do molotov", "Fogo/vidro_3"),
            ("Caiaque encalhando", "Agua/caiaque_encalha_1"), ("Caiaque encalhando", "Agua/caiaque_encalha_2"), ("Caiaque encalhando", "Agua/caiaque_encalha_3"),
        };
#if UNITY_EDITOR
        // o que ainda não entrou no jogo, para ouvir e decidir (só no Editor: ficam nas pastas dos pacotes, fora do Resources)
        static readonly string[] CandidatosDosPacotes =
        {
            "Assets/Nature - Essentials/Ambiance_Night_Loop_Stereo.wav", "Assets/Nature - Essentials/Ambiance_Cicadas_Loop_Stereo.wav",
            "Assets/Nature - Essentials/Ambiance_Forest_Birds_Loop_Stereo.wav", "Assets/Nature - Essentials/Ambiance_Wind_Forest_Loop_Stereo.wav",
            "Assets/Rain Forest SFX/Animal/Cricket 1.wav", "Assets/Rain Forest SFX/Animal/Cricket 2.wav", "Assets/Rain Forest SFX/Animal/Cricket Long.wav",
            "Assets/Rain Forest SFX/Animal/Frog Long.wav", "Assets/Rain Forest SFX/Animal/Bat Long.wav", "Assets/Rain Forest SFX/Forest/Breeze Long.wav",
            "Assets/Animal Sound Effect Pack Lite/Wolf/Snarl/animal_wolf_snarl_1.wav", "Assets/Animal Sound Effect Pack Lite/Wolf/Attack/animal_wolf_attack_1.wav",
            "Assets/Animal Sound Effect Pack Lite/Wolf/Hurt/animal_wolf_hurt_1.wav", "Assets/Animal Sound Effect Pack Lite/Bear/Growl/animal_bear_growl_1.wav",
            "Assets/Animal Sound Effect Pack Lite/Bear/Attack/animal_bear_attack_1.wav", "Assets/Animal Sound Effect Pack Lite/Bear/Hurt/animal_bear_hurt_1.wav",
            "Assets/Animal Sound Effect Pack Lite/Deer/Hurt/animal_deer_hurt_1.wav", "Assets/Animal Sound Effect Pack Lite/Snake/Hiss/animal_snake_hiss_1.wav",
            "Assets/Animal Sound Effect Pack Lite/Eagle/Wing Flap/animal_eagle_wingflap_1.wav", "Assets/Animal Sound Effect Pack Lite/Frog/Group Croak/animal_frog_group_croak_1.wav",
        };
#endif
        static System.Collections.Generic.List<(string grupo, AudioClip clip)> _sonsParaOuvir;
        int _somParaOuvir;
        AudioSource _ouvirNatureza;

        static System.Collections.Generic.List<(string grupo, AudioClip clip)> SonsCarregados()
        {
            if (_sonsParaOuvir != null && _sonsParaOuvir.Count > 0 && _sonsParaOuvir[0].clip != null) return _sonsParaOuvir;
            var l = new System.Collections.Generic.List<(string grupo, AudioClip clip)>();
            foreach (var (g, c) in SonsParaOuvir)
            {
                if (c.EndsWith("/*"))
                    foreach (var a in Resources.LoadAll<AudioClip>(c.Substring(0, c.Length - 2))) l.Add((g, a));
                else
                {
                    var a = Resources.Load<AudioClip>(c);
                    if (a != null) l.Add((g, a));
                }
            }
#if UNITY_EDITOR
            foreach (var c in CandidatosDosPacotes)
            {
                var a = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(c);
                if (a != null) l.Add(("Pacotes (fora do jogo)", a));
            }
#endif
            return _sonsParaOuvir = l;
        }

        bool OuvindoNatureza => _ouvirNatureza != null && _ouvirNatureza.isPlaying;

        void OuvirNatureza()
        {
            if (_ouvirNatureza == null)
            {
                _ouvirNatureza = gameObject.AddComponent<AudioSource>();
                _ouvirNatureza.playOnAwake = false;
                _ouvirNatureza.spatialBlend = 0f;
            }
            if (_ouvirNatureza.isPlaying)
            {
                _ouvirNatureza.Stop();
                return;
            }
            var sons = SonsCarregados();
            if (sons.Count == 0) return;
            _ouvirNatureza.clip = sons[Mathf.Clamp(_somParaOuvir, 0, sons.Count - 1)].clip;
            if (_ouvirNatureza.clip != null) _ouvirNatureza.Play();
        }

        // o próximo (ou o anterior) da lista; tocando, já toca o novo
        void ProximoSomDaNatureza(int passo)
        {
            var sons = SonsCarregados();
            if (sons.Count == 0) return;
            _somParaOuvir = ((_somParaOuvir + passo) % sons.Count + sons.Count) % sons.Count;
            if (!OuvindoNatureza) return;
            _ouvirNatureza.Stop();
            OuvirNatureza();
        }

        // o primeiro som do grupo seguinte
        void ProximoGrupo()
        {
            var sons = SonsCarregados();
            if (sons.Count == 0) return;
            int i = Mathf.Clamp(_somParaOuvir, 0, sons.Count - 1);
            string g = sons[i].grupo;
            int k = i;
            do k = (k + 1) % sons.Count;
            while (sons[k].grupo == g && k != i);
            _somParaOuvir = k;
            if (!OuvindoNatureza) return;
            _ouvirNatureza.Stop();
            OuvirNatureza();
        }

        // um ajuste ao vivo: o nome com o valor e a barra embaixo dele, na mesma linha
        float Ajuste(float x, ref float y, float w, string rotulo, float valor, float min, float max, float u, float passo)
        {
            y += passo * u;
            GUI.Label(new Rect(x, y, w * 0.55f, 22f * u), rotulo, _texto);
            return GUI.HorizontalSlider(new Rect(x + w * 0.56f, y + 6f * u, w * 0.44f, 14f * u), valor, min, max);
        }

        void IrParaOLugar(TipoDeLugarDoBicho tipo)
        {
            if (FaunaDoMundo.LugarDeTeste(GetComponent<JogadorRede>(), tipo, out var onde, out float giro))
                GetComponent<MovimentoPrimeiraPessoa>()?.Teleportar(onde + Vector3.up * 0.1f, giro);
            else Avisar(FaunaDoMundo.Lugares.Count == 0 ? "Os lugares dos bichos ainda não ficaram prontos (ou este mapa não tem)." : "Nenhum lugar desse tipo neste mapa.");
        }

        [Rpc(SendTo.Server)]
        void TesteDaFaunaRpc(byte acao, byte especie, RpcParams parametros = default)
        {
            if (!Aceita(parametros)) return;
            var f = FaunaDoMundo.Instancia;
            var j = GetComponent<JogadorRede>();
            string aviso = null;
            switch (acao)
            {
                case 0:
                    aviso = f != null && especie < global::ZombieCityCleaner.Fauna.Especies.Quantas ? f.PorPerto(j, (Especie)especie) : "Sem a fauna neste mundo.";
                    break;
                case 1:
                case 2:
                    aviso = f != null ? f.MatarOMaisPerto(j, acao == 2) : "Sem a fauna neste mundo.";
                    break;
                case 3:
                    f?.TirarTodos();
                    aviso = "Os bichos saíram (os veados voltam sozinhos, longe da vista).";
                    break;
                case 4:
                case 5:
                    var inv = GetComponent<InventarioJogador>();
                    var item = acao == 4 ? Item.Faca : Item.CarneCrua;
                    bool cabe = inv != null && (Itens.EmPilha(item) ? inv.ReceberEmPilha(item, 3) < 3 : inv.Receber(item));
                    aviso = cabe ? (acao == 4 ? "A faca está na mochila: ponha na mão para carnear." : "Carne crua na mochila: asse perto da fogueira acesa.")
                                 : "Sem lugar na mochila.";
                    break;
                case 6:
                    FaunaDoMundo.Restaurar(null);
                    aviso = "A população de todas as regiões voltou a 100%.";
                    break;
                case 7:
                    aviso = f != null ? f.FilaDeTodos(j) : "Sem a fauna neste mundo.";
                    break;
                case 8:
                    aviso = f != null ? f.CacaDeTeste(j, (Especie)especie) : "Sem a fauna neste mundo.";
                    break;
                case 9:
                    aviso = f != null ? f.CercarDeTeste(j) : "Sem a fauna neste mundo.";
                    break;
                case 10:
                    aviso = f != null ? f.VDeTeste(j) : "Sem a fauna neste mundo.";
                    break;
                case 11:
                    aviso = f != null ? f.PescaDeTeste(j) : "Sem a fauna neste mundo.";
                    break;
                case 13:
                    aviso = f != null ? f.UrubusDeTeste(j) : "Sem a fauna neste mundo.";
                    break;
                case 14:
                    aviso = f != null ? f.CastoresNoRiacho(j) : "Sem a fauna neste mundo.";
                    break;
                case 15:
                    aviso = f != null ? f.GuaxinimNoLixo(j) : "Sem a fauna neste mundo.";
                    break;
                case 16:
                    aviso = f != null ? f.BichosDoLugar(j) : "Sem a fauna neste mundo.";
                    break;
                case 17:
                    aviso = f != null ? f.UrsoPegaOSalmao(j) : "Sem a fauna neste mundo.";
                    break;
                case 18:
                    aviso = ConstrucoesNoMundo.Instancia != null ? ConstrucoesNoMundo.Instancia.FogueiraDeTeste(j) : "Sem as construções neste mundo.";
                    break;
                case 19:
                    aviso = ConstrucoesNoMundo.ItensDaFogueiraDeTeste(GetComponent<InventarioJogador>());
                    break;
                case 20:
                    GetComponent<InventarioJogador>()?.EntregarItem(Item.Lanca, 1);
                    aviso = "A lança está na mochila (ou aos seus pés): ponha na mão e golpeie um salmão no raso.";
                    break;
                case 22:
                    {
                        var mochila = GetComponent<InventarioJogador>();
                        if (mochila != null)
                            foreach (var instrumento in new[] { Item.Violao, Item.Pandeiro, Item.Gaita, Item.Flauta }) mochila.EntregarItem(instrumento, 1);
                        aviso = "O violão, o pandeiro, a gaita e a flauta (o que não coube está aos seus pés). Na mão, clique para tocar; botão direito, outra música.";
                    }
                    break;
                case 23:
                    {
                        var mochila = GetComponent<InventarioJogador>();
                        if (mochila != null)
                            foreach (var branca in new[] { Item.Machado, Item.Tocha, Item.Faca, Item.Lanca, Item.Bastao }) mochila.EntregarItem(branca, 1);
                        aviso = "O machado, a tocha, a faca, a lança e o bastão (o que não coube está aos seus pés). Ligue o golpe do Mixamo e golpeie.";
                    }
                    break;
                case 21:
                    if (j.Inventario == null || j.Inventario.NaMao != Item.Lanca) aviso = "Ponha a lança na mão antes.";
                    else
                    {
                        j.SalmaoNaLanca.Value = true;
                        aviso = "Um salmão na ponta da lança (clique para tirar e guardar).";
                    }
                    break;
                case 12:
                    FaunaDoMundo.CastorRapido = !FaunaDoMundo.CastorRapido;
                    aviso = FaunaDoMundo.CastorRapido ? "Os castores roem depressa (a árvore cai em ~8 s de roída)." : "Os castores roem no tempo normal (~90 s).";
                    break;
                case 30:
                    aviso = f != null ? f.UivoDeTeste(j) : "Sem a fauna neste mundo.";
                    break;
            }
            if (aviso != null) AvisoDoTesteRpc(aviso, RpcTarget.Single(parametros.Receive.SenderClientId, RpcTargetUse.Temp));
        }
    }
}
