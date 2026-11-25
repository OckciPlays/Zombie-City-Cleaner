using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// A conversa com os NPCs do Dia 0 (24/11/2026, o Pedro: "um chat de conversa com algumas perguntas que daria para gente
    /// fazer aos vizinhos, aos atendentes e alguns outros npcs"). Cada um tem um nome (sorteado pela semente, os nomes de
    /// uma vila dos EUA) e o papel com o artigo ("Martha, a vizinha"; "Earl, o atendente do mercado"); as perguntas mudam
    /// com o papel: o que acham da doença, se estão bem (o doente tosse), o que a loja vende, onde ficam as lojas (a direção e
    /// a distância de verdade). Só no Dia 0: na noite da fuga, ninguém para para conversar.
    /// </summary>
    public class ConversaDoDiaZero : MonoBehaviour, IConversavel
    {
        NpcDoDiaZero _npc;
        string _nome, _papel;
        LojaDoDiaZero _loja;
        bool _mulher;

        static readonly string[] Homens = { "Bill", "Frank", "Tom", "Earl", "Mike", "Dave", "Joe", "Hank", "Walt", "Ray", "Carl", "Ben", "Gary", "Ted" };
        static readonly string[] Mulheres = { "Martha", "Linda", "Susan", "Peggy", "Carol", "Ann", "Judy", "Rose", "Donna", "Kate", "Lucy", "Nancy", "Jean", "Ruth" };

        public static void Por(NpcDoDiaZero npc)
        {
            var c = npc.gameObject.AddComponent<ConversaDoDiaZero>();
            c._npc = npc;
            ConversaComNpc.Por(npc.gameObject, c);
        }

        void Start()
        {
            var l = _npc != null ? _npc.Lugar : null;
            _mulher = _npc != null && _npc.Mulher;
            var r = new System.Random(l != null ? l.semente * 7 + 3 : 1);
            _nome = _mulher ? Mulheres[r.Next(Mulheres.Length)] : Homens[r.Next(Homens.Length)];
        }

        string A => _mulher ? "a" : "o";

        // o papel, com o artigo (e a loja do atendente: a mais perto dele)
        string Papel()
        {
            if (_papel != null) return _papel;
            var l = _npc != null ? _npc.Lugar : null;
            if (l == null) return _papel = _mulher ? "a moradora" : "o morador";
            switch (l.rotina)
            {
                case Rotina.Atendente:
                {
                    _loja = LojaMaisPerto(_npc.transform.position, 30f);
                    // (a loja ainda não montada, longe de todos: sem guardar o papel, para achar a loja depois)
                    if (_loja == null) return $"{A} atendente da loja";
                    string onde = _loja == null ? "da loja" : _loja.Tipo switch
                    {
                        LojaDoDia.Mercado => "do mercado",
                        LojaDoDia.Farmacia => "da farmácia",
                        LojaDoDia.Armas => "da loja de armas",
                        LojaDoDia.Ferragens => "da loja de ferragens",
                        LojaDoDia.Roupas => "da loja de roupas",
                        _ => "do posto",
                    };
                    if (_loja != null && _loja.Tipo == LojaDoDia.Farmacia) return _papel = _mulher ? "a farmacêutica" : "o farmacêutico";
                    return _papel = $"{A} atendente {onde}";
                }
                case Rotina.Regar:
                case Rotina.CortarGrama:
                case Rotina.Conversar:
                    return _papel = _mulher ? "a vizinha" : "o vizinho";
                case Rotina.Correr:
                    return _papel = _mulher ? "a moradora (correndo)" : "o morador (correndo)";
                case Rotina.Faxineiro:
                    return _papel = _mulher ? "a faxineira" : "o faxineiro";
                case Rotina.Comprar:
                    return _papel = _mulher ? "a cliente" : "o cliente";
                default:
                    return _papel = _mulher ? "a moradora" : "o morador";
            }
        }

        static LojaDoDiaZero LojaMaisPerto(Vector3 p, float ate)
        {
            var w = WoodcreekDoDiaZero.Atual;
            if (w == null) return null;
            LojaDoDiaZero melhor = null;
            float d = ate * ate;
            foreach (var l in w.Lojas)
            {
                if (l == null) continue;
                float dl = (l.transform.TransformPoint(l.Area.center) - p).sqrMagnitude;
                if (dl < d) { d = dl; melhor = l; }
            }
            return melhor;
        }

        public string NomeNaConversa => $"{_nome}, {Papel()}";
        public Vector3 Onde => transform.position;
        public bool PodeConversar
        {
            get
            {
                var m = MundoDeLakewood.Instancia;
                return _npc != null && (m == null || !m.NaFuga) && _npc.Lugar != null && _npc.Lugar.rotina != Rotina.CortarGrama;
            }
        }

        public void AoConversar(bool comecou)
        {
            if (_npc != null) _npc.Conversando = comecou;
        }

        bool Doente => _npc != null && _npc.Doente;
        bool Atendente => _npc != null && _npc.Lugar != null && _npc.Lugar.rotina == Rotina.Atendente;

        public string Saudacao()
        {
            Papel();
            if (Doente) return Um("*tosse* Oi... desculpa, acho que peguei uma gripe forte.", "Oi. *tosse* Não chega muito perto, tá? Não estou bem.");
            if (Atendente) return Um("Bom dia! Pode olhar à vontade, o preço está na etiqueta.", "Oi! Hoje está uma correria, todo mundo comprando.", "Bem-vindo. Se precisar de algo, é só falar.");
            return Um("Oi, vizinho! Viu o jornal?", "Bom dia. Dia estranho, não é?", "Oi! Tudo bem por aí?", "Ei. Você também está indo às compras?");
        }

        public List<Pergunta> Perguntas()
        {
            var lista = new List<Pergunta>
            {
                new Pergunta("O que você acha dessa doença?",
                    "Dizem que começou no Brasil. Febre, olhos vermelhos... E agora já está aqui no estado.",
                    $"O governo pediu calma. Quando o governo pede calma, eu fico {(_mulher ? "nervosa" : "nervoso")}.",
                    "Eu acho exagero da TV. Mas mesmo assim vou encher a despensa.",
                    "Minha irmã é enfermeira em Lakewood. Disse que o hospital está lotado e que ninguém explica nada."),
            };
            if (Doente)
                lista.Add(new Pergunta("Você está bem? Seus olhos estão vermelhos.",
                    "*tosse* É só uma gripe. Uma coceira no braço que não passa... *coça o braço*",
                    "Estou com febre desde ontem. *tosse* Vou passar na farmácia depois.",
                    "Não sei... *tosse forte* Acho que vou deitar um pouco."));
            else
                lista.Add(new Pergunta("Tudo bem com você?",
                    $"Tudo, graças a Deus. Só {(_mulher ? "preocupada" : "preocupado")} com as crianças.",
                    "Estou bem. Mas o vizinho da esquina está tossindo há dois dias.",
                    "Vou ficando. Comprei água e enlatados para um mês, por via das dúvidas."));
            if (Atendente && _loja != null)
            {
                var nomes = PrecosDoDiaZero.Produtos(_loja.Tipo).Select(p => p.Nome).Distinct().Take(6).ToList();
                string lista2 = nomes.Count == 0 ? "um pouco de tudo" : string.Join(", ", nomes.Take(nomes.Count - 1)) + (nomes.Count > 1 ? " e " : "") + nomes.Last();
                lista.Add(new Pergunta("O que vocês vendem aqui?",
                    $"Temos {lista2}. O preço está na etiqueta de cada prateleira; é só levar ao caixa.",
                    $"De tudo um pouco: {lista2}. Corre que está acabando!"));
                if (_loja.Tipo == LojaDoDia.Armas)
                    lista.Add(new Pergunta("Por que as armas estão tão caras?",
                        "Desde o anúncio do governo, todo mundo quer uma. A pistola sai pelas economias de uma vida; as longas, só para quem tem bala na agulha.",
                        "Oferta e procura, amigo. E eu não vendo para qualquer um. Munição, sim: essa ainda dá."));
                else if (_loja.Tipo == LojaDoDia.Farmacia)
                    lista.Add(new Pergunta("O que é bom ter em casa?",
                        "Bandagem, gaze e álcool para os cortes; o kit médico, se der. E o antibiótico, mas só dois por pessoa.",
                        "Tala para quebradura, atadura, remédio para febre. E calmante: tem gente sem dormir com esse noticiário."));
                else
                    lista.Add(new Pergunta("O movimento está grande hoje?",
                        _loja.Tipo == LojaDoDia.Mercado || _loja.Tipo == LojaDoDia.Posto
                            ? "Desde cedo. As prateleiras de água e enlatados já estão quase vazias."
                            : "Desde cedo. Metade das prateleiras já foi embora.",
                        "Nunca vi assim. Parece véspera de furacão."));
            }
            else
            {
                lista.Add(new Pergunta("Onde ficam as lojas?", OndeFicamAsLojas()));
                lista.Add(new Pergunta("Vai se preparar para a quarentena?",
                    "Vou. Comprei lenha, água e pilhas. Se a luz cair, ninguém me pega de surpresa.",
                    _mulher ? "Meu marido acha exagero. Eu já separei os remédios e as lanternas."
                            : "Minha mulher acha exagero. Eu já separei os remédios e as lanternas.",
                    "A polícia fechou as estradas que saem da cidade. Ninguém entra, ninguém sai. Então é ficar em casa."));
            }
            lista.Add(new Pergunta("Tchau.") { fecha = true });
            return lista;
        }

        // as duas lojas mais perto: a direção (pelo mapa: o +z é o norte) e a distância
        string OndeFicamAsLojas()
        {
            // (24/11/2026: pelas lojas do mapa, montadas ou não: as de longe só são montadas quando alguém chega perto)
            var w = WoodcreekDoDiaZero.Atual;
            var lojas = w != null ? w.LojasNoMapa().ToList() : null;
            if (lojas == null || lojas.Count == 0) return "Na rodovia, no comércio da vila. Não tem erro.";
            var eu = transform.position;
            var partes = new List<string>();
            foreach (var (tipo, ponto) in lojas.OrderBy(l => (l.ponto - eu).sqrMagnitude).Take(3))
            {
                var d = ponto - eu;
                d.y = 0f;
                partes.Add($"{ComArtigo(tipo)} fica a uns {Mathf.Max(10f, Mathf.Round(d.magnitude / 10f) * 10f):0} m, {Rumo(d)}");
            }
            string texto = string.Join("; ", partes) + ".";
            return char.ToUpperInvariant(texto[0]) + texto.Substring(1);
        }

        static string ComArtigo(LojaDoDia t) => t switch
        {
            LojaDoDia.Mercado => "o mercado",
            LojaDoDia.Farmacia => "a farmácia",
            LojaDoDia.Armas => "a loja de armas",
            LojaDoDia.Ferragens => "a loja de ferragens e autopeças",
            LojaDoDia.Roupas => "a loja de roupas",
            _ => "a loja do posto",
        };

        static string Rumo(Vector3 d)
        {
            float a = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            if (a < 0f) a += 360f;
            string[] rumos = { "para o norte", "para o nordeste", "para o leste", "para o sudeste", "para o sul", "para o sudoeste", "para o oeste", "para o noroeste" };
            return rumos[Mathf.RoundToInt(a / 45f) % 8];
        }

        static string Um(params string[] falas) => falas[Random.Range(0, falas.Length)];
    }
}
