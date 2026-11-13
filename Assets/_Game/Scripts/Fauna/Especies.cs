using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// A fauna dos EUA (12/11/2026, a pedido; o plano no Docs/19-Fauna.md). A ordem é o número que vai pela rede: novas no
    /// fim. Os modelos de hoje são caixas (ModelosDosBichos), menos o veado; o Pedro troca todos depois.
    /// </summary>
    public enum Especie : byte
    {
        Veado, Alce, Lobo, Coiote, UrsoNegro, UrsoPardo, Coelho, Castor, Pato, Ganso, Aguia, Urubu, Sapo, Cascavel,
        Guaxinim, Gamba, Esquilo, Peru, Salmao,
    }

    /// <summary>O adulto (os que não mudam pelo sexo), o macho, a fêmea e o filhote.</summary>
    public enum Variante : byte { Adulto, Macho, Femea, Filhote }

    /// <summary>O jeito do corpo, para a animação e o movimento.</summary>
    public enum Corpo : byte { Quadrupede, Ave, Cobra, Peixe, Sapo }

    /// <summary>O jeito de viver: em rebanho (o alfa protege), em matilha, sozinho, ou pequeno (só foge).</summary>
    public enum Vida : byte { Rebanho, Matilha, Sozinho, Pequeno }

    /// <summary>
    /// O papel na cadeia (a etapa 2, 12/11/2026): a presa foge (Bicho.Mente.cs); a matilha caça as presas e, à noite, quem
    /// está ferido ou sozinho (Bicho.Matilha.cs); o urso avisa, às vezes finge e às vezes ataca (Bicho.Urso.cs).
    /// </summary>
    public enum Papel : byte { Presa, Matilha, Urso }

    /// <summary>
    /// Onde anda (a etapa 3, 12/11/2026): na terra (a água funda é parede); na água (o peixe: a terra é parede); anfíbio
    /// (nada onde não dá pé: o alce, o castor; e as aves da água boiam); e no ar (a águia, sempre voando).
    /// </summary>
    public enum Meio : byte { Terra, Agua, Anfibio, Ar }

    /// <summary>
    /// O que cada espécie é: o tamanho, a vida, as velocidades, os sentidos, o grupo, o que a carcaça dá e onde vive. Os
    /// números são para o Pedro ajustar.
    /// </summary>
    public sealed class DadosDaEspecie
    {
        public Especie especie;
        public string nome, comArtigo, plural;
        public Corpo corpo = Corpo.Quadrupede;
        public Vida vida = Vida.Rebanho;
        public Papel papel = Papel.Presa;
        public Meio meio = Meio.Terra;
        /// <summary>A altura do lombo (m) e o comprimento do corpo (m), do adulto.</summary>
        public float altura, comprimento;
        /// <summary>A vida do adulto (o zumbi tem 100).</summary>
        public float pontos;
        /// <summary>As velocidades (m/s): andando, trotando e correndo.</summary>
        public float andar, trotar, correr;
        /// <summary>Os sentidos (m): até onde vê quem se mexe, ouve quem corre e sente o cheiro de quem está a favor do vento.</summary>
        public float visao, ouvido, faro;
        /// <summary>A distância de fuga (m): mais perto que isso, foge na hora.</summary>
        public float fuga;
        public int grupoMin = 1, grupoMax = 1;
        /// <summary>O que sai ao carnear o adulto (o filhote dá a metade, arredondada para cima).</summary>
        public int carne, couro, gordura;
        public float segundosParaCarnear = 6f;
        /// <summary>Onde vive (as manchas do mapa).</summary>
        public Mancha[] habitats;
        /// <summary>Ativo de noite (o guaxinim, o gambá); os outros, de dia e no crepúsculo.</summary>
        public bool noturno;
        /// <summary>Gancho para o futuro: o filhote órfão dá para domar (o lobo e os ursos).</summary>
        public bool domavel;
        /// <summary>Sai no mundo sozinho (a etapa 1: o veado; a 2: os predadores e o coelho); os outros só pelo F1, por enquanto.</summary>
        public bool noMundo;
        /// <summary>
        /// No mundo: quantos grupos em volta de cada jogador, e a chance de um grupo que falta nascer em cada volta do
        /// povoamento (a cada 2,5 s): 1, logo; 0,02, em ~2 minutos. Os predadores são raros.
        /// </summary>
        public int gruposPorJogador = 1;
        public float raridade = 1f;
        /// <summary>A mordida (os predadores e o urso, no jogador e na presa) e o tempo entre um bote e outro.</summary>
        public float mordida;

        public string NomeDaVariante(Variante v) => v switch
        {
            Variante.Macho => nome + " macho",
            Variante.Femea => nome + " fêmea",
            Variante.Filhote => "filhote de " + nome,
            _ => nome,
        };
    }

    public static class Especies
    {
        public static readonly int Quantas = System.Enum.GetValues(typeof(Especie)).Length;

        static DadosDaEspecie[] _dados;

        public static DadosDaEspecie De(Especie e)
        {
            if (_dados == null) Montar();
            return _dados[(int)e];
        }

        static readonly Mancha[] Campo = { Mancha.Campo, Mancha.Rebrota, Mancha.Lavoura };
        static readonly Mancha[] BeiraDaMata = { Mancha.Campo, Mancha.Rebrota, Mancha.Mista, Mancha.Outono, Mancha.Beira, Mancha.Lavoura };
        static readonly Mancha[] Mata = { Mancha.Mista, Mancha.Coniferas, Mancha.Outono, Mancha.Pinheiral };
        static readonly Mancha[] Lago = { Mancha.Beira };
        static readonly Mancha[] Cidade = { Mancha.Quintal, Mancha.Baldio };

        static DadosDaEspecie D(Especie e, string nome, string artigo, string plural) =>
            new DadosDaEspecie { especie = e, nome = nome, comArtigo = artigo, plural = plural };

        static void Montar()
        {
            _dados = new DadosDaEspecie[Quantas];
            void Por(DadosDaEspecie d) => _dados[(int)d.especie] = d;

            var veado = D(Especie.Veado, "veado", "o veado", "veados");
            veado.altura = 0.95f; veado.comprimento = 1.7f; veado.pontos = 120f;
            veado.andar = 1.3f; veado.trotar = 3.5f; veado.correr = 12f;
            veado.visao = 60f; veado.ouvido = 30f; veado.faro = 70f; veado.fuga = 40f;
            veado.grupoMin = 4; veado.grupoMax = 9;
            veado.carne = 4; veado.couro = 1; veado.gordura = 1; veado.segundosParaCarnear = 8f;
            veado.habitats = BeiraDaMata; veado.noMundo = true; veado.gruposPorJogador = 2;
            Por(veado);

            var alce = D(Especie.Alce, "alce", "o alce", "alces");
            alce.altura = 1.8f; alce.comprimento = 2.8f; alce.pontos = 400f;
            alce.andar = 1.4f; alce.trotar = 4f; alce.correr = 13f;
            alce.visao = 45f; alce.ouvido = 30f; alce.faro = 80f; alce.fuga = 25f;
            alce.grupoMin = 1; alce.grupoMax = 3;
            alce.carne = 10; alce.couro = 2; alce.gordura = 3; alce.segundosParaCarnear = 15f;
            alce.habitats = new[] { Mancha.Beira, Mancha.Mista, Mancha.Coniferas };
            alce.meio = Meio.Anfibio; alce.noMundo = true; alce.raridade = 0.02f; alce.mordida = 35f;
            Por(alce);

            var lobo = D(Especie.Lobo, "lobo-cinzento", "o lobo", "lobos");
            lobo.vida = Vida.Matilha; lobo.altura = 0.8f; lobo.comprimento = 1.4f; lobo.pontos = 90f;
            lobo.andar = 1.5f; lobo.trotar = 4f; lobo.correr = 15f;
            lobo.visao = 60f; lobo.ouvido = 45f; lobo.faro = 120f; lobo.fuga = 30f;
            lobo.grupoMin = 4; lobo.grupoMax = 7; lobo.carne = 2; lobo.couro = 1; lobo.segundosParaCarnear = 6f;
            lobo.habitats = new[] { Mancha.Coniferas, Mancha.Mista, Mancha.Serra }; lobo.domavel = true;
            lobo.papel = Papel.Matilha; lobo.mordida = 12f; lobo.noMundo = true; lobo.raridade = 0.02f;
            Por(lobo);

            var coiote = D(Especie.Coiote, "coiote", "o coiote", "coiotes");
            coiote.vida = Vida.Matilha; coiote.altura = 0.6f; coiote.comprimento = 1.1f; coiote.pontos = 60f;
            coiote.andar = 1.4f; coiote.trotar = 3.5f; coiote.correr = 15f;
            coiote.visao = 50f; coiote.ouvido = 40f; coiote.faro = 100f; coiote.fuga = 40f;
            coiote.grupoMin = 3; coiote.grupoMax = 5; coiote.carne = 1; coiote.couro = 1; coiote.segundosParaCarnear = 5f;
            coiote.habitats = new[] { Mancha.Campo, Mancha.Rebrota, Mancha.Lavoura, Mancha.Baldio };
            coiote.papel = Papel.Matilha; coiote.mordida = 8f; coiote.noMundo = true; coiote.raridade = 0.035f;
            Por(coiote);

            var negro = D(Especie.UrsoNegro, "urso-negro", "o urso-negro", "ursos-negros");
            negro.vida = Vida.Sozinho; negro.altura = 0.95f; negro.comprimento = 1.7f; negro.pontos = 350f;
            negro.andar = 1.2f; negro.trotar = 3f; negro.correr = 13f;
            negro.visao = 30f; negro.ouvido = 30f; negro.faro = 150f; negro.fuga = 25f;
            negro.grupoMin = 1; negro.grupoMax = 3; negro.carne = 6; negro.couro = 2; negro.gordura = 4; negro.segundosParaCarnear = 14f;
            negro.habitats = new[] { Mancha.Mista, Mancha.Coniferas, Mancha.Beira }; negro.domavel = true;
            negro.papel = Papel.Urso; negro.mordida = 30f; negro.noMundo = true; negro.raridade = 0.015f;
            Por(negro);

            var pardo = D(Especie.UrsoPardo, "urso-pardo", "o urso-pardo", "ursos-pardos");
            pardo.vida = Vida.Sozinho; pardo.altura = 1.15f; pardo.comprimento = 2.1f; pardo.pontos = 600f;
            pardo.andar = 1.3f; pardo.trotar = 3.2f; pardo.correr = 14f;
            pardo.visao = 30f; pardo.ouvido = 30f; pardo.faro = 160f; pardo.fuga = 15f;
            pardo.grupoMin = 1; pardo.grupoMax = 2; pardo.carne = 9; pardo.couro = 3; pardo.gordura = 5; pardo.segundosParaCarnear = 18f;
            pardo.habitats = new[] { Mancha.Serra, Mancha.Coniferas }; pardo.domavel = true;
            pardo.papel = Papel.Urso; pardo.mordida = 45f; pardo.noMundo = true; pardo.raridade = 0.01f;
            Por(pardo);

            var coelho = D(Especie.Coelho, "coelho", "o coelho", "coelhos");
            coelho.vida = Vida.Pequeno; coelho.altura = 0.25f; coelho.comprimento = 0.4f; coelho.pontos = 15f;
            coelho.andar = 0.8f; coelho.trotar = 3f; coelho.correr = 12f;
            coelho.visao = 30f; coelho.ouvido = 25f; coelho.faro = 20f; coelho.fuga = 18f;
            coelho.grupoMin = 1; coelho.grupoMax = 3; coelho.carne = 1; coelho.segundosParaCarnear = 3f;
            coelho.habitats = new[] { Mancha.Campo, Mancha.Rebrota, Mancha.Lavoura, Mancha.Quintal, Mancha.Baldio };
            coelho.noMundo = true; coelho.gruposPorJogador = 3; coelho.raridade = 0.4f;
            Por(coelho);

            var castor = D(Especie.Castor, "castor", "o castor", "castores");
            castor.vida = Vida.Pequeno; castor.altura = 0.3f; castor.comprimento = 1.0f; castor.pontos = 30f;
            castor.andar = 0.8f; castor.trotar = 1.5f; castor.correr = 3f;
            castor.visao = 15f; castor.ouvido = 20f; castor.faro = 20f; castor.fuga = 15f;
            castor.grupoMin = 2; castor.grupoMax = 4; castor.carne = 1; castor.couro = 1; castor.gordura = 1; castor.segundosParaCarnear = 5f;
            castor.habitats = Lago;
            castor.meio = Meio.Anfibio; castor.noMundo = true; castor.raridade = 0.05f;
            Por(castor);

            var pato = D(Especie.Pato, "pato", "o pato", "patos");
            pato.corpo = Corpo.Ave; pato.vida = Vida.Pequeno; pato.altura = 0.35f; pato.comprimento = 0.5f; pato.pontos = 10f;
            pato.andar = 0.6f; pato.trotar = 1.2f; pato.correr = 12f;
            pato.visao = 40f; pato.ouvido = 25f; pato.faro = 10f; pato.fuga = 25f;
            pato.grupoMin = 4; pato.grupoMax = 8; pato.carne = 1; pato.segundosParaCarnear = 3f; pato.habitats = Lago;
            pato.meio = Meio.Anfibio; pato.noMundo = true; pato.raridade = 0.2f; pato.gruposPorJogador = 2;
            Por(pato);

            var ganso = D(Especie.Ganso, "ganso", "o ganso", "gansos");
            ganso.corpo = Corpo.Ave; ganso.vida = Vida.Pequeno; ganso.altura = 0.6f; ganso.comprimento = 0.8f; ganso.pontos = 15f;
            ganso.andar = 0.7f; ganso.trotar = 1.5f; ganso.correr = 13f;
            ganso.visao = 40f; ganso.ouvido = 25f; ganso.faro = 10f; ganso.fuga = 18f;
            ganso.grupoMin = 4; ganso.grupoMax = 10; ganso.carne = 2; ganso.segundosParaCarnear = 4f;
            ganso.habitats = new[] { Mancha.Beira, Mancha.Campo };
            ganso.meio = Meio.Anfibio; ganso.noMundo = true; ganso.raridade = 0.1f; ganso.mordida = 3f;
            Por(ganso);

            var aguia = D(Especie.Aguia, "águia-careca", "a águia", "águias");
            aguia.corpo = Corpo.Ave; aguia.vida = Vida.Sozinho; aguia.altura = 0.8f; aguia.comprimento = 0.9f; aguia.pontos = 30f;
            aguia.andar = 0.6f; aguia.trotar = 1.2f; aguia.correr = 15f;
            aguia.visao = 80f; aguia.ouvido = 30f; aguia.faro = 0f; aguia.fuga = 30f;
            aguia.carne = 1; aguia.segundosParaCarnear = 4f; aguia.habitats = new[] { Mancha.Beira, Mancha.Serra };
            aguia.meio = Meio.Ar; aguia.noMundo = true; aguia.raridade = 0.03f;
            Por(aguia);

            var urubu = D(Especie.Urubu, "urubu", "o urubu", "urubus");
            urubu.corpo = Corpo.Ave; urubu.vida = Vida.Pequeno; urubu.altura = 0.6f; urubu.comprimento = 0.7f; urubu.pontos = 20f;
            urubu.andar = 0.5f; urubu.trotar = 1.0f; urubu.correr = 10f;
            urubu.visao = 60f; urubu.ouvido = 20f; urubu.faro = 200f; urubu.fuga = 12f;
            urubu.grupoMin = 3; urubu.grupoMax = 8; urubu.carne = 1; urubu.segundosParaCarnear = 3f;
            urubu.habitats = new[] { Mancha.Campo, Mancha.Baldio, Mancha.Rebrota };
            Por(urubu);

            var sapo = D(Especie.Sapo, "sapo", "o sapo", "sapos");
            sapo.corpo = Corpo.Sapo; sapo.vida = Vida.Pequeno; sapo.altura = 0.06f; sapo.comprimento = 0.12f; sapo.pontos = 2f;
            sapo.andar = 0.4f; sapo.trotar = 0.8f; sapo.correr = 1.6f;
            sapo.visao = 8f; sapo.ouvido = 8f; sapo.faro = 0f; sapo.fuga = 3f;
            sapo.grupoMin = 1; sapo.grupoMax = 4; sapo.habitats = Lago; sapo.noturno = true;
            sapo.meio = Meio.Anfibio; sapo.noMundo = true; sapo.raridade = 0.4f; sapo.gruposPorJogador = 3;
            Por(sapo);

            var cascavel = D(Especie.Cascavel, "cascavel", "a cascavel", "cascavéis");
            cascavel.corpo = Corpo.Cobra; cascavel.vida = Vida.Sozinho; cascavel.altura = 0.06f; cascavel.comprimento = 1.1f; cascavel.pontos = 20f;
            cascavel.andar = 0.3f; cascavel.trotar = 0.5f; cascavel.correr = 0.9f;
            cascavel.visao = 6f; cascavel.ouvido = 10f; cascavel.faro = 6f; cascavel.fuga = 2f;
            cascavel.habitats = new[] { Mancha.Serra, Mancha.Campo };
            cascavel.noMundo = true; cascavel.raridade = 0.04f; cascavel.mordida = 6f;
            Por(cascavel);

            var guaxinim = D(Especie.Guaxinim, "guaxinim", "o guaxinim", "guaxinins");
            guaxinim.vida = Vida.Pequeno; guaxinim.altura = 0.3f; guaxinim.comprimento = 0.65f; guaxinim.pontos = 25f;
            guaxinim.andar = 0.9f; guaxinim.trotar = 2.2f; guaxinim.correr = 6f;
            guaxinim.visao = 20f; guaxinim.ouvido = 20f; guaxinim.faro = 30f; guaxinim.fuga = 10f;
            guaxinim.grupoMin = 1; guaxinim.grupoMax = 2; guaxinim.carne = 1; guaxinim.couro = 1; guaxinim.segundosParaCarnear = 4f;
            guaxinim.habitats = new[] { Mancha.Quintal, Mancha.Baldio, Mancha.Beira }; guaxinim.noturno = true;
            guaxinim.noMundo = true; guaxinim.raridade = 0.12f;
            Por(guaxinim);

            var gamba = D(Especie.Gamba, "gambá", "o gambá", "gambás");
            gamba.vida = Vida.Pequeno; gamba.altura = 0.25f; gamba.comprimento = 0.6f; gamba.pontos = 20f;
            gamba.andar = 0.7f; gamba.trotar = 1.5f; gamba.correr = 3f;
            gamba.visao = 12f; gamba.ouvido = 15f; gamba.faro = 20f; gamba.fuga = 4f;
            gamba.carne = 1; gamba.couro = 1; gamba.segundosParaCarnear = 4f;
            gamba.habitats = new[] { Mancha.Quintal, Mancha.Baldio, Mancha.Campo }; gamba.noturno = true;
            gamba.noMundo = true; gamba.raridade = 0.1f;
            Por(gamba);

            var esquilo = D(Especie.Esquilo, "esquilo", "o esquilo", "esquilos");
            esquilo.vida = Vida.Pequeno; esquilo.altura = 0.12f; esquilo.comprimento = 0.25f; esquilo.pontos = 8f;
            esquilo.andar = 1.0f; esquilo.trotar = 2.5f; esquilo.correr = 6f;
            esquilo.visao = 20f; esquilo.ouvido = 15f; esquilo.faro = 10f; esquilo.fuga = 8f;
            esquilo.grupoMin = 1; esquilo.grupoMax = 2; esquilo.habitats = new[] { Mancha.Mista, Mancha.Outono, Mancha.Quintal };
            esquilo.noMundo = true; esquilo.raridade = 0.3f; esquilo.gruposPorJogador = 2;
            Por(esquilo);

            var peru = D(Especie.Peru, "peru selvagem", "o peru", "perus");
            peru.corpo = Corpo.Ave; peru.altura = 0.8f; peru.comprimento = 1.0f; peru.pontos = 30f;
            peru.andar = 1.0f; peru.trotar = 3f; peru.correr = 7f;
            peru.visao = 45f; peru.ouvido = 25f; peru.faro = 10f; peru.fuga = 25f;
            peru.grupoMin = 4; peru.grupoMax = 10; peru.carne = 2; peru.segundosParaCarnear = 4f;
            peru.habitats = new[] { Mancha.Campo, Mancha.Rebrota, Mancha.Mista };
            peru.noMundo = true; peru.raridade = 0.12f;
            Por(peru);

            var salmao = D(Especie.Salmao, "salmão", "o salmão", "salmões");
            salmao.corpo = Corpo.Peixe; salmao.vida = Vida.Pequeno; salmao.altura = 0.15f; salmao.comprimento = 0.7f; salmao.pontos = 5f;
            salmao.andar = 0.6f; salmao.trotar = 1.5f; salmao.correr = 4f;
            salmao.visao = 6f; salmao.ouvido = 6f; salmao.fuga = 4f; salmao.grupoMin = 3; salmao.grupoMax = 8;
            salmao.habitats = Lago;
            salmao.meio = Meio.Agua; salmao.noMundo = true; salmao.raridade = 0.3f; salmao.gruposPorJogador = 2;
            Por(salmao);

            foreach (var d in _dados)
                if (d == null) throw new System.Exception("Especies: falta uma espécie no catálogo.");
        }

        /// <summary>O tamanho de uma variante, relativo ao adulto (o filhote e a fêmea menores).</summary>
        public static float Escala(Especie e, Variante v) => v switch
        {
            Variante.Filhote => e == Especie.Veado ? 0.58f : e is Especie.UrsoNegro or Especie.UrsoPardo ? 0.4f : 0.5f,
            Variante.Femea => e == Especie.Alce ? 0.9f : e == Especie.Veado ? 0.87f : 0.92f,
            _ => 1f,
        };

        /// <summary>A vida da variante.</summary>
        public static float Pontos(Especie e, Variante v) => De(e).pontos * (v == Variante.Filhote ? 0.35f : v == Variante.Femea ? 0.85f : 1f);

        /// <summary>Quantos de cada coisa a carcaça dá.</summary>
        public static void Carcaca(Especie e, Variante v, out int carne, out int couro, out int gordura)
        {
            var d = De(e);
            if (v == Variante.Filhote)
            {
                carne = (d.carne + 1) / 2;
                couro = (d.couro + 1) / 2;
                gordura = d.gordura / 2;
            }
            else
            {
                carne = d.carne;
                couro = d.couro;
                gordura = d.gordura;
            }
        }
    }
}
