using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O desmontar que dá sucata (09/11/2026, a 2ª leva): leva uns segundos (<see cref="Receitas.SegundosPara"/>), com a barra
    /// no rodapé da mochila e o barulhinho das peças; fechar a mochila, largar o item ou ele sumir interrompe. Só no dono:
    /// no fim, o pedido de sempre ao anfitrião (<see cref="Desmontar"/>), que confere e dá as peças, com o Sucateiro
    /// (<see cref="BonusDoSucateiro"/>: +1 sucata por ponto).
    /// </summary>
    public partial class InventarioJogador
    {
        ushort _desmontando;
        Item _itemDesmontando;
        float _desmontarDesde, _desmontarTempo, _somDoDesmontar;

        /// <summary>Desmontando agora (a barra correndo).</summary>
        public bool Desmontando => _desmontando != 0;
        /// <summary>O item que está sendo desmontado (o id na mochila).</summary>
        public ushort IdDesmontando => _desmontando;
        public float ProgressoDoDesmontar => Desmontando && _desmontarTempo > 0f ? Mathf.Clamp01((Time.time - _desmontarDesde) / _desmontarTempo) : 0f;
        public string TextoDoDesmontar => Desmontando ? $"Desmontando {Itens.NomeComArtigo(_itemDesmontando)}" : null;

        /// <summary>A sucata a mais por desmontar (o Sucateiro: 1 por ponto).</summary>
        public int BonusDoSucateiro => Habilidades.Pontos(_rede, Habilidade.Sucateiro);

        /// <summary>Para o desmontar (fechou a mochila, largou o item); o aviso vai para a mochila.</summary>
        public void CancelarDesmontar(string motivo = "Desmontagem interrompida")
        {
            if (!Desmontando) return;
            _desmontando = 0;
            if (motivo != null) Feito?.Invoke(motivo);
        }

        // começa a barra (devolve falso se o desmontar é na hora)
        bool ComecarADesmontar(ushort id, Desmontagem d)
        {
            float t = Receitas.SegundosPara(d);
            if (t <= 0f) return false;
            _desmontando = id;
            _itemDesmontando = d.item;
            _desmontarDesde = Time.time;
            _desmontarTempo = t;
            _somDoDesmontar = 0f;
            return true;
        }

        // no dono, a cada quadro: o barulho e o fim da barra
        void AtualizarDesmontar()
        {
            if (!Desmontando) return;
            if (!Achar(_desmontando, out var it) || it.item != _itemDesmontando || (_arma.Usando && _desmontando == IdNaMao))
            {
                CancelarDesmontar();
                return;
            }
            if (Time.time >= _somDoDesmontar)
            {
                Efeitos.Som(SomDaSucata.Desmontando, transform.position + Vector3.up * 0.9f, 0.55f, true, 12f);
                _somDoDesmontar = Time.time + 0.72f;
            }
            if (Time.time - _desmontarDesde < _desmontarTempo) return;
            ushort id = _desmontando;
            _desmontando = 0;
            DesmontarAgora(id);
        }
    }
}
