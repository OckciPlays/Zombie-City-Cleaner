using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>O que o bicho está fazendo (vai pela rede em 3 bits).</summary>
    // (a etapa 5, 13/11/2026) Sentado: o urso comendo o salmão na margem (o 7 é o último que cabe nos 3 bits da rede)
    public enum EstadoDoBicho : byte { Pastando, Andando, Alerta, Fugindo, Investindo, Morto, Deitado, Sentado }

    /// <summary>
    /// Um bicho no mundo. No anfitrião ele pensa (Bicho.Mente.cs) e anda; nos outros, é uma cópia que segue o que chega
    /// pela rede (FaunaDoMundo.Rede.cs). O corpo vem do ModelosDosBichos (as caixas, ou o veado do Blender), e a animação é
    /// feita na hora (AnimacaoDoBicho). Morto, vira a carcaça: com a faca na mão, E carneia (a <see cref="Carcaca"/>).
    /// </summary>
    public sealed partial class Bicho : MonoBehaviour
    {
        public ushort Id { get; private set; }
        public byte Geracao { get; private set; }
        public Especie Especie { get; private set; }
        public Variante Variante { get; private set; }
        public DadosDaEspecie Dados { get; private set; }
        /// <summary>Uma cópia do que o anfitrião manda (num cliente).</summary>
        public bool Copia { get; private set; }
        public EstadoDoBicho Estado { get; internal set; }
        public bool Morto => Estado == EstadoDoBicho.Morto;
        /// <summary>Ferido (sangrando: deixa o rastro de sangue).</summary>
        public bool Ferido { get; internal set; }
        /// <summary>Já carneado: a carcaça some logo.</summary>
        public bool Carneado { get; internal set; }
        /// <summary>A velocidade agora (m/s).</summary>
        public float Velocidade { get; internal set; }
        /// <summary>A vida (no anfitrião).</summary>
        public float Vida { get; internal set; }
        /// <summary>Levando alguma coisa (a etapa 3: o galho na boca do castor, o peixe nas garras da águia; vai pela rede).</summary>
        public bool Carregando { get; internal set; }
        /// <summary>O que sobrou da carcaça (1: inteira; a matilha comendo deixa menos para quem carnear depois).</summary>
        public float Restante { get; internal set; } = 1f;
        /// <summary>Quando morreu (as horas do jogo, para a carcaça apodrecer).</summary>
        public double MorreuNaHora { get; internal set; }
        /// <summary>O grupo (o rebanho), no anfitrião.</summary>
        internal Grupo Grupo;
        /// <summary>O líder do grupo (o macho alfa do rebanho).</summary>
        public bool Alfa { get; internal set; }
        /// <summary>A escala da variante (o filhote menor).</summary>
        public float Escala { get; private set; } = 1f;
        public ModeloDoBicho Modelo { get; private set; }
        public float Altura => Modelo != null ? Modelo.altura : Dados.altura;

        AnimacaoDoBicho _animacao;
        Transform _inclinacao;
        float _olhar, _ultimoSangue;
        Vector3 _ultimoPontoDoSangue;

        // ------------------------------------------------------------------ montar e desmontar

        internal void Montar(ushort id, byte geracao, Especie e, Variante v, bool copia, int semente)
        {
            Id = id;
            Geracao = geracao;
            Especie = e;
            Variante = v;
            Dados = Especies.De(e);
            Copia = copia;
            Escala = Especies.Escala(e, v);
            Estado = EstadoDoBicho.Pastando;
            Ferido = Carneado = Alfa = Carregando = false;
            Restante = 1f;
            Velocidade = 0f;
            Vida = Especies.Pontos(e, v);
            gameObject.name = Dados.NomeDaVariante(v) + " #" + id;
            gameObject.layer = ModelosDosBichos.Camada;
            _inclinacao = new GameObject("Inclinação").transform;
            _inclinacao.SetParent(transform, false);
            Modelo = ModelosDosBichos.Montar(e, v, _inclinacao, semente);
            foreach (var p in GetComponentsInChildren<ParteDoBicho>(true)) p.bicho = this;
            _animacao = new AnimacaoDoBicho(transform, Modelo, e);
            // um corpo rígido cinemático: os colisores andam com o bicho sem custo de colisor parado movido
            var rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            ZerarMente();
        }

        // ------------------------------------------------------------------ a cada quadro (todas as máquinas)

        internal void Animar(float dt)
        {
            var pose = Estado switch
            {
                EstadoDoBicho.Morto => AnimacaoDoBicho.Pose.Morto,
                EstadoDoBicho.Deitado => AnimacaoDoBicho.Pose.Deitado,
                EstadoDoBicho.Sentado => AnimacaoDoBicho.Pose.Sentado,
                EstadoDoBicho.Alerta => AnimacaoDoBicho.Pose.Alerta,
                EstadoDoBicho.Investindo => Velocidade < 0.5f ? AnimacaoDoBicho.Pose.Atacando : AnimacaoDoBicho.Pose.Correndo,
                EstadoDoBicho.Pastando => Velocidade < 0.25f ? AnimacaoDoBicho.Pose.Comendo : AnimacaoDoBicho.Pose.Andando,
                EstadoDoBicho.Fugindo => AnimacaoDoBicho.Pose.Correndo,
                _ => Velocidade < 0.15f ? AnimacaoDoBicho.Pose.Parado : AnimacaoDoBicho.Pose.Andando,
            };
            if (Dados.corpo == Corpo.Ave && Estado == EstadoDoBicho.Fugindo) pose = AnimacaoDoBicho.Pose.Voando;
            // (a etapa 3) a ave no alto voa; o quadrúpede onde não dá pé nada: pela posição, para servir nas cópias também
            if (!Morto && Dados.corpo == Corpo.Ave && AcimaDaSuperficie() > 0.7f && Estado != EstadoDoBicho.Deitado) pose = AnimacaoDoBicho.Pose.Voando;
            else if (!Morto && Dados.corpo == Corpo.Quadrupede && Nadando()) pose = AnimacaoDoBicho.Pose.Nadando;
            else if (!Morto && (Especie == Especie.Esquilo || Especie == Especie.Guaxinim) && AcimaDaSuperficie() > 0.6f) pose = AnimacaoDoBicho.Pose.Alerta;
            else if (!Morto && Dados.papel == Papel.Urso && Estado == EstadoDoBicho.Investindo && Velocidade < 0.5f && AlguemEmCima()) pose = AnimacaoDoBicho.Pose.Alerta;
            Olhos(dt);   // (a etapa 4) os olhos que brilham na lanterna
            // a referência das passadas: o trote fica perto de 30% dela e o galope acima de 60% (desde 13/11 a corrida
            // é mais lenta e o trote é ~40% dela; sem isso, o trote já saía galopando)
            _animacao.Atualizar(pose, Velocidade / Mathf.Max(0.3f, Escala), Mathf.Max(Dados.correr, Dados.trotar * 3.2f), _olhar, dt);
            MostrarCarga();
            Inclinar(dt);
            RastroDeSangue();
        }

        // a altura acima do chão ou da água (a ave voando), pela altura do mundo (sem raio: barato)
        float AcimaDaSuperficie()
        {
            if (!FaunaDoMundo.TemMapa) return 0f;
            Vector3 p = transform.position;
            return p.y - FaunaDoMundo.SuperficieEm(p.x, p.z, FaunaDoMundo.AlturaDoMapa(p.x, p.z));
        }

        // nadando: o lombo bem abaixo da água
        bool Nadando()
        {
            if (!FaunaDoMundo.TemMapa) return false;
            Vector3 p = transform.position;
            return FaunaDoMundo.AguaEm(p.x, p.z, out float nivel) > 0.2f && p.y < nivel - Altura * 0.45f;
        }

        // o que leva: o galho atravessado na boca do castor, o peixe nas garras da águia (feito na primeira vez)
        Transform _carga;
        void MostrarCarga()
        {
            if (!Carregando && _carga == null) return;
            if (_carga == null && Modelo != null)
            {
                if (Especie == Especie.Aguia && Modelo.corpo != null)
                    _carga = ModelosDosBichos.CaixaSolta(Modelo.corpo, new Vector3(0f, -0.14f, 0.02f), new Vector3(0.07f, 0.06f, 0.3f), new Color(0.55f, 0.58f, 0.6f));
                else
                {
                    var onde = Modelo.cabeca != null ? Modelo.cabeca : Modelo.raiz;
                    _carga = ModelosDosBichos.CaixaSolta(onde, new Vector3(0f, -0.02f, 0.08f), new Vector3(0.9f, 0.05f, 0.05f), new Color(0.4f, 0.3f, 0.2f), new Vector3(0f, 0f, 8f));
                }
            }
            if (_carga != null && _carga.gameObject.activeSelf != Carregando) _carga.gameObject.SetActive(Carregando);
        }

        // o corpo inclinado com o chão (a frente e a traseira), sem mexer no giro do bicho
        float _inclinacaoAtual;
        void Inclinar(float dt)
        {
            if (Dados.corpo != Corpo.Quadrupede || Morto || Nadando()) return;
            float meio = Dados.comprimento * Escala * 0.42f;
            Vector3 f = transform.position + transform.forward * meio, t = transform.position - transform.forward * meio;
            float alvo = Mathf.Atan2(Chao(t, out _) - Chao(f, out _), meio * 2f) * Mathf.Rad2Deg;
            _inclinacaoAtual = Mathf.Lerp(_inclinacaoAtual, Mathf.Clamp(alvo, -25f, 25f), 1f - Mathf.Exp(-6f * dt));
            _inclinacao.localRotation = Quaternion.Euler(_inclinacaoAtual, 0f, 0f);
        }

        // o rastro do ferido: uma mancha de sangue no chão a cada ~1,5 m andados
        void RastroDeSangue()
        {
            if (!Ferido || Morto) return;
            Vector3 p = transform.position;
            if ((p - _ultimoPontoDoSangue).sqrMagnitude < 1.5f * 1.5f || Time.time - _ultimoSangue < 0.25f) return;
            _ultimoPontoDoSangue = p;
            _ultimoSangue = Time.time;
            if (FaunaDoMundo.PertoDeAlguem(p, 60f)) Sangue.Mancha(p + Vector3.up * 0.05f, 0.25f + 0.25f * Escala);
        }

        // ------------------------------------------------------------------ o chão

        /// <summary>
        /// A altura do chão no ponto: o colisor do cenário perto dos jogadores (as ruas, as calçadas, o terreno com
        /// colisão), senão a altura exata do mundo (o sorteado ou o Lakewood). 'agua': a fundura da água ali; 'nivel': a altura
        /// dela (cada água do Lakewood tem a sua).
        /// </summary>
        internal static float Chao(Vector3 p, out float agua) => Chao(p, out agua, out _);

        internal static float Chao(Vector3 p, out float agua, out float nivel)
        {
            agua = FaunaDoMundo.AguaEm(p.x, p.z, out nivel);
            if (Physics.Raycast(new Vector3(p.x, p.y + 2.5f, p.z), Vector3.down, out var hit, 8f, 1, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return FaunaDoMundo.TemMapa ? FaunaDoMundo.AlturaDoMapa(p.x, p.z) : p.y;
        }

        // ------------------------------------------------------------------ a cópia (nos clientes)

        Vector3 _alvoDaCopia;
        float _giroDaCopia;
        bool _copiaPosta;

        internal void ReceberCopia(Vector3 posicao, float giro, EstadoDoBicho estado, float velocidade, bool ferido, bool carneado, bool alfa, bool carregando = false)
        {
            Carregando = carregando;
            _alvoDaCopia = posicao;
            _giroDaCopia = giro;
            if (!_copiaPosta)
            {
                transform.SetPositionAndRotation(posicao, Quaternion.Euler(0f, giro, 0f));
                _copiaPosta = true;
            }
            if (estado != Estado && estado == EstadoDoBicho.Morto) AoMorrer();
            Estado = estado;
            Velocidade = velocidade;
            Ferido = ferido;
            Carneado = carneado;
            Alfa = alfa;
        }

        internal void QuadroDaCopia(float dt)
        {
            Vector3 p = transform.position;
            float d = Vector3.Distance(p, _alvoDaCopia);
            // segue o ponto recebido (o pacote vem a cada 0,1 a 0,6 s): rápido se ficou para trás
            p = d > 8f ? _alvoDaCopia : Vector3.MoveTowards(p, _alvoDaCopia, Mathf.Max(Velocidade * 1.15f, d * 5f) * dt);
            transform.SetPositionAndRotation(p, Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, _giroDaCopia, 0f), 400f * dt));
            Animar(dt);
        }

        // ------------------------------------------------------------------ a morte e a carcaça (todas as máquinas)

        /// <summary>Morreu (no anfitrião e, pela rede, nas cópias): a carcaça passa a carnear.</summary>
        internal void AoMorrer()
        {
            Velocidade = 0f;
            foreach (var p in GetComponentsInChildren<ParteDoBicho>(true))
            {
                if (p.cabeca) continue;
                if (p.GetComponent<Carcaca>() == null) p.gameObject.AddComponent<Carcaca>().bicho = this;
            }
            if (FaunaDoMundo.PertoDeAlguem(transform.position, 60f)) Sangue.Mancha(transform.position + Vector3.up * 0.05f, 0.6f * Escala + 0.3f);
        }
    }

    /// <summary>
    /// A carcaça: com a faca na mão, segure E para carnear (o tempo pela espécie). O anfitrião confere e dá a carne crua,
    /// o couro e a gordura (FaunaDoMundo.Caca.cs).
    /// </summary>
    public sealed class Carcaca : Player.Interagivel
    {
        public Bicho bicho;

        bool TemFaca(Player.ArmaJogador quem) => quem != null && quem.NaMao == Loot.Item.Faca;

        public override string Dica(Player.ArmaJogador quem)
        {
            if (bicho == null || !bicho.Morto || bicho.Carneado) return null;
            string nome = bicho.Dados.NomeDaVariante(bicho.Variante);
            return TemFaca(quem) ? $"[Segure E] Carnear {nome}" : $"{Primeira(nome)} morto (com a faca na mão: carnear)";
        }

        static string Primeira(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        public override void Usar(Player.ArmaJogador quem) { }

        public override float Segurar(Player.ArmaJogador quem) =>
            bicho != null && bicho.Morto && !bicho.Carneado && TemFaca(quem) ? bicho.Dados.segundosParaCarnear * (bicho.Variante == Variante.Filhote ? 0.6f : 1f) : 0f;

        public override string Segurando(Player.ArmaJogador quem) => "Carneando";

        public override void Segurado(Player.ArmaJogador quem)
        {
            if (bicho == null || quem == null || quem.rede == null) return;
            quem.rede.PedirCarnear(bicho);
        }
    }
}
