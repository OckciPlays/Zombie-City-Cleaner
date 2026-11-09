using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A etapa 3 da mente (06/11/2026, a sanidade): o sono, o calmante, o vício na nicotina e a sinuca. Tudo contado pelo
    /// anfitrião, como a mente (VidaJogador.Mente.cs), e só com o corpo andando (fora do Dia 0).
    /// <para><b>O sono:</b> o cansaço (0 a 100) sobe acordado (100 em 3 dias do jogo; 06/11/2026, a pedido: "O sono só vai
    /// acontecer em algumas determinadas ocasiões (3 dias sem dormir, 3 comprimidos de calmante)"); no último meio dia, com
    /// sono, a mente cai 1 por hora e, perto do fim, a vista pisca; nos 3 dias, ele apaga onde estiver e dorme (pelo menos 4
    /// h). Dormir (a cama: "[E] Dormir e salvar") deita o corpo e tira o controle; dormindo, o cansaço cai (16 por hora; 20
    /// com todos) e a mente sobe. A pedido: "Se apenas um estiver dormindo, recupera devagar e o tempo não passa, se todos
    /// estiverem dormindo (independente do local do mundo que estejam) vai passar a noite e recuperar boa parte da
    /// sanidade": sozinho, o tempo anda normal (a mente +5 por hora numa cama, +3 apagado no chão); todos dormindo, o relógio
    /// corre <see cref="PressaDoSono"/> vezes mais rápido (<see cref="CicloDoDia.Pressa"/>) e a mente +7 por hora (08/11/2026;
    /// eram 2,5 e 5). Dormindo, o escuro, a solidão e a tempestade não pesam na mente. As camas da mobília (as das casas, dos
    /// prédios e do bunker) servem para dormir (<see cref="Salvar.CamaDeDormir"/>), e o corpo deita nelas.
    /// Quem dormiu de noite acorda às 6h30 (todos dormindo); de dia, quando o cansaço acaba. O golpe acorda, um amigo acorda
    /// (mirar e segurar E), e o E acorda quem dorme (menos o sono do calmante, nas 3 primeiras horas).</para>
    /// <para><b>O calmante</b> (a pedido: "esquema de recuperar automático por hora", sem sorte): +8 por hora por 5 h (até 8
    /// h somadas) e a crise de pânico passa. Seguidos (a menos de 3 h do jogo um do outro): o 2º deixa sonolento por 2 h (a
    /// vista um pouco embaçada, escrito "Sonolento"); o 3º faz desmaiar e dormir onde estiver (pelo menos 3 h).</para>
    /// <para><b>O vício na nicotina:</b> cada tragada soma 0,35 na dependência (uns 2,5 maços seguidos chegam a 100), que cai
    /// 10 por dia sem fumar. Com 50 ele fica viciado (e só deixa abaixo de 25). Viciado, 4 h sem fumar trazem a abstinência
    /// (a mente −2 por hora e a mão tremendo); 10 h, a forte (−4 e o tremor maior). Fumar passa na hora.</para>
    /// <para><b>A sinuca:</b> cada tacada +1,2 (no máximo uma a cada 3 s); o fim de uma partida, +6.</para>
    /// </summary>
    public partial class VidaJogador
    {
        /// <summary>Quantas vezes o relógio corre com todos dormindo.</summary>
        public const float PressaDoSono = 12f;
        /// <summary>Horas do jogo acordado até a exaustão (3 dias): aí ele apaga onde estiver.</summary>
        public const float HorasAteExaustao = 72f;
        /// <summary>Daqui para cima (o último meio dia acordado), com sono: a mente cai 1 por hora e a vista pisca.</summary>
        public const float ComSono = 83f;
        /// <summary>Os bits de <see cref="EfeitosDaMente"/>.</summary>
        public const byte EfeitoSonolento = 1, EfeitoAbstinencia = 2, EfeitoAbstinenciaForte = 4, EfeitoComSono = 8, EfeitoExausto = 16,
                          EfeitoViciado = 32, EfeitoSonoForcado = 64;

        /// <summary>Dormindo agora (o corpo deitado, sem controle). Só o anfitrião escreve.</summary>
        public readonly NetworkVariable<bool> Dormindo = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>O cansaço (0 a 100). Só o anfitrião escreve.</summary>
        public readonly NetworkVariable<byte> Cansaco = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>A dependência da nicotina (0 a 100). Só o anfitrião escreve.</summary>
        public readonly NetworkVariable<byte> Nicotina = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>O que o dono mostra (os bits Efeito*: sonolento, a abstinência, o sono, viciado, o sono do calmante).</summary>
        public readonly NetworkVariable<byte> EfeitosDaMente = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public bool TemEfeito(byte bit) => (EfeitosDaMente.Value & bit) != 0;

        float _cansaco, _nicotina, _semFumar, _horasVividas, _calmanteFalta, _sonolentoFalta, _ultimaDose = -99f, _acordaDoForcado, _proximaSinuca;
        int _doses;
        bool _viciado, _sonoForcado, _dormiuDeNoite;
        static int _quadroDaPressa = -1;

        // ------------------------------------------------ na conta da mente (a cada 0,5 s) ------------------------------------------------

        void MenteDoSonoEDosRemedios(float horas, ref float taxa, ref uint motivos)
        {
            _horasVividas += horas;
            bool todos = TodosDormindo();
            // o sono
            // (06/11/2026, a pedido: "está cansando muito fácil": o sono só pesa aos 3 dias sem dormir)
            if (Dormindo.Value)
            {
                _cansaco = Mathf.Max(0f, _cansaco - (todos ? 20f : 16f) * horas);
                // (08/11/2026, a pedido: "dormir recuperar sanidade") numa cama, +5 por hora (uma noite: uns +40); apagado no
                // chão (o cansaço, o calmante), +3; com todos dormindo, +7 (eram 2,5 e 5). E, dormindo, o escuro, a solidão e
                // a tempestade não pesam (VidaJogador.Mente.cs)
                Acalma(ref taxa, ref motivos, MotivoDaMente.Descanso, todos ? 7f : _sonoForcado ? 3f : 5f);
            }
            else
            {
                _cansaco = Mathf.Min(100f, _cansaco + 100f / HorasAteExaustao * horas);
                if (_cansaco > ComSono) Abala(ref taxa, ref motivos, MotivoDaMente.Sono, 1f);
                // os 3 dias sem dormir: ele apaga onde estiver (o desmaio do cansaço) e dorme
                if (_cansaco >= 100f) Dormir(true, 4f);
            }
            // o calmante
            if (_calmanteFalta > 0f)
            {
                _calmanteFalta = Mathf.Max(0f, _calmanteFalta - horas * 60f);
                Acalma(ref taxa, ref motivos, MotivoDaMente.Calmante, 8f);
            }
            if (_sonolentoFalta > 0f) _sonolentoFalta = Mathf.Max(0f, _sonolentoFalta - horas * 60f);
            // o vício
            _semFumar += horas;
            _nicotina = Mathf.Max(0f, _nicotina - 10f / 24f * horas);
            if (_viciado && _nicotina < 25f) _viciado = false;
            if (_viciado && _semFumar > 4f) Abala(ref taxa, ref motivos, MotivoDaMente.Abstinencia, _semFumar > 10f ? 4f : 2f);
            PublicarOSono();
        }

        // o que vai pela rede (só o que mudou)
        void PublicarOSono()
        {
            byte c = (byte)Mathf.RoundToInt(_cansaco), n = (byte)Mathf.RoundToInt(_nicotina);
            if (Cansaco.Value != c) Cansaco.Value = c;
            if (Nicotina.Value != n) Nicotina.Value = n;
            byte ef = 0;
            if (_sonolentoFalta > 0f) ef |= EfeitoSonolento;
            if (_viciado && _semFumar > 4f) ef |= _semFumar > 10f ? (byte)(EfeitoAbstinencia | EfeitoAbstinenciaForte) : EfeitoAbstinencia;
            if (!Dormindo.Value && _cansaco > ComSono) ef |= _cansaco > 92f ? (byte)(EfeitoComSono | EfeitoExausto) : EfeitoComSono;
            if (_viciado) ef |= EfeitoViciado;
            if (Dormindo.Value && _sonoForcado) ef |= EfeitoSonoForcado;
            if (EfeitosDaMente.Value != ef) EfeitosDaMente.Value = ef;
        }

        // ------------------------------------------------ a cada quadro, no anfitrião ------------------------------------------------

        void AtualizarSono()
        {
            // a pressa do relógio: um VidaJogador só faz a conta, uma vez por quadro
            if (_quadroDaPressa != Time.frameCount)
            {
                _quadroDaPressa = Time.frameCount;
                CicloDoDia.DefinirPressa(TodosDormindo() ? PressaDoSono : 1f);
            }
            if (!Dormindo.Value) return;
            if (Caido.Value || Desmaiado.Value || !CorpoAnda)
            {
                AcordarDoSono(true);
                return;
            }
            // o sono do calmante: pelo menos até a hora marcada
            if (_sonoForcado && _horasVividas < _acordaDoForcado) return;
            bool todos = TodosDormindo();
            float h = CicloDoDia.Hora;
            if (todos && _dormiuDeNoite)
            {
                // a noite passou: de manhã, todos acordam
                if (h >= 6.5f && h < 12f) AcordarDoSono(true);
            }
            else if (_cansaco <= 0.5f) AcordarDoSono(true);
        }

        /// <summary>No anfitrião: todos os jogadores da partida (vivos e criados) estão dormindo (pelo menos um).</summary>
        public static bool TodosDormindo()
        {
            int n = 0;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.IsSpawned || !j.Criado || j.Vida == null || j.Vida.Caido.Value) continue;
                if (!j.Vida.Dormindo.Value) return false;
                n++;
            }
            return n > 0;
        }

        // ------------------------------------------------ dormir e acordar ------------------------------------------------

        /// <summary>No dono: deitar e dormir aqui (a cama).</summary>
        public void PedirDormir()
        {
            if (!IsOwner || !IsSpawned || Caido.Value || Desmaiado.Value || Dormindo.Value) return;
            if (_rede != null && _rede.Passageiro != null && _rede.Passageiro.NoCarro) return;
            DormirRpc();
        }

        [Rpc(SendTo.Server)]
        void DormirRpc(RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            Dormir(false, 0f);
        }

        // só no anfitrião: dorme (o forçado é o do calmante, com o mínimo em horas do jogo)
        void Dormir(bool forcado, float minimo)
        {
            if (!IsServer || !IsSpawned || Caido.Value || !CorpoAnda) return;
            if (Desmaiado.Value) Desmaiado.Value = false;
            _sonoForcado = forcado;
            _acordaDoForcado = _horasVividas + minimo;
            _dormiuDeNoite = CicloDoDia.Noite > 0.5f;
            Dormindo.Value = true;
            PublicarOSono();
        }

        /// <summary>No dono: levantar (o E dormindo). O sono do calmante não deixa nas primeiras horas.</summary>
        public void PedirAcordar()
        {
            if (IsOwner && IsSpawned && Dormindo.Value) AcordarDoSonoRpc();
        }

        [Rpc(SendTo.Server)]
        void AcordarDoSonoRpc(RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            AcordarDoSono(false);
        }

        /// <summary>Só no anfitrião: acorda do sono (forçar: o golpe, o amigo, a manhã; sem forçar, o sono do calmante segura).</summary>
        public void AcordarDoSono(bool forcar)
        {
            if (!IsServer || !Dormindo.Value) return;
            if (!forcar && _sonoForcado && _horasVividas < _acordaDoForcado) return;
            Dormindo.Value = false;
            _sonoForcado = false;
            PublicarOSono();
        }

        // ------------------------------------------------ deitar na cama da mobília (08/11/2026) ------------------------------------------------

        bool _naCama, _dormiuNaCama;
        float _esperaOSono, _giroAntesDaCama;
        Vector3 _ladoDaCama;

        /// <summary>
        /// No dono (a cama da mobília, <see cref="Salvar.CamaDeDormir"/>): o corpo vai para o colchão (parado ali, sem cair,
        /// até o sono chegar pela rede); acordando, ele volta para onde estava, do lado da cama. Se o sono não vem (o anfitrião
        /// recusou), volta logo.
        /// </summary>
        public void DeitarNaCama(Vector3 pos, float yaw)
        {
            if (!IsOwner || Dormindo.Value || Caido.Value || Desmaiado.Value) return;
            if (_rede != null && _rede.Passageiro != null && _rede.Passageiro.NoCarro) return;
            var mov = GetComponent<MovimentoPrimeiraPessoa>();
            if (mov == null) return;
            _ladoDaCama = transform.position;
            _giroAntesDaCama = transform.eulerAngles.y;
            mov.Teleportar(pos, yaw);
            mov.Esperando = true;   // (sem cair nem andar: a caixa da cama fica embaixo dele)
            _naCama = true;
            _dormiuNaCama = false;
            _esperaOSono = Time.time + 3f;
        }

        // no dono, a cada quadro: acordou (ou o sono não veio), volta para o lado da cama
        void CuidarDaCama()
        {
            if (!_naCama) return;
            if (Dormindo.Value)
            {
                _dormiuNaCama = true;
                return;
            }
            if (!_dormiuNaCama && Time.time < _esperaOSono) return;   // (o sono ainda vem pela rede)
            _naCama = false;
            var mov = GetComponent<MovimentoPrimeiraPessoa>();
            if (mov == null) return;
            mov.Esperando = false;
            if (Caido.Value) return;   // (caiu dormindo: a queda leva para a cama improvisada ou o nascimento)
            mov.Teleportar(_ladoDaCama, _giroAntesDaCama);
        }

        // ------------------------------------------------ o calmante, o cigarro e a sinuca ------------------------------------------------

        // o calmante tomado (do Tratar): o efeito por hora; seguidos, o sono
        string TomarCalmante()
        {
            bool seguida = _horasVividas - _ultimaDose < 3f;
            _doses = seguida ? _doses + 1 : 1;
            _ultimaDose = _horasVividas;
            _calmanteFalta = Mathf.Min(8f * 60f, _calmanteFalta + 5f * 60f);
            if (_doses >= 3)
            {
                _doses = 0;
                _sonolentoFalta = 0f;
                Dormir(true, 3f);
                return "Três calmantes seguidos: você apagou";
            }
            if (_doses == 2)
            {
                _sonolentoFalta = 2f * 60f;
                PublicarOSono();
                return "Dois calmantes seguidos: sonolento (um terceiro agora faz apagar)";
            }
            PublicarOSono();
            return "O calmante começou a agir: a mente acalma aos poucos (umas 5 h)";
        }

        // a tragada (do TragouRpc): a dependência sobe e a fissura passa
        void MenteNaTragada()
        {
            _nicotina = Mathf.Min(100f, _nicotina + 0.35f);
            _semFumar = 0f;
            if (!_viciado && _nicotina >= 50f)
            {
                _viciado = true;
                GetComponent<InventarioJogador>()?.AvisarDono("Você ficou viciado em nicotina: sem fumar, vem a abstinência");
            }
            PublicarOSono();
        }

        /// <summary>Só no anfitrião (a SinucaDoMundo): ele terminou uma tacada (ou uma partida).</summary>
        public void JogouSinuca(bool fimDaPartida)
        {
            if (!IsServer || Time.time < _proximaSinuca) return;
            _proximaSinuca = Time.time + 3f;
            AcalmarAgora(fimDaPartida ? 6f : 1.2f, MotivoDaMente.Sinuca);
        }

        // ------------------------------------------------ o save e os testes ------------------------------------------------

        /// <summary>Só no anfitrião: o sono e o vício que vieram do save.</summary>
        public void DefinirSonoEVicio(float cansaco, float nicotina, bool viciado)
        {
            if (!IsServer) return;
            _cansaco = Mathf.Clamp(cansaco, 0f, 100f);
            _nicotina = Mathf.Clamp(nicotina, 0f, 100f);
            _viciado = viciado && _nicotina >= 25f;
            _semFumar = 0f;
            PublicarOSono();
        }

        /// <summary>Só no anfitrião (o F1): o cansaço neste valor.</summary>
        public void CansacoDeTeste(float valor)
        {
            if (!IsServer) return;
            _cansaco = Mathf.Clamp(valor, 0f, 100f);
            PublicarOSono();
        }

        /// <summary>Só no anfitrião (o F1): dorme aqui, como na cama.</summary>
        public void DormirDeTeste() => Dormir(false, 0f);

        /// <summary>Só no anfitrião (o F1): viciado agora, já há 5 h sem fumar (a abstinência começa).</summary>
        public void VicioDeTeste()
        {
            if (!IsServer) return;
            _nicotina = 70f;
            _viciado = true;
            _semFumar = 5f;
            PublicarOSono();
        }

        /// <summary>Os testes: o sono, o calmante e o vício.</summary>
        public string ResumoDoSono() =>
            $"cansaço {_cansaco:0}{(Dormindo.Value ? " (dormindo" + (_sonoForcado ? ", calmante" : "") + ")" : "")} | calmante {_calmanteFalta / 60f:0.0} h, doses {_doses}" +
            $"{(_sonolentoFalta > 0f ? ", sonolento" : "")} | nicotina {_nicotina:0}{(_viciado ? $" (viciado, {_semFumar:0.0} h sem fumar)" : "")} | relógio x{CicloDoDia.Pressa:0}";
    }
}
