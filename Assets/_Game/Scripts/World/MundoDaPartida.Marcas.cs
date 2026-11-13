using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// As marcas do mapa da partida (03/11/2026, a pedido: "pings, marcações e anotações no mapa"), na rede, de todos: a
    /// lista (<see cref="Marcas"/>) é escrita pelo anfitrião, a pedido dos jogadores (Player/JogadorRede.Marcas.cs).
    ///  - o ping some sozinho em <see cref="MarcasDoMapa.DuracaoDoPing"/> s, e cada jogador tem até
    ///    <see cref="MarcasDoMapa.PingsPorJogador"/> (o quarto tira o mais velho);
    ///  - a marca e a anotação ficam (e vão no save do mundo); qualquer um edita ou apaga.
    /// Em cada máquina, a marca nova chega pelo <see cref="MarcaNova"/> (o bipe do ping, no minimapa).
    /// </summary>
    public partial class MundoDaPartida : IMarcasDoMundo
    {
        NetworkList<MarcaNoMapa> _marcas;
        ushort _proximaMarca = 1;
        float _proximaLimpeza;

        /// <summary>As marcas e os pings no mapa (de todos, na rede).</summary>
        public NetworkList<MarcaNoMapa> Marcas => _marcas;

        /// <summary>Em cada máquina: uma marca (ou um ping) acabou de entrar na lista.</summary>
        public event Action<MarcaNoMapa> MarcaNova;

        /// <summary>O tempo do anfitrião (s), o mesmo em todas as máquinas: o fim dos pings.</summary>
        public double Agora => NetworkManager != null && IsSpawned ? NetworkManager.ServerTime.Time : 0.0;

        void IniciarAsMarcas()
        {
            if (_marcas != null) return;
            _marcas = new NetworkList<MarcaNoMapa>();
            _marcas.OnListChanged += AoMudarAsMarcas;
        }

        void AoMudarAsMarcas(NetworkListEvent<MarcaNoMapa> e)
        {
            if (e.Type == NetworkListEvent<MarcaNoMapa>.EventType.Add) MarcaNova?.Invoke(e.Value);
        }

        // ------------------------------------------------------------------ no anfitrião

        /// <summary>No anfitrião (o F1): liga ou desliga o fogo amigo deste mundo (e no save dele).</summary>
        public void DefinirFogoAmigo(bool ligado)
        {
            if (!IsServer || !IsSpawned) return;
            _fogoAmigo.Value = ligado;
            if (Salvar.Salvamento.Atual != null) Salvar.Salvamento.Atual.fogoAmigo = ligado;
        }

        /// <summary>No anfitrião: uma marca nova (o ping tira o mais velho do mesmo jogador, passando de 3).</summary>
        public void PorMarca(TipoDeMarca tipo, Vector3 pos, byte cor, string texto, ulong dono, string autor)
        {
            if (!IsServer || !IsSpawned) return;
            if (tipo == TipoDeMarca.Ping)
            {
                int pings = 0;
                foreach (var m in _marcas) if (m.tipo == TipoDeMarca.Ping && m.dono == dono) pings++;
                for (int i = 0; i < _marcas.Count && pings >= MarcasDoMapa.PingsPorJogador; i++)
                    if (_marcas[i].tipo == TipoDeMarca.Ping && _marcas[i].dono == dono)
                    {
                        _marcas.RemoveAt(i);
                        pings--;
                        i--;
                    }
            }
            else if (_marcas.Count >= MarcasDoMapa.MarcasNoMundo) return;
            _marcas.Add(new MarcaNoMapa
            {
                id = NovoIdDeMarca(), tipo = tipo, cor = (byte)Mathf.Clamp(cor, 0, MarcasDoMapa.Cores.Length - 1), dono = dono, pos = pos,
                ate = tipo == TipoDeMarca.Ping ? Agora + MarcasDoMapa.DuracaoDoPing : 0.0,
                autor = Fixa32(MarcasDoMapa.Cortar(autor, 20)), texto = tipo == TipoDeMarca.Anotacao ? Fixa128(MarcasDoMapa.Cortar(texto)) : default,
            });
        }

        /// <summary>No anfitrião: a marca (ou o ping) sai.</summary>
        public void ApagarMarca(ushort id)
        {
            if (!IsServer) return;
            for (int i = 0; i < _marcas.Count; i++)
                if (_marcas[i].id == id)
                {
                    _marcas.RemoveAt(i);
                    return;
                }
        }

        /// <summary>No anfitrião: a cor e o texto de uma marca mudam (sem texto, ela vira uma marca; com, uma anotação).</summary>
        public void EditarMarca(ushort id, byte cor, string texto)
        {
            if (!IsServer) return;
            for (int i = 0; i < _marcas.Count; i++)
            {
                var m = _marcas[i];
                if (m.id != id || m.tipo == TipoDeMarca.Ping) continue;
                texto = MarcasDoMapa.Cortar(texto);
                m.cor = (byte)Mathf.Clamp(cor, 0, MarcasDoMapa.Cores.Length - 1);
                m.texto = Fixa128(texto);
                m.tipo = texto.Length > 0 ? TipoDeMarca.Anotacao : TipoDeMarca.Marca;
                _marcas[i] = m;
                return;
            }
        }

        /// <summary>No anfitrião, uma vez por segundo: os pings que acabaram saem.</summary>
        public void LimparOsPings()
        {
            if (!IsServer || !IsSpawned || Time.unscaledTime < _proximaLimpeza) return;
            _proximaLimpeza = Time.unscaledTime + 1f;
            double agora = Agora;
            for (int i = _marcas.Count - 1; i >= 0; i--)
                if (_marcas[i].tipo == TipoDeMarca.Ping && _marcas[i].ate <= agora) _marcas.RemoveAt(i);
        }

        ushort NovoIdDeMarca()
        {
            for (int volta = 0; volta < 65535; volta++)
            {
                ushort id = _proximaMarca++;
                if (_proximaMarca == 0) _proximaMarca = 1;
                bool usado = false;
                foreach (var m in _marcas) usado |= m.id == id;
                if (!usado && id != 0) return id;
            }
            return 1;
        }

        static FixedString32Bytes Fixa32(string s)
        {
            var f = new FixedString32Bytes();
            foreach (char c in s ?? "") if (f.Append(c) != FormatError.None) break;
            return f;
        }

        static FixedString128Bytes Fixa128(string s)
        {
            var f = new FixedString128Bytes();
            foreach (char c in s ?? "") if (f.Append(c) != FormatError.None) break;
            return f;
        }

        // ------------------------------------------------------------------ o save

        /// <summary>As marcas e as anotações (sem os pings), para o save do mundo.</summary>
        public List<(TipoDeMarca tipo, Vector3 pos, byte cor, string texto, string autor)> CopiaDasMarcas()
        {
            var lista = new List<(TipoDeMarca, Vector3, byte, string, string)>();
            if (_marcas == null) return lista;
            foreach (var m in _marcas)
                if (m.tipo != TipoDeMarca.Ping) lista.Add((m.tipo, m.pos, m.cor, m.texto.ToString(), m.autor.ToString()));
            return lista;
        }

        /// <summary>No anfitrião: as marcas do save voltam (as de agora saem).</summary>
        public void RestaurarMarcas(IEnumerable<(TipoDeMarca tipo, Vector3 pos, byte cor, string texto, string autor)> lista)
        {
            if (!IsServer || !IsSpawned) return;
            _marcas.Clear();
            foreach (var (tipo, pos, cor, texto, autor) in lista)
                if (tipo != TipoDeMarca.Ping) PorMarca(tipo, pos, cor, texto, ulong.MaxValue, autor);
        }
    }
}
