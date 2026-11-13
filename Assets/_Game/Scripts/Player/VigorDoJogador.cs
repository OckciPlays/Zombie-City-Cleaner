using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O vigor (a stamina, 12/11/2026, a pedido: "Deveríamos criar um sistema de stamina pro jogador e também colocarmos alguma
    /// skill de habilidade para poder aumentar/perder menos ao correr a stamina e colocar algumas funções pra ela, como nadar
    /// perde stamina e você pode se afogar se chegar a 0"; "Golpe com armas brancas gastam stamina a depender do peso da arma
    /// (faca gasta menos, machado gasta mais)"). Só no dono (o movimento é dele): de 0 a <see cref="Maximo"/>.
    /// Depois do primeiro teste do Pedro (12/11/2026: "o valor dela está um pouco alto, ou o gasto dela está um pouco baixo";
    /// "quando estamos molhados eu acho que está tirando muito pouco"; "com entorse/fratura/infecção/sepse/febre, a estamina
    /// abaixa também?"), ela gasta mais, volta mais devagar, a roupa molhada pesa o dobro e o corpo machucado ou doente pesa.
    ///  - <b>Gasta:</b> correr (100 em <see cref="SegundosCorrendo"/> s), pular (<see cref="CustoDoPulo"/>), o golpe das armas
    ///    brancas (pelo peso: <see cref="CustoDoGolpe"/>) e nadar (boiando parado, nadando e nadando rápido, cada um mais).
    ///    Zerado, ele não corre até voltar a <see cref="VoltaACorrer"/>; sem vigor para o golpe, o golpe sai fraco e lento.
    ///    Na água funda, zerado, ele não se segura em cima: afunda devagar, e aí é o fôlego (o afogamento).
    ///  - <b>Volta:</b> fora da água, um segundo depois de parar de gastar: parado, cheio em ~10 s; andando, em ~15 s.
    ///  - <b>O que pesa:</b>
    ///    - a roupa molhada (o <see cref="InventarioJogador.Molhado01"/>): gasta até 60% mais e o máximo cai até 30%;
    ///    - a perna: a entorse gasta 30% mais, a fratura 60% e a exposta o dobro (a perna muito machucada, 25%); o braço
    ///      quebrado deixa o golpe 50% mais caro;
    ///    - a doença: a ferida infeccionada tira 10% do máximo; a febre, 25% (e volta 30% mais devagar); a sepse, metade
    ///      (e volta pela metade);
    ///    - a fome e a sede baixas tiram 25% do máximo (zeradas, metade);
    ///    - exausto ou sonolento: volta 40% mais devagar, e o movimento fica letárgico (<see cref="Letargico"/>).
    ///  - <b>O ramo Atleta</b> (Sobrevivência): Pulmão forte, Passada leve, Nadador, Fôlego de volta e Maratonista.
    /// </summary>
    public class VigorDoJogador : MonoBehaviour
    {
        /// <summary>O vigor do jogador local (o dono).</summary>
        public static VigorDoJogador Local { get; private set; }

        public const float SegundosCorrendo = 15f, CustoDoPulo = 9f, VoltaACorrer = 30f;
        /// <summary>Nadando: parado (boiando), nadando e nadando rápido (s para gastar 100).</summary>
        public const float SegundosBoiando = 120f, SegundosNadando = 70f, SegundosNadandoRapido = 22f;
        /// <summary>Volta cheio em tantos segundos: parado e andando.</summary>
        public const float SegundosParaEncherParado = 10f, SegundosParaEncherAndando = 15f;
        const float EsperaParaVoltar = 1f;

        /// <summary>O ramo Atleta: +vigor por ponto do Pulmão forte; a corrida, o nado e a volta por ponto; o Maratonista no golpe e no pulo.</summary>
        public const float PulmaoPorPonto = 0.12f, PassadaPorPonto = 0.12f, NadadorPorPonto = 0.2f, RecuperacaoPorPonto = 0.2f, MaratonistaNoGolpe = 0.3f;

        /// <summary>O que gasta (cada habilidade vale num).</summary>
        public enum Gasto : byte { Outro, Corrida, Nado, Golpe, Pulo }

        /// <summary>O vigor agora.</summary>
        public float Vigor { get; private set; } = 100f;
        /// <summary>Zerou correndo (ou nadando): só corre de novo com <see cref="VoltaACorrer"/>.</summary>
        public bool Esgotado { get; private set; }

        JogadorRede _rede;
        InventarioJogador _inventario;
        FomeESede _fome;
        float _gastouEm = -99f;
        float _proximaConta;
        float _maximo = 100f, _gasto = 1f, _volta = 1f, _golpe = 1f;
        int _passada, _nadador;
        bool _maratonista;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _inventario = GetComponent<InventarioJogador>();
            _fome = GetComponent<FomeESede>();
        }

        void OnDestroy()
        {
            if (Local == this) Local = null;
        }

        /// <summary>O máximo de agora (a habilidade, a roupa molhada, a doença, a fome e a sede).</summary>
        public float Maximo => _maximo;
        /// <summary>De 0 a 1.</summary>
        public float Fracao => Mathf.Clamp01(Vigor / Mathf.Max(1f, _maximo));

        /// <summary>Pode correr agora (não está esgotado).</summary>
        public bool PodeCorrer => !Esgotado && Vigor > 0.5f;

        /// <summary>
        /// Exausto (quase 3 dias sem dormir) ou sonolento (dois calmantes): o corpo fica lento (a pedido: "sonolento (ai o
        /// jogador ficaria letárgico também)").
        /// </summary>
        public bool Letargico => _rede != null && _rede.Vida != null && (_rede.Vida.TemEfeito(VidaJogador.EfeitoExausto) || _rede.Vida.TemEfeito(VidaJogador.EfeitoSonolento));

        /// <summary>Letárgico: o andar e a câmera ficam mais lentos.</summary>
        public const float LentidaoLetargica = 0.85f, CameraLetargica = 0.75f;

        // os fatores (refeitos a cada meio segundo)
        void Contas()
        {
            if (Time.time < _proximaConta) return;
            _proximaConta = Time.time + 0.5f;
            if (_rede != null && _rede.IsOwner) Local = this;
            int pulmao = Habilidades.Pontos(_rede, Habilidade.PulmaoForte);
            _passada = Habilidades.Pontos(_rede, Habilidade.PassadaLeve);
            _nadador = Habilidades.Pontos(_rede, Habilidade.Nadador);
            int recuperacao = Habilidades.Pontos(_rede, Habilidade.Recuperacao);
            _maratonista = Habilidades.Tem(_rede, Habilidade.Maratonista);
            float molhado = _inventario != null ? _inventario.Molhado01 : 0f;
            if (_maratonista) molhado *= 0.5f;
            float fomeESede = 1f;
            if (_fome != null)
            {
                float pior = Mathf.Min(_fome.Fome.Value, _fome.Sede.Value);
                fomeESede = pior <= 0f ? 0.5f : pior < _fome.pouca ? 0.75f : 1f;
            }
            float perna = 1f, doenca = 1f, voltaDaDoenca = 1f;
            _golpe = 1f;
            if (_rede != null && _rede.Vida != null)
            {
                var c = _rede.Vida.Corpo.Value;
                perna = Mathf.Max(FatorDaPerna(c, ParteDoCorpo.PernaE), FatorDaPerna(c, ParteDoCorpo.PernaD));
                if (c.LesaoDe(ParteDoCorpo.BracoE) >= Lesao.Fratura || c.LesaoDe(ParteDoCorpo.BracoD) >= Lesao.Fratura) _golpe = 1.5f;
                switch (c.Infeccao)
                {
                    case 1: doenca = 0.9f; break;
                    case 2: doenca = 0.75f; voltaDaDoenca = 0.7f; break;
                    case 3: doenca = 0.5f; voltaDaDoenca = 0.5f; break;
                }
            }
            _maximo = 100f * (1f + PulmaoPorPonto * pulmao) * (1f - 0.3f * molhado) * fomeESede * doenca;
            _gasto = (1f + 0.6f * molhado) * perna;
            _volta = (Letargico ? 0.6f : 1f) * voltaDaDoenca * (1f + RecuperacaoPorPonto * recuperacao);
            if (Vigor > _maximo) Vigor = _maximo;
        }

        // a perna: a lesão (a entorse, a fratura, a exposta) e o machucado forte
        static float FatorDaPerna(in EstadoDoCorpo c, ParteDoCorpo p) => c.LesaoDe(p) switch
        {
            Lesao.Exposta => 2f,
            Lesao.Fratura => 1.6f,
            Lesao.Entorse => 1.3f,
            _ => c.Dano(p) > 50 ? 1.25f : 1f,
        };

        /// <summary>Gasta (as habilidades do Atleta e o corpo valem pelo tipo do gasto).</summary>
        public void Gastar(float quanto, Gasto tipo = Gasto.Outro)
        {
            Contas();
            if (quanto <= 0f) return;
            switch (tipo)
            {
                case Gasto.Corrida: quanto *= 1f - PassadaPorPonto * _passada; break;
                case Gasto.Nado: quanto *= 1f - NadadorPorPonto * _nadador; break;
                case Gasto.Golpe: quanto *= _golpe * (_maratonista ? 1f - MaratonistaNoGolpe : 1f); break;
                case Gasto.Pulo: quanto *= _maratonista ? 1f - MaratonistaNoGolpe : 1f; break;
            }
            Vigor = Mathf.Max(0f, Vigor - quanto * _gasto);
            _gastouEm = Time.time;
            if (Vigor <= 0.01f) Esgotado = true;
        }

        /// <summary>O movimento, a cada quadro: correndo, nadando (e como) ou nada; a volta do vigor.</summary>
        public void Passo(float dt, bool correndo, bool nadando, bool nadandoRapido, bool parado)
        {
            Contas();
            if (correndo) Gastar(100f / SegundosCorrendo * dt, Gasto.Corrida);
            else if (nadando) Gastar(100f / (nadandoRapido ? SegundosNadandoRapido : parado ? SegundosBoiando : SegundosNadando) * dt, Gasto.Nado);
            else if (Time.time - _gastouEm > EsperaParaVoltar)
            {
                float s = parado ? SegundosParaEncherParado : SegundosParaEncherAndando;
                Vigor = Mathf.Min(_maximo, Vigor + 100f / s * _volta * dt);
            }
            if (Esgotado && Vigor >= VoltaACorrer) Esgotado = false;
        }

        /// <summary>O pulo: gasta e diz se dá (sem vigor, não pula).</summary>
        public bool Pular()
        {
            Contas();
            if (Vigor < CustoDoPulo * _gasto * (_maratonista ? 1f - MaratonistaNoGolpe : 1f)) return false;
            Gastar(CustoDoPulo, Gasto.Pulo);
            return true;
        }

        /// <summary>O custo do golpe pelo peso da arma (o soco, Item.Nada, é o mais leve; o machado, o mais pesado).</summary>
        public static float CustoDoGolpe(Item i) => i switch
        {
            Item.Faca => 5f,
            Item.Tocha => 6f,
            Item.Lanca => 10f,
            Item.Bastao => 12f,
            Item.Machado => 16f,
            _ => 4f,
        };

        /// <summary>O golpe: gasta o custo; sem vigor para ele, devolve false (o golpe sai fraco e lento).</summary>
        public bool Golpe(Item i)
        {
            Contas();
            float custo = CustoDoGolpe(i);
            bool tinha = Vigor >= custo * _gasto * _golpe * (_maratonista ? 1f - MaratonistaNoGolpe : 1f);
            Gastar(custo, Gasto.Golpe);
            return tinha;
        }

        /// <summary>O golpe sem vigor: o tempo entre golpes vezes isto e o dano vezes <see cref="DanoSemVigor"/>.</summary>
        public const float LentidaoSemVigor = 1.6f, DanoSemVigor = 0.6f;

        // ------------------------------------------------------------------ a barra (só quando não está cheio)

        float _alfa;

        // embaixo do monitor cardíaco, fina: aparece quando gasta e some quando enche; o pedaço que a roupa molhada, a fome
        // e a sede tiram do máximo fica escuro, e esgotado ela pisca vermelho
        void OnGUI()
        {
            if (_rede == null || !_rede.IsOwner || _rede.Vida == null || _rede.Vida.Caido.Value) return;
            if (UI.MenuOnline.Aberto || UI.MenuOnline.Cinema || Video.ModoVideo.Ligado || Video.GravacaoNoMundo.SemHud) return;
            if (Event.current.type != EventType.Repaint) return;
            bool cheio = Vigor >= _maximo - 0.5f;
            _alfa = Mathf.MoveTowards(_alfa, cheio ? 0f : 1f, Time.unscaledDeltaTime / (cheio ? 1.2f : 0.2f));
            if (_alfa <= 0.01f) return;
            float u = Screen.height / 720f;
            var m = MonitorCardiaco.Area(u);
            var r = new Rect(m.x, m.yMax + 5f * u, m.width, Mathf.Max(2f, 4f * u));
            float escala = r.width / Mathf.Max(100f, _maximo);
            var antes = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * _alfa);
            GUI.DrawTexture(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, r.height + 2f), Texture2D.whiteTexture);
            // o que falta no máximo (a roupa molhada, a fome e a sede)
            if (_maximo < 100f)
            {
                GUI.color = new Color(0.35f, 0.12f, 0.1f, 0.7f * _alfa);
                GUI.DrawTexture(new Rect(r.x + _maximo * escala, r.y, (100f - _maximo) * escala, r.height), Texture2D.whiteTexture);
            }
            Color cor = Esgotado ? Color.Lerp(new Color(0.85f, 0.2f, 0.15f), new Color(1f, 0.5f, 0.4f), 0.5f + 0.5f * Mathf.Sin(Time.time * 8f))
                      : Fracao < 0.3f ? new Color(0.95f, 0.6f, 0.2f) : new Color(0.86f, 0.9f, 0.42f);
            GUI.color = new Color(cor.r, cor.g, cor.b, 0.95f * _alfa);
            GUI.DrawTexture(new Rect(r.x, r.y, Vigor * escala, r.height), Texture2D.whiteTexture);
            GUI.color = antes;
        }

        /// <summary>Só o teste: o vigor no valor (e o esgotado, se zerar).</summary>
        public void PorDeTeste(float v)
        {
            Vigor = Mathf.Clamp(v, 0f, _maximo);
            Esgotado = Vigor <= 0.01f;
            _gastouEm = Time.time;
        }
    }
}
