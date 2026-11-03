using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Um quadro do jogo: onde estão as 16 bolas (no plano da mesa), as que já caíram e o que soou desde o quadro
    /// anterior (a batida mais forte entre bolas e na tabela, de 0 a 255, as caçapas e se teve tacada).
    /// </summary>
    public struct QuadroDaSinuca : INetworkSerializable
    {
        public Vector2[] pos;
        public ushort fora;
        public byte bola, tabela, cacapas, tacada;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            if (s.IsReader) pos = new Vector2[FisicaDaSinuca.Bolas];
            for (int i = 0; i < FisicaDaSinuca.Bolas; i++) s.SerializeValue(ref pos[i]);
            s.SerializeValue(ref fora);
            s.SerializeValue(ref bola);
            s.SerializeValue(ref tabela);
            s.SerializeValue(ref cacapas);
            s.SerializeValue(ref tacada);
        }
    }

    /// <summary>
    /// A mesa de sinuca que se joga (a pedido em 29/10/2026; no bunker e no mapa desde 01/11/2026). A mesa é o modelo da
    /// mobília nova (<c>MesaSinuca</c>, na escala dos móveis), e as 16 bolas são as filhas dele. O jogo dela fica no
    /// <see cref="SinucaDoMundo"/>, pela <see cref="chave"/> (a mesma em todas as máquinas): a mesa aparece e some com os
    /// móveis de perto, e o jogo continua onde estava.
    /// - **As bolas** vêm numa caixa (o item <see cref="Item.CaixaDeBolas"/>): com ela na mochila, E na mesa põe as bolas
    ///   arrumadas no triângulo, e a caixa fica vazia na mochila; com a vazia, E recolhe as bolas (ninguém jogando).
    /// - **Jogar:** com um taco na mão (<see cref="Itens.EhTaco"/>), E na mesa entra no jogo: a câmera vai para cima da
    ///   mesa, o mouse mira a branca e o clique segurado dá a força (veja <c>MesaDeSinuca.Jogo.cs</c>). Um jogador por vez;
    ///   o corpo dele fica parado onde estava, e os outros só veem as bolas andando.
    /// - **A rede:** o anfitrião guarda as posições, as que caíram e quem joga. Quem joga simula a tacada
    ///   (<see cref="FisicaDaSinuca"/>) e manda um quadro a cada 0,05 s para os outros; quando as bolas param, manda o fim
    ///   para o anfitrião, que grava. A preta na caçapa, ou todas as 15, recomeça o jogo (o triângulo de novo).
    /// </summary>
    [DefaultExecutionOrder(200)]   // a câmera de quem joga é posta depois de tudo o que mexe nela
    public partial class MesaDeSinuca : MonoBehaviour
    {
        [Tooltip("A chave da mesa no SinucaDoMundo (a da obra, do ponto e da parte da mesa; a mesma em todas as máquinas).")]
        public ulong chave;
        [Tooltip("A mesa (o modelo da mobília nova, na escala dos móveis): o pano é o plano dela; as bolas são filhas dela.")]
        public Transform mesa;
        [Tooltip("As 16 bolas (MesaSinuca.Bola.0 a .15), filhas da mesa.")]
        public Transform[] bolas;
        [Tooltip("As malhas dos 4 tacos (as do chão, na ordem do enum Item), para o taco que mira; o material é o da paleta.")]
        public Mesh[] tacos;
        public Material materialDoTaco;
        [Tooltip("O material das linhas da mira (sem luz).")]
        public Material materialDaLinha;
        [Tooltip("A altura do pano, no tamanho real da mesa (sem a escala).")]
        public float alturaDoPano = 0.78f;

        // a vista desta máquina: onde cada bola aparece, para onde ela vai e as que estão escondidas
        readonly Vector2[] _vista = new Vector2[FisicaDaSinuca.Bolas];
        readonly Vector2[] _alvo = new Vector2[FisicaDaSinuca.Bolas];
        ushort _foraDoAlvo;
        bool _pular = true;
        int _rodadaVista = int.MinValue;
        SinucaDoMundo _registrada;
        // o último quadro de quem joga (os outros mostram ele até o fim da tacada chegar no anfitrião)
        float _quadroAte;

        /// <summary>O jogo das mesas (na rede), ou null fora de uma partida.</summary>
        static SinucaDoMundo Rede => SinucaDoMundo.Instancia != null && SinucaDoMundo.Instancia.IsSpawned ? SinucaDoMundo.Instancia : null;

        MesaNaRede Estado()
        {
            var r = Rede;
            if (r != null && r.Estado(chave, out var m)) return m;
            return new MesaNaRede { chave = chave, jogador = SinucaDoMundo.Ninguem };
        }

        /// <summary>As bolas estão na mesa.</summary>
        public bool ComBolas => Estado().comBolas;
        /// <summary>Alguém está jogando (o anfitrião aceitou).</summary>
        public bool Ocupada => Estado().jogador != SinucaDoMundo.Ninguem;
        public bool EuJogo => Rede != null && NetworkManager.Singleton != null && Estado().jogador == NetworkManager.Singleton.LocalClientId;
        /// <summary>O meio da mesa (para a distância dos pedidos).</summary>
        public Vector3 Centro => mesa != null ? mesa.position : transform.position;

        void OnEnable()
        {
            _pular = true;
            Registrar();
        }

        void OnDisable()
        {
            SairJa();
            if (_registrada != null) _registrada.Esquecer(this);
            _registrada = null;
        }

        void Registrar()
        {
            var r = SinucaDoMundo.Instancia;
            if (r == null || r == _registrada) return;
            _registrada?.Esquecer(this);
            _registrada = r;
            r.Registrar(this);
            Confirmado();
        }

        /// <summary>O anfitrião gravou (ou a mesa apareceu): a vista vai para o que está na rede.</summary>
        internal void Confirmado()
        {
            _quadroAte = 0f;
            _esperandoAnfitriao = false;
            int rodada = Estado().rodada;
            if (rodada != _rodadaVista) _pular = true;
            _rodadaVista = rodada;
            LerAlvo();
        }

        readonly Vector2[] _lidas = new Vector2[FisicaDaSinuca.Bolas];

        void LerAlvo()
        {
            var r = Rede;
            if (r != null) r.Posicoes(chave, _lidas);
            else
            {
                var arrumadas = FisicaDaSinuca.Arrumadas(1);
                for (int i = 0; i < _lidas.Length; i++) _lidas[i] = arrumadas[i];
            }
            for (int i = 0; i < FisicaDaSinuca.Bolas; i++) _alvo[i] = _lidas[i];
            _foraDoAlvo = Estado().fora;
        }

        /// <summary>Um quadro de quem joga (de outra máquina): as bolas vão para lá, e os sons saem no meio da mesa.</summary>
        internal void ReceberQuadro(QuadroDaSinuca quadro)
        {
            for (int i = 0; i < FisicaDaSinuca.Bolas; i++) _alvo[i] = quadro.pos[i];
            _foraDoAlvo = quadro.fora;
            _quadroAte = Time.time + 1.5f;
            var meio = mesa != null ? mesa.position + Vector3.up * alturaDoPano * mesa.lossyScale.y : transform.position;
            if (quadro.tacada != 0) Efeitos.Som(SomDaSinuca.Tacada, meio, 0.7f, true, 25f);
            if (quadro.bola > 4) Efeitos.Som(SomDaSinuca.Bolas, meio, quadro.bola / 255f, true, 25f);
            if (quadro.tabela > 4) Efeitos.Som(SomDaSinuca.Tabela, meio, quadro.tabela / 255f * 0.8f, true, 25f);
            if (quadro.cacapas > 0) Efeitos.Som(SomDaSinuca.Cacapa, meio, 0.8f, true, 25f);
        }

        void Update()
        {
            // o jogo das mesas chegou depois da mesa (a partida começando), ou mudou (outra partida)
            if (SinucaDoMundo.Instancia != _registrada) Registrar();
            if (Rede == null) return;
            AtualizarJogo();
            AtualizarVista();
        }

        // ------------------------------------------------------------------ a vista (todas as máquinas)

        void AtualizarVista()
        {
            if (bolas == null || mesa == null) return;
            ushort fora;
            if (_simulando)
            {
                // quem joga vê a própria simulação
                for (int i = 0; i < FisicaDaSinuca.Bolas; i++) _alvo[i] = _sim.Pos[i];
                fora = _sim.Fora;
                _pular = false;
                Mostrar(true, fora);
                return;
            }
            if (_esperandoAnfitriao && Time.time > _esperarAte) Confirmado();
            if (!_esperandoAnfitriao && Time.time > _quadroAte && _quadroAte > 0f) Confirmado();
            fora = _foraDoAlvo;
            Mostrar(false, fora);
        }

        void Mostrar(bool direto, ushort fora)
        {
            bool com = ComBolas;
            float k = 1f - Mathf.Exp(-22f * Time.deltaTime);
            float y = alturaDoPano + FisicaDaSinuca.Raio;
            for (int i = 0; i < FisicaDaSinuca.Bolas && i < bolas.Length; i++)
            {
                var b = bolas[i];
                if (b == null) continue;
                bool dentro = com && (fora & (1 << i)) == 0;
                if (b.gameObject.activeSelf != dentro) b.gameObject.SetActive(dentro);
                var antes = _vista[i];
                var alvo = _alvo[i];
                _vista[i] = direto || _pular || (alvo - antes).sqrMagnitude > 0.25f ? alvo : Vector2.Lerp(antes, alvo, k);
                if (!dentro) continue;
                b.localPosition = new Vector3(_vista[i].x, y, _vista[i].y);
                // a bola rola: gira em volta do eixo de lado do caminho, o quanto andou sobre o raio
                var anda = _vista[i] - antes;
                float d = anda.magnitude;
                if (d > 1e-5f && d < 0.3f)
                {
                    var eixo = Vector3.Cross(Vector3.up, new Vector3(anda.x, 0f, anda.y) / d);
                    b.localRotation = Quaternion.AngleAxis(d / FisicaDaSinuca.Raio * Mathf.Rad2Deg, eixo) * b.localRotation;
                }
            }
            _pular = false;
        }

        // ------------------------------------------------------------------ o E na mesa

        /// <summary>A dica embaixo da mira, para o jogador local.</summary>
        public string Dica(ArmaJogador quem)
        {
            var inventario = quem != null ? quem.Inventario : null;
            if (Rede == null || inventario == null || _jogando) return null;
            if (Ocupada && !EuJogo) return "Alguém está jogando sinuca";
            bool taco = Itens.EhTaco(inventario.NaMao);
            if (!ComBolas)
                return inventario.Primeiro(Item.CaixaDeBolas) != 0 ? "[E] Pôr as bolas na mesa"
                    : inventario.Primeiro(Item.CaixaDeBolasVazia) != 0 ? "A sua caixa de bolas está vazia" : "Mesa sem as bolas: ache uma caixa de bolas";
            if (taco) return "[E] Jogar sinuca";
            if (inventario.Primeiro(Item.CaixaDeBolasVazia) != 0) return "[E] Recolher as bolas   ·   segure um taco para jogar";
            return "Segure um taco para jogar";
        }

        /// <summary>O jogador local apertou E na mesa.</summary>
        public void Usar(ArmaJogador quem)
        {
            var r = Rede;
            var inventario = quem != null ? quem.Inventario : null;
            if (r == null || inventario == null || _jogando || (Ocupada && !EuJogo)) return;
            if (!ComBolas)
            {
                if (inventario.Primeiro(Item.CaixaDeBolas) != 0)
                {
                    r.PorBolas(this);
                    Efeitos.Som(SomDaSinuca.Cacapa, transform.position + Vector3.up, 0.5f, true, 20f);
                }
                return;
            }
            if (Itens.EhTaco(inventario.NaMao))
            {
                _armaQuePediu = quem;
                _pediuEm = Time.time;
                r.Jogar(this);
                return;
            }
            if (inventario.Primeiro(Item.CaixaDeBolasVazia) != 0)
            {
                r.RecolherBolas(this);
                Efeitos.Som(SomDaSinuca.Cacapa, transform.position + Vector3.up, 0.5f, true, 20f);
            }
        }
    }
}
