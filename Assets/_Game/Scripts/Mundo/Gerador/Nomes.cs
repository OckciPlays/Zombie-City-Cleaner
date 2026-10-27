using System.Collections.Generic;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Os nomes do mapa (Plano_Mapa, "As cidades": brasileiros e em português, decidido em 26/10/2026), juntando pedaços
    /// com o gênero certo (Santa Rita, São Jerônimo; "Velha" ou "Velho") e sem repetir no mesmo mapa: as cidades, as
    /// vilas, os bairros, as fazendas, as bases, as comunidades, os postos e o laboratório. Cada pedido usa o sorteio de
    /// quem pede (a mesma semente dá os mesmos nomes).
    /// </summary>
    public sealed class Nomes
    {
        readonly HashSet<string> _usados = new HashSet<string>();
        readonly HashSet<string> _comecosUsados = new HashSet<string>();

        // os começos das cidades, com o gênero (f = feminino) e se já são completos sozinhos
        static readonly (string nome, bool feminino)[] Comecos =
        {
            ("Santa Rita", true), ("Santa Luzia", true), ("Santa Cruz", true), ("Santa Bárbara", true), ("Santa Clara", true),
            ("Santa Helena", true), ("Santa Teresa", true), ("Santana", true), ("São Jerônimo", false), ("São Bento", false),
            ("São Tomé", false), ("São Vicente", false), ("São Gabriel", false), ("São Pedro", false), ("São José", false),
            ("São Sebastião", false), ("Pedra Branca", true), ("Pedra Azul", true), ("Campo Belo", false), ("Campo Alegre", false),
            ("Monte Alto", false), ("Monte Verde", false), ("Porto Seguro", false), ("Porto Novo", false), ("Lagoa Seca", true),
            ("Lagoa Funda", true), ("Águas Claras", true), ("Três Lagoas", true), ("Barro Preto", false), ("Cerro Azul", false),
            ("Bom Jardim", false), ("Bom Retiro", false), ("Boa Vista", true), ("Boa Esperança", true), ("Alto Paraíso", false),
            ("Ribeirão Fundo", false), ("Pinhal", false), ("Cachoeira", true), ("Itaquara", true), ("Ibitinga", true),
            ("Guaratuba", true), ("Itapuã", true), ("Jacarezinho", false), ("Nova Esperança", true), ("Nova Aurora", true),
            ("Vale Verde", false), ("Ouro Fino", false), ("Rio Claro", false), ("Ponte Alta", true), ("Cruz Alta", true),
        };

        static readonly string[] FinsDeCidade =
        {
            "do Vale", "da Serra", "das Pedras", "do Sul", "do Norte", "dos Campos", "da Mata", "do Oeste", "das Flores",
            "dos Pinhais", "da Colina", "do Rio",
        };

        static readonly string[] ComecosDeBairro = { "Jardim", "Vila", "Parque", "Recanto", "Conjunto", "Alto" };

        static readonly string[] FinsDeBairro =
        {
            "das Flores", "Primavera", "Esperança", "Santa Clara", "dos Pinheiros", "Bela Vista", "Operária", "Industrial",
            "São Jorge", "das Acácias", "Aurora", "Boa Esperança", "Planalto", "dos Ipês", "Paraíso", "América", "Europa",
            "Morada do Sol", "São Cristóvão", "Santo Antônio", "das Palmeiras", "Novo Horizonte", "dos Lagos", "Itamaraty",
            "Imperial", "Floresta", "Califórnia", "São Francisco", "das Hortênsias", "Monte Castelo",
        };

        static readonly string[] Fazendas =
        {
            "Santa Clara", "Boa Vista", "Três Irmãos", "São Joaquim", "Santo Antônio", "Água Limpa", "Bom Sucesso",
            "Monte Alegre", "Barreiro", "Cachoeirinha", "Sete Lagoas", "São Lourenço", "Palmital", "Capão Bonito",
            "Recreio", "Pau d'Alho", "Campestre", "Sertãozinho", "Bela Aliança", "Rio Fundo", "Santa Fé", "Tabuleiro",
            "Floresta", "Serrinha", "Paineiras", "Jatobá", "Cedro", "Estrela", "Aroeira", "Ipiranga", "Vista Alegre",
            "Bom Retiro",
        };

        static readonly string[] Bases =
        {
            "Base Militar Serra Negra", "Quartel do 12º BI", "Base Militar Ponte Alta", "Batalhão de Fronteira",
            "Base Aérea do Planalto", "Quartel do 8º BEC", "Base Militar Campo Grande", "Forte da Colina",
        };

        static readonly string[] Aliadas =
        {
            "Refúgio Esperança", "Colônia Nova Vida", "Abrigo São Francisco", "Comunidade do Pinhal", "Refúgio da Serra",
            "Colônia Boa Vontade", "Abrigo Santa Marta", "Comunidade Recomeço", "Vila dos Sobreviventes", "Acampamento Aurora",
        };

        static readonly string[] Inimigas =
        {
            "Os Abutres", "Bando da Serra", "Os Corvos", "Os Ferros", "Clã do Lobo", "Os Ratos do Rio", "Bando do Velho",
            "Os Carniceiros", "Os Coiotes", "Os Cães de Guerra",
        };

        static readonly string[] Postos =
        {
            "Posto Estrela", "Posto Avenida", "Auto Posto Rodovia", "Posto Bandeirante", "Posto São Cristóvão",
            "Posto Trevo", "Posto Caminhoneiro", "Posto Serra Azul", "Posto do Lago", "Auto Posto Central",
            "Posto Boa Viagem", "Posto Rota Sul", "Posto Pioneiro", "Posto Três Irmãos", "Posto Ipê", "Posto Garça",
            "Posto Aliança", "Posto Mirante", "Posto Cruzeiro", "Posto Horizonte", "Auto Posto Planalto", "Posto Sabiá",
        };

        static readonly string[] Laboratorios = { "Instituto Prometeu", "Laboratórios Hélix", "Centro de Pesquisa Ômega", "Instituto Quimera" };

        bool Novo(string nome) => _usados.Add(nome);

        static T Um<T>(Sorteio s, IList<T> lista) => lista[s.Entre(0, lista.Count)];

        /// <summary>O nome de uma cidade ou vila: o começo, às vezes com um fim ("Santa Rita do Vale").</summary>
        public string Cidade(Sorteio s, bool comFim)
        {
            for (int tentativa = 0; tentativa < 200; tentativa++)
            {
                var (comeco, feminino) = Um(s, Comecos);
                // cada começo uma vez só no mapa (nada de "São Pedro" e "São Pedro da Serra" juntas)
                if (_comecosUsados.Contains(comeco)) continue;
                string nome = comeco;
                if (comFim || s.Chance(0.35f))
                {
                    string fim = s.Chance(0.12f) ? (feminino ? "Velha" : "Velho") : Um(s, FinsDeCidade);
                    // nada de "Campo Belo dos Campos" nem "Pedra Azul das Pedras": o começo não pode ter o radical do fim
                    var palavras = fim.Split(' ');
                    string radical = palavras[palavras.Length - 1].ToLowerInvariant();
                    radical = radical.Substring(0, System.Math.Min(4, radical.Length));
                    if (!comeco.ToLowerInvariant().Contains(radical)) nome = comeco + " " + fim;
                }
                if (!Novo(nome)) continue;
                _comecosUsados.Add(comeco);
                return nome;
            }
            return Repetido("Cidade");
        }

        /// <summary>O nome de um bairro ("Jardim das Flores", "Vila Operária").</summary>
        public string Bairro(Sorteio s)
        {
            for (int tentativa = 0; tentativa < 200; tentativa++)
            {
                string nome = Um(s, ComecosDeBairro) + " " + Um(s, FinsDeBairro);
                if (Novo(nome)) return nome;
            }
            return Repetido("Bairro");
        }

        public string Fazenda(Sorteio s)
        {
            for (int tentativa = 0; tentativa < 100; tentativa++)
            {
                string nome = (s.Chance(0.3f) ? "Sítio " : "Fazenda ") + Um(s, Fazendas);
                if (Novo(nome)) return nome;
            }
            return Repetido("Fazenda");
        }

        public string Base(Sorteio s) => DaLista(s, Bases, "Base Militar");
        public string Aliada(Sorteio s) => DaLista(s, Aliadas, "Refúgio");
        public string Inimiga(Sorteio s) => DaLista(s, Inimigas, "Bando");
        public string Posto(Sorteio s) => DaLista(s, Postos, "Posto");
        public string Laboratorio(Sorteio s) => DaLista(s, Laboratorios, "Laboratório");

        string DaLista(Sorteio s, IList<string> lista, string generico)
        {
            for (int tentativa = 0; tentativa < 100; tentativa++)
            {
                string nome = Um(s, lista);
                if (Novo(nome)) return nome;
            }
            return Repetido(generico);
        }

        // a lista acabou: numera
        string Repetido(string generico)
        {
            for (int n = 2; ; n++)
                if (Novo($"{generico} {n}")) return $"{generico} {n}";
        }
    }
}
