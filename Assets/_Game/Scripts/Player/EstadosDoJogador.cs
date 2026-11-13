using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os estados do jogador que viram símbolos (12/11/2026), na ordem em que aparecem (o mais urgente primeiro; os bons, no
    /// fim). Cada um tem o ícone <c>Resources/Estados/Estado_&lt;nome&gt;.png</c> (o rosto do personagem mostrando o estado,
    /// do <c>Blender/icones_estados.py</c>).
    /// </summary>
    public enum EstadoDoJogador : byte
    {
        Afogando, Panico, Colapso, Apavorado, Abalado, Susto, Tornado,
        SangrandoMuito, Sangrando, FraturaExposta, Fratura, Entorse, Sepse, Febre, Infeccao, Mancando, BracoInutil,
        // (12/11/2026, a etapa 4 da fauna) a picada da cascavel e o cheiro do gambá
        Envenenado, Fedendo,
        Faminto, Fome, MuitaSede, Sede,
        Exausto, Cansado, Sonolento, AbstinenciaForte, Abstinencia,
        SemVigor, SemFolego, Encharcado, Molhado, Tempestade,
        PertoDoFogo, Bunker,
    }

    /// <summary>
    /// Quais estados o jogador tem agora (12/11/2026, a pedido: "uns símbolos indicando algumas coisas, como cansaço,
    /// sonolento, em pânico, com medo, susto, tempestade, bunker"; "deixar os símbolos até resolver os problemas"; "se está com
    /// fome precisa, faminto também, com sede também, com muita sede também"; "nos entorse, fratura, sepse, infecção,
    /// sangramento... também precisam de um símbolo"). Só os problemas, até se resolverem (sem símbolo para quem está bem),
    /// mais dois bons que valem saber: perto do fogo (secando e aquecendo) e no bunker. A gravidade aparece no rosto (o
    /// ícone muda: com fome → faminto), não na cor. Tudo do que já vai pela rede: o dono lê do próprio personagem.
    /// O HUD (<see cref="EstadosNoHud"/>) mostra os ícones; o nome e o porquê aparecem com o mouse em cima, na aba CORPO.
    /// </summary>
    public static class EstadosDoJogador
    {
        /// <summary>Um estado de agora: o qual, o nome, o porquê (o que fazer) e se é bom.</summary>
        public struct Estado
        {
            public EstadoDoJogador id;
            public string nome, porque;
            public bool bom;
        }

        /// <summary>Com fome ou com sede daqui para baixo (a FomeESede.pouca); faminto e com muita sede daqui para baixo.</summary>
        public const float Faminto = 10f;
        /// <summary>O cansaço (0 a 100) daqui para cima: cansado (100 em 3 dias acordado: 60 é perto de 2 dias).</summary>
        public const float Cansado = 60f, Exausto = 92f;
        /// <summary>O tornado a menos disto (m) vira o símbolo do espanto.</summary>
        public const float TornadoPerto = 350f;

        static readonly StringBuilder _sb = new StringBuilder();

        /// <summary>Os estados de agora do personagem (no dono), na ordem do enum.</summary>
        public static void Montar(VidaJogador v, List<Estado> saida)
        {
            saida.Clear();
            if (v == null || !v.IsSpawned || v.Caido.Value) return;
            uint motivos = v.MotivosDaMente.Value;
            var e = v.Corpo.Value;

            // embaixo da água sem ar
            if (Agua.Submerso(v.transform.position) && v.Folego.Value < 60)
                Por(saida, EstadoDoJogador.Afogando, "Afogando", "Sem ar embaixo da água: a vida cai. Suba já!");

            // a mente
            bool fugindo = ColapsoDoJogador.Local != null && ColapsoDoJogador.Local.Fugindo;
            if (fugindo || (CriseDePanico.Local != null && CriseDePanico.Local.Ativa))
                Por(saida, EstadoDoJogador.Panico, fugindo ? "Fugindo em pânico" : "Crise de pânico",
                    "Falta o ar e o coração dispara. O calmante passa a crise; um lugar seguro e os amigos perto ajudam.");
            int faixa = MenteNoHud.Faixa(v.Sanidade.Value);
            string causas = Causas(motivos);
            if (faixa == 3)
                Por(saida, EstadoDoJogador.Colapso, "Em colapso", "A mente quase acabou: você vê e ouve coisas que não existem e pode desmaiar. Calmante, dormir e o bunker." + causas);
            else if (faixa == 2)
                Por(saida, EstadoDoJogador.Apavorado, "Apavorado", "A mente está baixa: sons falsos, vultos e crises de pânico. Fique perto do fogo, dos amigos ou no bunker." + causas);
            else if (faixa == 1)
                Por(saida, EstadoDoJogador.Abalado, "Abalado", "A mente está caindo." + causas);
            if ((motivos & VidaJogador.Bit(MotivoDaMente.Susto)) != 0)
                Por(saida, EstadoDoJogador.Susto, "Susto", "Um susto agora há pouco: a mente caiu de uma vez.");
            // o tornado perto (12/11/2026, a pedido: "um símbolo específico para ele ... 'Mas que $#&# é essa???'")
            if (Tempo.Tornado)
            {
                Vector3 d = Tempo.PosicaoDoTornado - v.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude < TornadoPerto * TornadoPerto)
                    Por(saida, EstadoDoJogador.Tornado, "Mas que $#&# é essa???", "Um tornado aqui perto! Ele puxa e arremessa o que está em volta. Saia do caminho dele ou se abrigue numa casa.");
            }

            // o corpo
            int sangrando = 0;
            for (int p = 0; p < PartesDoCorpo.Quantas; p++) if (e.Sangra((ParteDoCorpo)p)) sangrando++;
            if (sangrando > 0)
                Por(saida, sangrando >= 2 ? EstadoDoJogador.SangrandoMuito : EstadoDoJogador.Sangrando, sangrando >= 2 ? "Sangrando muito" : "Sangrando",
                    $"Sangra {Partes(e, (c, p) => c.Sangra(p))}: a vida cai até estancar. A atadura, as bandagens ou o kit.");
            Lesoes(saida, e, Lesao.Exposta, EstadoDoJogador.FraturaExposta, "Fratura exposta", "o osso saiu: dói muito, sangra e infecciona fácil. Tala, analgésico e limpe a ferida.");
            Lesoes(saida, e, Lesao.Fratura, EstadoDoJogador.Fratura, "Fratura", "o osso quebrou: dói e o membro fica fraco. Tala e analgésico.");
            Lesoes(saida, e, Lesao.Entorse, EstadoDoJogador.Entorse, "Entorse", "torceu: dói por um tempo. Tala e analgésico saram mais rápido.");
            switch (e.Infeccao)
            {
                case 1:
                    Por(saida, EstadoDoJogador.Infeccao, "Ferida infeccionada", $"A ferida {PartesDoCorpo.NaParte(e.OrigemDaInfeccao)} infeccionou. Tome antibiótico antes que vire febre.");
                    break;
                case 2:
                    Por(saida, EstadoDoJogador.Febre, "Febre", "A infecção virou febre: tome antibiótico (o xarope só esconde a febre por um tempo).");
                    break;
                case 3:
                    Por(saida, EstadoDoJogador.Sepse, "Sepse", "A infecção tomou o corpo e a vida está caindo. Antibiótico já!");
                    break;
            }
            string manca = PartesDoCorpo.PorQueManca(e);
            if (manca != null) Por(saida, EstadoDoJogador.Mancando, "Mancando", Maiuscula(manca) + ": sem correr direito.");
            if (e.Inutil(ParteDoCorpo.BracoE) || e.Inutil(ParteDoCorpo.BracoD))
                Por(saida, EstadoDoJogador.BracoInutil, "Braço inutilizado", $"O braço {(e.Inutil(ParteDoCorpo.BracoE) && e.Inutil(ParteDoCorpo.BracoD) ? "esquerdo e o direito" : e.Inutil(ParteDoCorpo.BracoE) ? "esquerdo" : "direito")} não aguenta: sem arma longa, e a mira treme. Trate o braço.");

            // a fome e a sede
            var fs = v.GetComponent<FomeESede>();
            if (fs != null)
            {
                float fome = fs.Fome.Value, sede = fs.Sede.Value;
                if (fome < Faminto) Por(saida, EstadoDoJogador.Faminto, "Faminto", fome <= 0f ? "Sem comer há tempo demais: a vida está caindo. Coma já." : "Quase sem forças de fome. Coma logo.");
                else if (fome < fs.pouca) Por(saida, EstadoDoJogador.Fome, "Com fome", "Hora de comer alguma coisa.");
                if (sede < Faminto) Por(saida, EstadoDoJogador.MuitaSede, "Com muita sede", sede <= 0f ? "Sem beber há tempo demais: a vida está caindo. Beba já." : "A boca rachando de sede. Beba logo.");
                else if (sede < fs.pouca) Por(saida, EstadoDoJogador.Sede, "Com sede", "Hora de beber alguma coisa.");
            }

            // o sono, o calmante e o cigarro
            int cansaco = v.Cansaco.Value;
            if (cansaco >= Exausto || v.TemEfeito(VidaJogador.EfeitoExausto))
                Por(saida, EstadoDoJogador.Exausto, "Exausto", "Quase 3 dias sem dormir: a vista pisca e logo você apaga onde estiver. Durma numa cama.");
            else if (cansaco >= Cansado)
                Por(saida, EstadoDoJogador.Cansado, "Cansado", cansaco >= VidaJogador.ComSono ? "Com muito sono: a mente cai e a vista começa a piscar. Durma logo." : "Muito tempo acordado. Durma quando puder.");
            if (v.TemEfeito(VidaJogador.EfeitoSonolento))
                Por(saida, EstadoDoJogador.Sonolento, "Sonolento", "Dois calmantes seguidos: a vista embaça e o corpo fica lento por um tempo. Um terceiro faz desmaiar.");
            if (v.TemEfeito(VidaJogador.EfeitoAbstinenciaForte))
                Por(saida, EstadoDoJogador.AbstinenciaForte, "Abstinência forte", "Muitas horas sem fumar: a mão treme muito e a mente cai. Fumar passa na hora.");
            else if (v.TemEfeito(VidaJogador.EfeitoAbstinencia))
                Por(saida, EstadoDoJogador.Abstinencia, "Abstinência", "Viciado e sem fumar há horas: a mão treme e a mente cai. Fumar passa na hora.");

            // a fauna (12/11/2026): o veneno da cascavel e o cheiro do gambá
            if (v.Veneno.Value > 0f)
                Por(saida, EstadoDoJogador.Envenenado, "Envenenado", "A picada da cascavel: o membro perde vida aos poucos, com dor e enjoo. O curativo não cura: passa sozinho em uns minutos.");
            var rede = v.GetComponent<JogadorRede>();
            if (rede != null && rede.Fedor.Value > 0f)
                Por(saida, EstadoDoJogador.Fedendo, "Fedendo", "O cheiro do gambá: os zumbis sentem de longe. Passa sozinho; na água sai bem mais rápido.");

            // o vigor (o dono) e a roupa molhada
            var vigor = VigorDoJogador.Local;
            if (vigor != null && vigor.Vigor <= 0.5f)
                Por(saida, EstadoDoJogador.SemVigor, "Sem vigor", "Acabou o vigor: sem correr nem pular, o golpe sai fraco, e na água funda você afunda. Pare um pouco.");
            else if (vigor != null && (vigor.Esgotado || vigor.Fracao < 0.3f))
                Por(saida, EstadoDoJogador.SemFolego, "Sem fôlego", "O vigor está acabando. Ande devagar para recuperar.");
            var inventario = v.GetComponent<InventarioJogador>();
            float molhado = inventario != null ? inventario.Molhado01 : 0f;
            if (molhado >= 0.7f)
                Por(saida, EstadoDoJogador.Encharcado, "Encharcado", "A roupa encharcada pesa: o vigor gasta mais e acaba antes. Seque perto do fogo, debaixo de um teto, ou troque a roupa.");
            else if (molhado >= 0.3f)
                Por(saida, EstadoDoJogador.Molhado, "Molhado", "A roupa molhada pesa um pouco no vigor. O fogo e um teto secam (trocar a roupa também ajuda).");

            // o clima e o lugar
            if ((motivos & VidaJogador.Bit(MotivoDaMente.Tempestade)) != 0)
                Por(saida, EstadoDoJogador.Tempestade, "Tempestade", "A tempestade lá fora abala a mente. Procure um teto.");
            if ((motivos & (VidaJogador.Bit(MotivoDaMente.Fogueira) | VidaJogador.Bit(MotivoDaMente.CalorDoFogo))) != 0)
                Por(saida, EstadoDoJogador.PertoDoFogo, "Perto do fogo", "O calor do fogo acalma a mente.", true);
            if ((motivos & VidaJogador.Bit(MotivoDaMente.Bunker)) != 0)
                Por(saida, EstadoDoJogador.Bunker, "No bunker", "Seguro: aqui dentro nada abala a mente.", true);
            Ordenar(saida);
        }

        static void Por(List<Estado> saida, EstadoDoJogador id, string nome, string porque, bool bom = false) =>
            saida.Add(new Estado { id = id, nome = nome, porque = porque, bom = bom });

        // os membros com a lesão (um símbolo por tipo de lesão, com os membros no porquê)
        static void Lesoes(List<Estado> saida, in EstadoDoCorpo e, Lesao qual, EstadoDoJogador id, string nome, string porque)
        {
            bool tem = false, tala = false;
            foreach (var p in Membros)
                if (e.LesaoDe(p) == qual)
                {
                    tem = true;
                    tala |= e.Tala(p);
                }
            if (!tem) return;
            string onde = Partes(e, (c, p) => c.LesaoDe(p) == qual);
            Por(saida, id, nome, $"{Maiuscula(onde)}: {porque}{(tala ? " (com tala: sarando)" : "")}");
        }

        static readonly ParteDoCorpo[] Membros = { ParteDoCorpo.BracoE, ParteDoCorpo.BracoD, ParteDoCorpo.PernaE, ParteDoCorpo.PernaD };

        // "na perna direita e no braço esquerdo"
        static string Partes(in EstadoDoCorpo e, System.Func<EstadoDoCorpo, ParteDoCorpo, bool> tem)
        {
            _sb.Clear();
            int n = 0, total = 0;
            for (int p = 0; p < PartesDoCorpo.Quantas; p++) if (tem(e, (ParteDoCorpo)p)) total++;
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
            {
                if (!tem(e, (ParteDoCorpo)p)) continue;
                _sb.Append(n == 0 ? "" : n == total - 1 ? " e " : ", ").Append(PartesDoCorpo.NaParte((ParteDoCorpo)p));
                n++;
            }
            return _sb.ToString();
        }

        // " O que abala agora: o escuro, zumbis caçando."
        static string Causas(uint motivos)
        {
            _sb.Clear();
            int n = 0;
            foreach (var m in MenteNoHud.OrdemDoQueAbala)
                if ((motivos & VidaJogador.Bit(m)) != 0)
                {
                    _sb.Append(n == 0 ? " O que abala agora: " : ", ").Append(MenteNoHud.Nome(m).ToLowerInvariant());
                    n++;
                }
            if (n > 0) _sb.Append('.');
            return _sb.ToString();
        }

        static void Ordenar(List<Estado> saida) => saida.Sort((a, b) => a.bom != b.bom ? (a.bom ? 1 : -1) : a.id.CompareTo(b.id));

        static string Maiuscula(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        // ------------------------------------------------------------------ os ícones

        static readonly Dictionary<EstadoDoJogador, Texture2D> _icones = new Dictionary<EstadoDoJogador, Texture2D>();

        /// <summary>O ícone do estado (Resources/Estados/Estado_&lt;nome&gt;); null se ainda não existe.</summary>
        public static Texture2D Icone(EstadoDoJogador e)
        {
            if (_icones.TryGetValue(e, out var t)) return t;
            t = Resources.Load<Texture2D>("Estados/Estado_" + e);
            _icones[e] = t;
            return t;
        }
    }
}
