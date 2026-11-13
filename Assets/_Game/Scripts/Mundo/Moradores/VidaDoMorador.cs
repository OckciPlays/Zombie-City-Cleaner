using System;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>O que o morador está fazendo (a agenda e as necessidades: o Plano_NPCs_Bunker.md da pasta de arte, a decisão 10).</summary>
    public enum AtividadeDoMorador : byte { Nada, Dormir, Banheiro, Comer, Trabalho, Lazer, Cochilo, Rezar, Buscar, Crise, Sinuca, Guardar }

    /// <summary>
    /// O que o corpo do morador faz no lugar (vai pela rede num byte, com o <see cref="AcoesDoCorpo.Espelho"/> no bit de cima):
    /// em pé (andando ou parado), deitado, sentado no sofá, na cadeira (parado, lendo, rezando ou triste), no banco do
    /// refeitório (parado ou comendo) e nas bancadas (a louça, o conserto, a panela), parado na frente do espelho e pegando
    /// na estante (na tábua do alto, do meio, de baixo ou do chão: a segunda fatia da parte 6).
    /// </summary>
    public enum AcaoDoCorpo : byte
    {
        Andando, Dormindo, Sofa, Cadeira, Lendo, Rezando, Triste, Banco, Comendo, LavandoALouca, Consertando, Cozinhando, Espelho,
        EstanteAlto, EstanteMeio, EstanteBaixo, EstanteChao,
        /// <summary>Em pé, chorando (a camada de cima do controlador: o "Chorar"; a crise de chorar no quarto).</summary>
        Chorando,
        /// <summary>Dobrando a roupa na mesa da lavanderia e da rouparia (o "Dobrar_Roupa": a parte 8, a segunda fatia).</summary>
        DobrandoRoupa,
    }

    /// <summary>Os grupos das ações: cada um com o sentar e o levantar dele (o de pé e o das bancadas sem).</summary>
    public enum PosturaDoCorpo : byte { EmPe, Cama, Sofa, Cadeira, Banco, Bancada }

    public static class AcoesDoCorpo
    {
        /// <summary>O bit do byte da rede: a cama do outro lado (o corpo espelhado, para a cabeça ir para a cabeceira).</summary>
        public const byte Espelho = 0x80;
        /// <summary>
        /// O bit da cama alta: a de casal e a de solteiro, com o colchão a 0,55 m (×1,30: 0,72), 12 cm acima do do beliche
        /// (0,46), que é o das animações; deitado, o corpo sobe isso.
        /// </summary>
        public const byte Alta = 0x40;
        /// <summary>Quanto o corpo sobe na cama alta (m).</summary>
        public const float AlturaDaCamaAlta = (0.55f - 0.46f) * 1.3f;

        public static AcaoDoCorpo Acao(byte b) => (AcaoDoCorpo)(b & 0x3F);
        public static bool Espelhado(byte b) => (b & Espelho) != 0;
        public static bool NaCamaAlta(byte b) => (b & Alta) != 0;

        public static PosturaDoCorpo Postura(AcaoDoCorpo a) => a switch
        {
            AcaoDoCorpo.Dormindo => PosturaDoCorpo.Cama,
            AcaoDoCorpo.Sofa => PosturaDoCorpo.Sofa,
            AcaoDoCorpo.Cadeira or AcaoDoCorpo.Lendo or AcaoDoCorpo.Rezando or AcaoDoCorpo.Triste => PosturaDoCorpo.Cadeira,
            AcaoDoCorpo.Banco or AcaoDoCorpo.Comendo => PosturaDoCorpo.Banco,
            AcaoDoCorpo.LavandoALouca or AcaoDoCorpo.Consertando or AcaoDoCorpo.Cozinhando or AcaoDoCorpo.DobrandoRoupa => PosturaDoCorpo.Bancada,
            _ => PosturaDoCorpo.EmPe,
        };

        /// <summary>O clipe (o estado do controlador) da ação parada no lugar; nulo em pé (a locomoção).</summary>
        public static string Clipe(AcaoDoCorpo a) => a switch
        {
            AcaoDoCorpo.Dormindo => "Dormindo",
            AcaoDoCorpo.Sofa => "Sentado_Sofa",
            AcaoDoCorpo.Cadeira => "Sentado_Cadeira",
            AcaoDoCorpo.Lendo => "Sentado_Lendo",
            AcaoDoCorpo.Rezando => "Sentado_Rezando",
            AcaoDoCorpo.Triste => "Sentado_Triste",
            AcaoDoCorpo.Banco => "Sentado_Banco",
            AcaoDoCorpo.Comendo => "Comer",
            AcaoDoCorpo.LavandoALouca => "Lavar_Louca",
            AcaoDoCorpo.Consertando => "Consertar",
            AcaoDoCorpo.Cozinhando => "Cozinhar",
            AcaoDoCorpo.DobrandoRoupa => "Dobrar_Roupa",
            AcaoDoCorpo.EstanteAlto => "Estante_Alto",
            AcaoDoCorpo.EstanteMeio => "Estante_Meio",
            AcaoDoCorpo.EstanteBaixo => "Estante_Baixo",
            AcaoDoCorpo.EstanteChao => "Estante_Chao",
            _ => null,
        };

        /// <summary>
        /// A ação de pegar na estante pela altura da tábua (m do chão, na escala do jogo): o chão, embaixo, no meio e no alto
        /// (até 2 m; a tábua de cima das estantes de aço, a 2,37 m, ninguém alcança).
        /// </summary>
        public static AcaoDoCorpo NaEstante(float altura) =>
            altura < 0.35f ? AcaoDoCorpo.EstanteChao : altura < 0.95f ? AcaoDoCorpo.EstanteBaixo : altura < 1.5f ? AcaoDoCorpo.EstanteMeio : AcaoDoCorpo.EstanteAlto;

        /// <summary>A tábua mais alta que um morador alcança (m do chão, na escala do jogo).</summary>
        public const float AlcanceNaEstante = 2.0f;

        /// <summary>O clipe de chegar na postura (sentar, deitar); nulo nas que entram direto.</summary>
        public static string Entrar(PosturaDoCorpo p) => p switch
        {
            PosturaDoCorpo.Cama => "Deitar",
            PosturaDoCorpo.Sofa => "Sentar_Sofa",
            PosturaDoCorpo.Cadeira => "Sentar_Cadeira",
            PosturaDoCorpo.Banco => "Sentar_Banco",
            _ => null,
        };

        /// <summary>O clipe de sair da postura (levantar); nulo nas que saem direto.</summary>
        public static string Sair(PosturaDoCorpo p) => p switch
        {
            PosturaDoCorpo.Cama => "Levantar_Cama",
            PosturaDoCorpo.Sofa => "Levantar_Sofa",
            PosturaDoCorpo.Cadeira => "Levantar_Cadeira",
            PosturaDoCorpo.Banco => "Levantar_Banco",
            _ => null,
        };

        /// <summary>O clipe parado da postura (o de depois do sentar), para onde o sentar vai sozinho.</summary>
        public static string Base(PosturaDoCorpo p) => p switch
        {
            PosturaDoCorpo.Cama => "Dormindo",
            PosturaDoCorpo.Sofa => "Sentado_Sofa",
            PosturaDoCorpo.Cadeira => "Sentado_Cadeira",
            PosturaDoCorpo.Banco => "Sentado_Banco",
            _ => null,
        };

        /// <summary>A ação de cada tipo de lugar, a de sempre.</summary>
        public static AcaoDoCorpo DoLugar(TipoDoLugar t) => t switch
        {
            TipoDoLugar.Cama => AcaoDoCorpo.Dormindo,
            TipoDoLugar.Sofa => AcaoDoCorpo.Sofa,
            TipoDoLugar.Cadeira => AcaoDoCorpo.Cadeira,
            TipoDoLugar.Banco => AcaoDoCorpo.Banco,
            TipoDoLugar.Pia => AcaoDoCorpo.LavandoALouca,
            TipoDoLugar.Bancada => AcaoDoCorpo.Consertando,
            TipoDoLugar.Fogao => AcaoDoCorpo.Cozinhando,
            TipoDoLugar.Espelho => AcaoDoCorpo.Espelho,
            TipoDoLugar.Estante => AcaoDoCorpo.EstanteMeio,
            TipoDoLugar.Radio => AcaoDoCorpo.Cadeira,
            TipoDoLugar.Dobrar => AcaoDoCorpo.DobrandoRoupa,
            _ => AcaoDoCorpo.Andando,
        };
    }

    /// <summary>
    /// A vida de um morador no bunker (a parte 6 do Plano_NPCs_Bunker.md da pasta de arte), guardada no mundo
    /// (<c>MundoSalvo.vidaDosMoradores</c>): as quatro barras (de 0 a 100; a fome, a sede e o sono cheios em 100, a vontade
    /// de fumar vazia em 0), a cama, o que ele está fazendo e onde ele está (nas coordenadas do bunker, a obra da
    /// <see cref="PlantaDeBunker"/>). O anfitrião segue a vida dele "no papel" quando o andar dele não está montado, e com o
    /// corpo quando está.
    /// </summary>
    /// <summary>Uma vaga do inventário do morador (a decisão 13): o item (o número do enum do jogo) e quantos (a pilha).</summary>
    [Serializable]
    public class VagaDoMorador
    {
        public int item;
        public int qtd = 1;
    }

    [Serializable]
    public class VidaDoMorador
    {
        public int id;
        public float fome = 75f, sede = 75f, sono = 70f, fumo;
        /// <summary>A cama dele (o índice em <see cref="LugaresDoBunker.Todos"/>; -1: nenhuma).</summary>
        public int cama = -1;
        public byte atividade;
        /// <summary>O lugar da atividade (o índice em <see cref="LugaresDoBunker.Todos"/>; -1: nenhum).</summary>
        public int lugar = -1;
        /// <summary>A ação no lugar (o <see cref="AcaoDoCorpo"/>).</summary>
        public byte acao;
        /// <summary>Até quando vai a atividade (as horas do jogo desde o dia 0: dia × 24 + hora).</summary>
        public float ate;
        /// <summary>No papel, andando: quando ele chega ao andar do lugar (na escada) e ao lugar.</summary>
        public float chegaNaEscada, chegaNoLugar;
        /// <summary>Já está no lugar (sentado, deitado, na bancada).</summary>
        public bool noLugar;
        /// <summary>Onde ele está agora (o andar e o ponto, nas coordenadas do bunker; com o corpo, o do corpo).</summary>
        public int andar = 1;
        public Vector3 pos;
        public float giro;
        /// <summary>Quando a fome e a sede passaram de 25 para baixo (para os motivos da parte 8; -1: não estão).</summary>
        public float fomeDesde = -1f, sedeDesde = -1f;
        /// <summary>As vagas do inventário (6; 4 com a coluna ruim): o que ele pegou e vai gastar (a comida, a bebida).</summary>
        public System.Collections.Generic.List<VagaDoMorador> vagas = new System.Collections.Generic.List<VagaDoMorador>();
        /// <summary>A refeição em andamento: o que ainda falta a fome e a sede receberem (do que ele tirou das vagas).</summary>
        public float aComer, aBeber;
        public bool refeicaoComecou;
        /// <summary>Até quando ele não procura comida de novo (o estoque estava vazio: as horas do jogo).</summary>
        public float semEstoqueAte;

        // ------------------------------------------------------------ o humor (a parte 8, a primeira fatia)

        /// <summary>O humor mostrado (de 0 a 100; anda até a conta dos motivos aos poucos: <see cref="HumorDoMorador"/>).</summary>
        public float humor = HumorDoMorador.Comeco;
        /// <summary>Os motivos do humor, com o valor e o prazo.</summary>
        public System.Collections.Generic.List<MotivoDoMorador> motivos = new System.Collections.Generic.List<MotivoDoMorador>();
        /// <summary>O último lazer (as horas do jogo; -1: ainda não contou) e o lazer, a reza e o sono que estão contando agora.</summary>
        public float ultimoLazer = -1f, lazerAgora, rezaAgora, dormiuHoras;
        /// <summary>A crise (o <see cref="CriseDoMorador"/>) e até quando ela vai (as horas do jogo).</summary>
        public byte crise;
        public float criseAte;
        /// <summary>Desde quando ele está abaixo da crise leve e da forte (-1: não está), e quando foi a última crise.</summary>
        public float baixoDesde = -1f, muitoBaixoDesde = -1f, ultimaCrise = -999f;
        /// <summary>O registro já disse que ele está triste (diz de novo depois que ele melhorar).</summary>
        public bool avisouTriste;

        // ------------------------------------------------------------ o lazer (a parte 8, a segunda fatia)

        /// <summary>Desde quando a vontade de fumar está cheia sem ele fumar (as horas do jogo; -1: não está).</summary>
        public float fumoCheioDesde = -1f;

        public CriseDoMorador Crise => (CriseDoMorador)crise;

        public AtividadeDoMorador Atividade => (AtividadeDoMorador)atividade;
    }
}
