using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// O lampião aceso no chão (11/11/2026, a etapa 4 do ramo Fogo): largado da mão aceso, ele continua aceso (o qtd dele é o
    /// <see cref="FogoDoJogador.LampiaoAcesoNoChao"/>), segura os zumbis como na mão (o ZombieManager.Fogo.cs) e gasta o
    /// combustível no anfitrião até acabar (aí ele apaga e fica). A chuva não o apaga. Pego de volta, ele vai para a mochila
    /// apagado (acende de novo na mão). Cada máquina mostra a chama e a luz dele.
    /// </summary>
    public partial class ItensNoMundo
    {
        /// <summary>O id do último item criado no chão (só no anfitrião).</summary>
        public ushort UltimoCriado { get; private set; }

        readonly Dictionary<ushort, float> _queimaDoLampiao = new Dictionary<ushort, float>();
        readonly Dictionary<ushort, ulong> _donosDosLampioes = new Dictionary<ushort, ulong>();
        float _proximoTiqueDoLampiao;

        static bool Aceso(ItemNoChao it) => it.item == Item.Lampiao && it.qtd == FogoDoJogador.LampiaoAcesoNoChao && it.desgaste < 100;

        /// <summary>Só no anfitrião: o dono do lampião aceso que acabou de ser largado (a Muralha de fogo dele).</summary>
        public void DonoDoLampiao(ushort id, ulong chave)
        {
            if (IsServer) _donosDosLampioes[id] = chave;
        }

        /// <summary>Só no anfitrião: os lampiões acesos no chão (o id, o ponto e a chave do dono).</summary>
        public void LampioesAcesos(List<(ushort id, Vector3 pos, ulong dono)> saida)
        {
            foreach (var it in _itens)
                if (Aceso(it)) saida.Add((it.id, it.apoio, _donosDosLampioes.TryGetValue(it.id, out var d) ? d : 0UL));
        }

        // no anfitrião, a cada segundo: o combustível dos lampiões acesos no chão
        void Update()
        {
            if (!IsServer || !IsSpawned || Time.time < _proximoTiqueDoLampiao) return;
            const float passo = 1f;
            _proximoTiqueDoLampiao = Time.time + passo;
            for (int k = 0; k < _itens.Count; k++)
            {
                var it = _itens[k];
                if (!Aceso(it)) continue;
                _queimaDoLampiao.TryGetValue(it.id, out float q);
                q += passo * 100f / (FogoDoJogador.MinutosDoLampiao * 60f);
                if (q < 1f)
                {
                    _queimaDoLampiao[it.id] = q;
                    continue;
                }
                int n = Mathf.FloorToInt(q);
                _queimaDoLampiao[it.id] = q - n;
                it.desgaste = (byte)Mathf.Min(100, it.desgaste + n);
                if (it.desgaste >= 100)
                {
                    it.qtd = 0;   // acabou o combustível: apagou
                    _queimaDoLampiao.Remove(it.id);
                    _donosDosLampioes.Remove(it.id);
                }
                _itens[k] = it;
            }
        }

        // em cada máquina: a chama e a luz do lampião aceso (no vidro: a altura do meio da malha)
        void FogoNoChao(ItemNoChaoNaMira o, ItemNoChao it)
        {
            bool aceso = Aceso(it);
            var fogo = o.transform.Find("Fogo");
            if (aceso && fogo == null && ConstrucoesNoMundo.Instancia != null)
            {
                var malha = o.GetComponent<MeshFilter>().sharedMesh;
                var b = malha != null ? malha.bounds : new Bounds(Vector3.up * 0.15f, Vector3.one * 0.3f);
                var onde = new Vector3(b.center.x, b.min.y + b.size.y * 0.42f, b.center.z);
                var f = ConstrucoesNoMundo.Fogo(o.transform, onde, ConstrucoesNoMundo.Instancia.materialDoFogo, 0.09f, FogoDoJogador.LuzDoLampiao, true, o.gameObject.layer);
                if (f.TryGetComponent<Light>(out var luz)) luz.intensity = FogoDoJogador.ForcaDaLuzDoLampiao;
                fogo = f.transform;
            }
            if (fogo != null && fogo.gameObject.activeSelf != aceso) fogo.gameObject.SetActive(aceso);
        }
    }
}
