using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.World;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O que mexe com a mente (os bits do <see cref="VidaJogador.MotivosDaMente"/>). Do 0 ao 15, o que abala; do 16 em
    /// diante, o que acalma.
    /// </summary>
    public enum MotivoDaMente : byte
    {
        Escuro = 0, Zumbis, Tensao, Susto, Sozinho, Tempestade, Dor, Febre, Sangue, Fome, Sede, AmigoCaiu, Matou, Golpe,
        // a etapa 3: o sono e a abstinência
        Sono, Abstinencia,
        Dia = 16, Amigos, Fogueira, Bunker, Cigarro,
        // a etapa 3: o calmante, a sinuca e o descanso (dormindo)
        Calmante, Sinuca, Descanso,
        // a etapa 4 do ramo Fogo (11/11/2026): o Calor do fogo
        CalorDoFogo,
    }

    /// <summary>
    /// A mente (06/11/2026, a sanidade, a pedido: "Vamos implementar esse sistema"), a etapa 1: o número de 0 a 100, que só
    /// o anfitrião conta, como a vida e o corpo. Ela cai com o medo e volta com a segurança e a companhia; não mata (a 0, o
    /// jogador fica em colapso, sem game over). Os efeitos (a crise de pânico, os vultos, o colapso) são a etapa 2; o sono, o
    /// calmante e o vício no cigarro, a etapa 3.
    /// <para>O que abala, por hora do jogo (1 minuto real): o escuro (a noite sem luz nenhuma, ou o bunker apagado) −4; os
    /// zumbis caçando a até 15 m −6, mais 2 por zumbi a mais (até −16); só investigando ou procurando perto −1,5; sozinho à
    /// noite (com outro jogador na partida e ninguém a 20 m) −2; a tempestade fora do teto −2; a dor (a lesão sem o
    /// analgésico) −1,5; a febre −3 (a sepse −5); o sangramento −2; a fome e a sede baixas −1 cada (vazias −2). E de uma vez:
    /// o susto (um zumbi que começa a caçar a menos de 7 m) −4 a −10; o Brutamonte saindo da terra perto −12; o raio perto
    /// −5 (em cima de alguém, −10); o golpe −1,5; a fratura −6; um amigo caindo a até 25 m −15; matar uma pessoa −8.</para>
    /// <para>O que acalma, por hora: o dia (até 85) +2; os amigos a até 8 m +3 cada (até +6); a fogueira acesa a até 8 m
    /// +4; o bunker aceso +2,5 (com a porta fechada, +5); e cada tragada do cigarro +2,5.</para>
    /// <para>No Dia 0 (e com o mundo carregando) ela não anda, como o corpo. Quem renasce volta com pelo menos 30. Vai no save
    /// do personagem como o abalo (100 menos a mente: o save antigo entra calmo).</para>
    /// </summary>
    public partial class VidaJogador
    {
        public const float MenteMaxima = 100f;
        /// <summary>Quem renasce volta com pelo menos isto (não nasce em colapso).</summary>
        public const float MenteAoRenascer = 30f;
        /// <summary>O dia sozinho acalma só até aqui (o resto vem dos amigos, do fogo, do bunker).</summary>
        const float DiaAte = 85f;
        const float PassoDaMente = 0.5f;            // s: de quanto em quanto o anfitrião conta
        const float MotivoDeEventoDura = 6f;        // s: o susto, o golpe, o amigo caindo ficam no motivo um tempo
        // o ritmo das perdas (06/11/2026, a pedido: "acho que está caindo muito rápido"): o que abala por hora (o escuro, os
        // zumbis, a dor...) vale 65% do da tabela, e o que abala de uma vez (o susto, o golpe, o amigo caindo) 75%
        public const float RitmoDasPerdas = 0.65f, RitmoDosSustos = 0.75f;

        /// <summary>A mente (a sanidade), de 0 a 100. Só o anfitrião escreve.</summary>
        public readonly NetworkVariable<float> Sanidade = new(MenteMaxima, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>O que está mexendo com a mente agora (um bit por <see cref="MotivoDaMente"/>). Só o anfitrião escreve.</summary>
        public readonly NetworkVariable<uint> MotivosDaMente = new(0u, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        float _mente = MenteMaxima, _tempoDaMente, _proximoSusto, _proximoBruto, _proximoGolpeNaMente, _proximaTragada;
        bool _abrigado;
        readonly float[] _motivoAte = new float[32];
        HashSet<Zumbi> _cacandoAgora = new HashSet<Zumbi>();
        // o susto (12/11/2026): os zumbis que ele via por perto no passo de antes e os de agora
        HashSet<Zumbi> _vistosAntes = new HashSet<Zumbi>(), _vistosAgora = new HashSet<Zumbi>();
        /// <summary>O susto: o zumbi que aparece (passa a ser visto) a menos disto (m)...</summary>
        const float SustoPerto = 3.5f;
        /// <summary>...e que não estava sendo visto a menos disto (m) meio segundo antes.</summary>
        const float SustoVisto = 8f;
        /// <summary>O raio que cai até esta distância (m) assusta (eles caem de 8 a 40 m de alguém).</summary>
        const float SustoDoRaio = 45f;
        // o que tapa a vista (o cenário: as casas, os muros)
        const int MascaraDaVista = (1 << 0) | (1 << 1);
        LanternaJogador _lanterna;
        bool _ouvindoOsRaios;

        public static uint Bit(MotivoDaMente m) => 1u << (int)m;
        public bool MenteTem(MotivoDaMente m) => (MotivosDaMente.Value & Bit(m)) != 0;

        // ------------------------------------------------ o anfitrião conta ------------------------------------------------

        void AtualizarMente(float dt)
        {
            if (!_ouvindoOsRaios)
            {
                _ouvindoOsRaios = true;
                Tempo.AoCairUmRaio += AoCairUmRaio;
            }
            _tempoDaMente += dt;
            if (_tempoDaMente < PassoDaMente) return;
            float passo = _tempoDaMente;
            _tempoDaMente = 0f;
            if (Caido.Value || !CorpoAnda)
            {
                if (MotivosDaMente.Value != 0u) MotivosDaMente.Value = 0u;
                return;
            }
            float horas = passo * CicloDoDia.HorasPorSegundo;
            float taxa = 0f;   // por hora do jogo
            uint motivos = 0u;
            Vector3 pes = transform.position;
            bool noite = CicloDoDia.Noite > 0.6f, dia = CicloDoDia.Noite < 0.4f;

            // a luz: a própria (a lanterna com pilha, a tocha acesa, o carro), a fogueira, o bunker aceso (o do mundo: a luz
            // do andar onde ele está e o portão; o da cidade de antes: alguma luz e a porta)
            bool noBunker = false, bunkerAceso = false, bunkerFechado = false;
            int andar = BunkerDoMundo.AndarDe(pes);
            if (andar >= 0)
            {
                var b = BunkerDoMundo.Instancia;
                noBunker = true;
                bunkerAceso = b.Acesa(andar);
                bunkerFechado = !b.Aberta(BunkerDoMundo.Portao) && !b.Aberta(BunkerDoMundo.Portinhola);
            }
            else if (BunkerRede.Instancia != null && BunkerRede.Instancia.Dentro(pes))
            {
                noBunker = true;
                bunkerAceso = AlgumaLuz(BunkerRede.Instancia);
                bunkerFechado = !BunkerRede.Instancia.Aberta;
            }
            // (06/11/2026, a pedido: "dentro do bunker, independente se há zumbis perto, que eu não perca sanidade") no bunker
            // nada tira da mente: o Abala e o AbalarAgora não contam lá dentro
            _abrigado = noBunker;
            bool fogueira = FogueiraAcesaPerto(pes, 8f);
            bool luzPropria = LanternaAcesa() || (_rede != null && (_rede.TochaAcesa || _rede.LampiaoAceso || (_rede.Passageiro != null && _rede.Passageiro.NoCarro)));
            bool escuro = !luzPropria && !fogueira && (noBunker ? !bunkerAceso : noite);
            // dormindo (08/11/2026, a pedido: "dormir recuperar sanidade"), o escuro, a solidão e a tempestade não pesam: só o
            // que é de verdade (os zumbis caçando, o corpo, a fome e a sede). Antes, dormindo sozinho de noite fora do bunker,
            // a mente até caía (o escuro e a solidão passavam do descanso)
            bool dormindo = Dormindo.Value;
            if (escuro && !dormindo) Abala(ref taxa, ref motivos, MotivoDaMente.Escuro, 4f);

            // os zumbis: caçando perto, ou só investigando; o susto é quem começa a caçar colado
            var zm = ZombieManager.Instancia;
            int tensao = 0, pertoDeMim = 0;
            bool brutoSaindo = false;
            if (zm != null) zm.PerigoParaAMente(pes, 15f, 22f, 12f, _cacandoAgora, out tensao, out pertoDeMim, out brutoSaindo);
            else _cacandoAgora.Clear();
            int cacando = _cacandoAgora.Count;
            if (cacando > 0) Abala(ref taxa, ref motivos, MotivoDaMente.Zumbis, Mathf.Min(16f, 6f + 2f * (cacando - 1)));
            else if (tensao > 0 || pertoDeMim > 0) Abala(ref taxa, ref motivos, MotivoDaMente.Tensao, 2f);
            // com zumbi por perto, o fogo e o dia não acalmam (o bunker, sim: lá dentro é o abrigo)
            bool ameaca = cacando > 0 || tensao > 0 || pertoDeMim > 0;
            // o susto (12/11/2026, a pedido: "toda vez que tem contato com um zumbi conta como susto, e não deveria ser assim, o
            // susto só deve ser quando o zumbi aparece muito perto do jogador"): só o zumbi que ele passa a ver já colado (a
            // menos de SustoPerto), sem ter visto por perto meio segundo antes. O que vem vindo à vista não assusta; o que sai
            // de uma porta, de trás de um muro, ou chega por trás e aparece quando ele vira, sim
            int novos = ZumbisQueApareceram(zm, pes);
            if (novos > 0 && Time.time >= _proximoSusto)
            {
                _proximoSusto = Time.time + 8f;
                AbalarAgora(Mathf.Min(10f, 4f + 2f * (novos - 1)), MotivoDaMente.Susto);
            }
            if (brutoSaindo && Time.time >= _proximoBruto)
            {
                _proximoBruto = Time.time + 25f;
                AbalarAgora(12f, MotivoDaMente.Susto);
            }

            // sozinho à noite (só com mais alguém na partida)
            if (noite && !dormindo && JogadorRede.Todos.Count > 1 && Amigos(pes, 20f) == 0) Abala(ref taxa, ref motivos, MotivoDaMente.Sozinho, 2f);
            // a tempestade, fora do teto
            if (!dormindo && Tempo.Nivel >= NivelDoTempo.Tempestade && !Tempo.SobTeto(pes)) Abala(ref taxa, ref motivos, MotivoDaMente.Tempestade, 2f);

            // o corpo
            var e = Corpo.Value;
            if (e.ComDor) Abala(ref taxa, ref motivos, MotivoDaMente.Dor, 1.5f);
            if (e.Sepse) Abala(ref taxa, ref motivos, MotivoDaMente.Febre, 5f);
            else if (e.ComFebre) Abala(ref taxa, ref motivos, MotivoDaMente.Febre, 3f);
            if (e.AlgumSangrando) Abala(ref taxa, ref motivos, MotivoDaMente.Sangue, 2f);
            if (_fomeESede != null)
            {
                float fome = _fomeESede.Fome.Value, sede = _fomeESede.Sede.Value;
                if (fome < _fomeESede.pouca) Abala(ref taxa, ref motivos, MotivoDaMente.Fome, fome <= 0f ? 2f : 1f);
                if (sede < _fomeESede.pouca) Abala(ref taxa, ref motivos, MotivoDaMente.Sede, sede <= 0f ? 2f : 1f);
            }

            // o que acalma
            if (dia && !escuro && !ameaca && _mente < DiaAte) Acalma(ref taxa, ref motivos, MotivoDaMente.Dia, 2f);
            int perto = Amigos(pes, 8f);
            if (perto > 0) Acalma(ref taxa, ref motivos, MotivoDaMente.Amigos, 3f * Mathf.Min(2, perto));
            if (fogueira && !ameaca) Acalma(ref taxa, ref motivos, MotivoDaMente.Fogueira, 4f);
            // o Calor do fogo (11/11/2026, a etapa 4): perto de um fogo aceso (a fogueira, a tocha, o lampião, o molotov)
            int calor = Habilidades.Pontos(_rede, Habilidade.CalorDoFogo);
            if (calor > 0 && FogoDoJogador.FogoAcesoPerto(pes)) Acalma(ref taxa, ref motivos, MotivoDaMente.CalorDoFogo, FogoDoJogador.CalorPorNivel * calor);
            if (bunkerAceso) Acalma(ref taxa, ref motivos, MotivoDaMente.Bunker, bunkerFechado ? 5f : 2.5f);

            // a etapa 3: o sono, o calmante e o vício (VidaJogador.Sono.cs)
            MenteDoSonoEDosRemedios(horas, ref taxa, ref motivos);

            // os de uma vez ficam no motivo por uns segundos
            for (int i = 0; i < _motivoAte.Length; i++)
                if (Time.time < _motivoAte[i]) motivos |= 1u << i;

            float nova = Mathf.Clamp(_mente + taxa * horas, 0f, MenteMaxima);
            // o dia acalma até o teto dele, mas não puxa para baixo quem já está acima
            if (taxa > 0f && (motivos & ~Bit(MotivoDaMente.Dia) & 0xFFFF0000u) == 0u && _mente < DiaAte) nova = Mathf.Min(nova, DiaAte);
            DefinirMenteNoServidor(nova);
            if (MotivosDaMente.Value != motivos) MotivosDaMente.Value = motivos;
        }

        // (no bunker, nada abala: o _abrigado da conta de agora)
        void Abala(ref float taxa, ref uint motivos, MotivoDaMente m, float porHora)
        {
            if (_abrigado) return;
            taxa -= porHora * RitmoDasPerdas * (1f - 0.1f * Habilidades.Pontos(_rede, Habilidade.NervosDeAco));   // (os Nervos de aço, 06/11/2026: 10% por ponto)
            motivos |= Bit(m);
        }

        /// <summary>Dentro do bunker agora (a caixa e a casinha do bunker do mundo, ou o da cidade de antes): a mente não cai.</summary>
        public bool Abrigado()
        {
            var p = transform.position;
            return BunkerDoMundo.AndarDe(p) >= 0 || (BunkerRede.Instancia != null && BunkerRede.Instancia.Dentro(p));
        }

        static void Acalma(ref float taxa, ref uint motivos, MotivoDaMente m, float porHora)
        {
            taxa += porHora;
            motivos |= Bit(m);
        }

        void DefinirMenteNoServidor(float valor)
        {
            _mente = Mathf.Clamp(valor, 0f, MenteMaxima);
            // a rede só leva o que mudou de verdade (a conta é a cada meio segundo)
            if (Mathf.Abs(Sanidade.Value - _mente) >= 0.05f || (_mente <= 0f && Sanidade.Value > 0f) || (_mente >= MenteMaxima && Sanidade.Value < MenteMaxima))
                Sanidade.Value = _mente;
        }

        bool LanternaAcesa()
        {
            if (_lanterna == null) _lanterna = GetComponent<LanternaJogador>();
            var inv = _rede != null ? _rede.Inventario : null;
            return _lanterna != null && _lanterna.IsSpawned && _lanterna.Ligada.Value && inv != null && inv.CargaDaLanterna > 0;
        }

        static bool AlgumaLuz(BunkerRede b)
        {
            for (int g = 0; g < 8; g++)
                if (b.Acesa(g)) return true;
            return false;
        }

        static bool FogueiraAcesaPerto(Vector3 p, float alcance)
        {
            var c = ConstrucoesNoMundo.Instancia;
            return c != null && c.FogueiraPerto(p, alcance, out var f) && c.Acesa(f);
        }

        // os outros jogadores vivos a até o alcance
        int Amigos(Vector3 p, float alcance)
        {
            int n = 0;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || j == _rede || !j.AlvoValido || j.Vida == null || j.Vida.Caido.Value) continue;
                if ((j.transform.position - p).sqrMagnitude <= alcance * alcance) n++;
            }
            return n;
        }

        // ------------------------------------------------ o que acontece de uma vez ------------------------------------------------

        /// <summary>Só no anfitrião: um abalo de uma vez (o susto, o golpe, o amigo caindo, matar alguém).</summary>
        public void AbalarAgora(float quanto, MotivoDaMente motivo)
        {
            if (!IsServer || !IsSpawned || Caido.Value || !CorpoAnda || quanto <= 0f) return;
            if (Abrigado()) return;   // (no bunker, nada abala: nem o susto, nem o golpe)
            quanto *= RitmoDosSustos;
            quanto *= 1f - 0.1f * Habilidades.Pontos(_rede, Habilidade.NervosDeAco);   // (os Nervos de aço, 06/11/2026: 10% por ponto)
            DefinirMenteNoServidor(_mente - quanto);
            _motivoAte[(int)motivo] = Time.time + MotivoDeEventoDura;
        }

        /// <summary>Só no anfitrião: um alívio de uma vez (a tragada do cigarro).</summary>
        public void AcalmarAgora(float quanto, MotivoDaMente motivo)
        {
            if (!IsServer || !IsSpawned || Caido.Value || !CorpoAnda || quanto <= 0f) return;
            DefinirMenteNoServidor(_mente + quanto);
            _motivoAte[(int)motivo] = Time.time + MotivoDeEventoDura;
        }

        // o golpe (do VidaJogador.Dano): um pouco a cada um (no máximo um a cada 1,5 s) e a fratura mais
        void MenteNoGolpe(Lesao nova)
        {
            if (nova >= Lesao.Fratura) AbalarAgora(6f, MotivoDaMente.Dor);
            if (Time.time < _proximoGolpeNaMente) return;
            _proximoGolpeNaMente = Time.time + 1.5f;
            AbalarAgora(1.5f, MotivoDaMente.Golpe);
        }

        // ele caiu: quem está perto vê o amigo cair
        void MenteDeQuemViuCair()
        {
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || j == _rede || j.Vida == null || !j.AlvoValido) continue;
                if ((j.transform.position - transform.position).sqrMagnitude <= 25f * 25f) j.Vida.AbalarAgora(15f, MotivoDaMente.AmigoCaiu);
            }
        }

        /// <summary>Só no anfitrião: ele matou uma pessoa (um sobrevivente). Um dia, as habilidades de assassino tiram isso.</summary>
        public void MatouUmaPessoa()
        {
            if (Habilidades.Tem(_rede, Habilidade.SangueFrio)) return;   // (o Sangue frio, a ponta do Assassino, 06/11/2026)
            AbalarAgora(8f, MotivoDaMente.Matou);
        }

        // os zumbis à vista agora (a menos de SustoVisto, na frente dele, sem parede no meio); devolve quantos apareceram já
        // colados (a menos de SustoPerto) sem estar à vista no passo de antes
        int ZumbisQueApareceram(ZombieManager zm, Vector3 pes)
        {
            _vistosAgora.Clear();
            int novos = 0;
            if (zm != null)
            {
                Vector3 olho = pes + Vector3.up * 1.6f, frente = transform.forward;
                frente.y = 0f;
                foreach (var z in zm.Ativos)
                {
                    if (z == null || z.Morto) continue;
                    Vector3 d = z.transform.position - pes;
                    float d2 = d.sqrMagnitude;
                    if (d2 > SustoVisto * SustoVisto) continue;
                    d.y = 0f;
                    // na frente (uns 140° de vista) ou encostado, e sem parede no meio
                    bool naFrente = d2 < 1.2f * 1.2f || Vector3.Angle(frente, d) < 70f;
                    if (!naFrente || Physics.Linecast(olho, z.transform.position + Vector3.up * 1.2f, MascaraDaVista, QueryTriggerInteraction.Ignore)) continue;
                    _vistosAgora.Add(z);
                    if (d2 < SustoPerto * SustoPerto && !_vistosAntes.Contains(z)) novos++;
                }
            }
            (_vistosAntes, _vistosAgora) = (_vistosAgora, _vistosAntes);
            return novos;
        }

        // o raio (em cada máquina; aqui, no anfitrião): perto assusta, em cima de alguém mais
        void AoCairUmRaio(Raio r)
        {
            if (!IsServer || !IsSpawned || r.tipo == 0) return;
            if ((r.ponto - transform.position).sqrMagnitude > SustoDoRaio * SustoDoRaio) return;
            AbalarAgora(r.tipo >= 2 ? 10f : 5f, MotivoDaMente.Susto);
        }

        /// <summary>No dono, a cada tragada do cigarro (o FumoJogador): o anfitrião acalma um pouco.</summary>
        public void Tragou()
        {
            if (IsOwner && IsSpawned) TragouRpc();
        }

        [Rpc(SendTo.Server)]
        void TragouRpc(RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || Time.time < _proximaTragada) return;
            _proximaTragada = Time.time + 1f;   // (a tragada leva 1,5 s: mais rápido que isso é engano)
            AcalmarAgora(2.5f, MotivoDaMente.Cigarro);
            MenteNaTragada();   // a etapa 3: a dependência e a fissura
        }

        // ------------------------------------------------ o save e os testes ------------------------------------------------

        /// <summary>O abalo para o save (100 menos a mente; caído, renasce com pelo menos <see cref="MenteAoRenascer"/>).</summary>
        public float AbaloParaOSave => MenteMaxima - (Caido.Value ? Mathf.Max(_mente, MenteAoRenascer) : (IsServer ? _mente : Sanidade.Value));

        /// <summary>Só no anfitrião: a mente que veio do save (o abalo: 0 = calma).</summary>
        public void DefinirAbalo(float abalo)
        {
            if (!IsServer) return;
            DefinirMenteNoServidor(MenteMaxima - Mathf.Clamp(abalo, 0f, MenteMaxima));
            Sanidade.Value = _mente;
        }

        /// <summary>Só no anfitrião: a mente inteira de novo (os 5 anos do Dia 0).</summary>
        public void AcalmarTudo()
        {
            if (!IsServer) return;
            DefinirMenteNoServidor(MenteMaxima);
            Sanidade.Value = _mente;
            System.Array.Clear(_motivoAte, 0, _motivoAte.Length);
        }

        /// <summary>Só no anfitrião (o F1): a mente neste valor.</summary>
        public void MenteDeTeste(float valor)
        {
            if (!IsServer) return;
            DefinirMenteNoServidor(valor);
            Sanidade.Value = _mente;
        }

        // quem renasce não volta em colapso
        void MenteAoVoltar()
        {
            if (_mente < MenteAoRenascer) DefinirMenteNoServidor(MenteAoRenascer);
            Sanidade.Value = _mente;
        }

        void PararDeOuvirOsRaios()
        {
            if (!_ouvindoOsRaios) return;
            _ouvindoOsRaios = false;
            Tempo.AoCairUmRaio -= AoCairUmRaio;
        }

        /// <summary>Os testes: a mente e o que mexe com ela agora.</summary>
        public string ResumoDaMente()
        {
            var s = new System.Text.StringBuilder($"mente {Sanidade.Value:0.0}");
            for (int i = 0; i < 32; i++)
                if ((MotivosDaMente.Value & (1u << i)) != 0) s.Append(' ').Append((MotivoDaMente)i);
            return s.ToString();
        }
    }
}
