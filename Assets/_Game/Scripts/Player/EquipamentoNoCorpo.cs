using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Põe no corpo do jogador o que ele leva: a mochila nas costas e as armas dos atalhos 1, 2 e 3 que não estão na mão
    /// (31/10/2026, a pedido: "as armas que vão ficar visíveis nas costas do personagem, vai ser somente as que estão na hot
    /// bar do jogador, 1 2 e 3. Na mesmas posições de hoje"). A pistola e a faca vão no cós da calça, atrás (a primeira à
    /// direita, a segunda à esquerda); as outras, nas costas: sem mochila, deitadas na diagonal (a primeira com a boca do
    /// cano por cima do ombro direito, a segunda cruzando por cima do esquerdo, a terceira em pé no meio); com ela, presas
    /// nas laterais (a primeira na direita, a segunda na esquerda) e a terceira na diagonal por cima. Só visual.
    /// As posições são medidas no corpo em repouso (o corpo do personagem no prefab, sem animação, a <c>referencia</c>) e
    /// presas aos ossos (o peito e o quadril), então acompanham a animação em qualquer pose. As medidas do corpo (as costas,
    /// o cinto, o quadril...) são as do personagem (<see cref="CorpoDoPersonagem"/>): o gordo tem as costas mais para trás.
    /// Serve o retrato do inventário, o boneco de teste do menu do desenvolvedor e, desde 28/10/2026, o corpo dos jogadores
    /// (<see cref="EquipamentoDoJogador"/>). O que vai no corpo sai do inventário por <see cref="OQueVeste"/>.
    /// </summary>
    public class EquipamentoNoCorpo : MonoBehaviour
    {
        // medidas do corpo em repouso (o espaço do corpo, olhando para +Z), as do personagem (CorpoDoPersonagem)
        float Costas = -0.115f;        // a superfície das costas, na altura do peito
        float TopoDaMochila = 1.45f;   // logo abaixo da altura dos ombros
        float CentroDasCostas = 1.15f; // onde a arma da alça cruza as costas
        float Cinto = 0.97f;
        const float InclinacaoDaAlca = 35f;  // a diagonal das costas: a boca do cano por cima do ombro direito
        const float InclinacaoNaLateral = 6f;     // na lateral da mochila, a boca do cano um pouco para fora
        float TopoDaArmaNaLateral = 1.78f;  // até onde a boca do cano sobe (3 cm abaixo do topo da cabeça)
        // a mochila nas costas é menor que a do chão (a pedido, 28/10/2026: "muito grandes em relação ao personagem"; a
        // grande, de 48 × 84 × 33 cm, era mais larga que o tronco do principal em blocos)
        const float TamanhoDaMochila = 0.75f;

        readonly List<GameObject> _pecas = new List<GameObject>();

        /// <summary>O começo do nome de cada peça: a cópia do corpo (o retrato do inventário) acha e tira as que vieram junto.</summary>
        public const string Prefixo = "Equipamento: ";

        /// <summary>As peças postas agora.</summary>
        public IReadOnlyList<GameObject> Pecas => _pecas;

        /// <summary>
        /// O que vai no corpo, pelo inventário (o que vem pela rede, em qualquer máquina): a mochila vestida e, em
        /// <paramref name="armas"/> (uma por atalho, na ordem deles), as armas dos atalhos que não estão na mão (Nada nos
        /// outros). Devolve uma chave que muda quando alguma delas muda.
        /// </summary>
        public static long OQueVeste(InventarioJogador inventario, out Item mochila, Item[] armas)
        {
            mochila = inventario.MochilaVestida.Value;
            long chave = (long)mochila;
            ushort idNaMao = inventario.IdNaMao;
            for (int k = 0; k < armas.Length; k++)
            {
                ushort id = k < InventarioJogador.Atalhos ? inventario.Atalho(k) : (ushort)0;
                var item = inventario.Do(id);
                armas[k] = id != idNaMao && VaiNoCorpo(item) ? item : Item.Nada;
                chave |= (long)armas[k] << (8 * (k + 1));
            }
            return chave;
        }

        /// <summary>O que fica pendurado no corpo, num atalho e fora da mão: as armas e as armas brancas.</summary>
        public static bool VaiNoCorpo(Item i) => Itens.EhArma(i) || Itens.EhBranca(i);

        /// <summary>Tira as peças que vieram numa cópia do corpo (Instantiate leva os filhos dos ossos, não a lista).</summary>
        public static void TirarCopiadas(Transform raiz)
        {
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith(Prefixo)) continue;
                t.gameObject.SetActive(false);
                Destroy(t.gameObject);
            }
        }

        /// <summary>O material das peças (vazio: o dos itens no chão). O retrato do inventário usa o dele, sem sombra.</summary>
        public Material material;
        /// <summary>
        /// Com o <see cref="material"/> do retrato: a cópia dele com a textura de um material próprio (a pistola nova, que
        /// não usa a paleta). Vazio: o material próprio vira o do retrato (a paleta).
        /// </summary>
        public System.Func<Material, Material> copiaDoProprio;
        public bool semSombra;

        // a arma com a textura própria dela (a pistola nova) ou a paleta (o material dos itens no chão)
        static Material DaArma(ItensNoMundo mundo, Item arma)
        {
            var proprio = Itens.MaterialProprio(arma);
            return proprio != null ? proprio : mundo.material;
        }

        /// <summary>
        /// Veste o corpo: a mochila (ou <see cref="Item.Nada"/>) e as armas, na ordem dos atalhos (<see cref="Item.Nada"/>
        /// nos vazios).
        /// </summary>
        public void Vestir(Transform referencia, Item mochila, IList<Item> armas)
        {
            Limpar();
            var mundo = ItensNoMundo.Instancia;
            if (mundo == null || referencia == null) return;
            var peito = Osso(transform, "chest");
            var quadril = Osso(transform, "hips");
            if (peito == null || quadril == null) return;
            var medidas = GetComponent<CorpoDoPersonagem>();
            if (medidas == null) medidas = referencia.GetComponent<CorpoDoPersonagem>();
            if (medidas != null)
            {
                Costas = medidas.costas;
                TopoDaMochila = medidas.topoDaMochila;
                CentroDasCostas = medidas.centroDasCostas;
                Cinto = medidas.cinto;
                TopoDaArmaNaLateral = medidas.topoDaCabeca - 0.03f;
            }

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
                    float s = TamanhoDaMochila;
                    var pos = new Vector3(-b.center.x * s, TopoDaMochila - b.max.y * s, Costas - 0.005f);
                    Por(referencia, peito, "Mochila", malha, mundo.material, pos, Quaternion.identity, s);
                    fora = pos.z + b.min.z * s;
                    comMochila = true;
                    naMochila = new Bounds(pos + b.center * s, b.size * s);
                }
            }

            // as armas, na ordem dos atalhos: a pistola e a faca no cós (duas, no máximo: as outras vão nas costas); as outras
            // nas laterais da mochila ou deitadas na diagonal das costas
            int noCos = 0, nasCostas = 0;
            for (int k = 0; armas != null && k < armas.Count; k++)
            {
                var arma = armas[k];
                if (arma == Item.Nada) continue;
                string nome = $"Arma do atalho {k + 1}";
                if (Itens.VaiNoCos(arma) && noCos < 2)
                {
                    NoCos(referencia, quadril, mundo, arma, noCos++ == 0 ? 1f : -1f, nome);
                    continue;
                }
                int lugar = nasCostas++;
                if (comMochila && lugar < 2) NaLateral(referencia, peito, mundo, arma, naMochila, lugar == 0 ? 1f : -1f, nome);
                else
                {
                    float inclinacao = comMochila || lugar == 0 ? InclinacaoDaAlca : lugar == 1 ? -InclinacaoDaAlca : 0f;
                    fora = NaDiagonal(referencia, peito, mundo, arma, inclinacao, fora, nome);
                }
            }
        }

        /// <summary>
        /// Uma arma deitada na diagonal das costas, encostada no que já está nelas (<paramref name="fora"/>: até onde, para
        /// trás), na altura do meio das costas; a inclinação em graus (+ a boca do cano para a direita). Devolve até onde
        /// ela ocupa.
        /// </summary>
        float NaDiagonal(Transform referencia, Transform peito, ItensNoMundo mundo, Item arma, float inclinacao, float fora, string nome)
        {
            var malha = Malha(mundo, arma);
            if (malha == null) return fora;
            var rot = DeitadaNasCostas(inclinacao);
            float meia = malha.bounds.extents.x;
            var centro = new Vector3(0f, CentroDasCostas, fora - meia - 0.01f);
            Por(referencia, peito, nome, malha, DaArma(mundo, arma), centro - rot * malha.bounds.center, rot);
            return centro.z - meia;
        }

        /// <summary>
        /// A pistola (ou a faca) no cós da calça, atrás: a boca do cano (a ponta) para baixo, o cabo para cima e para o lado
        /// (<paramref name="lado"/> 1 à direita, -1 à esquerda, espelhada). A faca, com o cabo acima do cinto.
        /// </summary>
        void NoCos(Transform referencia, Transform quadril, ItensNoMundo mundo, Item arma, float lado, string nome)
        {
            var malha = Malha(mundo, arma);
            if (malha == null) return;
            var rot = Quaternion.LookRotation(Vector3.down, lado > 0f ? Vector3.left : Vector3.right);
            var b = malha.bounds;
            float y = arma == Item.Pistola ? Cinto - 0.02f : Cinto + 0.08f - b.extents.z;
            var centro = new Vector3(lado * 0.06f, y, Costas + 0.005f - b.extents.x);
            Por(referencia, quadril, nome, malha, DaArma(mundo, arma), centro - rot * b.center, rot);
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
            Por(referencia, peito, nome, malha, DaArma(mundo, arma), centro - rot * b.center, rot);
        }

        /// <summary>
        /// A arma deitada nas costas: o lado dela encostado, a boca do cano para cima, inclinada (graus, + para a direita).
        /// Inclinada para a esquerda, o topo dela vai para o outro lado (espelhada).
        /// </summary>
        static Quaternion DeitadaNasCostas(float inclinacao)
        {
            float t = inclinacao * Mathf.Deg2Rad;
            var cano = new Vector3(Mathf.Sin(t), Mathf.Cos(t), 0f);
            var topo = inclinacao >= 0f ? new Vector3(-Mathf.Cos(t), Mathf.Sin(t), 0f) : new Vector3(Mathf.Cos(t), -Mathf.Sin(t), 0f);
            return Quaternion.LookRotation(cano, topo);
        }

        public void Limpar()
        {
            foreach (var p in _pecas)
            {
                if (p == null) continue;
                p.SetActive(false);   // (o Destroy só vale no fim do quadro)
                Destroy(p);
            }
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
        /// Põe uma peça presa ao <paramref name="osso"/>, na pose dada no espaço do corpo em repouso (a da referência),
        /// no tamanho dado (<paramref name="escala"/>: 1, o do item no chão).
        /// </summary>
        void Por(Transform referencia, Transform osso, string nome, Mesh malha, Material material, Vector3 pos, Quaternion rot, float escala = 1f)
        {
            var ossoRef = Osso(referencia, osso.name);
            var go = new GameObject(Prefixo + nome);
            go.layer = gameObject.layer;
            go.transform.SetParent(osso, false);
            // a pose do osso em repouso, no espaço do corpo; a peça, no espaço do osso (com a escala dele, se tiver)
            var ossoNoCorpo = referencia.worldToLocalMatrix * ossoRef.localToWorldMatrix;
            var local = ossoNoCorpo.inverse * Matrix4x4.TRS(pos, rot, Vector3.one * escala);
            go.transform.localPosition = local.GetColumn(3);
            go.transform.localRotation = Quaternion.LookRotation(local.GetColumn(2), local.GetColumn(1));
            go.transform.localScale = new Vector3(((Vector3)local.GetColumn(0)).magnitude, ((Vector3)local.GetColumn(1)).magnitude, ((Vector3)local.GetColumn(2)).magnitude);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            // no retrato, o material dele: a paleta, ou a cópia com a textura do material próprio
            bool proprio = ItensNoMundo.Instancia != null && material != ItensNoMundo.Instancia.material;
            r.sharedMaterial = this.material == null ? material : proprio && copiaDoProprio != null ? copiaDoProprio(material) : this.material;
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
