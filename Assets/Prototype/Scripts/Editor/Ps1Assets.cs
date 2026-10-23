using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ZombieCityCleaner.Prototype.EditorTools
{
    /// <summary>
    /// Peças comuns aos construtores de cena: texturas 64x64 geradas por código (filtro Point), materiais com o
    /// shader de PS1, a variante do zumbi, a atmosfera, o jogador (SCAR, pistola e lanterna) e as Build Settings.
    /// </summary>
    public static class Ps1Assets
    {
        public const string Root = "Assets/Prototype";
        public const string ShaderName = "ZombieCityCleaner/PS1 Lit";
        public const string ZombieBasePrefab = "Assets/Characters/ZombiePS1/ZombiePS1.prefab";
        public const string ZombieMaterial = "Assets/Characters/ZombiePS1/Materials/ZombiePS1.mat";
        public const string ZombieVariantPath = Root + "/Prefabs/Zombie_Prototype.prefab";
        public const string ZombieVoicePath = Root + "/Audio/SomZombie1.mp3";
        public const string ScarModelPath = "Assets/Weapons/Scar/Scar.fbx";
        public const string ScarTexturePath = "Assets/Weapons/Scar/Scar_basecolor.jpg";
        public const float ZombieScale = 1.8f;

        public static readonly Color FogColor = new Color(0.012f, 0.012f, 0.016f);

        public static Shader Ps1Shader => Shader.Find(ShaderName);

        public static void EnsureFolders()
        {
            foreach (var folder in new[] { "Textures", "Materials", "Prefabs", "Audio" })
                Directory.CreateDirectory(Path.Combine(Root, folder));
        }

        // ------------------------------------------------------------------ Materiais

        /// <summary>Materiais usados pelas duas cenas (sangue, lâmpadas, arma, clarão, partículas) e o do zumbi.</summary>
        public static Dictionary<string, Material> CommonMaterials()
        {
            var shader = Ps1Shader;
            var m = new Dictionary<string, Material>
            {
                ["Blood"] = Ps1Material("Blood", MakeTexture("Blood", BloodTexture, alpha: true)),
                ["Lamp"] = Ps1Material("Lamp", null, emission: new Color(1f, 0.85f, 0.6f) * 2f),
                ["LampOff"] = Ps1Material("LampOff", null, baseColor: new Color(0.25f, 0.25f, 0.24f)),
                ["EmergencyLamp"] = Ps1Material("EmergencyLamp", null, emission: new Color(1f, 0.1f, 0.05f) * 2.5f),
                ["Gun"] = Ps1Material("Gun", MakeTexture("Gun", GunTexture), emission: new Color(0.45f, 0.45f, 0.45f), snap: 0f, affine: 0f),
                ["Scar"] = Ps1Material("Scar", ImportScarTexture(), emission: new Color(0.35f, 0.35f, 0.35f), snap: 0f, affine: 0f),
                ["MuzzleFlash"] = Ps1Material("MuzzleFlash", MakeTexture("MuzzleFlash", MuzzleTexture, alpha: true), emission: new Color(3f, 2.3f, 1.1f), baseColor: Color.black, snap: 0f, affine: 0f),
            };

            var particles = LoadOrCreate($"{Root}/Materials/Particles.mat", () => new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")));
            particles.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(particles);
            m["Particles"] = particles;

            // O zumbi usa o mesmo shader de PS1 (a textura continua a mesma).
            var zombie = AssetDatabase.LoadAssetAtPath<Material>(ZombieMaterial);
            if (zombie != null)
            {
                var texture = zombie.GetTexture("_BaseMap");
                zombie.shader = shader;
                zombie.SetTexture("_BaseMap", texture);
                zombie.SetColor("_BaseColor", Color.white);
                zombie.SetFloat("_VertexSnap", 1f);
                zombie.SetFloat("_Affine", 0.3f);
                EditorUtility.SetDirty(zombie);
            }
            AssetDatabase.SaveAssets();
            return m;
        }

        public static Material Ps1Material(string name, Texture2D texture, Color? emission = null, Color? baseColor = null, float snap = 1f, float affine = 0.6f)
        {
            var shader = Ps1Shader;
            var mat = LoadOrCreate($"{Root}/Materials/{name}.mat", () => new Material(shader));
            mat.shader = shader;
            mat.SetTexture("_BaseMap", texture != null ? texture : Texture2D.whiteTexture);
            mat.SetColor("_BaseColor", baseColor ?? Color.white);
            mat.SetColor("_EmissionColor", emission ?? Color.black);
            mat.SetFloat("_VertexSnap", snap);
            mat.SetFloat("_Affine", affine);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // A textura da SCAR vem em 2048 px: reduzida para 256 e com filtro Point, fica com a cara das outras.
        static Texture2D ImportScarTexture()
        {
            var importer = AssetImporter.GetAtPath(ScarTexturePath) as TextureImporter;
            if (importer == null) return null;
            importer.maxTextureSize = 256;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ScarTexturePath);
        }

        public static Material LoadOrCreate(string path, Func<Material> create)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = create();
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        public static Texture2D MakeTexture(string name, Func<int, Color[]> generator, int size = 64, bool alpha = false)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(generator(size));
            tex.Apply();
            string path = $"{Root}/Textures/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = alpha;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ Ruído e desenho

        // Ruído "value noise" que repete nas bordas (a textura emenda sem costura).
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        public static float Noise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            int Wrap(int v) => ((v % period) + period) % period;
            float a = Hash(Wrap(x0), Wrap(y0), seed), b = Hash(Wrap(x0 + 1), Wrap(y0), seed);
            float c = Hash(Wrap(x0), Wrap(y0 + 1), seed), d = Hash(Wrap(x0 + 1), Wrap(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        public static float Fbm(int px, int py, int size, int cells, int octaves, int seed)
        {
            float sum = 0f, amplitude = 0.5f, total = 0f;
            for (int o = 0; o < octaves; o++)
            {
                int c = cells << o;
                sum += Noise(px * c / (float)size, py * c / (float)size, c, seed + o * 31) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
            }
            return sum / total;
        }

        public static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        public static Color[] Fill(int size, Func<int, int, Color> pixel)
        {
            var p = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    p[y * size + x] = pixel(x, y);
            return p;
        }

        public static void Crack(Color[] p, int size, int seed, float darkness)
        {
            var r = new System.Random(seed);
            float x = r.Next(size), y = r.Next(size), angle = (float)(r.NextDouble() * Math.PI * 2);
            for (int i = 0; i < size * 1.2f; i++)
            {
                int ix = ((int)x % size + size) % size, iy = ((int)y % size + size) % size;
                p[iy * size + ix] = Shade(p[iy * size + ix], darkness);
                angle += (float)(r.NextDouble() - 0.5) * 0.9f;
                x += Mathf.Cos(angle);
                y += Mathf.Sin(angle);
            }
        }

        // ------------------------------------------------------------------ Texturas comuns

        public static Color[] DoorTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                float c = 0.8f + 0.25f * (Fbm(x, y, s, 4, 3, 61) - 0.5f);
                Color col = new Color(0.45f * c, 0.07f * c, 0.06f * c, 1f);
                if (x < 3 || x >= s - 3 || y >= s - 3) col = new Color(0.15f, 0.15f, 0.16f, 1f);
                if (y > 28 && y < 32 && x > 44 && x < 56) col = new Color(0.6f, 0.6f, 0.55f, 1f);   // maçaneta
                return col;
            });
        }

        public static Color[] BloodTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                float dx = (x - s * 0.5f) / (s * 0.5f), dy = (y - s * 0.5f) / (s * 0.5f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) + 0.45f * (Fbm(x, y, s, 4, 3, 71) - 0.5f);
                bool drop = Hash(x / 3, y / 3, 73) > 0.97f && d < 1.1f;
                float a = d < 0.6f || drop ? 1f : 0f;
                float c = 0.6f + 0.4f * Fbm(x, y, s, 8, 2, 75);
                return new Color(0.32f * c, 0.02f * c, 0.02f * c, a);
            });
        }

        public static Color[] GunTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                // Metal escuro com bordas mais claras (desgaste), para a silhueta da arma ler bem no escuro.
                float c = 0.2f + 0.06f * (Fbm(x, y, s, 8, 2, 81) - 0.5f);
                if (y % 16 == 0) c *= 0.6f;
                if (x < 3 || x >= s - 3 || y < 3 || y >= s - 3) c = 0.42f;
                return new Color(c, c, c * 1.05f, 1f);
            });
        }

        public static Color[] MuzzleTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                float dx = x - s * 0.5f + 0.5f, dy = y - s * 0.5f + 0.5f;
                float angle = Mathf.Atan2(dy, dx), radius = Mathf.Sqrt(dx * dx + dy * dy) / (s * 0.5f);
                float star = 0.35f + 0.65f * Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 3f)), 6f);
                return new Color(1f, 1f, 1f, radius < star ? 1f : 0f);
            });
        }

        // ------------------------------------------------------------------ Cena

        public static void SetupAtmosphere(float fogStart, float fogEnd)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.reflectionIntensity = 0f;
        }

        public static Light NewLight(Transform parent, string name, LightType type, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            return light;
        }

        public static GameObject CreateZombieVariant()
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombieBasePrefab);
            if (basePrefab == null) throw new FileNotFoundException("Prefab do zumbi não encontrado", ZombieBasePrefab);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            instance.transform.localScale = Vector3.one * ZombieScale;
            var controller = instance.GetComponent<ZombieController>();
            if (controller == null) controller = instance.AddComponent<ZombieController>();
            controller.voiceClip = ImportVoice();
            if (!controller.ComputeHeadSphere()) Debug.LogWarning("Não deu para medir a cabeça do zumbi pela malha.");
            var variant = PrefabUtility.SaveAsPrefabAsset(instance, ZombieVariantPath);
            Object.DestroyImmediate(instance);
            return variant;
        }

        // O som do zumbi toca em 3D (mono) e é descomprimido ao carregar, para começar em trechos diferentes sem atraso.
        static AudioClip ImportVoice()
        {
            var importer = AssetImporter.GetAtPath(ZombieVoicePath) as AudioImporter;
            if (importer == null) return null;
            importer.forceToMono = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(ZombieVoicePath);
        }

        public static (PlayerHealth health, WeaponHolder weapons, Flashlight flashlight) BuildPlayer(Dictionary<string, Material> m, Vector3 position, float yaw)
        {
            var player = new GameObject("Player");
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.35f;
            var controller = player.AddComponent<FirstPersonController>();
            var health = player.AddComponent<PlayerHealth>();

            // Os zumbis desviam do jogador em vez de se espremer contra ele (e empurrá-lo através da parede).
            var obstacle = player.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = 0.45f;
            obstacle.height = 1.8f;
            obstacle.center = new Vector3(0f, 0.9f, 0f);
            obstacle.carving = false;

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0f, 1.62f, 0f);
            controller.cameraPivot = pivot;

            var camGo = new GameObject("PlayerCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pivot, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;
            camGo.AddComponent<AudioListener>();
            var cameraData = cam.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            camGo.AddComponent<PS1Screen>();

            var guns = new[] { BuildScar(player.transform, camGo.transform, m), BuildPistol(player.transform, camGo.transform, m) };
            foreach (var gun in guns)
            {
                gun.aimCamera = cam;
                gun.controller = controller;
                gun.health = health;
            }
            var weapons = player.AddComponent<WeaponHolder>();
            weapons.guns = guns;
            weapons.controller = controller;

            // Lanterna: segue a câmera com atraso, por isso fica fora dela na hierarquia.
            var flashlightGo = new GameObject("Lanterna");
            flashlightGo.transform.SetParent(player.transform, false);
            var spot = flashlightGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(1f, 0.95f, 0.82f);
            spot.intensity = 22f;
            spot.range = 22f;
            spot.spotAngle = 58f;
            spot.innerSpotAngle = 22f;
            spot.shadows = LightShadows.Hard;
            spot.shadowNearPlane = 0.2f;
            var flashlight = flashlightGo.AddComponent<Flashlight>();
            flashlight.spotLight = spot;
            flashlight.follow = camGo.transform;

            return (health, weapons, flashlight);
        }

        // SCAR (arma principal): o modelo de Modelo 3D/Scar com o shader de PS1. O cano do FBX aponta para +X.
        static Gun BuildScar(Transform player, Transform cam, Dictionary<string, Material> m)
        {
            var viewModel = NewViewModel(cam, "SCAR", new Vector3(0.13f, -0.19f, 0.3f), Quaternion.identity);
            var mesh = Array.Find(AssetDatabase.LoadAllAssetsAtPath(ScarModelPath), a => a is Mesh) as Mesh;
            if (mesh == null) throw new FileNotFoundException("Malha da SCAR não encontrada", ScarModelPath);
            var body = new GameObject("Scar");
            body.transform.SetParent(viewModel, false);
            body.transform.SetLocalPositionAndRotation(new Vector3(0f, -0.085f, 0.153f), Quaternion.Euler(0f, -90f, 0f));
            body.transform.localScale = Vector3.one * 0.85f;
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = body.AddComponent<MeshRenderer>();
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = m["Scar"];
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var gun = NewGun(player, "SCAR", viewModel, m, new Vector3(0f, 0.056f, 0.6f), 0.18f, new Vector3(0f, 0.06f, 0.7f));
            gun.automatic = true;
            gun.magazineSize = 30;
            gun.fireInterval = 0.1f;
            gun.reloadTime = 2.2f;
            gun.recoilDegrees = 0.9f;
            gun.kickBack = 0.03f;
            gun.kickRotation = 4f;
            gun.soundPitch = 0.8f;
            gun.baseSpread = 0.15f;
            gun.moveSpread = 1f;
            gun.shotBloom = 0.25f;
            gun.maxBloom = 2f;
            return gun;
        }

        // Pistola (arma secundária): modelo feito de blocos. Fica mais longe da câmera, para a perspectiva não esticá-la.
        static Gun BuildPistol(Transform player, Transform cam, Dictionary<string, Material> m)
        {
            var viewModel = NewViewModel(cam, "Pistola", new Vector3(0.15f, -0.17f, 0.46f), Quaternion.Euler(-2f, -6f, 0f));
            GunPart(viewModel, "Ferrolho", new Vector3(0f, 0.012f, 0f), new Vector3(0.034f, 0.04f, 0.2f), 0f, m["Gun"]);
            GunPart(viewModel, "Cano", new Vector3(0f, 0.002f, 0.085f), new Vector3(0.02f, 0.02f, 0.05f), 0f, m["Gun"]);
            GunPart(viewModel, "Cabo", new Vector3(0f, -0.05f, -0.065f), new Vector3(0.03f, 0.09f, 0.045f), -15f, m["Gun"]);
            GunPart(viewModel, "Guarda-mato", new Vector3(0f, -0.022f, -0.02f), new Vector3(0.012f, 0.012f, 0.05f), 0f, m["Gun"]);
            GunPart(viewModel, "Mira", new Vector3(0f, 0.036f, 0.09f), new Vector3(0.006f, 0.008f, 0.008f), 0f, m["Gun"]);
            return NewGun(player, "PISTOLA", viewModel, m, new Vector3(0f, 0.012f, 0.135f), 0.11f, new Vector3(0f, 0.02f, 0.2f));
        }

        static Transform NewViewModel(Transform cam, string name, Vector3 position, Quaternion rotation)
        {
            var viewModel = new GameObject($"Modelo {name}").transform;
            viewModel.SetParent(cam, false);
            viewModel.SetLocalPositionAndRotation(position, rotation);
            return viewModel;
        }

        // O componente fica num filho do jogador; o clarão e a luz do tiro ficam no modelo, na boca do cano.
        static Gun NewGun(Transform player, string name, Transform viewModel, Dictionary<string, Material> m, Vector3 muzzle, float flashSize, Vector3 lightPosition)
        {
            var flash = GameObject.CreatePrimitive(PrimitiveType.Quad);
            flash.name = "Clarão";
            Object.DestroyImmediate(flash.GetComponent<Collider>());
            flash.transform.SetParent(viewModel, false);
            flash.transform.localPosition = muzzle;
            flash.transform.localScale = Vector3.one * flashSize;
            var flashRenderer = flash.GetComponent<MeshRenderer>();
            flashRenderer.sharedMaterial = m["MuzzleFlash"];
            flashRenderer.shadowCastingMode = ShadowCastingMode.Off;
            var muzzleLight = NewLight(viewModel, "Luz do tiro", LightType.Point, lightPosition, new Color(1f, 0.75f, 0.4f), 10f, 8f);
            muzzleLight.shadows = LightShadows.None;

            var holder = new GameObject($"Arma {name}");
            holder.transform.SetParent(player, false);
            var gun = holder.AddComponent<Gun>();
            gun.weaponName = name;
            gun.viewModel = viewModel;
            gun.muzzleLight = muzzleLight;
            gun.muzzleFlash = flashRenderer;
            return gun;
        }

        static void GunPart(Transform parent, string name, Vector3 position, Vector3 size, float pitch, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(pitch, 0f, 0f));
            part.transform.localScale = size;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Coloca a cena em primeiro nas Build Settings (é a que abre ao jogar uma build).</summary>
        public static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
