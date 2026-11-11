using System.Collections;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O lampião e o molotov na primeira pessoa (11/11/2026, a etapa 4 do ramo Fogo). Os braços deles não estão no prefab do
    /// jogador: na primeira troca, vêm do Resources/ItensDoFogo (os FBX Jogador_FP_Lampiao e _Molotov) e entram depois dos
    /// outros em <see cref="modelos"/>, <see cref="animadores"/>, <see cref="bocas"/> e nos <see cref="JogadorRede.pintaveis"/>
    /// (a pele do personagem), nos lugares <see cref="Itens.LampiaoFP"/> e <see cref="Itens.MolotovFP"/>.
    ///  - o lampião: a chama pequena na boca (a do vidro), só com combustível; o clique só o balança (o soco direito dele);
    ///  - o molotov: o pano aceso na boca (no osso da garrafa); o clique arremessa: no quadro 8 do soco direito a garrafa sai
    ///    da mão e o anfitrião a joga (InventarioJogador.ArremessarMolotov).
    /// </summary>
    public partial class ArmaJogador
    {
        bool _bracosDoFogoProntos;

        /// <summary>Põe os braços do lampião e do molotov ao lado dos outros (uma vez; sem o prefab de Resources, nada).</summary>
        void BracosDoFogo()
        {
            if (_bracosDoFogoProntos || modelos == null || animadores == null || bocas == null || rede == null) return;
            _bracosDoFogoProntos = true;
            var d = ItensDoFogo.Dados;
            int n = Itens.QuantosModelosFP, t = Itens.ModeloNaMao(Item.Tocha);
            if (d == null || modelos.Length != n || animadores.Length != n || bocas.Length != n || t < 0 || t >= n || modelos[t] == null)
            {
                Debug.LogWarning("[Fogo] Sem os braços do lampião e do molotov (monte em Zombie City Cleaner > Habilidades > Montar o fogo).");
                return;
            }
            System.Array.Resize(ref modelos, n + 2);
            System.Array.Resize(ref animadores, n + 2);
            System.Array.Resize(ref bocas, n + 2);
            var pintaveis = rede.pintaveis;
            if (pintaveis != null && pintaveis.Length == n) System.Array.Resize(ref pintaveis, n + 2);
            rede.pintaveis = pintaveis;
            Braco(Itens.LampiaoFP, d.bracosDoLampiao, d.controladorDoLampiao, "Lampiao", t);
            Braco(Itens.MolotovFP, d.bracosDoMolotov, d.controladorDoMolotov, "Molotov", t);
        }

        void Braco(int i, GameObject fbx, RuntimeAnimatorController controlador, string nome, int molde)
        {
            if (fbx == null) return;
            var base_ = modelos[molde];
            var fp = Instantiate(fbx, base_.transform.parent, false);
            fp.name = "Bracos_" + nome;
            fp.transform.localPosition = base_.transform.localPosition;
            fp.transform.localRotation = base_.transform.localRotation;
            fp.transform.localScale = base_.transform.localScale;
            foreach (var tr in fp.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = JogadorRede.CamadaBracos;
            foreach (var r in fp.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var anim = fp.GetComponent<Animator>();
            if (anim != null)
            {
                anim.runtimeAnimatorController = controlador;
                anim.applyRootMotion = false;
                if (animadores[molde] != null)
                {
                    anim.cullingMode = animadores[molde].cullingMode;
                    anim.updateMode = animadores[molde].updateMode;
                }
            }
            Transform boca = null, bracos = null;
            foreach (var tr in fp.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name == "Boca_FP_" + nome) boca = tr;
                else if (tr.name == "FP_Bracos_" + nome) bracos = tr;
            }
            // a boca: no osso e no ponto que a montagem achou (o vazio Boca_FP do FBX é animado e não serve, como na tocha)
            var d = ItensDoFogo.Dados;
            string osso = nome == "Lampiao" ? d.ossoDaChama : d.ossoDoPavio;
            if (!string.IsNullOrEmpty(osso))
                foreach (var tr in fp.GetComponentsInChildren<Transform>(true))
                    if (tr.name == osso)
                    {
                        var ponta = new GameObject("Ponta_Boca_FP_" + nome) { layer = JogadorRede.CamadaBracos }.transform;
                        ponta.SetParent(tr, false);
                        ponta.localPosition = nome == "Lampiao" ? d.chamaNosBracos : d.pavioNosBracos;
                        boca = ponta;
                        break;
                    }
            modelos[i] = fp;
            animadores[i] = anim;
            bocas[i] = boca != null ? boca : fp.transform;
            // a pele: o material e a cor do personagem, os dos braços da tocha (o JogadorRede pinta todos os pintáveis depois)
            var smr = bracos != null ? bracos.GetComponent<SkinnedMeshRenderer>() : null;
            var pele = rede.pintaveis != null && molde < rede.pintaveis.Length ? rede.pintaveis[molde] : null;
            if (smr != null && pele != null)
            {
                var mats = smr.sharedMaterials;
                if (mats.Length > 0 && pele.sharedMaterials.Length > 0) mats[0] = pele.sharedMaterials[0];
                smr.sharedMaterials = mats;
                var bloco = new MaterialPropertyBlock();
                pele.GetPropertyBlock(bloco, 0);
                smr.SetPropertyBlock(bloco, 0);
                if (pele is SkinnedMeshRenderer s) smr.updateWhenOffscreen = s.updateWhenOffscreen;
            }
            if (rede.pintaveis != null && i < rede.pintaveis.Length) rede.pintaveis[i] = smr;
            fp.SetActive(false);
        }

        /// <summary>A chama na boca dos braços do lampião ou do molotov (criada na primeira vez; a luz só ilumina os braços).</summary>
        void ChamaDoFogoNosBracos(Item item, int m)
        {
            if (!Itens.EhFogoDeMao(item) || m < 0 || m >= bocas.Length || bocas[m] == null) return;
            if (bocas[m].Find("Fogo") != null || ConstrucoesNoMundo.Instancia == null) return;
            bool lampiao = item == Item.Lampiao;
            var fogo = ConstrucoesNoMundo.Fogo(bocas[m], Vector3.zero, ConstrucoesNoMundo.Instancia.materialDoFogo, lampiao ? 0.07f : 0.11f,
                                               lampiao ? 2.5f : 1.5f, false, JogadorRede.CamadaBracos);
            if (fogo.TryGetComponent<Light>(out var luz)) luz.intensity = lampiao ? 1.6f : 1f;
            // (a garrafa some da mão no quadro 8, o osso dela a 0,001: o pano aceso some junto, pelo FogoNaMaoAgora)
        }

        // no Update: a chama do lampião (com combustível) e a do pano do molotov (até ele sair da mão)
        bool _molotovNaMao = true;

        void FogoNaMaoAgora()
        {
            if (!Itens.EhFogoDeMao(NaMao) || bocas == null) return;
            int m = ModeloDaMao(NaMao);
            if (m < 0 || m >= bocas.Length || bocas[m] == null) return;
            var fogo = bocas[m].Find("Fogo");
            if (fogo == null) return;
            bool aceso = NaMao == Item.Lampiao ? Inventario != null && Inventario.LampiaoAcesoNaMao : _molotovNaMao;
            if (fogo.gameObject.activeSelf != aceso) fogo.gameObject.SetActive(aceso);
        }

        /// <summary>O clique com o lampião ou o molotov na mão (no Update, no lugar do golpe da arma branca).</summary>
        void CliqueDoFogo(bool clicou)
        {
            if (!clicou || Time.time < _proximoTiro) return;
            if (NaMao == Item.Lampiao)
            {
                // o lampião não ataca: só balança (o soco direito dele)
                _proximoTiro = Time.time + 0.7f;
                Acao(FireId);
                return;
            }
            if (NaMao == Item.Molotov) StartCoroutine(Arremessar());
        }

        IEnumerator Arremessar()
        {
            ushort id = _id;
            _proximoTiro = Time.time + FogoDoJogador.IntervaloDoArremesso;
            Acao(FireId);
            rede.AnunciarSoco();   // os outros veem o golpe da tocha, com a garrafa na mão
            Efeitos.Som(Efeitos.SocoVento, rede.cameraMundo.transform.position, 0.6f, false);
            float velocidade = Animador != null ? Mathf.Max(0.1f, Animador.speed) : 1f;
            yield return new WaitForSeconds(FogoDoJogador.SoltaAos / velocidade);
            if (NaMao != Item.Molotov || _id != id) yield break;   // trocou de item no meio
            _molotovNaMao = false;
            var olho = rede.cameraMundo.transform;
            // a garrafa sai da mão direita, um pouco à frente do olho (os braços ficam longe, perto da origem: BracosNaOrigem)
            Vector3 origem = olho.position + olho.forward * 0.4f + olho.right * 0.2f - olho.up * 0.05f;
            if (Physics.Linecast(olho.position, origem, out var parede, ~((1 << 2) | (1 << JogadorRede.CamadaBracos) | (1 << JogadorRede.CamadaJogador)), QueryTriggerInteraction.Ignore))
                origem = parede.point - olho.forward * 0.05f;
            Vector3 direcao = (olho.forward + Vector3.up * FogoDoJogador.SubidaDoArremesso).normalized;
            Inventario.ArremessarMolotov(id, origem, direcao);
            ArremessosNoTeste++;
        }

        /// <summary>Quantos molotovs este jogador arremessou (o teste pelo MCP).</summary>
        public static int ArremessosNoTeste;
    }
}
