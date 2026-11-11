using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Os modelos das melhorias das armas de sucata (10/11/2026, a etapa 2), no <c>Resources/MelhoriasDaSucata.prefab</c>
    /// (montado pelo menu Zombie City Cleaner > Armas > Montar as melhorias de sucata: o <c>MontagemDasMelhorias</c>). As
    /// peças estão no espaço da arma (a malha solta: a origem no cabo, o cano em +Z) e vão como filhas de quem segura a
    /// arma: o osso <c>gun</c> dos braços de primeira pessoa (e o carregador estendido no <c>mag</c>), a peça do corpo da
    /// arma no corpo (<c>P_Sucata_*_corpo</c> e o <c>_carregador</c>, para os outros) e o item no chão; as matrizes levam
    /// do espaço da arma para o de cada um. Nada disso muda as cenas nem o prefab do jogador: tudo é posto no Play.
    /// Também guarda as malhas, os ícones e as fotos dos 9 itens (as cenas e o prefab ainda não têm), e a foto de cada
    /// arma com as peças por cima (o detalhe da mochila).
    /// </summary>
    public class PecasDasMelhorias : MonoBehaviour
    {
        [Tooltip("A paleta dos itens (o Materials/Pistola.mat) e o do pontinho vermelho da mira de ponto (sem luz).")]
        public Material material, materialDoPonto;
        [Tooltip("As peças de cada arma (3 × 9: a arma na ordem do Melhorias.DasArmas, a peça na do PecaDaMelhoria); vazia: não vai nela.")]
        public Mesh[] pecas;
        [Tooltip("Do espaço da arma para o osso gun e o osso mag dos braços de primeira pessoa (bindpose × encaixe), uma por arma.")]
        public Matrix4x4[] fpCorpo, fpCarregador;
        [Tooltip("Do espaço da arma para a P_Sucata_*_corpo e a P_Sucata_*_carregador do corpo, uma por arma.")]
        public Matrix4x4[] tpCorpo, tpCarregador;
        [Tooltip("Os braços de primeira pessoa com e sem o carregador curto (o estendido vai no lugar dele), uma por arma.")]
        public Mesh[] bracosComCarregador, bracosSemCarregador;
        [Tooltip("A arma solta (o chão) sem o carregador curto, uma por arma.")]
        public Mesh[] soltaSemCarregador;
        [Tooltip("Os 9 itens (na ordem do PecaDaMelhoria): a malha (o chão), o ícone e a foto.")]
        public Mesh[] malhasDosItens;
        public Texture2D[] icones, fotos;
        [Tooltip("A foto de cada arma com espaço para as peças, e as peças por cima, no mesmo enquadramento (3 × 9).")]
        public Texture2D[] fotoDaArma, fotosDasPecas;

        const string Recurso = "MelhoriasDaSucata";
        static PecasDasMelhorias _dados;
        static int _procurouNoQuadro = -1;
        static Mesh _ponto;

        /// <summary>O que o prefab guarda (null, se ainda não foi montado).</summary>
        public static PecasDasMelhorias Dados
        {
            get
            {
                if (_dados == null && _procurouNoQuadro != Time.frameCount)
                {
                    _procurouNoQuadro = Time.frameCount;
                    var go = Resources.Load<GameObject>(Recurso);
                    _dados = go != null ? go.GetComponent<PecasDasMelhorias>() : null;
                }
                return _dados;
            }
        }

        static T Em<T>(T[] lista, int i) where T : class => lista != null && i >= 0 && i < lista.Length ? lista[i] : null;

        public static Mesh Peca(int arma, PecaDaMelhoria p) => Dados != null ? Em(Dados.pecas, arma * Melhorias.Quantas + (int)p) : null;
        public static Mesh MalhaDoItem(Item i) => Melhorias.EhPeca(i) && Dados != null ? Em(Dados.malhasDosItens, (int)Melhorias.PecaDo(i)) : null;
        public static Texture2D IconeDo(Item i) => Melhorias.EhPeca(i) && Dados != null ? Em(Dados.icones, (int)Melhorias.PecaDo(i)) : null;
        public static Texture2D FotoDo(Item i) => Melhorias.EhPeca(i) && Dados != null ? Em(Dados.fotos, (int)Melhorias.PecaDo(i)) : null;

        // ------------------------------------------------------------------ as peças num lugar

        /// <summary>
        /// As peças postas como filhas de 'pai' (num objeto "Melhorias", ou "Melhorias_Carregador" com o carregador
        /// estendido), na pose 'local' (do espaço da arma para o de 'pai'). Só refaz quando o que está posto muda. Os
        /// renderers novos copiam os de 'modelo' (o ligado, a máscara e a sombra), se vier.
        /// </summary>
        public static void Aplicar(Transform pai, int arma, ulong b, Matrix4x4 local, bool doCarregador, int camada, Material mat, Renderer modelo = null)
        {
            if (pai == null) return;
            string nome = doCarregador ? "Melhorias_Carregador" : "Melhorias";
            var caixa = pai.Find(nome);
            var d = Dados;
            int pecas = d == null || arma < 0 ? 0 : doCarregador ? (Melhorias.CarregadorLongo(b) ? 1 : 0) : Melhorias.Pecas(b) & ~(1 << 5);
            if (caixa == null)
            {
                if (pecas == 0) return;
                caixa = new GameObject(nome).transform;
                caixa.SetParent(pai, false);
                caixa.gameObject.layer = camada;
                caixa.gameObject.AddComponent<MelhoriasPostas>();
            }
            var marca = caixa.GetComponent<MelhoriasPostas>();
            if (marca.arma == arma && marca.pecas == pecas) return;
            marca.arma = arma;
            marca.pecas = pecas;
            for (int i = caixa.childCount - 1; i >= 0; i--)
            {
                var filho = caixa.GetChild(i).gameObject;
                filho.SetActive(false);   // (o Destroy só vale no fim do quadro)
                Destroy(filho);
            }
            caixa.gameObject.SetActive(pecas != 0);
            if (pecas == 0) return;
            caixa.localPosition = local.GetColumn(3);
            caixa.localRotation = Quaternion.LookRotation(local.GetColumn(2), local.GetColumn(1));
            caixa.localScale = new Vector3(((Vector3)local.GetColumn(0)).magnitude, ((Vector3)local.GetColumn(1)).magnitude, ((Vector3)local.GetColumn(2)).magnitude);
            var tipo = Melhorias.DasArmas[arma];
            for (int i = 0; i < Melhorias.Quantas; i++)
            {
                var p = (PecaDaMelhoria)i;
                if ((p == PecaDaMelhoria.CarregadorLongo) != doCarregador || !Melhorias.Posta(b, p)) continue;
                var malha = Em(d.pecas, arma * Melhorias.Quantas + i);
                if (malha == null) continue;
                Filho(caixa, p.ToString(), malha, mat != null ? mat : d.material, camada, modelo, Vector3.zero, Quaternion.identity, Vector3.one);
            }
            // o pontinho vermelho da mira de ponto (sem luz: aparece também no escuro), virado para o olho
            if (!doCarregador && Melhorias.MiraDeCima(b) == Melhorias.Ponto && d.materialDoPonto != null)
                Filho(caixa, "PontoVermelho", Ponto, d.materialDoPonto, camada, modelo, Melhorias.PontoVermelhoDa(tipo),
                      Quaternion.LookRotation(Vector3.back, Vector3.up), Vector3.one, false);
        }

        static void Filho(Transform caixa, string nome, Mesh malha, Material mat, int camada, Renderer modelo, Vector3 pos, Quaternion giro, Vector3 escala, bool sombra = true)
        {
            var go = new GameObject(nome);
            go.layer = camada;
            go.transform.SetParent(caixa, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = giro;
            go.transform.localScale = escala;
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (modelo != null)
            {
                r.enabled = modelo.enabled;
                r.renderingLayerMask = modelo.renderingLayerMask;
                r.shadowCastingMode = sombra ? modelo.shadowCastingMode : ShadowCastingMode.Off;
                r.receiveShadows = sombra && modelo.receiveShadows;
            }
            else if (!sombra) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // o pontinho: um octógono de 1,2 mm virado para +Z (o filho gira para o olho)
        static Mesh Ponto
        {
            get
            {
                if (_ponto != null) return _ponto;
                const int lados = 8;
                const float raio = 0.0006f;
                var v = new Vector3[lados + 1];
                var t = new int[lados * 3];
                for (int i = 0; i < lados; i++)
                {
                    float a = i * Mathf.PI * 2f / lados;
                    v[i + 1] = new Vector3(Mathf.Cos(a) * raio, Mathf.Sin(a) * raio, 0f);
                    t[i * 3] = 0;
                    t[i * 3 + 1] = 1 + (i + 1) % lados;
                    t[i * 3 + 2] = 1 + i;
                }
                _ponto = new Mesh { name = "PontoVermelho", vertices = v, triangles = t };
                _ponto.RecalculateNormals();
                _ponto.RecalculateBounds();
                return _ponto;
            }
        }

        static Transform Osso(SkinnedMeshRenderer smr, string nome)
        {
            if (smr == null || smr.bones == null) return null;
            foreach (var o in smr.bones)
                if (o != null && o.name == nome) return o;
            return null;
        }

        /// <summary>O osso da arma (gun) nos braços de primeira pessoa.</summary>
        public static Transform OssoDaArma(SkinnedMeshRenderer smr) => Osso(smr, "gun");

        // ------------------------------------------------------------------ onde a arma aparece

        /// <summary>
        /// Os braços de primeira pessoa (no dono): as peças no osso gun, o carregador estendido no mag (e os braços sem o
        /// carregador curto) e a boca do cano (o clarão) no lugar novo, com o silenciador.
        /// </summary>
        public static void NosBracos(int arma, SkinnedMeshRenderer smr, ulong b, Transform boca)
        {
            var d = Dados;
            if (d == null || smr == null || arma < 0 || d.fpCorpo == null || arma >= d.fpCorpo.Length || d.fpCarregador == null || arma >= d.fpCarregador.Length) return;
            var mats = smr.sharedMaterials;
            var mat = mats.Length > 1 ? mats[1] : d.material;
            Aplicar(OssoDaArma(smr), arma, b, d.fpCorpo[arma], false, smr.gameObject.layer, mat);
            Aplicar(Osso(smr, "mag"), arma, b, d.fpCarregador[arma], true, smr.gameObject.layer, mat);
            var com = Em(d.bracosComCarregador, arma);
            var sem = Em(d.bracosSemCarregador, arma);
            var querida = Melhorias.CarregadorLongo(b) && sem != null ? sem : com;
            if (querida != null && smr.sharedMesh != querida) smr.sharedMesh = querida;
            if (boca != null) boca.localPosition = d.fpCorpo[arma].MultiplyPoint3x4(Melhorias.Boca(Melhorias.DasArmas[arma], b));
        }

        /// <summary>
        /// O corpo (o que os outros veem e a sombra do dono): as peças na P_Sucata_*_corpo, o carregador estendido no lugar
        /// do curto (que deixa de ser desenhado) e a boca do cano do corpo.
        /// </summary>
        public static void NoCorpo(int arma, GameObject armaNoCorpo, ulong b, Transform boca)
        {
            var d = Dados;
            if (d == null || armaNoCorpo == null || arma < 0 || d.tpCorpo == null || arma >= d.tpCorpo.Length || d.tpCarregador == null || arma >= d.tpCarregador.Length) return;
            string modelo = Armas.Modelos[(int)Melhorias.DasArmas[arma]];
            var corpo = armaNoCorpo.transform.Find($"P_{modelo}_corpo");
            if (corpo == null) return;
            var rCorpo = corpo.GetComponent<Renderer>();
            Aplicar(corpo, arma, b, d.tpCorpo[arma], false, corpo.gameObject.layer, rCorpo != null ? rCorpo.sharedMaterial : null, rCorpo);
            var pa = armaNoCorpo.GetComponent<Player.PecasDaArma>();
            if (pa != null && pa.pecas != null)
                foreach (var p in pa.pecas)
                {
                    if (p == null || p.name != $"P_{modelo}_carregador") continue;
                    var rc = p.GetComponent<Renderer>();
                    Aplicar(p.transform, arma, b, d.tpCarregador[arma], true, p.layer, rc != null ? rc.sharedMaterial : null, rc);
                    if (rc != null) rc.forceRenderingOff = Melhorias.CarregadorLongo(b);
                }
            if (boca != null && boca.parent != null)
                boca.localPosition = boca.parent.InverseTransformPoint(corpo.TransformPoint(d.tpCorpo[arma].MultiplyPoint3x4(Melhorias.Boca(Melhorias.DasArmas[arma], b))));
        }

        /// <summary>
        /// O item no chão (a malha solta, no espaço da arma): as peças por cima e, com o carregador estendido, a malha sem o
        /// curto. 'solta': a malha de sempre (volta nela sem o estendido). Fora das de sucata, tira o que tinha.
        /// </summary>
        public static void NoChao(Transform item, MeshFilter mf, Item qual, ulong b, Mesh solta, Material mat)
        {
            int arma = Itens.DeSucata(qual) ? Melhorias.IndiceDa(Itens.Arma(qual)) : -1;
            if (arma < 0) b = 0;
            Aplicar(item, arma, b, Matrix4x4.identity, false, item.gameObject.layer, mat);
            Aplicar(item, arma, b, Matrix4x4.identity, true, item.gameObject.layer, mat);
            var sem = Dados != null && arma >= 0 ? Em(Dados.soltaSemCarregador, arma) : null;
            if (mf != null && arma >= 0 && Melhorias.CarregadorLongo(b) && sem != null) mf.sharedMesh = sem;
            else if (mf != null && solta != null && mf.sharedMesh != solta) mf.sharedMesh = solta;
        }

        /// <summary>
        /// A foto da arma de sucata com as peças postas (o detalhe da mochila): a da arma, com espaço em volta, e cada peça
        /// por cima, no mesmo enquadramento. False: não há foto (a de sempre fica).
        /// </summary>
        public static bool DesenharFoto(Rect r, Item qual, ulong b)
        {
            var d = Dados;
            int arma = Itens.DeSucata(qual) ? Melhorias.IndiceDa(Itens.Arma(qual)) : -1;
            if (d == null || arma < 0 || !Melhorias.Alguma(b)) return false;
            var fundo = Em(d.fotoDaArma, arma);
            if (fundo == null) return false;
            GUI.DrawTexture(r, fundo, ScaleMode.ScaleToFit);
            for (int i = 0; i < Melhorias.Quantas; i++)
            {
                if (!Melhorias.Posta(b, (PecaDaMelhoria)i)) continue;
                var f = Em(d.fotosDasPecas, arma * Melhorias.Quantas + i);
                if (f != null) GUI.DrawTexture(r, f, ScaleMode.ScaleToFit);
            }
            return true;
        }
    }

    /// <summary>O que está posto num lugar (para não refazer as peças a cada quadro).</summary>
    public class MelhoriasPostas : MonoBehaviour
    {
        public int arma = -1, pecas = -1;
    }
}
