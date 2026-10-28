using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Os zumbis na rede. Não são NetworkObjects (seriam centenas): o anfitrião manda, para cada cliente, um pacote
    /// próprio só com os zumbis perto dele. Os que estão a menos de 15 m vão 10 vezes por segundo, os de 15 a 40 m a cada
    /// 3 envios, e os mais longe não vão. Cada zumbi ocupa 15 bytes: posição em centímetros, giro, estado (com as
    /// contagens de golpes e de tiros levados), velocidade, como ele morreu (morto, a velocidade dá lugar à direção do
    /// tiro que o derrubou), os membros perdidos (2 bits por membro) e a pedra do sol. As mensagens são não confiáveis e em
    /// ordem: um pacote perdido é coberto pelo próximo, e como o estado inteiro vai em todos, nada se perde de vez.
    /// No cliente, cada zumbi recebido vira uma cópia (do mesmo pool), que some se ficar sem notícias.
    /// </summary>
    public partial class ZombieManager
    {
        const string NomeDaMensagem = "ZCC.Zumbis";
        // ~1,1 KB por mensagem: abaixo do limite das mensagens não confiáveis (1.300 bytes)
        const int PorPacote = 80;
        const int BytesPorZumbi = 15;   // (o 15º, desde 28/10/2026: a pedra do sol, de 0 a 255)

        [Header("Rede")]
        [Tooltip("Intervalo entre envios (s). Os zumbis perto de cada cliente vão em todos.")]
        public float intervaloDeEnvio = 0.1f;
        [Tooltip("Os do nível médio vão a cada N envios.")]
        public int divisorMedio = 3;
        [Tooltip("Sem notícias por este tempo (s), a cópia some: o zumbi saiu do alcance.")]
        public float tempoParaSumir = 1.5f;

        static readonly ProfilerMarker MarcaRede = new("Zumbis.Rede");
        static readonly ProfilerMarker MarcaCopias = new("Zumbis.Copias");

        /// <summary>Bytes de zumbis enviados desde o começo (para o benchmark).</summary>
        public long BytesEnviados { get; private set; }
        /// <summary>Clientes (fora o anfitrião) que receberam zumbis no último envio.</summary>
        public int ClientesNaRede { get; private set; }
        public float KbPorSegundo { get; private set; }

        // nível de cada zumbi em relação a cada cliente, com a mesma histerese do LOD de IA
        readonly Dictionary<ulong, byte[]> _niveisPorCliente = new Dictionary<ulong, byte[]>();
        readonly List<Zumbi> _envio = new List<Zumbi>();
        readonly List<Zumbi> _copias = new List<Zumbi>();
        float _proximoEnvio, _proximaMedidaDeRede;
        long _bytesNaMedida;
        int _tick;

        void AoSairCliente(ulong cliente) => _niveisPorCliente.Remove(cliente);

        void EnviarParaClientes()
        {
            if (Time.unscaledTime >= _proximaMedidaDeRede)
            {
                _proximaMedidaDeRede = Time.unscaledTime + 1f;
                KbPorSegundo = (BytesEnviados - _bytesNaMedida) / 1024f;
                _bytesNaMedida = BytesEnviados;
            }
            if (Time.unscaledTime < _proximoEnvio) return;
            _proximoEnvio = Time.unscaledTime + intervaloDeEnvio;
            _tick++;
            using (MarcaRede.Auto())
            {
                double tempo = NetworkManager.ServerTime.Time;
                int clientes = 0;
                for (int j = 0; j < QuantosJogadores; j++)
                {
                    ulong cliente = Jogadores[j].jogador.OwnerClientId;
                    if (cliente == NetworkManager.ServerClientId) continue;   // o anfitrião vê os zumbis de verdade
                    clientes++;
                    if (!_niveisPorCliente.TryGetValue(cliente, out var niveis))
                    {
                        niveis = new byte[MaxZumbis];
                        for (int k = 0; k < niveis.Length; k++) niveis[k] = (byte)Zumbi.Nivel.Longe;
                        _niveisPorCliente[cliente] = niveis;
                    }
                    _envio.Clear();
                    foreach (var z in _ativos)
                    {
                        // a distância conta a luneta: na linha dela, os zumbis de longe vão como se estivessem perto
                        var n = NivelPara(Distancia(Jogadores[j], z.transform.position), (Zumbi.Nivel)niveis[z.Id]);
                        niveis[z.Id] = (byte)n;
                        // os do nível médio se revezam (pelo Id), para não irem todos no mesmo envio
                        if (n == Zumbi.Nivel.Perto || (n == Zumbi.Nivel.Medio && (_tick + z.Id) % divisorMedio == 0)) _envio.Add(z);
                    }
                    if (_envio.Count > 0) Mandar(cliente, tempo, _envio);
                }
                ClientesNaRede = clientes;
            }
        }

        void Mandar(ulong cliente, double tempo, List<Zumbi> lista)
        {
            for (int i = 0; i < lista.Count; i += PorPacote)
            {
                int n = Mathf.Min(PorPacote, lista.Count - i);
                using var w = new FastBufferWriter(sizeof(double) + sizeof(ushort) + n * BytesPorZumbi, Allocator.Temp);
                w.WriteValueSafe(tempo);
                w.WriteValueSafe((ushort)n);
                for (int k = 0; k < n; k++)
                {
                    var z = lista[i + k];
                    Vector3 p = z.transform.position;
                    w.WriteValueSafe((ushort)z.Id);
                    w.WriteValueSafe(z.Geracao);
                    w.WriteValueSafe(Centimetros(p.x));
                    w.WriteValueSafe(Centimetros(p.y));
                    w.WriteValueSafe(Centimetros(p.z));
                    w.WriteValueSafe((byte)(Mathf.RoundToInt(z.transform.eulerAngles.y * (256f / 360f)) & 255));
                    w.WriteValueSafe((byte)((byte)z.Atual | ((z.Golpes & 3) << 4) | ((z.Reacoes & 3) << 6)));
                    // morto, no lugar da velocidade vai a direção do tiro (para o corpo cair igual em todas as máquinas)
                    w.WriteValueSafe(z.Morto ? (byte)(Mathf.RoundToInt(z.DirecaoDaMorte * (256f / 360f)) & 255)
                                             : (byte)Mathf.Clamp(Mathf.RoundToInt(z.Velocidade * 40f), 0, 255));
                    w.WriteValueSafe(z.InfoDaMorte);
                    w.WriteValueSafe(z.Cortes);
                    w.WriteValueSafe((byte)Mathf.RoundToInt(z.Pedra * 255f));
                }
                NetworkManager.CustomMessagingManager.SendNamedMessage(NomeDaMensagem, cliente, w, NetworkDelivery.UnreliableSequenced);
                BytesEnviados += w.Length;
            }
        }

        static short Centimetros(float v) => (short)Mathf.Clamp(Mathf.RoundToInt(v * 100f), short.MinValue, short.MaxValue);

        // ------------------------------------------------------------------ cliente: as cópias

        void AoReceber(ulong remetente, FastBufferReader r)
        {
            using (MarcaCopias.Auto())
            {
                r.ReadValueSafe(out double tempo);
                r.ReadValueSafe(out ushort n);
                for (int i = 0; i < n; i++)
                {
                    r.ReadValueSafe(out ushort id);
                    r.ReadValueSafe(out byte geracao);
                    r.ReadValueSafe(out short x);
                    r.ReadValueSafe(out short y);
                    r.ReadValueSafe(out short z);
                    r.ReadValueSafe(out byte yaw);
                    r.ReadValueSafe(out byte estado);
                    r.ReadValueSafe(out byte velocidade);
                    r.ReadValueSafe(out byte morte);
                    r.ReadValueSafe(out byte cortes);
                    r.ReadValueSafe(out byte pedra);
                    if (id >= MaxZumbis) continue;
                    var c = _porId[id];
                    if (c == null || c.Geracao != geracao)
                    {
                        // Id novo, ou o mesmo Id reaproveitado por outro zumbi
                        if (c != null) DevolverCopia(c);
                        c = NovaCopia(id, geracao);
                    }
                    var e = (Zumbi.Estado)(estado & 15);
                    c.Receber(tempo, new Vector3(x, y, z) * 0.01f, yaw * (360f / 256f), e, (byte)((estado >> 4) & 3), (byte)((estado >> 6) & 3),
                              morte, e == Zumbi.Estado.Morto ? velocidade * (360f / 256f) : velocidade / 40f, cortes, pedra);
                }
            }
        }

        Zumbi NovaCopia(int id, byte geracao)
        {
            var c = PegarDoPool(ModeloDe(id, geracao));
            _porId[id] = c;
            _copias.Add(c);
            c.PrepararCopia(id, geracao, this);
            return c;
        }

        void DevolverCopia(Zumbi c)
        {
            _copias.Remove(c);
            if (_porId[c.Id] == c) _porId[c.Id] = null;
            GuardarNoPool(c);
        }

        void LimparCopias()
        {
            for (int i = _copias.Count - 1; i >= 0; i--) DevolverCopia(_copias[i]);
        }

        void AtualizarCopias()
        {
            using (MarcaCopias.Auto())
            {
                double agora = NetworkManager.ServerTime.Time;
                float dt = Time.deltaTime, limite = Time.unscaledTime - tempoParaSumir;
                var local = AlvoLocal();
                for (int i = _copias.Count - 1; i >= 0; i--)
                {
                    var c = _copias[i];
                    if (c.ultimoRecebido < limite)
                    {
                        DevolverCopia(c);
                        continue;
                    }
                    c.QuadroDaCopia(agora, dt);
                    // hitboxes e sombra pela distância de quem joga aqui (a cópia só existe até ~40 m, ou mais na luneta)
                    float daqui = Distancia(local, c.transform.position);
                    var n = NivelPara(daqui, c.NivelAtual);
                    c.AplicarNivel(n == Zumbi.Nivel.Longe ? Zumbi.Nivel.Medio : n);
                    c.DefinirDetalhe(daqui, distanciaDoLOD1, distanciaDoLOD2);
                }
            }
        }
    }
}
