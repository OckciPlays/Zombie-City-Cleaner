using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Os zumbis especiais (01/11/2026, a pedido), a parte comum: o tipo (<see cref="TipoDeZumbi"/>), a vida e a velocidade de
    /// cada um (os números em <see cref="AjustesDeZumbi"/>: corredor, cuspidor, brutamonte), o que vai pela rede (um byte, o
    /// 18º de cada zumbi) e o nível de detalhe (o Brutamonte e o Long ficam no nível Perto de mais longe: o olho e o papo são
    /// hitboxes, que só existem nesse nível). Cada um tem o resto no seu arquivo: o Corredor aqui mesmo (só números: frágil,
    /// rápido e com o bote), o Long em Zumbi.Long.cs e o Brutamonte em Zumbi.Brutamonte.cs.
    /// </summary>
    public partial class Zumbi
    {
        [Header("Especiais (a montagem preenche)")]
        [Tooltip("O tipo: o Oco de sempre ou um especial.")]
        public TipoDeZumbi tipo;
        [Tooltip("O Brutamonte: o olho nas costas e as pálpebras (ossos próprios do modelo).")]
        public Transform olho, palpebraSup, palpebraInf;
        [Tooltip("O Long: o papo (a gosma).")]
        public Transform papo;
        [Tooltip("As hitboxes do olho (Brutamonte) e do papo (Long).")]
        public Hitbox hitboxDoOlho, hitboxDoPapo;

        public bool Especial => tipo != TipoDeZumbi.Oco;

        /// <summary>A última decisão do especial, em palavras (no anfitrião): o menu do desenvolvedor e os testes mostram.</summary>
        public string Decisao { get; private set; } = "";

        /// <summary>O Brutamonte longe de todos desde quando (o ZombieManager o devolve à terra depois de um tempo).</summary>
        internal float semNinguemDesde = -1f;
        /// <summary>O dano que ele já levou (o Brutamonte guarda ao voltar para a terra).</summary>
        internal float DanoNoCorpo => _danoNoCorpo;
        internal void GuardarDano(float dano) => _danoNoCorpo = Mathf.Max(0f, dano);

        AjustesDeZumbi A => _m != null ? _m.ajustes : null;

        float VidaDoCorpo => tipo switch
        {
            TipoDeZumbi.Corredor => A.corredor.vida,
            TipoDeZumbi.Long => A.cuspidor.vida,
            TipoDeZumbi.Brutamonte => A.brutamonte.vida,
            _ => A.vidaDoCorpo,
        };

        float VidaDoMembro => tipo switch
        {
            TipoDeZumbi.Corredor => A.corredor.vidaDoMembro,
            TipoDeZumbi.Long => A.cuspidor.vidaDoMembro,
            TipoDeZumbi.Brutamonte => 9999f,   // o casco: não perde braço nem perna
            _ => A.vidaDoMembro,
        };

        float VelocidadeDePerseguir => tipo switch
        {
            TipoDeZumbi.Corredor => A.corredor.velocidadePerseguindo,
            TipoDeZumbi.Long => A.cuspidor.velocidade,
            TipoDeZumbi.Brutamonte => A.brutamonte.velocidade,
            _ => A.velocidadePerseguindo,
        };

        /// <summary>Vagando, investigando, indo para o abrigo: o Corredor anda mais rápido.</summary>
        float FatorAndando => tipo == TipoDeZumbi.Corredor ? A.corredor.fatorAndando : 1f;

        float TempoParaPetrificar => tipo == TipoDeZumbi.Corredor ? A.corredor.tempoParaPetrificar : A.tempoParaPetrificar;

        /// <summary>O Brutamonte não reage ao sol nem à luz (cego e surdo, com o casco).</summary>
        bool ImuneALuz => tipo == TipoDeZumbi.Brutamonte;

        /// <summary>A vida que sobra, de 0 a 1 (a barra do Brutamonte). No anfitrião, a de verdade; nas cópias, a que chega.</summary>
        public float FracaoDaVida => Copia ? _fracaoRecebida : Mathf.Clamp01(1f - _danoNoCorpo / Mathf.Max(1f, VidaDoCorpo));
        float _fracaoRecebida = 1f;

        /// <summary>Perto de alguém até esta distância, o especial fica no nível Perto (o olho e o papo são hitboxes).</summary>
        internal bool SempreDePerto(float d) => tipo == TipoDeZumbi.Brutamonte ? d < 75f : tipo == TipoDeZumbi.Long ? d < 50f : false;

        /// <summary>O byte do especial na rede (o 18º de cada zumbi).</summary>
        internal byte EspecialNaRede => tipo switch
        {
            TipoDeZumbi.Brutamonte => BrutamonteNaRede,
            TipoDeZumbi.Long => LongNaRede,
            _ => (byte)0,
        };

        void ReceberEspecial(byte valor, bool comEfeito)
        {
            if (tipo == TipoDeZumbi.Brutamonte) ReceberBrutamonte(valor, comEfeito);
            else if (tipo == TipoDeZumbi.Long) ReceberLong(valor, comEfeito);
        }

        // ------------------------------------------------------------------ preparar e zerar

        /// <summary>No anfitrião, ao nascer: o agente de cada tipo (o Corredor acelera rápido; o Brutamonte é largo e pesado).</summary>
        void PrepararEspecial()
        {
            switch (tipo)
            {
                case TipoDeZumbi.Corredor:
                    agente.acceleration = 14f;
                    agente.angularSpeed = 540f;
                    break;
                case TipoDeZumbi.Brutamonte:
                    agente.radius = 0.55f;
                    agente.acceleration = 8f;
                    agente.angularSpeed = 160f;
                    agente.avoidancePriority = 10;   // os outros saem da frente dele
                    PrepararBrutamonte();
                    break;
                case TipoDeZumbi.Long:
                    agente.acceleration = 5f;
                    PrepararLong();
                    break;
            }
        }

        void ZerarEspecial()
        {
            _fracaoRecebida = 1f;
            if (tipo == TipoDeZumbi.Brutamonte) ZerarBrutamonte();
            else if (tipo == TipoDeZumbi.Long) ZerarLong();
        }

        // ------------------------------------------------------------------ a cada quadro

        /// <summary>No anfitrião, a cada quadro (níveis Perto e Médio): o que o especial faz fora das decisões.</summary>
        void EspecialNoServidor(float agora, float dt)
        {
            if (Morto) return;
            if (tipo == TipoDeZumbi.Brutamonte) BrutamonteNoQuadro(agora, dt);
            else if (tipo == TipoDeZumbi.Long) LongNoQuadro(agora, dt);
        }

        /// <summary>Em todas as máquinas, depois da animação: o olho e as pálpebras do Brutamonte, o papo do Long.</summary>
        void EspecialDepoisDaAnimacao(float dt)
        {
            if (Morto || !animador.enabled) return;
            if (tipo == TipoDeZumbi.Brutamonte) OlhoDepoisDaAnimacao(dt);
            else if (tipo == TipoDeZumbi.Long) PapoDepoisDaAnimacao(dt);
        }

        /// <summary>O Corredor: perto do alvo, o bote (mais rápido que o jogador correndo).</summary>
        float VelocidadeNoBote(float distancia, float normal) =>
            tipo == TipoDeZumbi.Corredor && distancia < A.corredor.distanciaDoBote ? A.corredor.velocidadeDoBote : normal;

        /// <summary>O Corredor e o Brutamonte andam pelo "Andar dos especiais" ("Pernas" em 7): o do Oco, com a disparada a 3,8 m/s.</summary>
        bool CorreComoEspecial => tipo == TipoDeZumbi.Corredor || tipo == TipoDeZumbi.Brutamonte;

        /// <summary>
        /// A animação mais rápida que o normal: o Brutamonte no avanço (a 9 m/s, a disparada de um corpo maior) e o Corredor
        /// no bote (acima dos 3,8 m/s da disparada).
        /// </summary>
        float RitmoDoEspecial => tipo switch
        {
            TipoDeZumbi.Brutamonte when FaseBruto == FaseDoBruto.Avancando => 1.8f,
            TipoDeZumbi.Corredor when Velocidade > 3.8f => Mathf.Min(1.3f, Velocidade / 3.8f),
            _ => 1f,
        };

        /// <summary>Toca um estado do Animator, se ele existe (os especiais têm os seus; os Ocos montados antes, não).</summary>
        bool Tocar(int estado, float mistura = 0.15f, float de = 0f)
        {
            if (!animador.isActiveAndEnabled || !animador.HasState(0, estado)) return false;
            animador.CrossFadeInFixedTime(estado, mistura, 0, de);
            return true;
        }
    }
}
