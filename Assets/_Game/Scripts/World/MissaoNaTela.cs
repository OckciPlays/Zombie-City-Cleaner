using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Sobreviventes;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A conversa com o Capitão (24/11/2026, a primeira missão depois dos 5 anos): ele conta o que houve, chama para retomar a
    /// Woodcreek (a escolta começa), espera ou vem junto, e explica como consertar a caminhonete. O que muda vai pelo
    /// <see cref="MundoDeLakewood.EscoltaRpc"/> (o anfitrião move o Capitão).
    /// </summary>
    public class ConversaDoCapitao : MonoBehaviour, IConversavel
    {
        public Sobrevivente capitao;

        static EtapaDaMissao Etapa => MundoDeLakewood.Instancia != null ? MundoDeLakewood.Instancia.Missao : EtapaDaMissao.Nenhuma;

        public string NomeNaConversa => "o Capitão, da comunidade";
        public Vector3 Onde => transform.position;
        public bool PodeConversar => capitao != null && capitao.IsSpawned && !capitao.Morto;
        public void AoConversar(bool comecou) { }

        public string Saudacao() => Etapa switch
        {
            EtapaDaMissao.FaleComOCapitao =>
                "Até que enfim. Cinco anos aqui embaixo... Lá fora, a Woodcreek é deles agora. E tudo o que vocês compraram naquele dia ficou na caminhonete, na frente da casa de vocês.",
            EtapaDaMissao.VaParaACasa => "Estou com vocês. Fiquem perto e não gastem bala à toa.",
            EtapaDaMissao.RetomeARua => "Eles estão por toda a rua! Limpem a área, eu cubro vocês!",
            EtapaDaMissao.ConserteACaminhonete =>
                "A rua é nossa. A caminhonete está acabada: sem rodas, sem vela, sem bateria, sem radiador. Os carros largados pela vila ainda têm peças.",
            EtapaDaMissao.Cumprida => "Ela anda! Bom trabalho. Agora dá para trazer suprimentos para o bunker.",
            _ => "Tudo tranquilo por aqui.",
        };

        public List<Pergunta> Perguntas()
        {
            var e = Etapa;
            var m = MundoDeLakewood.Instancia;
            var lista = new List<Pergunta>
            {
                new Pergunta("Quem é você?",
                    "Sou o capitão da comunidade. Servi no exército antes de tudo isso; o major Ziquinatti me pôs no comando das saídas.",
                    "O capitão. O Gus cuida de quem está aqui dentro; eu cuido de quem sai pelo portão."),
                new Pergunta("O que aconteceu lá fora?",
                    "Na noite em que vocês chegaram, a cidade caiu. O que sobrou da Woodcreek é deles. Mas ainda tem muita coisa útil lá, inclusive o que é de vocês.",
                    "Os Ocos tomaram as ruas. De dia eles ficam lentos; à noite, nem pense em sair sem luz."),
            };
            if (e == EtapaDaMissao.FaleComOCapitao)
                lista.Add(new Pergunta("Vamos retomar a Woodcreek.",
                    "Então vamos. A casa de vocês fica na Spruce Lane. Peguem armas no painel do bunker antes de sair.")
                { acao = () => m?.EscoltaRpc(true), fecha = true });
            if (e >= EtapaDaMissao.VaParaACasa && e < EtapaDaMissao.Cumprida)
            {
                lista.Add(new Pergunta("Venha comigo.", "Estou logo atrás de você.", "Vamos.") { acao = () => m?.EscoltaRpc(true) });
                lista.Add(new Pergunta("Espere aqui.", "Fico de olho daqui. Grite se precisar.", "Certo, seguro esta posição.") { acao = () => m?.EscoltaRpc(false) });
                lista.Add(new Pergunta("Como conserto a caminhonete?",
                    "Quatro rodas, uma vela, uma bateria e um radiador. Tire dos carros largados: o capô abre, e as rodas saem com o macaco, que ficou na caçamba.",
                    "Procure os carros abandonados da vila. Roda sem furo, bateria com carga, vela boa. O radiador pode estar meio gasto, mas tem que ter um."));
            }
            lista.Add(new Pergunta("Tchau.") { fecha = true });
            return lista;
        }
    }

    /// <summary>
    /// A missão no canto de cima, à direita (24/11/2026, como a da fuga): "RETOME A WOODCREEK", o que fazer agora, quantos
    /// zumbis faltam na rua, quantas peças faltam na caminhonete e o que está na caçamba, e a distância até onde ir. Em cada
    /// máquina, pelos números da <see cref="MundoDeLakewood"/>. Cumprida, ela fica 15 s na tela e sai.
    /// </summary>
    public class MissaoNaTela : MonoBehaviour
    {
        EtapaDaMissao _ultima;
        float _cumpridaEm = -99f;
        GUIStyle _titulo, _texto, _pequeno;
        float _u;

        void Update()
        {
            var m = MundoDeLakewood.Instancia;
            var e = m != null ? m.Missao : EtapaDaMissao.Nenhuma;
            if (e != _ultima && e == EtapaDaMissao.Cumprida) _cumpridaEm = Time.time;
            _ultima = e;
        }

        void Estilos(float u)
        {
            if (_titulo != null && Mathf.Approximately(u, _u)) return;
            _u = u;
            _titulo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(19 * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            _titulo.normal.textColor = new Color(0.95f, 0.83f, 0.45f);
            _texto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * u), wordWrap = true, alignment = TextAnchor.UpperRight };
            _texto.normal.textColor = new Color(0.95f, 0.94f, 0.9f);
            _pequeno = new GUIStyle(_texto) { fontSize = Mathf.RoundToInt(13 * u) };
            _pequeno.normal.textColor = new Color(0.8f, 0.78f, 0.72f);
        }

        void OnGUI()
        {
            var m = MundoDeLakewood.Instancia;
            var j = JogadorRede.Local;
            if (m == null || j == null || TelaDeConversa.Aberta) return;
            var e = m.Missao;
            if (e == EtapaDaMissao.Nenhuma || (e == EtapaDaMissao.Cumprida && Time.time - _cumpridaEm > 15f)) return;
            float u = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            Estilos(u);
            string oQue;
            Vector3? alvo = null;
            switch (e)
            {
                case EtapaDaMissao.FaleComOCapitao:
                    oQue = "Fale com o Capitão, no pátio do bunker. Tudo o que vocês levavam ficou na caçamba da caminhonete, na casa da família.";
                    alvo = Capitao();
                    break;
                case EtapaDaMissao.VaParaACasa:
                    oQue = "Vá com o Capitão até a casa da família, na Spruce Lane, na Woodcreek.";
                    alvo = Caminhonete();
                    break;
                case EtapaDaMissao.RetomeARua:
                    oQue = $"Retome a rua: {m.ZumbisDaMissao} {(m.ZumbisDaMissao == 1 ? "zumbi" : "zumbis")} perto da casa.";
                    alvo = Caminhonete();
                    break;
                case EtapaDaMissao.ConserteACaminhonete:
                    oQue = $"Recupere as compras na caçamba ({m.NaCacamba} {(m.NaCacamba == 1 ? "coisa" : "coisas")}) e conserte a caminhonete: faltam {m.PecasQueFaltam} " +
                           $"{(m.PecasQueFaltam == 1 ? "peça" : "peças")} (as rodas, a vela, a bateria e o radiador; tire dos carros largados).";
                    alvo = Caminhonete();
                    break;
                default:
                    oQue = "Missão cumprida: a caminhonete anda de novo. Volte ao bunker com o Capitão.";
                    break;
            }
            float w = 420 * u, x = Screen.width - w - 24 * u, y = 24 * u;
            Sombra(new Rect(x, y, w, 26 * u), e == EtapaDaMissao.Cumprida ? "MISSÃO CUMPRIDA" : "RETOME A WOODCREEK", _titulo);
            float h = _texto.CalcHeight(new GUIContent(oQue), w);
            Sombra(new Rect(x, y + 28 * u, w, h), oQue, _texto);
            if (alvo.HasValue)
            {
                var d = alvo.Value - j.transform.position;
                d.y = 0f;
                Sombra(new Rect(x, y + 32 * u + h, w, 22 * u), $"{d.magnitude:0} m", _pequeno);
            }
        }

        static Vector3? Capitao()
        {
            foreach (var s in FindObjectsByType<Sobrevivente>(FindObjectsSortMode.None))
                if (s != null && s.IsSpawned && s.Identidade.Value.grupo == LakewoodNaCena.GrupoDoCapitao) return s.transform.position;
            return null;
        }

        static Vector3? Caminhonete()
        {
            var lw = LakewoodNaCena.Instancia;
            if (lw == null || lw.caminhonete == null) return null;
            foreach (var c in Carro.Todos)
                if (c != null && c.IsSpawned && c.name.StartsWith(lw.caminhonete.name)) return c.transform.position;
            return null;
        }

        static void Sombra(Rect r, string texto, GUIStyle estilo)
        {
            var cor = estilo.normal.textColor;
            estilo.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), texto, estilo);
            estilo.normal.textColor = cor;
            GUI.Label(r, texto, estilo);
        }
    }
}
