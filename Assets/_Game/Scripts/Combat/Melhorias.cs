using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// As peças das melhorias das armas de sucata (10/11/2026, a etapa 2 do Armeiro de sucata), na ordem dos itens (do
    /// <see cref="Item.SilenciadorPET"/> em diante) e dos modelos (<c>Sucata_&lt;Arma&gt;_acc_&lt;Peça&gt;</c> do
    /// Sucata_Acessorios.fbx e <c>Item_&lt;Peça&gt;_corpo</c> do Loot_Acessorios.fbx).
    /// </summary>
    public enum PecaDaMelhoria : byte { SilenciadorPET, SilenciadorFiltro, MiraDeFerro, MiraDePonto, Luneta, CarregadorLongo, CanoReforcado1, CanoReforcado2, CanoReforcado3 }

    /// <summary>
    /// Os encaixes do painel MELHORIAS: o cano (um silenciador), a mira (a de ferro ajustada e, em cima, a de ponto ou a
    /// luneta: a de ferro convive com as duas), o carregador (o estendido) e o reforço (o cano reforçado I, II ou III).
    /// </summary>
    public enum EncaixeDaMelhoria : byte { Cano, Mira, Carregador, Reforco }

    /// <summary>
    /// As melhorias das armas de sucata (10/11/2026, a etapa 2): o que fica posto vai na própria arma, nos bits do
    /// <c>cores</c> do item (o <see cref="Player.ItemNaMochila"/>, o <see cref="ItemNoChao"/>, a estante e o save; nas
    /// roupas, o mesmo campo guarda as cores). Assim a melhoria anda com a arma: para o chão, para a estante, para outro
    /// jogador e para o save. As armas reais não aceitam as de sucata (por enquanto).
    ///  - bits 0 e 1: o silenciador (0 nenhum, 1 o de garrafa PET, 2 o de filtro de óleo);
    ///  - bit 2: a mira de ferro ajustada; bits 3 e 4: a mira de cima (0 nenhuma, 1 a de ponto, 2 a luneta);
    ///  - bit 5: o carregador estendido; bits 6 e 7: o cano reforçado (0 a 3);
    ///  - bits 8 a 15: os tiros que o silenciador já deu (ele se desfaz em <see cref="TirosDoPET"/> ou <see cref="TirosDoFiltro"/>).
    /// Os números ficam aqui em cima, para ajustar.
    /// </summary>
    public static class Melhorias
    {
        public const int Quantas = 9;

        // ------------------------------------------------------------------ os números (a pedido; mude aqui)
        /// <summary>O raio do barulho do tiro com o silenciador de garrafa PET e com o de filtro, vezes o de sem ele.</summary>
        public const float BarulhoDoPET = 0.4f, BarulhoDoFiltro = 0.5f;
        /// <summary>O dano com um silenciador, vezes o de sem ele.</summary>
        public const float DanoDoSilenciador = 0.95f;
        /// <summary>Quantos tiros cada silenciador aguenta (depois, ele se desfaz, com o aviso).</summary>
        public const int TirosDoPET = 15, TirosDoFiltro = 80;
        /// <summary>O volume do tiro que se ouve com o silenciador, vezes o de sem ele (e o alcance do som).</summary>
        public const float VolumeDoPET = 0.45f, VolumeDoFiltro = 0.5f;
        /// <summary>O espalhamento do tiro com a mira de ferro ajustada (sempre), com a de ponto e com a luneta (mirando), vezes o de sem elas.</summary>
        public const float EspalhaDaMiraDeFerro = 0.85f, EspalhaDaMiraDePonto = 0.7f, EspalhaDaLuneta = 0.4f;
        /// <summary>O aumento da luneta de cano (o campo de visão mirando: <see cref="FovDaLuneta"/>).</summary>
        public const float ZoomDaLuneta = 2.5f;
        /// <summary>O campo de visão sem mirar (o <c>ArmaJogador.fovNormal</c>), para a conta do aumento.</summary>
        const float FovNormal = 70f;
        /// <summary>Quantas balas cabem com o carregador estendido.</summary>
        public const int PenteLongoDaPistola = 3, PenteLongoDaSubmetralhadora = 32;
        /// <summary>O cano reforçado: o dano e o desgaste a mais por nível (o I, o II e o III).</summary>
        public const float DanoPorReforco = 0.08f, DesgastePorReforco = 0.10f;
        /// <summary>A Mola nova (a8): as armas de sucata emperram isto a menos por nível.</summary>
        public const float MenosEmperraPorMola = 0.25f;
        /// <summary>O Mestre armeiro (a10): as receitas do Armeiro de sucata gastam esta fração da sucata (arredondada para cima).</summary>
        public const float SucataDoMestre = 0.75f;
        /// <summary>A velocidade do mirar pela mira de ponto (a câmera indo até o tubinho; por segundo).</summary>
        public const float VelocidadeDaMira = 11f;

        /// <summary>O campo de visão mirando pela luneta de cano (2,5 vezes mais perto que o normal: ~31°).</summary>
        public static float FovDaLuneta => 2f * Mathf.Atan(Mathf.Tan(FovNormal * 0.5f * Mathf.Deg2Rad) / ZoomDaLuneta) * Mathf.Rad2Deg;

        // ------------------------------------------------------------------ os pontos dos modelos (o Sucata_Acessorios_Miras.json)

        /// <summary>As armas de sucata, na ordem dos modelos das melhorias (0 a pistola, 1 o rifle, 2 a submetralhadora).</summary>
        public static readonly TipoDeArma[] DasArmas = { TipoDeArma.PistolaSucata, TipoDeArma.RifleSucata, TipoDeArma.SubmetralhadoraSucata };
        public static int IndiceDa(TipoDeArma t) => t == TipoDeArma.PistolaSucata ? 0 : t == TipoDeArma.RifleSucata ? 1 : t == TipoDeArma.SubmetralhadoraSucata ? 2 : -1;

        // (na malha da arma: a origem no cabo, o cano em +Z, o alto em +Y, em metros)
        /// <summary>
        /// O olho na mira de ponto, no eixo do tubinho: a câmera vai para ele, mirando. Na pistola, o braço estendido (28 cm
        /// atrás do tubinho); na submetralhadora (18 cm) e no rifle (16 cm), o rosto na coronha. (O JSON sugeria 8 cm: o
        /// tubinho tomava um quarto da tela.)
        /// </summary>
        static readonly Vector3[] OlhoDoPonto = { new Vector3(0f, 0.1333f, -0.223f), new Vector3(0f, 0.1543f, 0.101f), new Vector3(0f, 0.1538f, -0.131f) };
        /// <summary>O pontinho vermelho (o meio da lente, por dentro).</summary>
        static readonly Vector3[] PontoVermelho = { new Vector3(0f, 0.1333f, 0.092f), new Vector3(0f, 0.1543f, 0.296f), new Vector3(0f, 0.1538f, 0.084f) };
        /// <summary>A boca do cano com o silenciador de garrafa PET e com o de filtro.</summary>
        static readonly Vector3[] BocaDoPET = { new Vector3(0f, 0.088f, 0.333f), new Vector3(0f, 0.105f, 1.049f), new Vector3(0f, 0.1f, 0.421f) };
        static readonly Vector3[] BocaDoFiltro = { new Vector3(0f, 0.088f, 0.307f), new Vector3(0f, 0.105f, 1.023f), new Vector3(0f, 0.1f, 0.395f) };

        public static Vector3 OlhoDaMiraDePonto(TipoDeArma t) => OlhoDoPonto[Mathf.Max(0, IndiceDa(t))];
        public static Vector3 PontoVermelhoDa(TipoDeArma t) => PontoVermelho[Mathf.Max(0, IndiceDa(t))];

        /// <summary>A boca do cano da arma com o que está posto (o silenciador muda ela; o clarão sai dela).</summary>
        public static Vector3 Boca(TipoDeArma t, ulong b)
        {
            int k = IndiceDa(t), s = Silenciador(b);
            if (k < 0 || s == 0) return Armas.Dados(t).boca;
            return s == PET ? BocaDoPET[k] : BocaDoFiltro[k];
        }

        // ------------------------------------------------------------------ as peças e os itens

        public static Item ItemDa(PecaDaMelhoria p) => (Item)((int)Item.SilenciadorPET + (int)p);
        /// <summary>Um item que é uma melhoria das armas de sucata.</summary>
        public static bool EhPeca(Item i) => i >= Item.SilenciadorPET && i <= Item.CanoReforcado3;
        public static PecaDaMelhoria PecaDo(Item i) => (PecaDaMelhoria)Mathf.Clamp(i - Item.SilenciadorPET, 0, Quantas - 1);

        /// <summary>O nome do modelo (o Item_&lt;nome&gt;_corpo do Loot_Acessorios.fbx, o Icone_ e o Foto_).</summary>
        public static string Modelo(PecaDaMelhoria p) => p switch
        {
            PecaDaMelhoria.MiraDeFerro => "MiraFerro",
            PecaDaMelhoria.MiraDePonto => "MiraPonto",
            _ => p.ToString(),
        };

        /// <summary>O objeto da peça no Sucata_Acessorios.fbx, depois de "Sucata_&lt;Arma&gt;_".</summary>
        public static string Objeto(PecaDaMelhoria p) => p == PecaDaMelhoria.CarregadorLongo ? "carregadorLongo" : "acc_" + Modelo(p);

        public static EncaixeDaMelhoria EncaixeDa(PecaDaMelhoria p) =>
            p <= PecaDaMelhoria.SilenciadorFiltro ? EncaixeDaMelhoria.Cano
            : p <= PecaDaMelhoria.Luneta ? EncaixeDaMelhoria.Mira
            : p == PecaDaMelhoria.CarregadorLongo ? EncaixeDaMelhoria.Carregador : EncaixeDaMelhoria.Reforco;

        public static string NomeDo(EncaixeDaMelhoria e) => e switch
        {
            EncaixeDaMelhoria.Cano => "Cano",
            EncaixeDaMelhoria.Mira => "Mira",
            EncaixeDaMelhoria.Carregador => "Carregador",
            _ => "Reforço",
        };

        /// <summary>A peça serve nesta arma? (só nas de sucata; a luneta, só no rifle; o carregador, na pistola e na submetralhadora)</summary>
        public static bool Serve(PecaDaMelhoria p, TipoDeArma t) =>
            IndiceDa(t) >= 0 && (p == PecaDaMelhoria.Luneta ? t == TipoDeArma.RifleSucata
                                 : p != PecaDaMelhoria.CarregadorLongo || t != TipoDeArma.RifleSucata);

        /// <summary>Quantos tiros o silenciador aguenta (0: a peça não se gasta).</summary>
        public static int Duracao(PecaDaMelhoria p) => p == PecaDaMelhoria.SilenciadorPET ? TirosDoPET : p == PecaDaMelhoria.SilenciadorFiltro ? TirosDoFiltro : 0;

        /// <summary>O que a peça faz, curto (o painel e os detalhes).</summary>
        public static string Efeito(PecaDaMelhoria p) => p switch
        {
            PecaDaMelhoria.SilenciadorPET => $"barulho −{Pct(1f - BarulhoDoPET)}%, dano −{Pct(1f - DanoDoSilenciador)}%, {TirosDoPET} tiros",
            PecaDaMelhoria.SilenciadorFiltro => $"barulho −{Pct(1f - BarulhoDoFiltro)}%, dano −{Pct(1f - DanoDoSilenciador)}%, {TirosDoFiltro} tiros",
            PecaDaMelhoria.MiraDeFerro => $"espalha −{Pct(1f - EspalhaDaMiraDeFerro)}%",
            PecaDaMelhoria.MiraDePonto => $"mirando, espalha −{Pct(1f - EspalhaDaMiraDePonto)}%",
            PecaDaMelhoria.Luneta => $"mirando, {ZoomDaLuneta:0.#}× e espalha −{Pct(1f - EspalhaDaLuneta)}%",
            PecaDaMelhoria.CarregadorLongo => $"pistola {PenteLongoDaPistola}, submetralhadora {PenteLongoDaSubmetralhadora}",
            _ => $"dano +{Pct(DanoPorReforco * NivelDoReforco(p))}%, desgaste +{Pct(DesgastePorReforco * NivelDoReforco(p))}%",
        };

        static int Pct(float f) => Mathf.RoundToInt(f * 100f);
        static int NivelDoReforco(PecaDaMelhoria p) => p - PecaDaMelhoria.CanoReforcado1 + 1;

        // ------------------------------------------------------------------ os bits

        public const int PET = 1, Filtro = 2;
        public const int Ponto = 1, ComLuneta = 2;

        public static int Silenciador(ulong b) => (int)(b & 3);
        public static bool MiraDeFerro(ulong b) => ((b >> 2) & 1) != 0;
        public static int MiraDeCima(ulong b) => (int)((b >> 3) & 3);
        public static bool CarregadorLongo(ulong b) => ((b >> 5) & 1) != 0;
        public static int Reforco(ulong b) => (int)((b >> 6) & 3);
        public static int TirosDoSilenciador(ulong b) => (int)((b >> 8) & 0xFF);
        /// <summary>Alguma peça posta.</summary>
        public static bool Alguma(ulong b) => (b & 0xFF) != 0;
        /// <summary>Só as peças (sem os tiros do silenciador): o que muda a arma.</summary>
        public static int Pecas(ulong b) => (int)(b & 0xFF);

        public static ulong ComTiros(ulong b, int tiros) => (b & ~(0xFFUL << 8)) | ((ulong)Mathf.Clamp(tiros, 0, 255) << 8);

        /// <summary>A peça está posta?</summary>
        public static bool Posta(ulong b, PecaDaMelhoria p) => p switch
        {
            PecaDaMelhoria.SilenciadorPET => Silenciador(b) == PET,
            PecaDaMelhoria.SilenciadorFiltro => Silenciador(b) == Filtro,
            PecaDaMelhoria.MiraDeFerro => MiraDeFerro(b),
            PecaDaMelhoria.MiraDePonto => MiraDeCima(b) == Ponto,
            PecaDaMelhoria.Luneta => MiraDeCima(b) == ComLuneta,
            PecaDaMelhoria.CarregadorLongo => CarregadorLongo(b),
            _ => Reforco(b) == NivelDoReforco(p),
        };

        /// <summary>As peças postas, na ordem do enum.</summary>
        public static List<PecaDaMelhoria> Postas(ulong b)
        {
            var l = new List<PecaDaMelhoria>();
            for (int i = 0; i < Quantas; i++)
                if (Posta(b, (PecaDaMelhoria)i)) l.Add((PecaDaMelhoria)i);
            return l;
        }

        /// <summary>A peça que já ocupa o lugar desta (o outro silenciador, a mira de ponto e a luneta, o outro cano reforçado), ou null.</summary>
        public static PecaDaMelhoria? NoLugarDe(ulong b, PecaDaMelhoria p)
        {
            for (int i = 0; i < Quantas; i++)
            {
                var o = (PecaDaMelhoria)i;
                if (o != p && Posta(b, o) && MesmoLugar(o, p)) return o;
            }
            return null;
        }

        static bool MesmoLugar(PecaDaMelhoria a, PecaDaMelhoria b)
        {
            int Lugar(PecaDaMelhoria p) => p <= PecaDaMelhoria.SilenciadorFiltro ? 0 : p == PecaDaMelhoria.MiraDeFerro ? 1
                : p <= PecaDaMelhoria.Luneta ? 2 : p == PecaDaMelhoria.CarregadorLongo ? 3 : 4;
            return Lugar(a) == Lugar(b);
        }

        /// <summary>Os bits com a peça posta (no lugar da que estava lá; o silenciador novo começa com 'tiros' já dados).</summary>
        public static ulong Por(ulong b, PecaDaMelhoria p, int tiros = 0)
        {
            switch (p)
            {
                case PecaDaMelhoria.SilenciadorPET:
                case PecaDaMelhoria.SilenciadorFiltro:
                    return ComTiros((b & ~3UL) | (p == PecaDaMelhoria.SilenciadorPET ? 1UL : 2UL), tiros);
                case PecaDaMelhoria.MiraDeFerro: return b | (1UL << 2);
                case PecaDaMelhoria.MiraDePonto: return (b & ~(3UL << 3)) | (1UL << 3);
                case PecaDaMelhoria.Luneta: return (b & ~(3UL << 3)) | (2UL << 3);
                case PecaDaMelhoria.CarregadorLongo: return b | (1UL << 5);
                default: return (b & ~(3UL << 6)) | ((ulong)NivelDoReforco(p) << 6);
            }
        }

        /// <summary>Os bits sem a peça.</summary>
        public static ulong Tirar(ulong b, PecaDaMelhoria p)
        {
            if (!Posta(b, p)) return b;
            switch (p)
            {
                case PecaDaMelhoria.SilenciadorPET:
                case PecaDaMelhoria.SilenciadorFiltro: return ComTiros(b & ~3UL, 0);
                case PecaDaMelhoria.MiraDeFerro: return b & ~(1UL << 2);
                case PecaDaMelhoria.MiraDePonto:
                case PecaDaMelhoria.Luneta: return b & ~(3UL << 3);
                case PecaDaMelhoria.CarregadorLongo: return b & ~(1UL << 5);
                default: return b & ~(3UL << 6);
            }
        }

        /// <summary>O silenciador posto (null, nenhum).</summary>
        public static PecaDaMelhoria? SilenciadorPosto(ulong b) =>
            Silenciador(b) == PET ? PecaDaMelhoria.SilenciadorPET : Silenciador(b) == Filtro ? PecaDaMelhoria.SilenciadorFiltro : (PecaDaMelhoria?)null;

        /// <summary>O desgaste (0 a 100) do silenciador solto, pelos tiros que ele já deu (o item guarda, para não voltar novo).</summary>
        public static int DesgasteDosTiros(PecaDaMelhoria p, int tiros) => Duracao(p) > 0 ? Mathf.Clamp(Mathf.CeilToInt(tiros * 100f / Duracao(p)), 0, 100) : 0;
        /// <summary>Os tiros já dados pelo silenciador solto com este desgaste.</summary>
        public static int TirosDoDesgaste(PecaDaMelhoria p, int desgaste) => Duracao(p) > 0 ? Mathf.Clamp(Mathf.RoundToInt(desgaste * Duracao(p) / 100f), 0, Duracao(p) - 1) : 0;

        // ------------------------------------------------------------------ os números da arma com as melhorias

        static readonly Dictionary<int, DadosDaArma> _dados = new Dictionary<int, DadosDaArma>();

        /// <summary>
        /// Os números da arma com o que está posto nela e as habilidades de quem atira (a Mola nova, 'mola' pontos; o Mestre
        /// armeiro). As reais e as de sucata sem nada: os de sempre. O espalhamento das miras entra no tiro
        /// (<see cref="FatorDaMira"/>), porque depende de estar mirando.
        /// </summary>
        public static DadosDaArma Dados(TipoDeArma t, ulong b, int mola = 0, bool mestre = false)
        {
            var real = Armas.Dados(t);
            if (IndiceDa(t) < 0 || (Pecas(b) == 0 && mola <= 0 && !mestre)) return real;
            int chave = (int)t | (Pecas(b) << 4) | (Mathf.Clamp(mola, 0, 3) << 12) | ((mestre ? 1 : 0) << 14);
            if (_dados.TryGetValue(chave, out var d)) return d;
            d = real.Copia();
            if (mestre) d.dano = real.dano / Armas.DanoDaSucata;   // (o Mestre armeiro: batem como as reais)
            int s = Silenciador(b);
            if (s != 0)
            {
                d.dano *= DanoDoSilenciador;
                d.barulho = real.barulho * (s == PET ? BarulhoDoPET : BarulhoDoFiltro);
            }
            d.boca = Boca(t, b);
            int r = Reforco(b);
            d.dano *= 1f + DanoPorReforco * r;
            d.desgaste = real.desgaste * (1f + DesgastePorReforco * r);
            if (CarregadorLongo(b))
                d.capacidade = t == TipoDeArma.PistolaSucata ? PenteLongoDaPistola : t == TipoDeArma.SubmetralhadoraSucata ? PenteLongoDaSubmetralhadora : d.capacidade;
            // a luneta de cano (só no rifle): mirando, a câmera fecha e a tela da luneta da sniper aparece
            if (t == TipoDeArma.RifleSucata && MiraDeCima(b) == ComLuneta) d.fovLuneta = FovDaLuneta;
            if (mola > 0) d.emperra = real.emperra * Mathf.Max(0f, 1f - MenosEmperraPorMola * mola);
            _dados[chave] = d;
            return d;
        }

        /// <summary>
        /// O espalhamento das miras (vezes o da arma): a de ferro ajustada sempre que a arma usa a mira (−15%); mirando, a
        /// melhor entre ela, a de ponto (−30%) e a luneta (−60%). 'semAMira': o rifle de quadril (as miras não ajudam).
        /// </summary>
        public static float FatorDaMira(ulong b, bool mirando, bool semAMira)
        {
            if (semAMira || Pecas(b) == 0) return 1f;
            float f = MiraDeFerro(b) ? EspalhaDaMiraDeFerro : 1f;
            if (mirando)
            {
                int c = MiraDeCima(b);
                if (c == Ponto) f = Mathf.Min(f, EspalhaDaMiraDePonto);
                else if (c == ComLuneta) f = Mathf.Min(f, EspalhaDaLuneta);
            }
            return f;
        }

        /// <summary>O desgaste de cada tiro com o cano reforçado, vezes o de sem ele.</summary>
        public static float FatorDoDesgaste(ulong b) => 1f + DesgastePorReforco * Reforco(b);

        /// <summary>O volume do tiro (e o alcance do som) com o silenciador, vezes o de sem ele.</summary>
        public static float VolumeDoTiro(ulong b) => Silenciador(b) == PET ? VolumeDoPET : Silenciador(b) == Filtro ? VolumeDoFiltro : 1f;

        /// <summary>O Mestre armeiro: quantas sucatas a receita do Armeiro de sucata gasta (25% menos, para cima).</summary>
        public static int SucataComOMestre(int n) => Mathf.Max(1, Mathf.CeilToInt(n * SucataDoMestre - 1e-4f));
    }
}
