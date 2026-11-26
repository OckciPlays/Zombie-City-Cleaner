using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>Uma fileira de produtos na prateleira (o Dia 0): E põe um no carrinho.</summary>
    public class ProdutoNaPrateleira : Interagivel
    {
        public LojaDoDiaZero loja;
        public int indice;

        public override string Dica(ArmaJogador quem)
        {
            if (loja == null || !DiaZeroDaPartida.NoDia) return null;
            var p = PrecosDoDiaZero.DaLoja(loja.Tipo, indice);
            return p.HasValue ? $"[E] Pôr no carrinho: {p.Value.Nome}  ·  {PrecosDoDiaZero.Dinheiro(p.Value.preco)}" : null;
        }

        public override void Usar(ArmaJogador quem) => Carrinho.Por(loja, indice);
    }

    /// <summary>Uma roupa na loja de roupas (o Dia 0): E prova (ou tira); paga no caixa.</summary>
    public class RoupaNaLoja : Interagivel
    {
        public LojaDoDiaZero loja;
        public int lugar;
        public byte valor;

        string Peca => AparenciaDoJogador.LugaresDaLoja[lugar].pecas[valor - 1];

        public override string Dica(ArmaJogador quem)
        {
            if (loja == null || !DiaZeroDaPartida.NoDia || JogadorRede.Local == null) return null;
            bool vestida = JogadorRede.Local.Aparencia.Value.DaLoja(lugar) == valor;
            string nome = Carrinho.Nome(Peca);
            return vestida ? $"[E] Tirar {nome}" : $"[E] Provar {nome}  ·  {PrecosDoDiaZero.Dinheiro(PrecosDoDiaZero.PrecoDaRoupa(Peca))}";
        }

        public override void Usar(ArmaJogador quem) => Carrinho.Provar(loja, lugar, valor);
    }

    /// <summary>O caixa de uma loja (o Dia 0): E abre a tela do caixa (o carrinho, o total e o pagar).</summary>
    public class CaixaDaLoja : Interagivel
    {
        public LojaDoDiaZero loja;

        public override string Dica(ArmaJogador quem)
        {
            if (loja == null || !DiaZeroDaPartida.NoDia) return null;
            if (Carrinho.Loja != loja || Carrinho.Quantos == 0) return "Caixa  ·  o carrinho está vazio (pegue os produtos nas prateleiras)";
            return $"[E] Passar no caixa  ·  {Carrinho.Quantos} {(Carrinho.Quantos == 1 ? "item" : "itens")}  ·  {PrecosDoDiaZero.Dinheiro(Carrinho.Total)}";
        }

        public override void Usar(ArmaJogador quem)
        {
            if (Carrinho.Loja == loja && Carrinho.Quantos > 0) TelaDoCaixa.Abrir(loja);
        }
    }

    /// <summary>
    /// A TV da sala (o lobby do Dia 0): na casa, E liga o jornal para todos; com a rua liberada, E mostra o jornal de novo,
    /// só para quem apertou.
    /// </summary>
    public class TelevisaoDoDiaZero : Interagivel
    {
        public override string Dica(ArmaJogador quem)
        {
            var dia = DiaZeroDaPartida.Instancia;
            if (!DiaZeroDaPartida.NoDia || dia == null || IntroDoDiaZero.Tocando) return null;
            switch (dia.Fase)
            {
                case FaseDoDia.Casa:
                    int n = 0;
                    foreach (var j in JogadorRede.Todos) if (j != null) n++;
                    return $"[E] Ligar a TV  ·  o jornal começa para todos ({n} {(n == 1 ? "jogador" : "jogadores")} na casa)";
                case FaseDoDia.Rua:
                    return "[E] Ver o jornal de novo (só você)";
                default:
                    return null;
            }
        }

        public override void Usar(ArmaJogador quem)
        {
            var dia = DiaZeroDaPartida.Instancia;
            if (dia == null) return;
            if (dia.Fase == FaseDoDia.Casa) dia.LigarATv();
            else if (dia.Fase == FaseDoDia.Rua) IntroDoDiaZero.Instancia?.Reprisar();
        }
    }

    /// <summary>
    /// As chaves da casa da família na Woodcreek (22/11/2026, o Dia 0 no Lakewood): o gancho ao lado da porta da frente, por
    /// dentro. Com a rua liberada, E duas vezes tranca a casa e acaba o dia para todos: por enquanto, o jogo pula para os 5
    /// anos depois, no bunker da comunidade (a quarentena, o colapso e a fuga são as próximas etapas do Docs/23). As compras
    /// já estão na caçamba da caminhonete: quem está fora de casa vai junto, sem perder nada.
    /// </summary>
    public class ChavesDaCasa : Interagivel
    {
        float _confirmarAte;

        /// <summary>O gancho com as chaves no objeto 'go' (encostado na parede, a frente para o +z dele) e a caixa da mira.</summary>
        public static void Montar(GameObject go, Material material)
        {
            var obra = new Obra(4243, Medidas.DoJogo);
            // a plaquinha de madeira, os três ganchos e as chaves penduradas (o chaveiro vermelho é o da caminhonete)
            obra.Caixa(new Vector3(0f, 0f, 0.01f), new Vector3(0.34f, 0.16f, 0.02f), "Madeira");
            for (int i = 0; i < 3; i++)
            {
                float x = -0.1f + i * 0.1f;
                obra.Caixa(new Vector3(x, 0.01f, 0.035f), new Vector3(0.012f, 0.012f, 0.03f), "Grade");
                if (i == 1) continue;
                obra.Caixa(new Vector3(x, -0.04f, 0.05f), new Vector3(0.022f, 0.06f, 0.006f), "Metal");
                obra.Caixa(new Vector3(x, -0.085f, 0.05f), new Vector3(0.035f, 0.03f, 0.012f), i == 0 ? "PlacaVermelha" : "PlacaAmarela");
            }
            var malha = obra.vis.ParaMesh("Chaves da casa");
            malha.hideFlags = HideFlags.DontSave;
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            MalhaDeObra.Devolver(obra.vis);
            MalhaDeObra.Devolver(obra.col);
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, -0.02f, 0.12f);
            col.size = new Vector3(0.5f, 0.4f, 0.24f);
            go.AddComponent<ChavesDaCasa>();
        }

        public override string Dica(ArmaJogador quem)
        {
            // com a rua liberada (no lobby, todos ficam na casa até ver o jornal)
            if (!DiaZeroDaPartida.NoDia || DiaZeroDaPartida.Instancia.Fase != FaseDoDia.Rua) return null;
            string fora = NomesDeQuemEstaFora.ComE(DiaZeroDaPartida.QuemEstaFora(), out bool varios);
            if (Time.time < _confirmarAte)
                return string.IsNullOrEmpty(fora)
                    ? "[E] De novo: trancar a casa (o Dia 0 acaba para todos)"
                    : $"[E] De novo: trancar mesmo assim ({fora} ainda na rua: {(varios ? "vêm" : "vem")} junto)";
            return string.IsNullOrEmpty(fora)
                ? "[E] Trancar a casa e acabar o Dia 0"
                : $"[E] Trancar a casa e acabar o Dia 0  ·  ainda na rua: {fora}";
        }

        public override void Usar(ArmaJogador quem)
        {
            if (Time.time < _confirmarAte)
            {
                _confirmarAte = 0f;
                DiaZeroDaPartida.Instancia?.PedirFecharAPorta();
                return;
            }
            _confirmarAte = Time.time + 4f;
        }
    }

    /// <summary>
    /// A porta do abrigo na nossa casa (o Dia 0, a pedido: "uma porta na nossa casa que conseguimos abrir para passar o
    /// dia"; o bunker de verdade é do Gustavo). E duas vezes fecha a porta: o dia acaba para todos, e o jogo pula 5 anos.
    /// Quem estiver fora de casa perde as compras (a pedido).
    /// </summary>
    public class PortaDoAbrigo : Interagivel
    {
        float _confirmarAte;

        /// <summary>A porta de aço no objeto 'go' (a frente para o +z dele, encostada na parede): o batente, a folha e a placa.</summary>
        public static void Montar(GameObject go, Material material)
        {
            var obra = new Obra(4242, Medidas.DoJogo);
            const float L = 1.0f, A = 2.1f;
            obra.Caixa(new Vector3(0f, A / 2, 0.03f), new Vector3(L + 0.16f, A + 0.08f, 0.06f), "Concreto");
            obra.Caixa(new Vector3(0f, A / 2 - 0.02f, 0.07f), new Vector3(L, A - 0.04f, 0.05f), "Metal");
            // os reforços e a maçaneta de alavanca
            for (int i = 0; i < 3; i++) obra.Caixa(new Vector3(0f, 0.35f + i * 0.7f, 0.1f), new Vector3(L - 0.1f, 0.06f, 0.02f), "Grade");
            obra.Caixa(new Vector3(L * 0.36f, 1.05f, 0.13f), new Vector3(0.05f, 0.05f, 0.06f), "Grade");
            obra.Caixa(new Vector3(L * 0.28f, 1.05f, 0.16f), new Vector3(0.2f, 0.035f, 0.035f), "Grade");
            // a placa amarela em cima
            obra.Caixa(new Vector3(0f, A + 0.18f, 0.06f), new Vector3(0.6f, 0.2f, 0.02f), "PlacaAmarela");
            var malha = obra.vis.ParaMesh("Porta do abrigo");
            malha.hideFlags = HideFlags.DontSave;
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            MalhaDeObra.Devolver(obra.vis);
            MalhaDeObra.Devolver(obra.col);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, A / 2, 0.08f);
            col.size = new Vector3(L, A, 0.12f);
            go.AddComponent<PortaDoAbrigo>();
        }

        public override string Dica(ArmaJogador quem)
        {
            // a porta só abre com a rua liberada (no lobby, todos ficam na casa até ver o jornal)
            if (!DiaZeroDaPartida.NoDia || DiaZeroDaPartida.Instancia.Fase != FaseDoDia.Rua) return null;
            string fora = NomesDeQuemEstaFora.ComE(DiaZeroDaPartida.QuemEstaFora(), out bool varios);
            if (Time.time < _confirmarAte)
                return string.IsNullOrEmpty(fora)
                    ? "[E] De novo: fechar a porta (o dia acaba para todos)"
                    : $"[E] De novo: fechar mesmo assim ({fora} fora de casa: {(varios ? "as compras deles ficam" : "as compras ficam")} para trás)";
            return string.IsNullOrEmpty(fora)
                ? "[E] Fechar a porta do abrigo e passar o dia"
                : $"[E] Fechar a porta do abrigo  ·  ainda fora de casa: {fora}";
        }

        public override void Usar(ArmaJogador quem)
        {
            if (Time.time < _confirmarAte)
            {
                _confirmarAte = 0f;
                DiaZeroDaPartida.Instancia?.PedirFecharAPorta();
                return;
            }
            _confirmarAte = Time.time + 4f;
        }
    }

    /// <summary>
    /// (26/11/2026, a revisão dos textos cortados) os nomes de quem ainda está fora, como o <see cref="DiaZeroDaPartida.QuemEstaFora"/>
    /// os junta ("Ana, Pedro"), com o "e" antes do último ("Ana e Pedro"), para as dicas das chaves e da porta do abrigo; e se são
    /// mais de um (o plural da frase)
    /// </summary>
    static class NomesDeQuemEstaFora
    {
        public static string ComE(string nomes, out bool varios)
        {
            varios = false;
            if (string.IsNullOrEmpty(nomes)) return nomes;
            int ultima = nomes.LastIndexOf(", ", System.StringComparison.Ordinal);
            varios = ultima >= 0;
            return varios ? nomes.Substring(0, ultima) + " e " + nomes.Substring(ultima + 2) : nomes;
        }
    }
}
