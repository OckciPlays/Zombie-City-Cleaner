using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Roupas
{
    /// <summary>
    /// Os ícones das roupas (a parte 4 do Plano_Personagem.md da pasta de arte, 31/10/2026): cada roupa tem as cores dela,
    /// então o ícone é feito no jogo, uma vez por roupa e cores (guardado), pela malha do item (<see cref="ItensDasRoupas"/>)
    /// com a luz fixa do retrato do inventário. Pedido no OnGUI, ele fica pronto no fim do quadro (até 6 por quadro): até
    /// lá, null. As roupas deitadas (as do tronco, as de cima, os coletes e as calças) são vistas de cima; os óculos, quase
    /// de frente; as luvas (que vêm uma de cada lado do corpo), só a da direita, de cima; o resto, de três quartos.
    /// </summary>
    public class IconesDasRoupas : MonoBehaviour
    {
        public const int Pequeno = 128, Grande = 320;
        // longe de tudo, na camada do retrato (a câmera do mundo não desenha)
        static readonly Vector3 Lugar = new Vector3(0f, -700f, 0f);

        static readonly Dictionary<(Item, ulong, int), Texture2D> _feitos = new Dictionary<(Item, ulong, int), Texture2D>();
        static readonly List<(Item, ulong, int)> _pedidos = new List<(Item, ulong, int)>();
        static IconesDasRoupas _host;

        Camera _camera;
        MeshFilter _filtro;
        MeshRenderer _render;
        Material _material, _vidro;

        /// <summary>O ícone da roupa com estas cores (null: ainda não feito; fica pronto no fim do quadro).</summary>
        public static Texture2D Icone(Item item, ulong cores, bool grande = false)
        {
            // (fora do Play, nada: o objeto que faz os ícones só vive no jogo)
            if (!Itens.EhRoupa(item) || !Application.isPlaying) return null;
            var chave = (item, cores, grande ? Grande : Pequeno);
            if (_feitos.TryGetValue(chave, out var t) && t != null) return t;
            if (!_pedidos.Contains(chave)) _pedidos.Add(chave);
            if (_host == null)
            {
                var go = new GameObject("Ícones das roupas");
                DontDestroyOnLoad(go);
                _host = go.AddComponent<IconesDasRoupas>();
            }
            return null;
        }

        void LateUpdate()
        {
            for (int n = 0; n < 6 && _pedidos.Count > 0; n++)
            {
                var p = _pedidos[0];
                _pedidos.RemoveAt(0);
                var t = Fazer(p.Item1, p.Item2, p.Item3);
                if (t != null) _feitos[p] = t;
            }
        }

        void OnDestroy()
        {
            foreach (var t in _feitos.Values) if (t != null) Destroy(t);
            _feitos.Clear();
            _pedidos.Clear();
            if (_material != null) Destroy(_material);
            if (_vidro != null) Destroy(_vidro);
            if (_host == this) _host = null;
        }

        void Preparar(ItensDasRoupas roupas)
        {
            if (_camera != null) return;
            var modelo = new GameObject("Roupa") { layer = JogadorRede.CamadaRetrato };
            modelo.transform.SetParent(transform, false);
            modelo.transform.position = Lugar;
            _filtro = modelo.AddComponent<MeshFilter>();
            _render = modelo.AddComponent<MeshRenderer>();
            _render.shadowCastingMode = ShadowCastingMode.Off;
            _render.receiveShadows = false;
            // a luz fixa do retrato do inventário
            _material = new Material(roupas.material);
            _material.SetFloat("_Retrato", 1f);
            _material.SetFloat("_Ambiente", 0.4f);
            _material.SetFloat("_Luz", 0.82f);
            _material.SetVector("_DirecaoDaLuz", new Vector4(0.3f, 0.85f, 0.65f, 0f));
            _render.sharedMaterial = _material;
            // o vidro (a viseira, os óculos de proteção), na mesma luz
            if (roupas.vidro != null)
            {
                _vidro = new Material(roupas.vidro);
                _vidro.SetFloat("_Retrato", 1f);
                _vidro.SetFloat("_Ambiente", 0.4f);
                _vidro.SetFloat("_Luz", 0.82f);
                _vidro.SetVector("_DirecaoDaLuz", new Vector4(0.3f, 0.85f, 0.65f, 0f));
            }

            var go = new GameObject("Câmera dos ícones");
            go.transform.SetParent(transform, false);
            _camera = go.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.cullingMask = 1 << JogadorRede.CamadaRetrato;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 10f;
            var dados = go.AddComponent<UniversalAdditionalCameraData>();
            dados.renderPostProcessing = false;
            dados.renderShadows = false;
            dados.antialiasing = AntialiasingMode.None;
            // o renderizador 1 (o dos braços) não tem a neblina de altura
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && urp.rendererDataList.Length > 1) dados.SetRenderer(1);
        }

        Texture2D Fazer(Item item, ulong cores, int tamanho)
        {
            var roupas = ItensDasRoupas.Carregar();
            var malha = roupas != null ? roupas.Malha(item) : null;
            if (malha == null || roupas.material == null) return null;
            Preparar(roupas);
            _filtro.sharedMesh = malha;
            _render.sharedMaterials = _vidro != null && roupas.TemVidro(item) ? new[] { _material, _vidro } : new[] { _material };
            roupas.Pintar(_render, item, cores);

            // deitada (as do tronco, as de cima, os coletes e as calças, achatadas no Roupas_Itens.fbx, com a gola e o cós
            // para o -Z): de cima, a gola para cima na imagem; os óculos quase de frente (de cima, só se viam as hastes); as luvas de cima, só a da direita
            // (as duas ficam longe uma da outra, como nas mãos); o resto, de três quartos, um pouco de cima
            var b = malha.bounds;
            var lugar = Itens.Roupa(item).lugar;
            bool deitada = lugar == LugarDaRoupa.Camiseta || lugar == LugarDaRoupa.PorCima || lugar == LugarDaRoupa.Calca;
            var de = deitada ? new Vector3(0f, 1f, 0.18f) : lugar == LugarDaRoupa.Oculos ? new Vector3(0.4f, 0.3f, 1f)
                : lugar == LugarDaRoupa.Luvas ? new Vector3(0.3f, 0.9f, 0.8f) : new Vector3(0.62f, 0.42f, 1f);
            // (a luva fica deitada, comprida ao longo do x: uns 30 cm com o punho)
            if (lugar == LugarDaRoupa.Luvas) b = new Bounds(new Vector3(b.max.x - 0.16f, b.center.y, b.center.z), new Vector3(0.32f, b.size.y, b.size.z));
            var centro = Lugar + b.center;
            var cam = _camera.transform;
            cam.position = centro + de.normalized * 4f;
            cam.LookAt(centro, deitada ? Vector3.back : Vector3.up);
            // o enquadramento: a caixa da malha vista pela câmera, com uma folga
            float mx = 0f, my = 0f;
            var meio = Vector2.zero;
            var cantos = new Vector3[8];
            for (int c = 0; c < 8; c++)
            {
                var p = Lugar + new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                cantos[c] = cam.InverseTransformPoint(p);
                meio += new Vector2(cantos[c].x, cantos[c].y) / 8f;
            }
            foreach (var q in cantos)
            {
                mx = Mathf.Max(mx, Mathf.Abs(q.x - meio.x));
                my = Mathf.Max(my, Mathf.Abs(q.y - meio.y));
            }
            cam.position += cam.right * meio.x + cam.up * meio.y;
            _camera.aspect = 1f;
            _camera.orthographicSize = Mathf.Max(mx, my, 0.03f) * 1.12f;

            var alvo = RenderTexture.GetTemporary(tamanho * 2, tamanho * 2, 24, RenderTextureFormat.ARGB32);
            var meioTamanho = RenderTexture.GetTemporary(tamanho, tamanho, 0, RenderTextureFormat.ARGB32);
            _camera.targetTexture = alvo;
            _camera.Render();
            _camera.targetTexture = null;
            Graphics.Blit(alvo, meioTamanho);   // metade do tamanho: sem serrilhado
            var antes = RenderTexture.active;
            RenderTexture.active = meioTamanho;
            var t = new Texture2D(tamanho, tamanho, TextureFormat.RGBA32, false) { name = $"Ícone {Itens.Nome(item)}", wrapMode = TextureWrapMode.Clamp };
            t.ReadPixels(new Rect(0, 0, tamanho, tamanho), 0, 0);
            t.Apply();
            RenderTexture.active = antes;
            RenderTexture.ReleaseTemporary(alvo);
            RenderTexture.ReleaseTemporary(meioTamanho);
            return t;
        }
    }
}
