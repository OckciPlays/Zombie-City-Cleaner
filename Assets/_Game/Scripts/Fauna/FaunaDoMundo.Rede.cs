using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// Os bichos na rede, como os zumbis (ZombieManager.Rede.cs): o anfitrião manda a cada cliente um pacote só com os
    /// bichos perto dele: os de até 30 m a cada 0,1 s, os de até 90 m a cada 0,3 s e os de até a distância de visão (no
    /// mínimo 120 m) a cada 0,6 s. Cada bicho vai em 13 bytes: o Id, a geração, a espécie e a variante, a posição em
    /// centímetros a partir do jogador que recebe, o giro, o estado (com os bits do ferido, do carneado e do alfa) e a
    /// velocidade. As mensagens não são confiáveis: o próximo pacote cobre o que se perdeu. No cliente, cada bicho vira uma
    /// cópia que some sem notícias por 2 s.
    /// </summary>
    public sealed partial class FaunaDoMundo
    {
        const string NomeDaMensagem = "ZCC.Fauna";
        const int PorPacote = 90;
        const int BytesPorBicho = 13;
        const float IntervaloDeEnvio = 0.1f, TempoParaSumir = 2f;
        const byte BitFerido = 0x08, BitCarneado = 0x10, BitAlfa = 0x20, BitCarregando = 0x40;

        float _proximoEnvio;
        int _tick;
        readonly List<Bicho> _envio = new List<Bicho>();
        readonly Dictionary<Bicho, float> _ultimoRecebido = new Dictionary<Bicho, float>();

        void Enviar()
        {
            if (Time.unscaledTime < _proximoEnvio) return;
            _proximoEnvio = Time.unscaledTime + IntervaloDeEnvio;
            _tick++;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;
            double tempo = nm.ServerTime.Time;
            foreach (var g in Pessoas)
            {
                ulong cliente = g.j.OwnerClientId;
                if (cliente == NetworkManager.ServerClientId) continue;   // o anfitrião vê os bichos de verdade
                float visao = Mathf.Max(120f, Graficos.Metros(g.j.Visao.Value) + 30f);
                _envio.Clear();
                foreach (var b in _ativos)
                {
                    float d = Vector3.Distance(g.pes, b.transform.position);
                    if (d > visao) continue;
                    int divisor = d < 30f ? 1 : d < 90f ? 3 : 6;
                    if ((_tick + b.Id) % divisor == 0) _envio.Add(b);
                }
                if (_envio.Count > 0) Mandar(nm, cliente, tempo, g.pes);
            }
        }

        void Mandar(NetworkManager nm, ulong cliente, double tempo, Vector3 origem)
        {
            for (int i = 0; i < _envio.Count; i += PorPacote)
            {
                int n = Mathf.Min(PorPacote, _envio.Count - i);
                using var w = new FastBufferWriter(sizeof(double) + 3 * sizeof(float) + sizeof(ushort) + n * BytesPorBicho, Allocator.Temp);
                w.WriteValueSafe(tempo);
                w.WriteValueSafe(origem);
                w.WriteValueSafe((ushort)n);
                for (int k = 0; k < n; k++)
                {
                    var b = _envio[i + k];
                    Vector3 p = b.transform.position - origem;
                    w.WriteValueSafe(b.Id);
                    w.WriteValueSafe(b.Geracao);
                    w.WriteValueSafe((byte)b.Especie);
                    w.WriteValueSafe((byte)b.Variante);
                    w.WriteValueSafe(Centimetros(p.x));
                    w.WriteValueSafe(Centimetros(p.y));
                    w.WriteValueSafe(Centimetros(p.z));
                    w.WriteValueSafe((byte)(Mathf.RoundToInt(b.transform.eulerAngles.y * (256f / 360f)) & 255));
                    byte estado = (byte)((byte)b.Estado | (b.Ferido ? BitFerido : 0) | (b.Carneado ? BitCarneado : 0) | (b.Alfa ? BitAlfa : 0)
                                         | (b.Carregando ? BitCarregando : 0));
                    w.WriteValueSafe(estado);
                    w.WriteValueSafe((byte)Mathf.Clamp(Mathf.RoundToInt(b.Velocidade * 10f), 0, 255));
                }
                nm.CustomMessagingManager.SendNamedMessage(NomeDaMensagem, cliente, w, NetworkDelivery.UnreliableSequenced);
            }
        }

        static short Centimetros(float v) => (short)Mathf.Clamp(Mathf.RoundToInt(v * 100f), short.MinValue, short.MaxValue);

        // ------------------------------------------------------------------ cliente: as cópias

        void AoReceber(ulong remetente, FastBufferReader r)
        {
            r.ReadValueSafe(out double tempo);
            r.ReadValueSafe(out Vector3 origem);
            r.ReadValueSafe(out ushort n);
            float agora = Time.unscaledTime;
            for (int i = 0; i < n; i++)
            {
                r.ReadValueSafe(out ushort id);
                r.ReadValueSafe(out byte geracao);
                r.ReadValueSafe(out byte especie);
                r.ReadValueSafe(out byte variante);
                r.ReadValueSafe(out short x);
                r.ReadValueSafe(out short y);
                r.ReadValueSafe(out short z);
                r.ReadValueSafe(out byte giro);
                r.ReadValueSafe(out byte estado);
                r.ReadValueSafe(out byte velocidade);
                if (id >= MaxBichos || especie >= Especies.Quantas || variante > (byte)Variante.Filhote) continue;
                var c = _porId[id];
                if (c == null || c.Geracao != geracao || (byte)c.Especie != especie || (byte)c.Variante != variante)
                {
                    if (c != null) TirarCopia(c);
                    c = NovaCopia(id, geracao, (Especie)especie, (Variante)variante);
                }
                c.ReceberCopia(origem + new Vector3(x, y, z) * 0.01f, giro * (360f / 256f), (EstadoDoBicho)(estado & 7), velocidade / 10f,
                               (estado & BitFerido) != 0, (estado & BitCarneado) != 0, (estado & BitAlfa) != 0, (estado & BitCarregando) != 0);
                _ultimoRecebido[c] = agora;
            }
        }

        Bicho NovaCopia(ushort id, byte geracao, Especie e, Variante v)
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);
            var b = go.AddComponent<Bicho>();
            b.Montar(id, geracao, e, v, true, id * 7919 + geracao * 31);
            _porId[id] = b;
            _ativos.Add(b);
            return b;
        }

        void TirarCopia(Bicho c)
        {
            _ativos.Remove(c);
            _ultimoRecebido.Remove(c);
            if (_porId[c.Id] == c) _porId[c.Id] = null;
            Destroy(c.gameObject);
        }

        void AtualizarCopias(float dt)
        {
            float limite = Time.unscaledTime - TempoParaSumir;
            for (int i = _ativos.Count - 1; i >= 0; i--)
            {
                var c = _ativos[i];
                if (c == null || !_ultimoRecebido.TryGetValue(c, out float quando) || quando < limite)
                {
                    if (c != null) TirarCopia(c);
                    else _ativos.RemoveAt(i);
                    continue;
                }
                c.QuadroDaCopia(dt);
            }
        }
    }
}
