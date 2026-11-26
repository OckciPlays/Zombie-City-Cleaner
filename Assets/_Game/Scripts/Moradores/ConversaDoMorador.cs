using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Moradores
{
    /// <summary>
    /// A conversa com um morador do bunker (25/11/2026, a etapa 8, a pedido do Pedro: os moradores conversando). Em todas as
    /// máquinas, pela <see cref="FalaDoMorador"/> do corpo: o nome e a função; a saudação pelo humor e pelo que falta (a fome,
    /// a sede, o sono); e as perguntas "Quem é você?" (a idade e o que fazia antes), "O que você faz aqui?" (a função),
    /// "Como estão as coisas?" (como ele está), "E nesses cinco anos?" (o que ele viveu lá fora) e "Tchau". Enquanto a tela
    /// está aberta, o anfitrião o segura parado (e, em pé, virado para quem fala); depois, o horário dele anda o tempo que ficou
    /// parado (MoradoresDoBunker.Conversa.cs).
    /// O Ygayr Franscheschy, o fumante da sala comum do B1, tem as falas dele: umas 30, cansadas do fim do mundo, e de vez em
    /// quando uma das teorias (os aliens, os lobisomens, a lua, o governo).
    /// </summary>
    public class ConversaDoMorador : MonoBehaviour, IConversavel
    {
        public CorpoDoMorador corpo;

        FalaDoMorador F => corpo != null ? corpo.Fala.Value : default;
        bool Mulher => F.mulher;
        string O => Mulher ? "a" : "o";

        public string NomeNaConversa
        {
            get
            {
                var f = F;
                string nome = f.Pronta ? f.nomeCompleto.ToString() : corpo != null ? corpo.Nome : "?";
                string papel = f.cargo.Length > 0 ? f.cargo.ToString().ToLowerInvariant() : f.ygayr ? "o fumante da sala comum" : Funcao(f.funcao, f.mulher);
                return string.IsNullOrEmpty(papel) ? nome : $"{nome}, {papel}";
            }
        }

        public Vector3 Onde => transform.position;
        // (25/11/2026, a revisão: só com a fala já escrita; antes dela, a tela abria com "Ygayr, guarda", a saudação de um
        // morador triste e faminto e "Quem é você?" respondendo ".". O anfitrião escreve a fala junto com o corpo novo)
        public bool PodeConversar => corpo != null && corpo.IsSpawned && corpo.Fala.Value.Pronta;

        public void AoConversar(bool comecou)
        {
            if (corpo != null && corpo.IsSpawned) corpo.PedirConversa(comecou);
        }

        // ------------------------------------------------------------------ a saudação

        public string Saudacao()
        {
            var f = F;
            // (25/11/2026, a revisão) dormindo, ele acorda de mau jeito (antes, respondia acordado, de olhos fechados)
            if (f.dormindo)
                return (AtividadeDoMorador)f.atividade == AtividadeDoMorador.Cochilo
                    ? Um("Hã? Eu só estava descansando os olhos...", "Hmm... Cochilei. Já é tarde?", "Zzz... Hã? Ah, é você.")
                    : Um("Hm...? Ah, é você. Eu estava dormindo.", "Zzz... Hã? Agora não, me deixa dormir.", "Já amanheceu? Não? Então me deixa dormir.");
            if (f.ygayr) return Sorteada(FalasDoYgayr);
            if (f.crise != 0) return "Agora não... me deixa um pouco.";
            var faixa = HumorDoMorador.Faixa(f.humor);
            string comeco = faixa == FaixaDoHumor.Triste ? Um("Hum. Oi.", "Oi... não repara, hoje não estou bem.", "Fala.")
                          : faixa == FaixaDoHumor.Feliz ? Um("Ei! Bom te ver por aqui.", "Opa! Tudo certo com você?", "Olha quem apareceu!")
                          : Um("Oi.", "E aí?", "Opa.");
            string falta = f.fome < 30 ? " Estou com uma fome danada." : f.sede < 30 ? " Preciso de água." : f.sono < 25 ? " Mal consigo ficar de olho aberto." : "";
            return comeco + falta;
        }

        public List<Pergunta> Perguntas()
        {
            var f = F;
            var lista = new List<Pergunta>();
            // (25/11/2026, a revisão) quem foi acordado não puxa assunto: só dá para deixá-lo dormir
            if (f.dormindo)
            {
                lista.Add(new Pergunta("Desculpa, pode voltar a dormir.") { fecha = true });
                return lista;
            }
            if (f.ygayr)
            {
                lista.Add(new Pergunta("Quem é você?",
                    $"Ygayr Franscheschy. Dava aula de filosofia, quando ainda tinha para quem. Hoje eu fumo. É o que sobrou de mim, e é honesto."));
                lista.Add(new Pergunta("No que você está pensando?", Sorteada(FalasDoYgayr), Sorteada(FalasDoYgayr), Sorteada(FalasDoYgayr)));
                lista.Add(new Pergunta("Você acredita em alguma coisa?", Sorteada(TeoriasDoYgayr), Sorteada(TeoriasDoYgayr)));
                lista.Add(new Pergunta("Não quer sair dessa cadeira um pouco?",
                    "Sair para onde? Lá fora é o fim do mundo; aqui dentro é o fim do mundo com cadeira. Eu escolhi a cadeira.",
                    "Já saí uma vez. Fiquei oito meses num porão ouvindo eles andarem em cima de mim. A cadeira é melhor."));
                lista.Add(new Pergunta("Tchau.") { fecha = true });
                return lista;
            }
            string idade = f.idade > 0 ? $", {f.idade} anos" : "";
            string antes = f.antes.Length > 0 ? $" Antes de tudo isso, eu era {Minusculo(f.antes.ToString())}." : "";
            lista.Add(new Pergunta("Quem é você?", $"{F.nomeCompleto}{idade}.{antes}"));
            lista.Add(new Pergunta("O que você faz aqui?", OQueFaz(f)));
            lista.Add(new Pergunta("Como estão as coisas?", ComoEsta(f)));
            string depois = Depois(f.depoisChave.ToString());
            if (!string.IsNullOrEmpty(depois)) lista.Add(new Pergunta("E nesses cinco anos?", depois));
            lista.Add(new Pergunta("Tchau.") { fecha = true });
            return lista;
        }

        // ------------------------------------------------------------------ as respostas

        static string Funcao(int h, bool mulher) => (Habilidade)h switch
        {
            Habilidade.Combate => "guarda",
            Habilidade.Cozinha => mulher ? "cozinheira" : "cozinheiro",
            Habilidade.Medicina => mulher ? "enfermeira" : "enfermeiro",
            Habilidade.Social => mulher ? "conselheira" : "conselheiro",
            Habilidade.Mecanica => mulher ? "mecânica" : "mecânico",
            Habilidade.Construcao => "faz-tudo",
            Habilidade.Plantio => mulher ? "horticultora" : "horticultor",
            Habilidade.Oficio => mulher ? "artesã" : "artesão",
            _ => "",
        };

        static string OQueFaz(FalaDoMorador f)
        {
            if (f.cargo.Length > 0 && f.cargo.ToString() == SorteioDosMoradores.CargoDoLider)
                return "Cuido de todo mundo aqui dentro. Alguém tem que decidir as coisas, e quem abriu o portão fui eu.";
            return (Habilidade)f.funcao switch
            {
                Habilidade.Combate => "Fico de olho no portão e treino quem quer aprender a se defender.",
                Habilidade.Cozinha => "Cozinho para todo mundo. Com o que tem, não com o que eu queria.",
                Habilidade.Medicina => "Cuido de quem se machuca ou adoece. Remédio é o que mais falta.",
                Habilidade.Social => "Converso com o pessoal e apazíguo as brigas. Aqui embaixo, a cabeça pesa tanto quanto a fome.",
                Habilidade.Mecanica => "Mantenho o gerador e os motores andando. Se ele para, a gente para junto.",
                Habilidade.Construcao => "Conserto o que quebra: parede, porta, cano. Sempre tem alguma coisa.",
                Habilidade.Plantio => "Cuido da horta. Não dá para tudo, mas uma folha verde levanta o ânimo de qualquer um.",
                Habilidade.Oficio => "Faço e conserto o que dá com as mãos: roupa, ferramenta, móvel.",
                _ => "Ajudo no que precisar.",
            };
        }

        string ComoEsta(FalaDoMorador f)
        {
            if (f.crise != 0) return "Não está nada bem. Eu só... preciso de um tempo.";
            var faixa = HumorDoMorador.Faixa(f.humor);
            string base_ = faixa == FaixaDoHumor.Triste
                ? Um("Mal. Cinco anos aqui embaixo pesam.", "Tem dia que eu nem queria levantar.", "Difícil. Cada dia parece igual ao outro.")
                : faixa == FaixaDoHumor.Feliz
                    ? Um("Bem, na medida do possível! Hoje foi um bom dia.", "Melhor do que eu esperava. A gente se ajuda.", "Animad" + O + ". Sei lá, hoje o ar parece mais leve.")
                    : Um("Vai indo. Um dia de cada vez.", "Dá para aguentar.", "Normal. Se é que alguma coisa ainda é normal.");
            var falta = new List<string>();
            if (f.fome < 30) falta.Add("a fome aperta");
            // (25/11/2026, a revisão: o "também" só depois da fome; sozinha, saía "E a sede também.", e é o caso mais comum,
            // porque a sede desce mais rápido)
            if (f.sede < 30) falta.Add(falta.Count > 0 ? "a sede também" : "a sede aperta");
            if (f.sono < 25) falta.Add("quase não dormi");
            if (falta.Count == 0) return base_;
            string lista = falta.Count == 1 ? falta[0] : string.Join(", ", falta.GetRange(0, falta.Count - 1)) + " e " + falta[falta.Count - 1];
            return $"{base_} E {lista}.";
        }

        // o que ele viveu nos 5 anos, em primeira pessoa (a chave da história de depois)
        string Depois(string chave) => chave switch
        {
            ListasDosMoradores.ComAFamilia => "Desci para cá com a minha família no Dia 0. Quase não vi o lado de fora desde então.",
            "SozinhoNoMato" => $"Fiquei sozinh{O} no mato, comendo o que achava. Um dia vi a fumaça do bunker e vim atrás.",
            "AcampamentoCaiu" => "Morava num acampamento perto da rodovia. Até a horda passar por lá. Não sobrou quase ninguém.",
            "Saqueador" => "Vivi de saquear as casas largadas. Não tenho orgulho, mas também não estaria aqui se não fosse isso.",
            "Feridos" => "Costurei ferida e baixei febre num abrigo de igreja, quase sem remédio. Vi gente demais ir embora.",
            "FerroVelho" => "Me escondi num ferro-velho e montava o que precisava com a sucata. Até hoje não jogo nada fora.",
            "Porao" => $"Passei meses trancad{O} num porão, ouvindo os passos deles lá em cima. Às vezes ainda ouço.",
            ListasDosMoradores.AbriuOBunker => "Abri o portão deste bunker para quem fugia. Não me arrependo, nem dos dias em que a comida faltou.",
            ListasDosMoradores.TrouxeOsSoldados => "Cheguei aqui com os soldados que sobraram. A gente veio proteger; acabou ficando.",
            "CacouZumbis" => "Caçava os Ocos para limpar as estradas. Depois de um tempo, a gente perde a conta e o sono.",
            "Resgatado" => "Minha família me tirou de um prédio cercado. Devo a vida a eles, e não deixo eles esquecerem.",
            "AbrigoDoGoverno" => "Estive num abrigo do governo. Fugi quando vi o que faziam com quem tossia.",
            "Estrada" => "Vivi na estrada, de carro em carro, até a gasolina acabar. Depois, a pé.",
            "Fazenda" => "Plantei escondid" + O + " numa fazenda. Milho, feijão e medo. Quando eles acharam a cerca, vim para cá.",
            SorteioDosMoradores.SeparadoDaFamilia => "Me perdi da minha família no Dia 0. Ainda procuro por eles em cada rosto que chega.",
            _ => "",
        };

        static string Minusculo(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

        static string Um(params string[] op) => op[Random.Range(0, op.Length)];

        // a fala do Ygayr: de vez em quando (uma em cinco), uma teoria
        static string Sorteada(string[] lista)
        {
            if (lista == FalasDoYgayr && Random.value < 0.2f) lista = TeoriasDoYgayr;
            return lista[Random.Range(0, lista.Length)];
        }

        // ------------------------------------------------------------------ o Ygayr

        static readonly string[] FalasDoYgayr =
        {
            "O mundo acabou e o cigarro continua com o mesmo gosto. Deve ser a única coisa honesta que sobrou.",
            "Sabe o que é pior que o fim do mundo? Descobrir que ele é chato. Os dias são todos iguais.",
            "Antes eu tinha medo de morrer. Agora eu tenho medo de durar.",
            "A gente passou a vida correndo atrás de coisa nenhuma. Agora corre de coisa nenhuma. Pelo menos melhorou o fôlego.",
            "Cada cigarro é um pequeno fim do mundo. Eu já assisti a uns três mil. Este aqui está bonito.",
            "Eles não pensam, não sentem, só andam. Às vezes eu olho para eles e me pergunto qual é a diferença.",
            "Eu dava aula sobre o sentido da vida. Ninguém prestava atenção. Agora ninguém precisa.",
            "A esperança é o último que morre, dizem. Aqui embaixo, ela está na fila, atrás de mim.",
            "Cinco anos. Já esqueci o cheiro de chuva. Lembro do cheiro de fumaça, mas esse eu trago comigo.",
            "Ouvi dizer que lá fora ainda tem sol. Eu prefiro acreditar. Dá menos trabalho do que ir ver.",
            "O silêncio daqui de baixo é diferente. Lá em cima o silêncio é de quem foi embora. Aqui é de quem ficou.",
            "Já pensou que a gente pode ser os últimos? Os últimos a reclamar da comida, os últimos a ter insônia...",
            "Eu conto as rachaduras do teto. Trinta e sete. Ontem eram trinta e seis. O mundo continua acabando, só que devagar.",
            "Não espere nada de ninguém. Assim, quando alguém te traz um café, parece um milagre.",
            "A gente sobreviveu ao fim do mundo para lavar a louça em turnos. A história tem senso de humor.",
            "O Gus diz que um dia a gente sai daqui. Eu digo que um dia a gente sai de tudo. Ele não gosta muito quando eu digo isso.",
            "Tem gente que reza. Tem gente que planta. Eu fumo. Cada um conversa com Deus do jeito que sabe.",
            "Eu tinha um gato. Ele foi embora antes de tudo. Talvez soubesse de alguma coisa.",
            "Toda noite eu sonho com uma rua cheia de gente. Acordo antes de saber se eram pessoas ou eles.",
            "O fim do mundo não veio com trombetas. Veio com uma gripe e um jornal das seis. Que coisa mais sem graça.",
            "A fumaça sobe e some. Igual a tudo. Pelo menos ela não reclama.",
            "Me pergunte de novo amanhã. Hoje eu não tenho resposta. Ontem também não tinha, mas estava mais animado para não ter.",
            "As pessoas acham que eu estou triste. Eu só estou atualizado.",
            "Quando acabar o cigarro do bunker, eu paro de fumar. Até lá, estou fazendo um estoque de pulmão ruim.",
            "Você já reparou como eles andam sempre para algum lugar? Pelo menos alguém aqui tem um objetivo.",
            "Eu escrevi um livro uma vez. Sobre o vazio. Esgotou. Literalmente: ninguém mais vai ler.",
            "Vocês saem, voltam, saem de novo. Eu fico aqui guardando a cadeira. Alguém tem que guardar alguma coisa.",
            "Antes do fim, a gente reclamava da segunda-feira. Hoje eu daria tudo por uma segunda-feira.",
            "O tempo aqui embaixo não passa. Ele só se acumula, feito a cinza no cinzeiro.",
            "Se um dia o mundo voltar ao normal, eu fico aqui. O normal nunca foi grande coisa.",
        };

        static readonly string[] TeoriasDoYgayr =
        {
            "Escuta essa: não foi uma gripe. Foram eles. Os de cima. As luzes no céu na semana antes do Dia 0... ninguém fala das luzes.",
            "Lobisomem existe, viu? Lua cheia, eu ouço os uivos do mato. Os Ocos param de andar nessas noites. Até eles têm medo.",
            "Repara na lua. Desde o surto ela está maior. Ou mais perto. Eu meço com o polegar toda noite. Quatro milímetros, em cinco anos.",
            "O governo sabia. O abrigo, o jornal das seis, os caminhões de máscara uma semana antes. Ninguém compra tanta máscara à toa.",
            "Os aliens estão esperando a gente acabar para levar o planeta de brinde. Por isso eu fumo: para ele vir com defeito.",
            "Já viu um Oco olhando para a lua? Eu já. Ficou parado a noite inteira. Eles sabem de alguma coisa que a gente não sabe.",
        };
    }
}
