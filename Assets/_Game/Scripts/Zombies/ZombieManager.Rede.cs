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
    /// 3 envios, e os mais longe não vão. Cada zumbi ocupa 18 bytes (BytesPorZumbi): posição em centímetros a partir do jogador que recebe
    /// (o pacote leva a posição dele: em centímetros num short, a posição do mundo só cabia até 327 m da origem, e no
    /// mapa gerado, de quilômetros, os zumbis chegavam presos na borda, longe de quem entrou; 28/10/2026), giro, estado (com as
    /// contagens de golpes e de tiros levados), velocidade, como ele morreu (morto, a velocidade dá lugar à direção do
    /// tiro que o derrubou), os membros perdidos (2 bits por membro), a pedra do sol e o equilíbrio sem uma perna (a fase e a
    /// inclinação, Zumbi.Equilibrio.cs). As mensagens são não confiáveis e em
    /// ordem: um pacote perdido é coberto pelo próximo, e como o estado inteiro vai em todos, nada se perde de vez.
    /// No cliente, cada zumbi recebido vira uma cópia (do mesmo pool), que some se ficar sem notícias.
    /// Desde 04/11/2026 o byte do modelo leva, no bit de cima, o "te caça": se aquele zumbi caça quem recebe o pacote
    /// (<see cref="Zumbi.Caca"/>). Só o anfitrião sabe o alvo de cada zumbi, e a trilha de cada jogador passou a contar só
    /// os caçadores dele (a pedido: os amigos acharam a música de luta "muito cansativa, pois toda hora a música entra").
    /// O pacote continua com 18 bytes por zumbi, e o modelo fica nos 7 bits de baixo (cabem 127 modelos).
    /// </summary>
    public partial class ZombieManager
    {
        const string NomeDaMensagem = "ZCC.Zumbis";
        // ~1,2 KB por mensagem: abaixo do limite das mensagens não confiáveis (1.300 bytes)
        const int PorPacote = 65;
        // (o 15º, desde 28/10/2026: a pedra do sol, de 0 a 255; o 16º, desde 31/10/2026: o equilíbrio sem uma perna, a fase
        // em 3 bits e a inclinação em 5; o 17º e o 18º, desde 01/11/2026: o modelo (os especiais não saem do Id, como os
        // Ocos) e o byte do especial: a fase e a vida do Brutamonte, o cuspe e o grito do Long)
        const int BytesPorZumbi = 18;
        // o bit de cima do byte do modelo (o 17º): este zumbi caça quem recebe o pacote (04/11/2026). O modelo fica nos 7 de
        // baixo; hoje são uns 21 modelos, e o EnviarParaClientes confere que são menos de 128
        const int BitDaCaca = 0x80;

        [Header("Rede")]
        [Tooltip("Intervalo entre envios (s). Os zumbis perto de cada cliente vão em todos.")]
        public float intervaloDeEnvio = 0.1f;
        [Tooltip("Os do nível médio vão a cada N envios.")]
        public int divisorMedio = 3;
        [Tooltip("Os de longe (parados), dentro da distância de visão do cliente (UI/Graficos), vão a cada N envios (abaixo do tempo para sumir).")]
        public int divisorLonge = 5;
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
                // o modelo divide o byte com o "te caça" (04/11/2026): com 128 modelos ou mais, o bit de cima viraria parte do número
                Debug.Assert(modelos.Length < BitDaCaca, "ZombieManager: com 128 modelos ou mais o modelo não cabe nos 7 bits de baixo do byte (o de cima é o \"te caça\", ZombieManager.Rede.cs).");
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
                        float d = Distancia(Jogadores[j], z.transform.position);
                        var n = z.SempreDePerto(d) ? Zumbi.Nivel.Perto : NivelPara(d, (Zumbi.Nivel)niveis[z.Id]);
                        niveis[z.Id] = (byte)n;
                        // os do nível médio se revezam (pelo Id), para não irem todos no mesmo envio; os de longe também, mais
                        // devagar, só até a distância de visão do cliente (lá ele os desenha parados, como o anfitrião)
                        if (n == Zumbi.Nivel.Perto || (n == Zumbi.Nivel.Medio && (_tick + z.Id) % divisorMedio == 0)
                            || (n == Zumbi.Nivel.Longe && d < Jogadores[j].visao && (_tick + z.Id) % divisorLonge == 0)) _envio.Add(z);
                    }
                    if (_envio.Count > 0) Mandar(cliente, tempo, Jogadores[j].jogador.transform.position, _envio, Jogadores[j].jogador);
                }
                ClientesNaRede = clientes;
            }
        }

        // origem: o jogador do cliente (os zumbis que vão estão até a distância de visão dele, no máximo 85 m, ou 160 na luneta);
        // receptor: ele mesmo, para o "te caça" de cada zumbi (04/11/2026)
        void Mandar(ulong cliente, double tempo, Vector3 origem, List<Zumbi> lista, IAlvoDeZumbi receptor)
        {
            for (int i = 0; i < lista.Count; i += PorPacote)
            {
                int n = Mathf.Min(PorPacote, lista.Count - i);
                using var w = new FastBufferWriter(sizeof(double) + 3 * sizeof(float) + sizeof(ushort) + n * BytesPorZumbi, Allocator.Temp);
                w.WriteValueSafe(tempo);
                w.WriteValueSafe(origem);
                w.WriteValueSafe((ushort)n);
                for (int k = 0; k < n; k++)
                {
                    var z = lista[i + k];
                    Vector3 p = z.transform.position - origem;
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
                    w.WriteValueSafe(z.EquilibrioNaRede);
                    // o modelo, com o "te caça" no bit de cima: o mesmo zumbi vai com 1 para quem ele caça e 0 para os outros
                    w.WriteValueSafe((byte)(z.modelo | (z.Caca(receptor) ? BitDaCaca : 0)));
                    w.WriteValueSafe(z.EspecialNaRede);
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
                r.ReadValueSafe(out Vector3 origem);
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
                    r.ReadValueSafe(out byte equilibrio);
                    r.ReadValueSafe(out byte modelo);
                    r.ReadValueSafe(out byte especial);
                    // o bit de cima é o "te caça" (04/11/2026); o modelo são os 7 de baixo, separados antes de qualquer conta com
                    // ele (o teste abaixo, a troca de cópia e o pool)
                    bool teCaca = (modelo & BitDaCaca) != 0;
                    modelo = (byte)(modelo & ~BitDaCaca);
                    if (id >= MaxZumbis || modelo >= modelos.Length) continue;
                    var c = _porId[id];
                    if (c == null || c.Geracao != geracao || c.modelo != modelo)
                    {
                        // Id novo, ou o mesmo Id reaproveitado por outro zumbi
                        if (c != null) DevolverCopia(c);
                        c = NovaCopia(id, geracao, modelo);
                    }
                    var e = (Zumbi.Estado)(estado & 15);
                    c.Receber(tempo, origem + new Vector3(x, y, z) * 0.01f, yaw * (360f / 256f), e, (byte)((estado >> 4) & 3), (byte)((estado >> 6) & 3),
                              morte, e == Zumbi.Estado.Morto ? velocidade * (360f / 256f) : velocidade / 40f, cortes, pedra, equilibrio, especial);
                    c.CacaQuemRecebe = teCaca;
                }
            }
        }

        Zumbi NovaCopia(int id, byte geracao, int modelo)
        {
            var c = PegarDoPool(modelo);
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
                // o "te caça" sem notícias (04/11/2026): quem te caça está a menos de 30 m, então vem a cada envio (perto) ou a
                // cada divisorMedio envios (0,3 s). Passadas 2,5 vezes isso (0,75 s) sem pacote, ele parou de vir (ou os pacotes
                // se perderam): a cópia só some com o tempoParaSumir (1,5 s), parada onde estava, mas já não conta para a trilha.
                // Se ele voltar a vir, o próximo pacote traz o bit de novo
                float limiteDaCaca = Time.unscaledTime - intervaloDeEnvio * Mathf.Max(1, divisorMedio) * 2.5f;
                var local = AlvoLocal();
                for (int i = _copias.Count - 1; i >= 0; i--)
                {
                    var c = _copias[i];
                    if (c.ultimoRecebido < limite)
                    {
                        DevolverCopia(c);   // o pool zera o "te caça" (Zumbi.Zerar)
                        continue;
                    }
                    if (c.ultimoRecebido < limiteDaCaca) c.CacaQuemRecebe = false;
                    c.QuadroDaCopia(agora, dt);
                    // hitboxes e sombra pela distância de quem joga aqui (a cópia só existe até ~40 m, ou mais na luneta)
                    float daqui = Distancia(local, c.transform.position);
                    var n = c.SempreDePerto(daqui) ? Zumbi.Nivel.Perto : NivelPara(daqui, c.NivelAtual);
                    c.AplicarNivel(n == Zumbi.Nivel.Longe ? Zumbi.Nivel.Medio : n);
                    c.DefinirDetalhe(daqui, distanciaDoLOD1, distanciaDoLOD2);
                }
            }
        }
    }
}
