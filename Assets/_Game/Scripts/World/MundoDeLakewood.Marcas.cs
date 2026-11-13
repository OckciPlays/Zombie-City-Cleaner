using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// As marcas do mapa no Lakewood (12/11/2026, o Plano_Lakewood, "O jogo do mundo sorteado no Lakewood", a parte 7): as
    /// mesmas do mundo sorteado (<see cref="MundoDaPartida"/>.Marcas.cs): o ping some sozinho, cada jogador tem até 3, e as
    /// marcas e as anotações ficam (e vão no save do Lakewood); o anfitrião escreve a lista, a pedido dos jogadores.
    /// </summary>
    public partial class MundoDeLakewood : IMarcasDoMundo
    {
        NetworkList<MarcaNoMapa> _marcas;
        ushort _proximaMarca = 1;
        float _proximaLimpeza;

        public NetworkList<MarcaNoMapa> Marcas => _marcas;
        public event Action<MarcaNoMapa> MarcaNova;
        public double Agora => NetworkManager != null && IsSpawned ? NetworkManager.ServerTime.Time : 0.0;

        partial void IniciarAsListasDasMarcas()
        {
            if (_marcas != null) return;
            _marcas = new NetworkList<MarcaNoMapa>();
            _marcas.OnListChanged += e =>
            {
                if (e.Type == NetworkListEvent<MarcaNoMapa>.EventType.Add) MarcaNova?.Invoke(e.Value);
            };
        }

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

        /// <summary>As marcas e as anotações (sem os pings), para o save.</summary>
        public List<(TipoDeMarca tipo, Vector3 pos, byte cor, string texto, string autor)> CopiaDasMarcas()
        {
            var lista = new List<(TipoDeMarca, Vector3, byte, string, string)>();
            if (_marcas == null) return lista;
            foreach (var m in _marcas)
                if (m.tipo != TipoDeMarca.Ping) lista.Add((m.tipo, m.pos, m.cor, m.texto.ToString(), m.autor.ToString()));
            return lista;
        }

        /// <summary>No anfitrião: as marcas do save voltam.</summary>
        public void RestaurarMarcas(IEnumerable<(TipoDeMarca tipo, Vector3 pos, byte cor, string texto, string autor)> lista)
        {
            if (!IsServer || !IsSpawned) return;
            _marcas.Clear();
            foreach (var (tipo, pos, cor, texto, autor) in lista)
                if (tipo != TipoDeMarca.Ping) PorMarca(tipo, pos, cor, texto, ulong.MaxValue, autor);
        }
    }
}
