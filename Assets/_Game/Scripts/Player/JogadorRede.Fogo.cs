using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O lampião e o molotov no corpo (11/11/2026, a etapa 4 do ramo Fogo; em qualquer máquina): vão no lugar da tocha na mão
    /// (as poses dela: o Tocha_Segurar e o Tocha_Golpe), com a malha deles (do Resources/ItensDoFogo) no lugar da dela. A
    /// luz do lampião no mundo é a do corpo, também no dono (como a da tocha).
    /// </summary>
    public partial class JogadorRede
    {
        /// <summary>O lampião aceso na mão, à vista (com combustível; no carro ele fica escondido): os zumbis ficam longe dele.</summary>
        public bool LampiaoAceso => Inventario != null && Inventario.LampiaoAcesoNaMao && !_naMaoEscondida && (Passageiro == null || !Passageiro.NoCarro);

        Transform _lugarDaTochaVisto, _pecaDaTocha, _pecaDoLampiao, _pecaDoMolotov;
        Renderer _rendererDaTocha;

        /// <summary>A peça certa no lugar da tocha (a tocha, o lampião ou o molotov) e a chama do lampião acesa ou não.</summary>
        void FogoNoCorpo()
        {
            if (Inventario == null || naMaoCorpo == null) return;
            int m = Itens.MaoDe(Item.Tocha);
            if (m < 0 || m >= naMaoCorpo.Length || naMaoCorpo[m] == null) return;
            var lugar = naMaoCorpo[m].transform;
            var item = Inventario.NaMao;
            if (lugar != _lugarDaTochaVisto)
            {
                // o corpo mudou (o personagem chegou pela rede): as peças são as deste
                _lugarDaTochaVisto = lugar;
                var mf = lugar.GetComponentInChildren<MeshFilter>(true);
                _pecaDaTocha = mf != null && mf.transform != lugar ? mf.transform : null;
                _rendererDaTocha = mf != null ? mf.GetComponent<Renderer>() : null;
                _pecaDoLampiao = _pecaDoMolotov = null;
            }
            bool fogoDeMao = Itens.EhFogoDeMao(item);
            if (_pecaDaTocha != null && _pecaDaTocha.gameObject.activeSelf == fogoDeMao) _pecaDaTocha.gameObject.SetActive(!fogoDeMao);
            if (fogoDeMao)
            {
                var material = ConstrucoesNoMundo.Instancia != null ? ConstrucoesNoMundo.Instancia.materialDoFogo : null;
                var pegada = (lugar.localPosition, lugar.localRotation);
                if (item == Item.Lampiao && _pecaDoLampiao == null) _pecaDoLampiao = ItensDoFogo.PecaNoCorpo(lugar, item, pegada, material);
                if (item == Item.Molotov && _pecaDoMolotov == null) _pecaDoMolotov = ItensDoFogo.PecaNoCorpo(lugar, item, pegada, material);
            }
            if (_pecaDoLampiao != null && _pecaDoLampiao.gameObject.activeSelf != (item == Item.Lampiao)) _pecaDoLampiao.gameObject.SetActive(item == Item.Lampiao);
            if (_pecaDoMolotov != null && _pecaDoMolotov.gameObject.activeSelf != (item == Item.Molotov)) _pecaDoMolotov.gameObject.SetActive(item == Item.Molotov);
        }

        /// <summary>A cada quadro, com o fogo na mão: a chama do lampião (sem combustível, apagada) e o que o dono vê do corpo.</summary>
        void FogoNoCorpoAgora()
        {
            var item = Inventario != null ? Inventario.NaMao : Item.Nada;
            if (!Itens.EhFogoDeMao(item)) return;
            if (_lugarDaTochaVisto == null || (item == Item.Lampiao ? _pecaDoLampiao : _pecaDoMolotov) == null) FogoNoCorpo();
            var peca = item == Item.Lampiao ? _pecaDoLampiao : _pecaDoMolotov;
            if (peca == null) return;
            // no dono, a peça some como a tocha (CorpoDoDono: só a luz fica)
            bool visivel = _rendererDaTocha == null || _rendererDaTocha.enabled;
            uint mascara = _rendererDaTocha != null ? _rendererDaTocha.renderingLayerMask : 1u;
            if (peca.TryGetComponent<MeshRenderer>(out var r) && (r.enabled != visivel || r.renderingLayerMask != mascara))
            {
                r.enabled = visivel;
                r.renderingLayerMask = mascara;
            }
            var fogo = peca.Find("Fogo");
            if (fogo == null) return;
            bool aceso = item == Item.Molotov || Inventario.LampiaoAcesoNaMao;
            if (fogo.gameObject.activeSelf != aceso) fogo.gameObject.SetActive(aceso);
            if (fogo.TryGetComponent<ParticleSystemRenderer>(out var chamas) && chamas.enabled != visivel) chamas.enabled = visivel;
            if (aceso && item == Item.Lampiao && fogo.TryGetComponent<Light>(out var luz))
                luz.intensity = FogoDoJogador.ForcaDaLuzDoLampiao * (0.92f + 0.08f * Mathf.PerlinNoise(Time.time * 2.3f, OwnerClientId));
        }
    }
}
