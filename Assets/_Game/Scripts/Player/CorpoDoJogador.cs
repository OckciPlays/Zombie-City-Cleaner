using System;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// As 6 partes do corpo do jogador (05/11/2026, o sistema de vida por membro). Os braços e as pernas são da esquerda e da
    /// direita do próprio jogador (como o .L e o .R dos ossos). Não confundir com o <c>Membro</c> e a <c>RegiaoDoCorpo</c>
    /// dos zumbis (Zombies/Hitbox.cs).
    /// </summary>
    public enum ParteDoCorpo : byte { Cabeca, Tronco, BracoE, BracoD, PernaE, PernaD }

    /// <summary>
    /// O jeito do golpe, que decide o que ele abre (05/11/2026): a mordida e o arranhão do Oco e do Corredor (20% de ferida),
    /// o corte (faca, machado, lança), a pancada (bastão, soco, tocha), o tiro (sempre abre ferida), o esmagamento do
    /// Brutamonte (fratura, nunca sangra), a queda, o tornado e o raio. Nenhum: só a vida geral (o tronco), sem ferida.
    /// </summary>
    public enum TipoDeGolpe : byte { Nenhum, Mordida, Corte, Pancada, Tiro, Esmagamento, Queda, Tornado, Raio }

    /// <summary>
    /// A lesão de um braço ou de uma perna (05/11/2026): a entorse (o tornozelo torcido, numa queda pequena), a fratura e a
    /// fratura exposta (que também sangra; na etapa 2, com grande risco de infecção). Sara com a tala, mais rápido com o
    /// analgésico agindo.
    /// </summary>
    public enum Lesao : byte { Nenhuma, Entorse, Fratura, Exposta }

    /// <summary>
    /// O corpo do jogador (05/11/2026), como vai pela rede e no save: o DANO de cada parte (0 = inteira, 100 = inutilizada;
    /// guardar o dano e não a vida faz o vazio, o <c>default</c>, ser o corpo sadio: no nascer, no save antigo, no pacote sem
    /// o campo), as feridas (sangrando ou enfaixadas, e os minutos do jogo que faltam para fechar), as lesões dos 4 membros
    /// (com a tala e os minutos que faltam para sarar) e o analgésico (os minutos do jogo que faltam). Uns 35 bytes. Escrito
    /// pelo anfitrião (<see cref="VidaJogador.Corpo"/>); o dono grava no save (o JsonUtility grava os campos inteiros).
    /// </summary>
    [Serializable]
    public struct EstadoDoCorpo : INetworkSerializeByMemcpy, IEquatable<EstadoDoCorpo>
    {
        /// <summary>O dano de cada parte (0 a 100), na ordem de <see cref="ParteDoCorpo"/>.</summary>
        public byte dCabeca, dTronco, dBracoE, dBracoD, dPernaE, dPernaD;
        /// <summary>Um bit por parte (1 &lt;&lt; parte): a ferida sangra (sem curativo).</summary>
        public byte sangra;
        /// <summary>Um bit por parte: a ferida está enfaixada (a faixa aparece no corpo).</summary>
        public byte faixa;
        /// <summary>Um bit por parte (só os membros): a tala.</summary>
        public byte talas;
        /// <summary>A lesão de cada membro (<see cref="Lesao"/>).</summary>
        public byte lBracoE, lBracoD, lPernaE, lPernaD;
        /// <summary>Os minutos do jogo que faltam para a lesão de cada membro sarar (com a tala e o analgésico).</summary>
        public ushort sBracoE, sBracoD, sPernaE, sPernaD;
        /// <summary>Os minutos do jogo que faltam para a ferida de cada parte fechar (0: sem ferida).</summary>
        public ushort fCabeca, fTronco, fBracoE, fBracoD, fPernaE, fPernaD;
        /// <summary>Os minutos do jogo que faltam do analgésico (os comprimidos).</summary>
        public ushort analgesico;

        // ---- a etapa 2 (05/11/2026): a limpeza e a infecção. Tudo zerado = sadio, como o resto ----
        /// <summary>A semente do sorteio da infecção (0: ainda não sorteada). Vai no save: sair e voltar não muda o sorteio.</summary>
        public uint semente;
        /// <summary>Quantos amanheceres o personagem já viveu (o sorteio de cada um é fixo) e os minutos desde o último.</summary>
        public ushort amanheceres, desdeOAmanhecer;
        /// <summary>A idade de cada ferida (horas do jogo, até 255), na ordem das partes.</summary>
        public byte iCabeca, iTronco, iBracoE, iBracoD, iPernaE, iPernaD;
        /// <summary>As horas que cada ferida ficou suja desde o último sorteio (até 255).</summary>
        public byte uCabeca, uTronco, uBracoE, uBracoD, uPernaE, uPernaD;
        /// <summary>Os dias já sorteados de cada ferida (2 bits por parte: 0 a 3).</summary>
        public ushort dias;
        /// <summary>Um bit por parte: o curativo antisséptico protege até o amanhecer (a bandagem antisséptica, o spray com a bandagem, o kit).</summary>
        public byte protegida;
        /// <summary>Um bit por parte: o spray limpou a ferida (falta enfaixar).</summary>
        public byte limpa;
        /// <summary>Um bit por parte: o antibiótico cobriu a ferida (ela não sorteia mais).</summary>
        public byte coberta;
        /// <summary>A infecção do personagem (0 nenhuma, 1 a ferida infeccionada, 2 a febre, 3 a sepse) e a parte de onde veio (+1).</summary>
        public byte infeccao, origem;
        /// <summary>Os minutos do jogo que faltam na fase da infecção; até a próxima dose do antibiótico; do xarope (a febre aliviada).</summary>
        public ushort infeccaoFalta, doseFalta, xarope;

        public const int Partes = 6;

        static byte Pega(ParteDoCorpo p, byte c, byte t, byte bE, byte bD, byte pE, byte pD) => p switch
        {
            ParteDoCorpo.Cabeca => c, ParteDoCorpo.Tronco => t, ParteDoCorpo.BracoE => bE, ParteDoCorpo.BracoD => bD,
            ParteDoCorpo.PernaE => pE, _ => pD,
        };

        /// <summary>A idade da ferida (horas do jogo).</summary>
        public int Idade(ParteDoCorpo p) => Pega(p, iCabeca, iTronco, iBracoE, iBracoD, iPernaE, iPernaD);
        /// <summary>As horas suja desde o último sorteio.</summary>
        public int Sujas(ParteDoCorpo p) => Pega(p, uCabeca, uTronco, uBracoE, uBracoD, uPernaE, uPernaD);
        public int Dias(ParteDoCorpo p) => (dias >> ((int)p * 2)) & 3;

        public void PorIdade(ParteDoCorpo p, int horas)
        {
            byte b = (byte)Mathf.Clamp(horas, 0, 255);
            switch (p)
            {
                case ParteDoCorpo.Cabeca: iCabeca = b; break;
                case ParteDoCorpo.Tronco: iTronco = b; break;
                case ParteDoCorpo.BracoE: iBracoE = b; break;
                case ParteDoCorpo.BracoD: iBracoD = b; break;
                case ParteDoCorpo.PernaE: iPernaE = b; break;
                default: iPernaD = b; break;
            }
        }

        public void PorSujas(ParteDoCorpo p, int horas)
        {
            byte b = (byte)Mathf.Clamp(horas, 0, 255);
            switch (p)
            {
                case ParteDoCorpo.Cabeca: uCabeca = b; break;
                case ParteDoCorpo.Tronco: uTronco = b; break;
                case ParteDoCorpo.BracoE: uBracoE = b; break;
                case ParteDoCorpo.BracoD: uBracoD = b; break;
                case ParteDoCorpo.PernaE: uPernaE = b; break;
                default: uPernaD = b; break;
            }
        }

        public void PorDias(ParteDoCorpo p, int d)
        {
            int s = (int)p * 2;
            dias = (ushort)((dias & ~(3 << s)) | (Mathf.Clamp(d, 0, 3) << s));
        }

        public bool Protegida(ParteDoCorpo p) => (protegida & (1 << (int)p)) != 0;
        public bool Limpa(ParteDoCorpo p) => (limpa & (1 << (int)p)) != 0;
        public bool Coberta(ParteDoCorpo p) => (coberta & (1 << (int)p)) != 0;

        /// <summary>A fase da infecção: 0 nenhuma, 1 a ferida infeccionada, 2 a febre, 3 a sepse.</summary>
        public int Infeccao => infeccao;
        public bool Infectado => infeccao > 0;
        /// <summary>A parte de onde a infecção veio (só com <see cref="Infectado"/>).</summary>
        public ParteDoCorpo OrigemDaInfeccao => (ParteDoCorpo)Mathf.Clamp(origem - 1, 0, 5);
        /// <summary>Os sintomas da febre (a febre ou a sepse, sem o xarope agindo): o tremor, a visão turva, o calafrio.</summary>
        public bool ComFebre => infeccao >= 2 && xarope == 0;
        public bool Sepse => infeccao == 3;

        public int Dano(ParteDoCorpo p) => p switch
        {
            ParteDoCorpo.Cabeca => dCabeca, ParteDoCorpo.Tronco => dTronco, ParteDoCorpo.BracoE => dBracoE,
            ParteDoCorpo.BracoD => dBracoD, ParteDoCorpo.PernaE => dPernaE, _ => dPernaD,
        };

        public void PorDano(ParteDoCorpo p, int dano)
        {
            byte b = (byte)Mathf.Clamp(dano, 0, 100);
            switch (p)
            {
                case ParteDoCorpo.Cabeca: dCabeca = b; break;
                case ParteDoCorpo.Tronco: dTronco = b; break;
                case ParteDoCorpo.BracoE: dBracoE = b; break;
                case ParteDoCorpo.BracoD: dBracoD = b; break;
                case ParteDoCorpo.PernaE: dPernaE = b; break;
                default: dPernaD = b; break;
            }
        }

        /// <summary>A vida da parte (100 - o dano).</summary>
        public int Vida(ParteDoCorpo p) => 100 - Dano(p);
        /// <summary>A parte chegou a 0: inutilizada (o braço não segura arma longa; a perna manca forte).</summary>
        public bool Inutil(ParteDoCorpo p) => Dano(p) >= 100;
        public bool Sangra(ParteDoCorpo p) => (sangra & (1 << (int)p)) != 0;
        public bool Faixa(ParteDoCorpo p) => (faixa & (1 << (int)p)) != 0;
        public bool Tala(ParteDoCorpo p) => (talas & (1 << (int)p)) != 0;
        /// <summary>Tem ferida (sangrando ou enfaixada).</summary>
        public bool Ferida(ParteDoCorpo p) => Sangra(p) || FeridaFalta(p) > 0;

        public int FeridaFalta(ParteDoCorpo p) => p switch
        {
            ParteDoCorpo.Cabeca => fCabeca, ParteDoCorpo.Tronco => fTronco, ParteDoCorpo.BracoE => fBracoE,
            ParteDoCorpo.BracoD => fBracoD, ParteDoCorpo.PernaE => fPernaE, _ => fPernaD,
        };

        public void PorFerida(ParteDoCorpo p, int minutos)
        {
            ushort v = (ushort)Mathf.Clamp(minutos, 0, ushort.MaxValue);
            switch (p)
            {
                case ParteDoCorpo.Cabeca: fCabeca = v; break;
                case ParteDoCorpo.Tronco: fTronco = v; break;
                case ParteDoCorpo.BracoE: fBracoE = v; break;
                case ParteDoCorpo.BracoD: fBracoD = v; break;
                case ParteDoCorpo.PernaE: fPernaE = v; break;
                default: fPernaD = v; break;
            }
        }

        /// <summary>A lesão do membro (a cabeça e o tronco não têm).</summary>
        public Lesao LesaoDe(ParteDoCorpo p) => (Lesao)(p switch
        {
            ParteDoCorpo.BracoE => lBracoE, ParteDoCorpo.BracoD => lBracoD, ParteDoCorpo.PernaE => lPernaE,
            ParteDoCorpo.PernaD => lPernaD, _ => 0,
        });

        public void PorLesao(ParteDoCorpo p, Lesao l)
        {
            byte b = (byte)l;
            switch (p)
            {
                case ParteDoCorpo.BracoE: lBracoE = b; break;
                case ParteDoCorpo.BracoD: lBracoD = b; break;
                case ParteDoCorpo.PernaE: lPernaE = b; break;
                case ParteDoCorpo.PernaD: lPernaD = b; break;
            }
        }

        public int SararFalta(ParteDoCorpo p) => p switch
        {
            ParteDoCorpo.BracoE => sBracoE, ParteDoCorpo.BracoD => sBracoD, ParteDoCorpo.PernaE => sPernaE,
            ParteDoCorpo.PernaD => sPernaD, _ => 0,
        };

        public void PorSarar(ParteDoCorpo p, int minutos)
        {
            ushort v = (ushort)Mathf.Clamp(minutos, 0, ushort.MaxValue);
            switch (p)
            {
                case ParteDoCorpo.BracoE: sBracoE = v; break;
                case ParteDoCorpo.BracoD: sBracoD = v; break;
                case ParteDoCorpo.PernaE: sPernaE = v; break;
                case ParteDoCorpo.PernaD: sPernaD = v; break;
            }
        }

        public void PorBit(ref byte campo, ParteDoCorpo p, bool ligado)
        {
            int b = 1 << (int)p;
            campo = (byte)(ligado ? campo | b : campo & ~b);
        }

        /// <summary>O analgésico está agindo (sem dor: a fratura com tala sara no tempo cheio).</summary>
        public bool ComAnalgesico => analgesico > 0;

        /// <summary>O braço está ruim (inutilizado ou quebrado, com ou sem tala): o que ele faz, faz com dificuldade.</summary>
        public bool BracoRuim(ParteDoCorpo p) => Inutil(p) || LesaoDe(p) >= Lesao.Fratura;
        public bool AlgumBracoRuim => BracoRuim(ParteDoCorpo.BracoE) || BracoRuim(ParteDoCorpo.BracoD);
        /// <summary>
        /// O braço não aguenta arma de duas mãos: inutilizado ou quebrado SEM a tala (05/11/2026, a pedido: com a tala, "ele
        /// poderá realizar, porém com dificuldades": atira, golpeia e dirige, com a mira tremendo e o golpe mais lento).
        /// </summary>
        public bool BracoImpede(ParteDoCorpo p) => (Inutil(p) || LesaoDe(p) >= Lesao.Fratura) && !Tala(p);
        public bool AlgumBracoImpede => BracoImpede(ParteDoCorpo.BracoE) || BracoImpede(ParteDoCorpo.BracoD);
        /// <summary>A perna está ruim (inutilizada, torcida ou quebrada, com ou sem tala).</summary>
        public bool PernaRuim(ParteDoCorpo p) => Inutil(p) || LesaoDe(p) != Lesao.Nenhuma;
        public bool AlgumaPernaRuim => PernaRuim(ParteDoCorpo.PernaE) || PernaRuim(ParteDoCorpo.PernaD);
        /// <summary>A perna não deixa correr nem pular a janela: inutilizada, torcida ou quebrada SEM a tala.</summary>
        public bool PernaImpede(ParteDoCorpo p) => (Inutil(p) || LesaoDe(p) != Lesao.Nenhuma) && !Tala(p);
        public bool AlgumaPernaImpede => PernaImpede(ParteDoCorpo.PernaE) || PernaImpede(ParteDoCorpo.PernaD);
        /// <summary>Alguma lesão sem tala, ou com a tala mas sem o analgésico: dói.</summary>
        public bool ComDor
        {
            get
            {
                for (int m = (int)ParteDoCorpo.BracoE; m <= (int)ParteDoCorpo.PernaD; m++)
                    if (LesaoDe((ParteDoCorpo)m) != Lesao.Nenhuma && !ComAnalgesico) return true;
                return false;
            }
        }

        public bool AlgumSangrando => sangra != 0;
        /// <summary>Alguma parte machucada, sangrando, enfaixada ou com lesão (a silhueta do HUD aparece).</summary>
        public bool AlgoErrado => dCabeca + dTronco + dBracoE + dBracoD + dPernaE + dPernaD > 0 || sangra != 0 || faixa != 0
                                  || lBracoE + lBracoD + lPernaE + lPernaD > 0 || infeccao > 0;

        public bool Equals(EstadoDoCorpo o) =>
            dCabeca == o.dCabeca && dTronco == o.dTronco && dBracoE == o.dBracoE && dBracoD == o.dBracoD && dPernaE == o.dPernaE && dPernaD == o.dPernaD
            && sangra == o.sangra && faixa == o.faixa && talas == o.talas
            && lBracoE == o.lBracoE && lBracoD == o.lBracoD && lPernaE == o.lPernaE && lPernaD == o.lPernaD
            && sBracoE == o.sBracoE && sBracoD == o.sBracoD && sPernaE == o.sPernaE && sPernaD == o.sPernaD
            && fCabeca == o.fCabeca && fTronco == o.fTronco && fBracoE == o.fBracoE && fBracoD == o.fBracoD && fPernaE == o.fPernaE && fPernaD == o.fPernaD
            && analgesico == o.analgesico
            && semente == o.semente && amanheceres == o.amanheceres && desdeOAmanhecer == o.desdeOAmanhecer
            && iCabeca == o.iCabeca && iTronco == o.iTronco && iBracoE == o.iBracoE && iBracoD == o.iBracoD && iPernaE == o.iPernaE && iPernaD == o.iPernaD
            && uCabeca == o.uCabeca && uTronco == o.uTronco && uBracoE == o.uBracoE && uBracoD == o.uBracoD && uPernaE == o.uPernaE && uPernaD == o.uPernaD
            && dias == o.dias && protegida == o.protegida && limpa == o.limpa && coberta == o.coberta
            && infeccao == o.infeccao && origem == o.origem && infeccaoFalta == o.infeccaoFalta && doseFalta == o.doseFalta && xarope == o.xarope;

        public override bool Equals(object o) => o is EstadoDoCorpo e && Equals(e);
        public override int GetHashCode() => HashCode.Combine(dCabeca | dTronco << 8 | dBracoE << 16 | dBracoD << 24, dPernaE | dPernaD << 8 | sangra << 16 | faixa << 24,
                                                              talas | lBracoE << 8 | lBracoD << 16 | lPernaE << 24, lPernaD, sPernaE + sPernaD + sBracoE + sBracoD, analgesico);

        /// <summary>O save (ou o pacote) com números fora da faixa volta para dentro dela.</summary>
        public EstadoDoCorpo Conferido()
        {
            var e = this;
            for (int p = 0; p < Partes; p++) e.PorDano((ParteDoCorpo)p, e.Dano((ParteDoCorpo)p));
            for (int m = (int)ParteDoCorpo.BracoE; m <= (int)ParteDoCorpo.PernaD; m++)
                if (e.LesaoDe((ParteDoCorpo)m) > Lesao.Exposta) e.PorLesao((ParteDoCorpo)m, Lesao.Nenhuma);
            e.talas &= 0b111100;
            e.sangra &= 0b111111;
            e.faixa &= 0b111111;
            e.protegida &= 0b111111;
            e.limpa &= 0b111111;
            e.coberta &= 0b111111;
            if (e.infeccao > 3) e.infeccao = 0;
            if (e.infeccao == 0) e.origem = 0;
            else if (e.origem < 1 || e.origem > Partes) e.origem = 2;   // (sem parte: o tronco)
            return e;
        }
    }

    /// <summary>
    /// As contas do corpo que o dono e o anfitrião fazem iguais (05/11/2026): os nomes das partes, a parte pelo ponto do
    /// acerto, o sorteio da parte pelo jeito do golpe, o que cada remédio trata (<see cref="PodeTratar"/>) e os efeitos que
    /// o dono sente (a perna que manca, a mira que treme, a arma de duas mãos que o braço quebrado não segura).
    /// </summary>
    public static class PartesDoCorpo
    {
        /// <summary>"Sorteie a parte": o golpe não sabe onde pegou.</summary>
        public const ParteDoCorpo Sortear = (ParteDoCorpo)255;
        public const int Quantas = EstadoDoCorpo.Partes;

        public static bool EhMembro(ParteDoCorpo p) => p >= ParteDoCorpo.BracoE && p <= ParteDoCorpo.PernaD;
        public static bool EhBraco(ParteDoCorpo p) => p == ParteDoCorpo.BracoE || p == ParteDoCorpo.BracoD;
        public static bool EhPerna(ParteDoCorpo p) => p == ParteDoCorpo.PernaE || p == ParteDoCorpo.PernaD;
        public static bool Valida(ParteDoCorpo p) => (byte)p < Quantas;

        /// <summary>"o braço esquerdo", "a perna direita"...</summary>
        public static string NomeComArtigo(ParteDoCorpo p) => p switch
        {
            ParteDoCorpo.Cabeca => "a cabeça", ParteDoCorpo.Tronco => "o tronco", ParteDoCorpo.BracoE => "o braço esquerdo",
            ParteDoCorpo.BracoD => "o braço direito", ParteDoCorpo.PernaE => "a perna esquerda", _ => "a perna direita",
        };

        /// <summary>"braço esquerdo" (sem o artigo).</summary>
        public static string Nome(ParteDoCorpo p) => NomeComArtigo(p).Substring(2);

        /// <summary>Para o HUD e as etiquetas: "CABEÇA", "BRAÇO ESQ."...</summary>
        public static string NomeCurto(ParteDoCorpo p) => p switch
        {
            ParteDoCorpo.Cabeca => "CABEÇA", ParteDoCorpo.Tronco => "TRONCO", ParteDoCorpo.BracoE => "BRAÇO ESQ.",
            ParteDoCorpo.BracoD => "BRAÇO DIR.", ParteDoCorpo.PernaE => "PERNA ESQ.", _ => "PERNA DIR.",
        };

        /// <summary>"no braço esquerdo", "na perna direita".</summary>
        public static string NaParte(ParteDoCorpo p) => (NomeComArtigo(p)[0] == 'a' ? "na " : "no ") + Nome(p);

        /// <summary>"entorse", "fratura", "fratura exposta".</summary>
        public static string NomeDaLesao(Lesao l) => l switch
        {
            Lesao.Entorse => "entorse", Lesao.Fratura => "fratura", Lesao.Exposta => "fratura exposta", _ => "",
        };

        /// <summary>
        /// A parte pelo ponto do acerto (o tiro, o golpe com o ponto): pela altura sobre os pés da cápsula de 1,8 m e pelo
        /// lado, no espaço da raiz do jogador (que gira com o olhar). Acima de 1,5 m, a cabeça; de 0,85 a 1,5, o tronco (ou
        /// o braço, a mais de 17 cm do meio); abaixo, a perna do lado.
        /// </summary>
        public static ParteDoCorpo PeloPonto(Transform raiz, Vector3 ponto)
        {
            var l = raiz.InverseTransformPoint(ponto);
            if (l.y >= 1.5f) return ParteDoCorpo.Cabeca;
            if (l.y >= 0.85f) return Mathf.Abs(l.x) >= 0.17f ? (l.x < 0f ? ParteDoCorpo.BracoE : ParteDoCorpo.BracoD) : ParteDoCorpo.Tronco;
            return l.x < 0f ? ParteDoCorpo.PernaE : ParteDoCorpo.PernaD;
        }

        // os pesos de cada parte por jeito do golpe (cabeça, tronco, braço E, braço D, perna E, perna D)
        static readonly float[] PesosDaMordida = { 10f, 30f, 20f, 20f, 10f, 10f };
        static readonly float[] PesosDoRastejante = { 0f, 15f, 5f, 5f, 37.5f, 37.5f };
        static readonly float[] PesosDoCorredor = { 10f, 35f, 25f, 25f, 2.5f, 2.5f };
        static readonly float[] PesosDaBriga = { 20f, 40f, 15f, 15f, 5f, 5f };
        static readonly float[] PesosDoAvanco = { 5f, 40f, 12.5f, 12.5f, 15f, 15f };
        static readonly float[] PesosDoChao = { 0f, 10f, 0f, 0f, 45f, 45f };
        static readonly float[] PesosDoTornado = { 15f, 35f, 12.5f, 12.5f, 12.5f, 12.5f };

        /// <summary>Os pesos para sortear a parte (os zumbis escolhem entre eles: o de pé, o rastejante, o Corredor...).</summary>
        public enum Pesos : byte { Mordida, Rastejante, Corredor, Briga, Avanco, Chao, Tornado }

        static float[] Tabela(Pesos p) => p switch
        {
            Pesos.Rastejante => PesosDoRastejante, Pesos.Corredor => PesosDoCorredor, Pesos.Briga => PesosDaBriga,
            Pesos.Avanco => PesosDoAvanco, Pesos.Chao => PesosDoChao, Pesos.Tornado => PesosDoTornado, _ => PesosDaMordida,
        };

        /// <summary>Os pesos que cada jeito de golpe usa, quando quem bate não escolhe.</summary>
        public static Pesos PesosDo(TipoDeGolpe t) => t switch
        {
            TipoDeGolpe.Corte or TipoDeGolpe.Pancada or TipoDeGolpe.Tiro => Pesos.Briga,
            TipoDeGolpe.Esmagamento => Pesos.Chao,
            TipoDeGolpe.Tornado => Pesos.Tornado,
            _ => Pesos.Mordida,
        };

        /// <summary>
        /// Sorteia a parte atingida: os pesos da tabela, com o lado de quem bate valendo 1,6 vez (o braço e a perna desse
        /// lado) e a altura dele (bem mais baixo: as pernas valem 3 vezes e a cabeça quase nada; bem mais alto, o contrário).
        /// </summary>
        public static ParteDoCorpo SortearParte(Pesos qual, Transform alvo, Vector3 de)
        {
            var t = Tabela(qual);
            Span<float> w = stackalloc float[Quantas];
            for (int i = 0; i < Quantas; i++) w[i] = t[i];
            if (alvo != null)
            {
                var l = alvo.InverseTransformPoint(de);
                bool esquerda = l.x < 0f;
                if (Mathf.Abs(l.x) > 0.1f)
                {
                    w[esquerda ? 2 : 3] *= 1.6f;
                    w[esquerda ? 4 : 5] *= 1.6f;
                }
                float dy = l.y;   // os pés de quem bate em relação aos pés do alvo
                if (dy < -0.35f) { w[4] *= 3f; w[5] *= 3f; w[0] *= 0.3f; }
                else if (dy > 0.35f) { w[0] *= 2f; w[4] *= 0.3f; w[5] *= 0.3f; }
            }
            float soma = 0f;
            for (int i = 0; i < Quantas; i++) soma += w[i];
            float r = UnityEngine.Random.value * soma;
            for (int i = 0; i < Quantas; i++)
            {
                r -= w[i];
                if (r <= 0f && w[i] > 0f) return (ParteDoCorpo)i;
            }
            return ParteDoCorpo.Tronco;
        }

        /// <summary>As armas que pedem as duas mãos (um braço quebrado ou inutilizado não deixa mirar nem atirar com elas).</summary>
        public static bool DuasMaos(Item i) => i == Item.Rifle || i == Item.Submetralhadora || i == Item.Espingarda || i == Item.Sniper
                                               || i == Item.Machado || i == Item.Bastao || i == Item.Lanca;

        // ---------------- os tratamentos ----------------

        /// <summary>
        /// Os itens que tratam o corpo (vão pela barra de tempo e pelo TratarRpc, não pelo comer): os curativos, a tala, o kit,
        /// os comprimidos, o xarope e, desde a etapa 2, a bandagem antisséptica, o spray e o antibiótico.
        /// </summary>
        public static bool EhTratamento(Item i) => i == Item.Atadura || i == Item.Bandagem || i == Item.KitMedico || i == Item.Tala
                                                   || i == Item.Comprimidos || i == Item.Remedio || i == Item.BandagemAntisseptica
                                                   || i == Item.SprayAntisseptico || i == Item.Antibiotico;

        /// <summary>O kit trata o corpo todo (a parte não importa).</summary>
        public static bool CorpoTodo(Item i) => i == Item.KitMedico;

        /// <summary>O analgésico (os comprimidos): tira a dor.</summary>
        public static bool EhAnalgesico(Item i) => i == Item.Comprimidos;

        /// <summary>Pela boca, não numa parte: os comprimidos, o xarope (a febre) e o antibiótico (a infecção).</summary>
        public static bool PelaBoca(Item i) => i == Item.Comprimidos || i == Item.Remedio || i == Item.Antibiotico;

        /// <summary>As faixas: a atadura, a bandagem de pano e a antisséptica.</summary>
        public static bool Enfaixa(Item i) => i == Item.Atadura || i == Item.Bandagem || i == Item.BandagemAntisseptica;

        /// <summary>
        /// Quanto tempo a aplicação leva (s reais): a atadura 3, a bandagem de pano e a antisséptica 4, a tala 5, o kit 6, o
        /// spray 2, o que se toma 1,7.
        /// </summary>
        public static float TempoDe(Item i) => i switch
        {
            Item.Atadura => 3f, Item.Bandagem => 4f, Item.BandagemAntisseptica => 4f, Item.Tala => 5f, Item.KitMedico => 6f,
            Item.SprayAntisseptico => 2f, _ => 40 / 24f,
        };

        /// <summary>"Enfaixando", "Pondo a tala", "Usando o kit", "Borrifando o spray", "Tomando o antibiótico"...</summary>
        public static string Acao(Item i) => i switch
        {
            Item.Atadura or Item.Bandagem or Item.BandagemAntisseptica => "Enfaixando", Item.Tala => "Pondo a tala",
            Item.KitMedico => "Usando o kit médico", Item.SprayAntisseptico => "Borrifando o spray",
            Item.Antibiotico => "Tomando o antibiótico", Item.Remedio => "Tomando o xarope", _ => "Tomando o analgésico",
        };

        /// <summary>
        /// O item pode tratar o corpo agora? Escolhe a parte quando ela vem <see cref="Sortear"/> (automático: o pior caso
        /// primeiro) e diz o motivo quando não pode (o item não é gasto). O dono confere antes de começar a barra, e o
        /// anfitrião confere de novo no fim.
        /// </summary>
        public static bool PodeTratar(in EstadoDoCorpo e, float vidaGeral, float vidaMaxima, Item item, ref ParteDoCorpo parte, out string motivo)
        {
            motivo = null;
            switch (item)
            {
                case Item.KitMedico:
                {
                    parte = ParteDoCorpo.Tronco;
                    if (vidaGeral < vidaMaxima - 0.5f || AlgoErradoParaOKit(e)) return true;
                    motivo = "Nada para tratar: você está inteiro";
                    return false;
                }
                case Item.Atadura:
                case Item.Bandagem:
                case Item.BandagemAntisseptica:
                {
                    bool antisseptica = item == Item.BandagemAntisseptica;
                    if (!Valida(parte))
                    {
                        // automático: a parte que sangra (a mais machucada); depois a ferida que precisa da troca do dia;
                        // depois a mais machucada
                        int sangrando = -1, troca = -1, pior = -1;
                        for (int p = 0; p < Quantas; p++)
                        {
                            var pp = (ParteDoCorpo)p;
                            if (e.Sangra(pp) && (sangrando < 0 || e.Dano(pp) > e.Dano((ParteDoCorpo)sangrando))) sangrando = p;
                            if (troca < 0 && PrecisaDaTroca(e, pp, antisseptica)) troca = p;
                            if (e.Dano(pp) > 0 && (pior < 0 || e.Dano(pp) > e.Dano((ParteDoCorpo)pior))) pior = p;
                        }
                        int escolhida = sangrando >= 0 ? sangrando : troca >= 0 ? troca : pior;
                        if (escolhida < 0)
                        {
                            motivo = "Nada para enfaixar";
                            return false;
                        }
                        parte = (ParteDoCorpo)escolhida;
                        return true;
                    }
                    if (e.Sangra(parte) || e.Dano(parte) > 0 || PrecisaDaTroca(e, parte, antisseptica)) return true;
                    string fim = NomeComArtigo(parte)[0] == 'a' ? "a" : "o";
                    motivo = e.Protegida(parte) ? $"{Maiuscula(NomeComArtigo(parte))} já está protegid{fim} até o amanhecer"
                           : e.Faixa(parte) ? $"{Maiuscula(NomeComArtigo(parte))} já está enfaixad{fim}"
                           : $"{Maiuscula(NomeComArtigo(parte))} não precisa de curativo";
                    return false;
                }
                case Item.SprayAntisseptico:
                {
                    if (!Valida(parte))
                    {
                        // automático: a ferida que ainda não está limpa nem protegida (a que sangra primeiro)
                        int achada = -1;
                        for (int p = 0; p < Quantas; p++)
                        {
                            var pp = (ParteDoCorpo)p;
                            if (!e.Ferida(pp) || e.Limpa(pp) || e.Protegida(pp)) continue;
                            if (achada < 0 || (e.Sangra(pp) && !e.Sangra((ParteDoCorpo)achada))) achada = p;
                        }
                        if (achada < 0)
                        {
                            motivo = "Nenhuma ferida para limpar";
                            return false;
                        }
                        parte = (ParteDoCorpo)achada;
                        return true;
                    }
                    string fim = NomeComArtigo(parte)[0] == 'a' ? "a" : "o";
                    if (!e.Ferida(parte)) { motivo = $"{Maiuscula(NomeComArtigo(parte))} não tem ferida"; return false; }
                    if (e.Protegida(parte)) { motivo = $"{Maiuscula(NomeComArtigo(parte))} já está protegid{fim} até o amanhecer"; return false; }
                    if (e.Limpa(parte)) { motivo = $"{Maiuscula(NomeComArtigo(parte))} já está limp{fim}: enfaixe"; return false; }
                    return true;
                }
                case Item.Tala:
                {
                    if (!Valida(parte))
                    {
                        // automático: a perna primeiro (é a que impede de andar), depois o braço
                        foreach (var m in new[] { ParteDoCorpo.PernaE, ParteDoCorpo.PernaD, ParteDoCorpo.BracoD, ParteDoCorpo.BracoE })
                            if (e.LesaoDe(m) != Lesao.Nenhuma && !e.Tala(m)) { parte = m; return true; }
                        motivo = "Nenhum osso para imobilizar";
                        return false;
                    }
                    if (!EhMembro(parte)) { motivo = "A tala vai num braço ou numa perna"; return false; }
                    if (e.Tala(parte)) { motivo = $"{Maiuscula(NomeComArtigo(parte))} já está com a tala"; return false; }
                    if (e.LesaoDe(parte) == Lesao.Nenhuma) { motivo = $"{Maiuscula(NomeComArtigo(parte))} não está quebrad{(NomeComArtigo(parte)[0] == 'a' ? "a" : "o")} nem torcid{(NomeComArtigo(parte)[0] == 'a' ? "a" : "o")}"; return false; }
                    return true;
                }
                case Item.Comprimidos:
                {
                    parte = ParteDoCorpo.Cabeca;
                    bool lesao = false;
                    for (int m = (int)ParteDoCorpo.BracoE; m <= (int)ParteDoCorpo.PernaD; m++)
                        if (e.LesaoDe((ParteDoCorpo)m) != Lesao.Nenhuma || e.Inutil((ParteDoCorpo)m)) lesao = true;
                    if (!lesao) { motivo = "Nada dói agora: guarde o analgésico"; return false; }
                    if (e.analgesico > 8 * 60) { motivo = "O analgésico ainda está agindo"; return false; }
                    return true;
                }
                case Item.Remedio:
                {
                    // o xarope tira a febre (05/11/2026, a pedido: "Xarope - tira a febre, analgesico tira a dor")
                    parte = ParteDoCorpo.Cabeca;
                    if (e.infeccao < 2) { motivo = "Sem febre: guarde o xarope"; return false; }
                    if (e.xarope > 60) { motivo = "O xarope ainda está agindo"; return false; }
                    return true;
                }
                case Item.Antibiotico:
                {
                    parte = ParteDoCorpo.Cabeca;
                    if (e.infeccao == 0) { motivo = "Sem infecção: o antibiótico não previne, guarde"; return false; }
                    if (e.doseFalta > 0) { motivo = $"A última dose ainda está agindo: a próxima em {HorasDoJogo(e.doseFalta)}"; return false; }
                    return true;
                }
            }
            motivo = "Isso não trata o corpo";
            return false;
        }

        // a ferida enfaixada que pede a troca do dia: sem a proteção (que acaba a cada amanhecer) e com o curativo que protege
        // (a antisséptica; ou a comum por cima do spray)
        static bool PrecisaDaTroca(in EstadoDoCorpo e, ParteDoCorpo p, bool antisseptica) =>
            e.Ferida(p) && !e.Protegida(p) && !e.Coberta(p) && (antisseptica || e.Limpa(p));

        /// <summary>"2 h", "40 min" (minutos do jogo).</summary>
        public static string HorasDoJogo(int minutos) => minutos >= 90 ? $"{Mathf.RoundToInt(minutos / 60f)} h" : $"{Mathf.Max(1, minutos)} min";

        // o kit serve se houver qualquer coisa errada no corpo (machucado, sangrando, ferida sem proteção)
        static bool AlgoErradoParaOKit(in EstadoDoCorpo e)
        {
            if (e.dCabeca + e.dTronco + e.dBracoE + e.dBracoD + e.dPernaE + e.dPernaD > 0 || e.sangra != 0) return true;
            for (int p = 0; p < Quantas; p++)
                if (e.Ferida((ParteDoCorpo)p) && !e.Protegida((ParteDoCorpo)p) && !e.Coberta((ParteDoCorpo)p)) return true;
            return false;
        }

        static string Maiuscula(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        // ---------------- os efeitos no dono ----------------

        /// <summary>
        /// O quanto a perna deixa andar (1: sã). Torcida 0,75 (0,9 com a tala); quebrada ou exposta 0,4 (0,75 com a tala:
        /// anda e até corre, com dificuldade); inutilizada 0,5. Com a dor (sem o analgésico), mais 10% a menos. As duas pernas
        /// ruins se multiplicam (no mínimo 0,3).
        /// </summary>
        public static float FatorDaPerna(in EstadoDoCorpo e, ParteDoCorpo perna)
        {
            float f = 1f;
            var l = e.LesaoDe(perna);
            bool tala = e.Tala(perna);
            if (l == Lesao.Entorse) f = tala ? 0.9f : 0.75f;
            else if (l >= Lesao.Fratura) f = tala ? 0.75f : 0.4f;
            if (e.Inutil(perna)) f = Mathf.Min(f, tala ? 0.75f : 0.5f);
            else if (e.Vida(perna) < 50) f = Mathf.Min(f, 0.9f);
            if (l != Lesao.Nenhuma && !e.ComAnalgesico) f *= 0.9f;
            // a ferida da perna infeccionada (a 1ª fase): a perna fica lenta e manca
            if (e.infeccao == 1 && e.OrigemDaInfeccao == perna) f *= 0.8f;
            return f;
        }

        /// <summary>O fator de andar das duas pernas juntas (no mínimo 0,3).</summary>
        public static float FatorDeAndar(in EstadoDoCorpo e)
        {
            float f = FatorDaPerna(e, ParteDoCorpo.PernaE) * FatorDaPerna(e, ParteDoCorpo.PernaD);
            if (e.Inutil(ParteDoCorpo.Tronco)) f *= 0.85f;
            // a febre e a sepse pesam no corpo todo (o xarope alivia a febre, não a sepse)
            if (e.Sepse) f *= e.xarope > 0 ? 0.85f : 0.75f;
            else if (e.ComFebre) f *= 0.9f;
            // a infecção na cabeça ou no tronco (a 1ª fase): a dor deixa mais lento
            else if (e.infeccao == 1 && !PartesDoCorpo.EhMembro(e.OrigemDaInfeccao)) f *= 0.9f;
            return Mathf.Max(0.3f, f);
        }

        /// <summary>Sem correr: alguma perna torcida, quebrada ou inutilizada sem a tala, ou o tronco a 0.</summary>
        public static bool SemCorrer(in EstadoDoCorpo e) => e.AlgumaPernaImpede || e.Inutil(ParteDoCorpo.Tronco);

        /// <summary>
        /// A altura do pulo que sobra (m): a perna quebrada ou inutilizada 0,1 (0,4 com a tala); torcida 0,4 (0,65 com a tala).
        /// </summary>
        public static float PuloQueSobra(in EstadoDoCorpo e, float pulo)
        {
            float p = pulo;
            foreach (var perna in new[] { ParteDoCorpo.PernaE, ParteDoCorpo.PernaD })
            {
                var l = e.LesaoDe(perna);
                bool tala = e.Tala(perna);
                if (l >= Lesao.Fratura || e.Inutil(perna)) p = Mathf.Min(p, tala ? 0.4f : 0.1f);
                else if (l == Lesao.Entorse) p = Mathf.Min(p, tala ? 0.65f : 0.4f);
            }
            return p;
        }

        /// <summary>
        /// O quanto a mira treme (graus) pelo braço pior: machucado (menos da metade) 0,3; quebrado com a tala 0,6; quebrado
        /// sem a tala ou inutilizado 1,2. O analgésico corta pela metade (a dor é o que mais treme); com a dor, mais 30%.
        /// </summary>
        public static float TremorDaMira(in EstadoDoCorpo e)
        {
            float t = 0f;
            foreach (var b in new[] { ParteDoCorpo.BracoE, ParteDoCorpo.BracoD }) t = Mathf.Max(t, TremorDoBraco(e, b));
            if (t <= 0f) return 0f;
            if (e.ComAnalgesico) t *= 0.5f;
            else if (e.ComDor) t *= 1.3f;
            return t;
        }

        static float TremorDoBraco(in EstadoDoCorpo e, ParteDoCorpo b)
        {
            var l = e.LesaoDe(b);
            // a ferida do braço infeccionada (a 1ª fase) também treme a mira
            if (e.infeccao == 1 && e.OrigemDaInfeccao == b && l < Lesao.Fratura && !e.Inutil(b)) return 0.5f;
            if (e.Inutil(b) || (l >= Lesao.Fratura && !e.Tala(b))) return 1.2f;
            if (l >= Lesao.Fratura) return 0.6f;
            if (e.Vida(b) < 50) return 0.3f;
            return 0f;
        }

        /// <summary>A dispersão a mais no tiro (graus) pelo braço pior: 0,5 machucado, 1,2 com a tala, 2 quebrado ou inutilizado.</summary>
        public static float DispersaoExtra(in EstadoDoCorpo e)
        {
            float t = 0f;
            foreach (var b in new[] { ParteDoCorpo.BracoE, ParteDoCorpo.BracoD }) t = Mathf.Max(t, TremorDoBraco(e, b));
            float d = t >= 1.2f ? 2f : t >= 0.6f ? 1.2f : t > 0f ? 0.5f : 0f;
            return e.ComAnalgesico ? d * 0.7f : d;
        }

        /// <summary>O golpe com a arma branca fica mais lento (o intervalo vezes isto) com um braço ruim: 1,35 com a tala.</summary>
        public static float LentidaoDoGolpe(in EstadoDoCorpo e) => e.AlgumBracoRuim ? 1.35f : 1f;

        /// <summary>
        /// Por que a mira treme (para o HUD: o Pedro tremeu e "nada foi dito na tela o real motivo"): "BRAÇO DIR. COM TALA",
        /// "BRAÇO ESQ. QUEBRADO", "BRAÇO ESQ. MACHUCADO"; null se não treme.
        /// </summary>
        public static string PorQueTreme(in EstadoDoCorpo e)
        {
            ParteDoCorpo pior = ParteDoCorpo.BracoE;
            float t = 0f;
            foreach (var b in new[] { ParteDoCorpo.BracoE, ParteDoCorpo.BracoD })
            {
                float x = TremorDoBraco(e, b);
                if (x > t) { t = x; pior = b; }
            }
            if (t <= 0f) return null;
            var l = e.LesaoDe(pior);
            if (e.infeccao == 1 && e.OrigemDaInfeccao == pior && l < Lesao.Fratura && !e.Inutil(pior)) return NomeCurto(pior) + " INFECCIONADO";
            if (e.Inutil(pior)) return NomeCurto(pior) + (e.Tala(pior) ? " INUTILIZADO (COM TALA)" : " INUTILIZADO");
            if (l >= Lesao.Fratura) return NomeCurto(pior) + (e.Tala(pior) ? " COM TALA" : " QUEBRADO");
            return NomeCurto(pior) + " MACHUCADO";
        }

        /// <summary>
        /// Por que ele manca ou anda devagar (para o HUD): "PERNA ESQ. QUEBRADA", "PERNA DIR. COM TALA", "PERNA ESQ.
        /// INFECCIONADA", "TORNOZELO DIR. TORCIDO"; null se anda normal.
        /// </summary>
        public static string PorQueManca(in EstadoDoCorpo e)
        {
            string melhor = null;
            float pior = 1f;
            foreach (var perna in new[] { ParteDoCorpo.PernaE, ParteDoCorpo.PernaD })
            {
                float f = FatorDaPerna(e, perna);
                if (f >= pior - 0.001f) continue;
                pior = f;
                var l = e.LesaoDe(perna);
                bool tala = e.Tala(perna);
                string lado = perna == ParteDoCorpo.PernaE ? "ESQ." : "DIR.";
                if (e.Inutil(perna) && l < Lesao.Fratura) melhor = tala ? $"PERNA {lado} COM TALA" : $"PERNA {lado} INUTILIZADA";
                else if (l == Lesao.Entorse) melhor = tala ? $"TORNOZELO {lado} COM TALA" : $"TORNOZELO {lado} TORCIDO";
                else if (l >= Lesao.Fratura) melhor = tala ? $"PERNA {lado} COM TALA" : $"PERNA {lado} QUEBRADA";
                else if (e.Inutil(perna)) melhor = $"PERNA {lado} INUTILIZADA";
                else if (e.infeccao == 1 && e.OrigemDaInfeccao == perna) melhor = $"PERNA {lado} INFECCIONADA";
                else melhor = $"PERNA {lado} MACHUCADA";
            }
            return melhor;
        }

        /// <summary>
        /// O volante e os pedais com o corpo ruim (no dono, antes do Carro.Comandar; 05/11/2026, a pedido: com a tala "poderá
        /// dirigir ... porém com dificuldades"): o braço ruim vira menos e o carro puxa devagar para um lado (com a tala,
        /// 0,7 e ±0,06; sem ela, 0,5 e ±0,12); a perna direita ruim (o pedal) acelera menos (0,8 com a tala, 0,6 sem).
        /// <paramref name="m"/>: x = virar, y = acelerar (de -1 a 1).
        /// </summary>
        public static Vector2 NoVolante(in EstadoDoCorpo e, Vector2 m, float tempo)
        {
            float virar = 1f, desvio = 0f;
            foreach (var b in new[] { ParteDoCorpo.BracoE, ParteDoCorpo.BracoD })
            {
                if (!e.BracoRuim(b)) continue;
                bool impede = e.BracoImpede(b);
                virar = Mathf.Min(virar, impede ? 0.5f : 0.7f);
                desvio = Mathf.Max(desvio, impede ? 0.12f : 0.06f);
            }
            m.x *= virar;
            if (desvio > 0f) m.x = Mathf.Clamp(m.x + (Mathf.PerlinNoise(tempo * 0.4f, 7.7f) - 0.5f) * 2f * desvio, -1f, 1f);
            if (e.PernaRuim(ParteDoCorpo.PernaD) && m.y > 0f) m.y *= e.PernaImpede(ParteDoCorpo.PernaD) ? 0.6f : 0.8f;
            return m;
        }

        /// <summary>A perna que faz mancar forte (sem a tala): quebrada, exposta ou inutilizada.</summary>
        public static bool MancaForte(in EstadoDoCorpo e, ParteDoCorpo perna)
        {
            var l = e.LesaoDe(perna);
            return (e.Inutil(perna) || l >= Lesao.Fratura) && !e.Tala(perna);
        }
    }
}
