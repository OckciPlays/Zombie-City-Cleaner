using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Fauna;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O painel de teste da fauna (12/11/2026), na sexta aba do canto ("Fauna"): quantos bichos há de cada espécie, a
    /// população da região e o mais perto; pôr um grupo de cada espécie a ~18 m na frente (os modelos de caixas, e o veado do
    /// Blender), a fila de todos parados (para ver os modelos), matar ou ferir o mais perto, tirar todos, a faca na mochila
    /// (para carnear), a carne crua e os nomes e os estados em cima dos bichos. A etapa 2: a caça na frente (os lobos atrás
    /// dos veados, os coiotes atrás dos coelhos) e a matilha mais perto cercando quem pediu (vale de dia). A etapa 3: ir para
    /// a margem do lago, o V dos gansos agora, o urso pescando o salmão e os castores roendo depressa. Os pedidos vão para o anfitrião.
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
            _conteudoDaFauna.text = f != null ? f.Resumo(transform.position) : "A fauna liga com o mundo gerado pronto.";
            float alturaDoTexto = _texto.CalcHeight(_conteudoDaFauna, w);
            int linhas = (BotoesDaFauna.Length + 1) / 2 + 13;
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
            if (GUI.Button(new Rect(x, y, w, alto * u), "A matilha mais perto me cerca", _botao)) TesteDaFaunaRpc(9, 0);
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
            return area;
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
                case 12:
                    FaunaDoMundo.CastorRapido = !FaunaDoMundo.CastorRapido;
                    aviso = FaunaDoMundo.CastorRapido ? "Os castores roem depressa (a árvore cai em ~8 s de roída)." : "Os castores roem no tempo normal (~90 s).";
                    break;
            }
            if (aviso != null) AvisoDoTesteRpc(aviso, RpcTarget.Single(parametros.Receive.SenderClientId, RpcTargetUse.Temp));
        }
    }
}
