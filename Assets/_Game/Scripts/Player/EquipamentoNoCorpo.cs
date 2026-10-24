using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Põe no corpo do jogador o que ele leva: a mochila nas costas, o coldre no quadril direito e as armas penduradas
    /// (sem mochila, a da alça na diagonal das costas; com ela, as duas presas nas laterais da mochila; a pistola da alça
    /// no cós e a curta no coldre). Só visual.
    /// As posições são medidas no corpo em repouso (o "Corpo" do prefab, sem animação, a <c>referencia</c>) e presas aos
    /// ossos (o peito e o quadril), então acompanham a animação em qualquer pose.
    /// Serve o retrato do inventário e o boneco de teste do menu do desenvolvedor; a fatia 2 das mochilas vai usar no jogador.
    /// </summary>
    public class EquipamentoNoCorpo : MonoBehaviour
    {
        // medidas do corpo em repouso (o espaço do "Corpo", olhando para +Z)
        const float Costas = -0.115f;        // a superfície das costas, na altura do peito
        const float TopoDaMochila = 1.45f;   // logo abaixo da altura dos ombros
        const float CentroDasCostas = 1.15f; // onde a arma da alça cruza as costas
        const float Cinto = 0.97f;
        const float LadoDoQuadril = 0.18f;
        const float InclinacaoDaAlca = 35f;  // a boca do cano por cima do ombro direito
        const float InclinacaoNaLateral = 6f;     // na lateral da mochila, a boca do cano um pouco para fora
        const float TopoDaArmaNaLateral = 1.78f;  // até onde a boca do cano sobe (a cabeça vai até 1,81)

        readonly List<GameObject> _pecas = new List<GameObject>();

        /// <summary>O material das peças (vazio: o dos itens no chão). O retrato do inventário usa o dele, sem sombra.</summary>
        public Material material;
        public bool semSombra;

        /// <summary>
        /// Veste o corpo. <paramref name="alca"/>, <paramref name="costas"/> e <paramref name="cintura"/> são as armas
        /// (ou <see cref="Item.Nada"/>); a da cintura só aparece com o coldre.
        /// </summary>
        public void Vestir(Transform referencia, Item mochila, bool coldre, Item alca, Item costas, Item cintura)
        {
            Limpar();
            var mundo = ItensNoMundo.Instancia;
            if (mundo == null || referencia == null) return;
            var peito = Osso(transform, "chest");
            var quadril = Osso(transform, "hips");
            if (peito == null || quadril == null) return;

            float fora = Costas;   // até onde já foi ocupado, para trás
            bool comMochila = false;
            Bounds naMochila = default;   // a caixa da mochila, no espaço do corpo

            // a mochila
            if (Itens.EhMochila(mochila))
            {
                var malha = Malha(mundo, mochila);
                if (malha != null)
                {
                    var b = malha.bounds;
                    var pos = new Vector3(-b.center.x, TopoDaMochila - b.max.y, Costas - 0.005f);
                    Por(referencia, peito, "Mochila", malha, mundo.material, pos, Quaternion.identity);
                    fora = pos.z + b.min.z;
                    comMochila = true;
                    naMochila = new Bounds(pos + b.center, b.size);
                }
            }

            // com a mochila, as armas vão presas nas laterais dela: a da alça na direita, a das costas na esquerda
            // (a pistola da alça continua no cós)
            if (comMochila)
            {
                if (costas != Item.Nada) NaLateral(referencia, peito, mundo, costas, naMochila, -1f, "Arma nas costas");
                if (alca != Item.Nada && alca != Item.Pistola) NaLateral(referencia, peito, mundo, alca, naMochila, 1f, "Arma da alça");
            }
            else if (alca != Item.Nada && alca != Item.Pistola)
            {
                // sem a mochila: a arma da alça na diagonal das costas
                var malha = Malha(mundo, alca);
                if (malha != null)
                {
                    var rot = DeitadaNasCostas(InclinacaoDaAlca);
                    float meia = malha.bounds.extents.x;
                    var centro = new Vector3(0f, CentroDasCostas, fora - meia - 0.01f);
                    Por(referencia, peito, "Arma da alça", malha, mundo.material, centro - rot * malha.bounds.center, rot);
                }
            }

            // a pistola da alça: no cós da calça, atrás (o cano para baixo, o cabo para cima e para a direita)
            if (alca == Item.Pistola)
            {
                var malha = Malha(mundo, alca);
                if (malha != null)
                {
                    var rot = Quaternion.LookRotation(Vector3.down, Vector3.left);
                    var centro = new Vector3(0.06f, Cinto - 0.02f, Costas + 0.005f - malha.bounds.extents.x);
                    Por(referencia, quadril, "Arma da alça (cós)", malha, mundo.material, centro - rot * malha.bounds.center, rot);
                }
            }

            // o coldre, no quadril direito, com a arma curta dentro
            if (coldre)
            {
                var malha = Malha(mundo, Item.Coldre);
                Vector3 pos = new Vector3(LadoDoQuadril + 0.03f, Cinto, 0.0f);
                if (malha != null) Por(referencia, quadril, "Coldre", malha, mundo.material, pos, Quaternion.identity);
                if (Itens.EhCurta(cintura))
                {
                    var arma = Malha(mundo, cintura);
                    if (arma != null)
                    {
                        // o cano para baixo e o topo da arma para a frente; o cabo sai por cima, para trás
                        var rot = Quaternion.Euler(90f, 0f, 0f);
                        Por(referencia, quadril, "Arma no coldre", arma, mundo.material, pos + new Vector3(0.002f, 0.025f, -0.02f), rot);
                    }
                }
            }
        }

        /// <summary>
        /// Uma arma presa na lateral da mochila (<paramref name="lado"/> 1 a direita, -1 a esquerda): em pé, o cano para
        /// cima e um pouco para fora, o lado dela encostado na mochila e o topo para a frente. No meio da mochila, mas a
        /// boca do cano não passa muito da cabeça (as longas descem).
        /// </summary>
        void NaLateral(Transform referencia, Transform peito, ItensNoMundo mundo, Item arma, Bounds mochila, float lado, string nome)
        {
            var malha = Malha(mundo, arma);
            if (malha == null) return;
            float t = InclinacaoNaLateral * Mathf.Deg2Rad;
            var rot = Quaternion.LookRotation(new Vector3(lado * Mathf.Sin(t), Mathf.Cos(t), 0f), Vector3.forward);
            var b = malha.bounds;
            float meiaAltura = b.extents.z * Mathf.Cos(t);
            float y = Mathf.Min(mochila.center.y, TopoDaArmaNaLateral - meiaAltura);
            var centro = new Vector3(lado * (mochila.extents.x + b.extents.x + 0.005f), y, mochila.center.z);
            Por(referencia, peito, nome, malha, mundo.material, centro - rot * b.center, rot);
        }

        /// <summary>A arma deitada nas costas: o lado dela encostado, a boca do cano para cima, inclinada (graus, + para a direita).</summary>
        static Quaternion DeitadaNasCostas(float inclinacao)
        {
            float t = inclinacao * Mathf.Deg2Rad;
            var cano = new Vector3(Mathf.Sin(t), Mathf.Cos(t), 0f);
            var topo = new Vector3(-Mathf.Cos(t), Mathf.Sin(t), 0f);
            return Quaternion.LookRotation(cano, topo);
        }

        public void Limpar()
        {
            foreach (var p in _pecas) if (p != null) Destroy(p);
            _pecas.Clear();
        }

        static Mesh Malha(ItensNoMundo mundo, Item item)
        {
            if (Itens.EhEquipamento(item))
            {
                int k = Itens.Equipamento(item);
                return mundo.malhasDeEquipamento != null && k >= 0 && k < mundo.malhasDeEquipamento.Length ? mundo.malhasDeEquipamento[k] : null;
            }
            int m = Itens.Modelo(item);
            return mundo.malhas != null && m >= 0 && m < mundo.malhas.Length ? mundo.malhas[m] : null;
        }

        /// <summary>
        /// Põe uma peça presa ao <paramref name="osso"/>, na pose dada no espaço do corpo em repouso (a da referência).
        /// </summary>
        void Por(Transform referencia, Transform osso, string nome, Mesh malha, Material material, Vector3 pos, Quaternion rot)
        {
            var ossoRef = Osso(referencia, osso.name);
            var go = new GameObject(nome);
            go.layer = gameObject.layer;
            go.transform.SetParent(osso, false);
            // a pose do osso em repouso, no espaço do corpo; a peça, no espaço do osso (com a escala dele, se tiver)
            var ossoNoCorpo = referencia.worldToLocalMatrix * ossoRef.localToWorldMatrix;
            var local = ossoNoCorpo.inverse * Matrix4x4.TRS(pos, rot, Vector3.one);
            go.transform.localPosition = local.GetColumn(3);
            go.transform.localRotation = Quaternion.LookRotation(local.GetColumn(2), local.GetColumn(1));
            go.transform.localScale = new Vector3(((Vector3)local.GetColumn(0)).magnitude, ((Vector3)local.GetColumn(1)).magnitude, ((Vector3)local.GetColumn(2)).magnitude);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = this.material != null ? this.material : material;
            if (semSombra)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            _pecas.Add(go);
        }

        static Transform Osso(Transform raiz, string nome)
        {
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nome) return t;
            return null;
        }
    }
}
