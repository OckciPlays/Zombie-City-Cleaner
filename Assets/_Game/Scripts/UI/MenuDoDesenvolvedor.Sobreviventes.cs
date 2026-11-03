using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Sobreviventes;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O painel de teste dos sobreviventes (02/11/2026), na terceira aba do canto ("Gente", ao lado de "Zumbis" e
    /// "Clima"): quantos estão no mundo, pôr o grupo da cidade (ou um de teste) a uns 20 m, tirar todos, e ligar ou
    /// desligar os grupos que aparecem sozinhos. Os acampamentos (a etapa 3): ir ao portão do mais perto, deixá-lo amigo ou
    /// hostil e repor o estoque agora. O carro quebrado (a etapa 4): ir até ele, recomeçar a cena, um pneu de 80% na mochila
    /// e acabar a troca agora. A base militar (03/11/2026): ir até a estrada na frente do portão e recomeçar (todos
    /// vivos). A morte (a etapa 4): morrer agora (a tela de morte) e ligar ou desligar o fogo amigo do mundo. Os pedidos
    /// vão para o anfitrião; o ir ao portão, ao carro e à base é aqui (o movimento é do dono).
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        bool _abaDosSobreviventes;

        // a aba "Gente", à esquerda das outras duas
        void AbaDosSobreviventes(float u, Rect painel)
        {
            float w = 50 * u, h = 20 * u, y = painel.y + 6 * u;
            var antes = GUI.color;
            GUI.color = _abaDosSobreviventes ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            if (GUI.Button(new Rect(painel.xMax - 3 * w - 18 * u, y, w, h), "Gente", _botao)) MostrarAba(2);
            GUI.color = antes;
        }

        Rect PainelDosSobreviventes(float u, Rect aoLado)
        {
            float x0 = aoLado.xMax + 8 * u, largura = 250 * u;
            float x = x0 + 12 * u, w = largura - 24 * u;
            float meio = (w - 6 * u) / 2f, x2 = x + meio + 6 * u;
            var area = new Rect(x0, aoLado.y, largura, 8 * u + 28 * u + 58 * u + 30 * u * 2 + 10 * u + 22 * u + 40 * u + 30 * u * 2
                                                     + 10 * u + 22 * u + 40 * u + 30 * u * 2 + 10 * u + 22 * u + 40 * u + 30 * u
                                                     + 10 * u + 22 * u + 30 * u + 6 * u);
            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.55f, 0.7f, 0.35f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "SOBREVIVENTES", _titulo);
            y += 28 * u;
            GUI.Label(new Rect(x, y, w, 54 * u), ResumoDosSobreviventes(), _texto);
            y += 58 * u;
            if (GUI.Button(new Rect(x, y, meio, 26 * u), "Grupo perto", _botao)) TesteDosSobreviventesRpc(0);
            if (GUI.Button(new Rect(x2, y, meio, 26 * u), "Tirar todos", _botao)) TesteDosSobreviventesRpc(1);
            y += 30 * u;
            var m = SobreviventesDoMundo.Instancia;
            bool ligado = m == null || m.ligado;
            if (GUI.Button(new Rect(x, y, w, 26 * u), ligado ? "Grupos sozinhos: ligados" : "Grupos sozinhos: desligados", _botao))
                TesteDosSobreviventesRpc((byte)(ligado ? 2 : 3));
            y += 40 * u;

            // os acampamentos
            GUI.Label(new Rect(x, y, w, 22 * u), "ACAMPAMENTOS", _titulo);
            y += 22 * u;
            var acampamentos = AcampamentosDoMundo.Instancia;
            int perto = -1;
            float distancia = 0f;
            if (acampamentos != null) perto = acampamentos.MaisPerto(transform.position, out distancia);
            var planta = perto >= 0 ? acampamentos.Planta(perto) : null;
            GUI.Label(new Rect(x, y, w, 36 * u), planta == null ? "Nenhum acampamento neste mundo." :
                      $"{planta.Lugar.Nome}: {distancia:0} m, {(acampamentos.Hostil(perto) ? "HOSTIL" : "amigo")}.", _texto);
            y += 40 * u;
            GUI.enabled = planta != null;
            if (GUI.Button(new Rect(x, y, meio, 26 * u), "Ir ao portão", _botao) && planta != null)
            {
                var portao = planta.Mundo(planta.Portao);
                GetComponent<MovimentoPrimeiraPessoa>().Teleportar(portao + Vector3.up * 0.2f, planta.GiroNoMundo(180f));
                Avisar($"No portão do {planta.Lugar.Nome}.");
            }
            bool hostil = planta != null && acampamentos.Hostil(perto);
            if (GUI.Button(new Rect(x2, y, meio, 26 * u), hostil ? "Fazer amigo" : "Fazer hostil", _botao)) TesteDosSobreviventesRpc((byte)(hostil ? 4 : 5));
            y += 30 * u;
            if (GUI.Button(new Rect(x, y, w, 26 * u), "Repor o estoque agora", _botao)) TesteDosSobreviventesRpc(6);
            GUI.enabled = true;
            y += 40 * u;

            // o carro quebrado (a etapa 4)
            GUI.Label(new Rect(x, y, w, 22 * u), "CARRO QUEBRADO", _titulo);
            y += 22 * u;
            var quebrados = CarrosQuebradosDoMundo.Instancia;
            int q = -1;
            float dq = 0f;
            if (quebrados != null) q = quebrados.MaisPerto(transform.position, out dq);
            GUI.Label(new Rect(x, y, w, 36 * u), q < 0 ? "Nenhum neste mundo." :
                      $"A {dq:0} m: {NomeDoEstado(quebrados.Estado(q))}{(quebrados.Trocando(q) ? $" ({quebrados.Falta(q)} s)" : "")}, {quebrados.Vivos(q)} de 2 vivos.", _texto);
            y += 40 * u;
            GUI.enabled = q >= 0;
            if (GUI.Button(new Rect(x, y, meio, 26 * u), "Ir ao carro", _botao) && q >= 0 && quebrados.LugarParaChegar(q, out var ponto, out var giro))
            {
                GetComponent<MovimentoPrimeiraPessoa>().Teleportar(ponto, giro);
                Avisar("Na estrada, a 32 m do carro quebrado.");
            }
            if (GUI.Button(new Rect(x2, y, meio, 26 * u), "Recomeçar", _botao)) TesteDosSobreviventesRpc(7);
            y += 30 * u;
            if (GUI.Button(new Rect(x, y, meio, 26 * u), "Pneu de 80%", _botao)) TesteDosSobreviventesRpc(8);
            if (GUI.Button(new Rect(x2, y, meio, 26 * u), "Acabar a troca", _botao)) TesteDosSobreviventesRpc(9);
            GUI.enabled = true;
            y += 40 * u;

            // a base militar (03/11/2026)
            GUI.Label(new Rect(x, y, w, 22 * u), "BASE MILITAR", _titulo);
            y += 22 * u;
            var bases = BasesMilitaresDoMundo.Instancia;
            int bm = -1;
            float db = 0f;
            if (bases != null) bm = bases.MaisPerto(transform.position, out db);
            var plantaDaBase = bm >= 0 ? bases.Planta(bm) : null;
            string sobreABase = "";
            if (plantaDaBase != null && bases.Grupo(bm) != null) sobreABase = $" {bases.Grupo(bm).Vivos} militares lá.";
            if (plantaDaBase != null && bases.DiaDoReforco(bm) >= 0) sobreABase += $" {bases.Mortos(bm)} mortos; o reforço no dia {bases.DiaDoReforco(bm)}.";
            GUI.Label(new Rect(x, y, w, 36 * u), plantaDaBase == null ? "Nenhuma base neste mundo." :
                      $"{plantaDaBase.Lugar.Nome}: {db:0} m.{sobreABase}", _texto);
            y += 40 * u;
            GUI.enabled = plantaDaBase != null;
            if (GUI.Button(new Rect(x, y, meio, 26 * u), "Ir à base", _botao) && plantaDaBase != null)
            {
                var fora = plantaDaBase.Mundo(plantaDaBase.Portao);
                GetComponent<MovimentoPrimeiraPessoa>().Teleportar(fora + Vector3.up * 0.3f, plantaDaBase.GiroNoMundo(180f));
                Avisar($"Na estrada, a 45 m do meio da {plantaDaBase.Lugar.Nome}.");
            }
            if (GUI.Button(new Rect(x2, y, meio, 26 * u), "Recomeçar a base", _botao)) TesteDosSobreviventesRpc(10);
            GUI.enabled = true;
            y += 40 * u;

            // a morte e o fogo amigo (03/11/2026, a etapa 4)
            GUI.Label(new Rect(x, y, w, 22 * u), "A MORTE", _titulo);
            y += 22 * u;
            var mundoDaPartida = World.MundoDaPartida.Instancia;
            bool fogo = mundoDaPartida != null && mundoDaPartida.FogoAmigo;
            if (GUI.Button(new Rect(x, y, meio, 26 * u), "Morrer agora", _botao)) TesteDosSobreviventesRpc(11);
            if (GUI.Button(new Rect(x2, y, meio, 26 * u), fogo ? "Fogo amigo: ligado" : "Fogo amigo: desligado", _botao)) TesteDosSobreviventesRpc((byte)(fogo ? 12 : 13));
            return area;
        }

        static string NomeDoEstado(EstadoDoCarroQuebrado e)
        {
            switch (e)
            {
                case EstadoDoCarroQuebrado.Esperando: return "esperando um pneu";
                case EstadoDoCarroQuebrado.Trocando: return "trocando";
                case EstadoDoCarroQuebrado.Ajudados: return "ajudados (amigos)";
                case EstadoDoCarroQuebrado.Hostis: return "HOSTIS";
                default: return "mortos";
            }
        }

        static string ResumoDosSobreviventes()
        {
            int vivos = 0, mortos = 0;
            foreach (var s in Sobrevivente.Todos)
                if (s != null) { if (s.Morto) mortos++; else vivos++; }
            var m = SobreviventesDoMundo.Instancia;
            string texto = $"No mundo: {vivos} vivos, {mortos} caídos.";
            if (m != null && m.Ativos.Count > 0)
                foreach (var g in m.Ativos)
                    texto += $"\nCidade {g.cidade}, parte {g.setor + 1}: {(g.estado == EstadoDoGrupo.Combate ? "em combate" : "explorando")}{(g.Fugindo ? ", o último fugindo" : "")}.";
            else if (m != null && m.IsServer) texto += "\nNenhum grupo agora.";
            return texto;
        }

        [Rpc(SendTo.Server)]
        void TesteDosSobreviventesRpc(byte acao, RpcParams parametros = default)
        {
            var m = SobreviventesDoMundo.Instancia;
            var acampamentos = AcampamentosDoMundo.Instancia;
            string aviso = null;
            switch (acao)
            {
                case 0:
                    var j = GetComponent<JogadorRede>();
                    if (j != null && m != null) aviso = m.PorUmGrupoPerto(j);
                    break;
                case 1:
                    m?.TirarTodos();
                    acampamentos?.TirarAsPessoas();
                    CarrosQuebradosDoMundo.Instancia?.TirarAsPessoas();
                    BasesMilitaresDoMundo.Instancia?.TirarAsPessoas();
                    aviso = "Os sobreviventes saíram (os dos acampamentos e das bases voltam quando você chegar perto).";
                    break;
                case 10:
                    var bases = BasesMilitaresDoMundo.Instancia;
                    if (bases == null) break;
                    int b = bases.MaisPerto(transform.position, out _);
                    bases.Recomecar(b);
                    aviso = b >= 0 ? "A base voltou ao começo: os militares todos vivos (aparecem a 170 m)." : "Nenhuma base neste mundo.";
                    break;
                case 11:
                    var vida = GetComponent<VidaJogador>();
                    if (vida != null) vida.Dano(1000f, transform.position, "O menu do desenvolvedor (F1).");
                    break;
                case 12:
                case 13:
                    World.MundoDaPartida.Instancia?.DefinirFogoAmigo(acao == 13);
                    aviso = acao == 13 ? "Fogo amigo ligado: o tiro e o golpe ferem os outros jogadores." : "Fogo amigo desligado.";
                    break;
                case 2: if (m != null) m.ligado = false; aviso = "Os grupos não aparecem mais sozinhos."; break;
                case 3: if (m != null) m.ligado = true; aviso = "Os grupos voltam a aparecer sozinhos."; break;
                case 4:
                case 5:
                    if (acampamentos == null) break;
                    int a = acampamentos.MaisPerto(transform.position, out _);
                    acampamentos.DefinirHostil(a, acao == 5);
                    aviso = acao == 5 ? "O acampamento ficou hostil." : "O acampamento voltou a ser amigo.";
                    break;
                case 6:
                    acampamentos?.Repor(true);
                    aviso = "Os estoques foram repostos.";
                    break;
                case 7:
                case 9:
                    var quebrados = CarrosQuebradosDoMundo.Instancia;
                    if (quebrados == null) break;
                    int q = quebrados.MaisPerto(transform.position, out _);
                    if (acao == 7)
                    {
                        quebrados.Recomecar(q);
                        aviso = "O carro quebrado voltou ao começo: os dois vivos, esperando um pneu.";
                    }
                    else
                    {
                        quebrados.AcabarATroca(q);
                        aviso = quebrados.Trocando(q) ? "A troca acaba agora." : "Eles não estão trocando o pneu.";
                    }
                    break;
                case 8:
                    var inv = GetComponent<InventarioJogador>();
                    aviso = inv != null && inv.Receber(Loot.Item.Pneu, 80) ? "Um pneu de 80% na mochila." : "Não coube um pneu na mochila.";
                    break;
            }
            if (aviso != null) AvisoDoTesteRpc(aviso, RpcTarget.Single(parametros.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void AvisoDoTesteRpc(string aviso, RpcParams parametros = default) => Avisar(aviso);
    }
}
