using System;
using System.Collections.Generic;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>As 8 habilidades dos moradores (a decisão 3 do Plano_NPCs_Bunker.md da pasta de arte), na ordem da ficha.</summary>
    public enum Habilidade { Combate, Cozinha, Medicina, Social, Mecanica, Construcao, Plantio, Oficio }

    /// <summary>A paixão por uma habilidade: nenhuma, interessado (uma chama) ou apaixonado (duas).</summary>
    public enum Paixao { Nenhuma, Interessado, Apaixonado }

    /// <summary>O que o morador não faz (as incapacidades do RimWorld): vem da história, das condições e, às vezes, do acaso.</summary>
    [Flags]
    public enum Incapacidade
    {
        Nenhuma = 0,
        NaoCozinha = 1,
        NadaDeTrabalhoPesado = 2,
        NaoCuidaDeDoentes = 4,
        NaoLuta = 8,
    }

    /// <summary>
    /// O parente da família dos jogadores (a decisão 1): o primeiro dos NPCs do mundo é sempre um, e ele aparece na primeira
    /// mão das cartas (se não for escolhido, fica no mapa); o outro do casal dele também é (a mãe, se ele é o pai). A palavra
    /// vem do sexo: o pai ou a mãe, o avô ou a avó, o tio ou a tia.
    /// </summary>
    public enum ParenteDaFamilia { Nenhum, Pai, Avo, Tio }

    /// <summary>
    /// Um laço com outro morador (a decisão 6), visto deste: o <see cref="tipo"/> diz o que o outro é para este (o "Pai": o
    /// outro é o pai ou a mãe deste). O outro tem o laço inverso (<see cref="SorteioDosMoradores.Inverso"/>).
    /// </summary>
    [Serializable]
    public class LacoDoMorador
    {
        public const string Casal = "Casal", Pai = "Pai", Filho = "Filho", Irmao = "Irmao", Avo = "Avo", Neto = "Neto",
                            Primo = "Primo", Amigo = "Amigo", Ex = "Ex";

        public int outro;       // o id do outro morador
        public string tipo;     // o que o outro é para este
    }

    /// <summary>
    /// A aparência do morador, para o sorteio das roupas (o <c>RoupaDoMorador</c> do jogo): o tipo da roupa (os tipos do
    /// <c>Roupas.json</c>: o "Trabalhador", o "Idoso"...), a semente do sorteio dele, o tom de pele (o índice nos 6 do
    /// catálogo), a cor dos pelos (o índice na lista do tipo; o grisalho usa a do "Idoso") e se a barba fica de fora (os
    /// muito novos).
    /// </summary>
    [Serializable]
    public class AparenciaDoMorador
    {
        public string tipoDaRoupa;
        public int semente;
        public bool mulher;
        public int pele;
        public int cabelo;
        public bool grisalho;
        public bool semBarba;
    }

    /// <summary>
    /// A ficha de um morador do bunker (as decisões 2 a 6 do Plano_NPCs_Bunker.md da pasta de arte): sorteada uma vez
    /// (<see cref="SorteioDosMoradores"/>) e guardada no mundo. As histórias, os traços e as condições vão pelas chaves das
    /// listas (<see cref="ListasDosMoradores"/>), para o save não depender da ordem delas.
    /// </summary>
    [Serializable]
    public class FichaDoMorador
    {
        public int id;
        public string nome;
        public string apelido;          // vazio: nenhum
        public string sobrenome;
        public int idade;
        public bool mulher;
        public ParenteDaFamilia parente;
        public string antes;            // a chave da história de antes do apocalipse
        public string depois;           // a chave da história dos anos do apocalipse
        public int[] habilidades = new int[ListasDosMoradores.Habilidades];      // de 0 a 20
        public Paixao[] paixoes = new Paixao[ListasDosMoradores.Habilidades];
        public Incapacidade naoFaz;
        public List<string> tracos = new List<string>();        // as chaves
        public List<string> condicoes = new List<string>();     // as chaves
        public List<LacoDoMorador> lacos = new List<LacoDoMorador>();
        public AparenciaDoMorador aparencia = new AparenciaDoMorador();
        /// <summary>
        /// O grupo dele entre os NPCs do mundo (<see cref="NpcsDoMundo"/>): 0 é a família dos jogadores (o parente e quem
        /// veio com ele); os outros são as famílias e os bandos que vão estar pelo mapa (as comunidades, as bases, os perdidos).
        /// </summary>
        public int grupo;

        public string NomeCompleto => string.IsNullOrEmpty(apelido) ? $"{nome} {sobrenome}" : $"{nome} \"{apelido}\" {sobrenome}";

        public int Pontos(Habilidade h) => habilidades[(int)h];
        public Paixao PaixaoEm(Habilidade h) => paixoes[(int)h];
        public bool Tem(string traco) => tracos.Contains(traco);
        public bool TemCondicao(string condicao) => condicoes.Contains(condicao);
        public bool NaoFaz(Incapacidade i) => (naoFaz & i) != 0;

        /// <summary>A habilidade que ele não faz (a Cozinha de quem não cozinha): na ficha, aparece como "—".</summary>
        public bool Incapaz(Habilidade h) => (naoFaz & ListasDosMoradores.Bloqueio(h)) != 0;

        public LacoDoMorador LacoCom(int outro) => lacos.Find(l => l.outro == outro);
    }

    /// <summary>
    /// Os NPCs de um mundo (a tela das cartas, 04/11/2026, a ideia do amigo do usuário): os 52 sorteados com o mundo, em
    /// grupos (<see cref="FichaDoMorador.grupo"/>; o primeiro é o parente da família). Na tela das cartas, o anfitrião
    /// escolhe os 5 do bunker entre eles; os outros ficam no mapa, para serem descobertos. O <see cref="proximoId"/> dá o
    /// id de quem for sorteado depois.
    /// </summary>
    [Serializable]
    public class NpcsDoMundo
    {
        public int semente;
        public int proximoId = 1;
        public List<FichaDoMorador> fichas = new List<FichaDoMorador>();

        public FichaDoMorador DoId(int id) => fichas.Find(f => f.id == id);
        /// <summary>O parente da família (o primeiro sorteado).</summary>
        public FichaDoMorador Parente => fichas.Count > 0 ? fichas[0] : null;

        /// <summary>Os laços que existem entre eles (cada um contado uma vez).</summary>
        public int Lacos
        {
            get
            {
                int n = 0;
                foreach (var f in fichas) n += f.lacos.Count;
                return n / 2;
            }
        }
    }
}
