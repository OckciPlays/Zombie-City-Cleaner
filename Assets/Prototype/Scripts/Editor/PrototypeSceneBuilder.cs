using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ZombieCityCleaner.Prototype.EditorTools
{
    /// <summary>
    /// Monta a cena de protótipo: um galpão escuro com cara de PS1, o jogador em primeira pessoa
    /// (pistola + lanterna) e zumbis espalhados. Também gera as texturas 64x64 e os materiais.
    /// Menu: Zombie City Cleaner > Recriar cena de protótipo.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        const string Root = "Assets/Prototype";
        const string ScenePath = "Assets/Scenes/Prototype_Galpao.unity";
        const string ZombieBasePrefab = "Assets/Characters/ZombiePS1/ZombiePS1.prefab";
        const string ZombieMaterial = "Assets/Characters/ZombiePS1/Materials/ZombiePS1.mat";
        const string ZombieVariantPath = Root + "/Prefabs/Zombie_Prototype.prefab";
        const string ShaderName = "ZombieCityCleaner/PS1 Lit";
        const float ZombieScale = 1.8f;

        static readonly Color FogColor = new Color(0.012f, 0.012f, 0.016f);

        [MenuItem("Zombie City Cleaner/Recriar cena de protótipo")]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        /// <summary>Monta e salva a cena sem perguntar nada (usado também por automação).</summary>
        public static void Build()
        {
            foreach (var folder in new[] { "Textures", "Materials", "Prefabs" })
                Directory.CreateDirectory(Path.Combine(Root, folder));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mats = CreateMaterials();
            var zombiePrefab = CreateZombieVariant();
            SetupAtmosphere();

            var level = new GameObject("Level");
            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            BuildWarehouse(level.transform, mats);
            BuildLights(level.transform, mats);

            var (player, pistol, flashlight) = BuildPlayer(mats);
            BuildZombies(zombiePrefab);

            var gameRoot = new GameObject("Game");
            gameRoot.AddComponent<PS1Atmosphere>();
            var game = gameRoot.AddComponent<PrototypeGame>();
            game.levelSurface = surface;
            game.particleMaterial = mats["Particles"];
            game.player = player;
            game.pistol = pistol;
            game.flashlight = flashlight;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Cena de protótipo criada em {ScenePath}");
        }

        // ------------------------------------------------------------------ Materiais e texturas

        static Dictionary<string, Material> CreateMaterials()
        {
            var shader = Shader.Find(ShaderName);
            var m = new Dictionary<string, Material>
            {
                ["Floor"] = Ps1Material("Floor", shader, MakeTexture("Floor", FloorTexture)),
                ["Wall"] = Ps1Material("Wall", shader, MakeTexture("Wall", WallTexture)),
                ["Ceiling"] = Ps1Material("Ceiling", shader, MakeTexture("Ceiling", CeilingTexture)),
                ["Crate"] = Ps1Material("Crate", shader, MakeTexture("Crate", CrateTexture)),
                ["Shelf"] = Ps1Material("Shelf", shader, MakeTexture("Shelf", ShelfTexture)),
                ["Pillar"] = Ps1Material("Pillar", shader, MakeTexture("Pillar", PillarTexture)),
                ["Door"] = Ps1Material("Door", shader, MakeTexture("Door", DoorTexture)),
                ["Blood"] = Ps1Material("Blood", shader, MakeTexture("Blood", BloodTexture, alpha: true)),
                ["Lamp"] = Ps1Material("Lamp", shader, null, emission: new Color(1f, 0.85f, 0.6f) * 2f),
                ["EmergencyLamp"] = Ps1Material("EmergencyLamp", shader, null, emission: new Color(1f, 0.1f, 0.05f) * 2.5f),
                ["Gun"] = Ps1Material("Gun", shader, MakeTexture("Gun", GunTexture), emission: new Color(0.45f, 0.45f, 0.45f), snap: 0f, affine: 0f),
                ["MuzzleFlash"] = Ps1Material("MuzzleFlash", shader, MakeTexture("MuzzleFlash", MuzzleTexture, alpha: true), emission: new Color(3f, 2.3f, 1.1f), baseColor: Color.black, snap: 0f, affine: 0f),
            };

            var particles = LoadOrCreate($"{Root}/Materials/Particles.mat", () => new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")));
            particles.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(particles);
            m["Particles"] = particles;

            // O zumbi passa a usar o mesmo shader de PS1 (a textura continua a mesma).
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

        static Material Ps1Material(string name, Shader shader, Texture2D texture, Color? emission = null, Color? baseColor = null, float snap = 1f, float affine = 0.6f)
        {
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

        static Material LoadOrCreate(string path, Func<Material> create)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = create();
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Texture2D MakeTexture(string name, Func<int, Color[]> generator, int size = 64, bool alpha = false)
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

        // Ruído "value noise" que repete nas bordas (a textura emenda sem costura).
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        static float Noise(float x, float y, int period, int seed)
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

        static float Fbm(int px, int py, int size, int cells, int octaves, int seed)
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

        static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        static Color[] Fill(int size, Func<int, int, Color> pixel)
        {
            var p = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    p[y * size + x] = pixel(x, y);
            return p;
        }

        static void Crack(Color[] p, int size, int seed, float darkness)
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

        static Color[] FloorTexture(int s)
        {
            var p = Fill(s, (x, y) =>
            {
                float c = 0.34f + 0.16f * (Fbm(x, y, s, 4, 4, 1) - 0.5f);
                if (Hash(x, y, 7) > 0.94f) c -= 0.07f;
                if (x % 32 == 0 || y % 32 == 0) c *= 0.6f;
                if (Fbm(x, y, s, 4, 3, 9) > 0.66f) c *= 0.7f;   // manchas de óleo
                return new Color(c, c * 0.97f, c * 0.92f, 1f);
            });
            Crack(p, s, 3, 0.55f);
            Crack(p, s, 11, 0.6f);
            return p;
        }

        static Color[] WallTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                int row = y / 16, offset = row % 2 == 0 ? 0 : 16;
                bool mortar = y % 16 == 0 || (x + offset) % 32 == 0;
                int block = row * 8 + (x + offset) / 32;
                float c = mortar ? 0.30f : 0.40f + 0.10f * (Fbm(x, y, s, 8, 3, 2) - 0.5f) + 0.06f * (Hash(block, 0, 5) - 0.5f);
                if (Hash(x, 0, 13) > 0.86f) c *= 0.8f + 0.2f * (y / (float)s);   // escorridos de água
                if (Fbm(x, y, s, 2, 3, 17) > 0.62f) c *= 0.7f;
                return new Color(c * 0.9f, c, c * 0.88f, 1f);
            });
        }

        static Color[] CeilingTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                float c = 0.20f + 0.06f * Mathf.Sin(x * Mathf.PI * 2f / 8f) + 0.05f * (Fbm(x, y, s, 4, 3, 21) - 0.5f);
                Color col = new Color(c, c, c * 1.05f, 1f);
                if (Fbm(x, y, s, 4, 3, 23) > 0.6f) col = Color.Lerp(col, new Color(0.30f, 0.15f, 0.07f), 0.6f);   // ferrugem
                return col;
            });
        }

        static Color[] CrateTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                bool frame = x < 6 || x >= s - 6 || y < 6 || y >= s - 6;
                bool brace = Mathf.Abs(x - y) < 4 && !frame;
                float grain = Fbm(x * 4, y / 2, s * 4, 4, 3, 31);
                float c = 0.8f + 0.3f * (grain - 0.5f);
                if (!frame && !brace && (y - 6) % 13 == 0) c *= 0.55f;   // vão entre tábuas
                if (frame || brace) c *= 0.82f;
                if ((x == 3 || x == s - 4) && (y == 3 || y == s - 4)) c = 0.3f;   // pregos
                return new Color(0.48f * c, 0.32f * c, 0.17f * c, 1f);
            });
        }

        static Color[] ShelfTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                bool beam = y % 30 < 4;
                bool post = x % 32 < 3;
                if (beam) return Shade(new Color(0.75f, 0.38f, 0.08f), 0.85f + 0.2f * Hash(x, y, 41));
                if (post) return Shade(new Color(0.12f, 0.2f, 0.36f), 0.9f + 0.2f * Hash(x, y, 43));
                int box = (y / 30) * 8 + x / 10;
                bool hasBox = Hash(box, 1, 47) > 0.35f && (x % 10) > 1 && (y % 30) > 6 && (y % 30) < 6 + 12 + (int)(Hash(box, 2, 47) * 10);
                if (hasBox) return Shade(new Color(0.52f, 0.38f, 0.22f), 0.75f + 0.3f * Hash(box, 3, 47) + 0.1f * (Fbm(x, y, s, 8, 2, 49) - 0.5f));
                return new Color(0.03f, 0.03f, 0.035f, 1f);
            });
        }

        static Color[] PillarTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                if (y < 9) return ((x + y) / 6) % 2 == 0 ? new Color(0.85f, 0.65f, 0.05f) : new Color(0.05f, 0.05f, 0.05f);
                float c = 0.42f + 0.14f * (Fbm(x, y, s, 4, 4, 51) - 0.5f);
                if (Hash(x, y, 53) > 0.95f) c -= 0.08f;
                return new Color(c, c * 0.98f, c * 0.95f, 1f);
            });
        }

        static Color[] DoorTexture(int s)
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

        static Color[] BloodTexture(int s)
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

        static Color[] GunTexture(int s)
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

        static Color[] MuzzleTexture(int s)
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

        static void SetupAtmosphere()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = 2f;
            RenderSettings.fogEndDistance = 20f;
            RenderSettings.reflectionIntensity = 0f;
        }

        static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, float uvScale = 0.5f, bool fit = false, float yaw = 0f)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider), typeof(ProceduralBox));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var box = go.GetComponent<ProceduralBox>();
            box.size = size;
            box.uvScale = uvScale;
            box.fitTextureToFace = fit;
            box.Rebuild();
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static void BuildWarehouse(Transform level, Dictionary<string, Material> m)
        {
            const float W = 30f, D = 20f, H = 4.5f;
            var structure = new GameObject("Estrutura").transform;
            structure.SetParent(level, false);
            Box(structure, "Piso", new Vector3(0, -0.25f, 0), new Vector3(W + 1, 0.5f, D + 1), m["Floor"]);
            Box(structure, "Teto", new Vector3(0, H + 0.25f, 0), new Vector3(W + 1, 0.5f, D + 1), m["Ceiling"]);
            Box(structure, "Parede Norte", new Vector3(0, H / 2, D / 2 + 0.25f), new Vector3(W + 1, H, 0.5f), m["Wall"]);
            Box(structure, "Parede Sul", new Vector3(0, H / 2, -D / 2 - 0.25f), new Vector3(W + 1, H, 0.5f), m["Wall"]);
            Box(structure, "Parede Leste", new Vector3(W / 2 + 0.25f, H / 2, 0), new Vector3(0.5f, H, D + 1), m["Wall"]);
            Box(structure, "Parede Oeste", new Vector3(-W / 2 - 0.25f, H / 2, 0), new Vector3(0.5f, H, D + 1), m["Wall"]);

            // Divisória entre a entrada e o depósito, com uma passagem larga e uma estreita.
            Box(structure, "Divisória A", new Vector3(-8.5f, H / 2, 2f), new Vector3(13f, H, 0.4f), m["Wall"]);
            Box(structure, "Divisória B", new Vector3(5.5f, H / 2, 2f), new Vector3(7f, H, 0.4f), m["Wall"]);
            Box(structure, "Divisória C", new Vector3(13f, H / 2, 2f), new Vector3(4f, H, 0.4f), m["Wall"]);
            Box(structure, "Verga", new Vector3(0f, H - 0.6f, 2f), new Vector3(4f, 1.2f, 0.4f), m["Wall"]);

            var props = new GameObject("Objetos").transform;
            props.SetParent(level, false);
            foreach (var p in new[] { new Vector2(-8f, -4f), new Vector2(8f, -4f), new Vector2(-1f, 6.5f) })
                Box(props, "Pilastra", new Vector3(p.x, H / 2, p.y), new Vector3(0.6f, H, 0.6f), m["Pillar"], fit: true);

            // Prateleiras do depósito (corredores de 1,8 m).
            float shelfScale = 1f / 2.2f;
            Box(props, "Prateleira", new Vector3(-8.5f, 1.1f, 5.5f), new Vector3(9f, 2.2f, 1f), m["Shelf"], shelfScale);
            Box(props, "Prateleira", new Vector3(-8.5f, 1.1f, 8.4f), new Vector3(9f, 2.2f, 1f), m["Shelf"], shelfScale);
            Box(props, "Prateleira", new Vector3(8.5f, 1.1f, 5.5f), new Vector3(9f, 2.2f, 1f), m["Shelf"], shelfScale);
            Box(props, "Prateleira", new Vector3(8.5f, 1.1f, 8.4f), new Vector3(9f, 2.2f, 1f), m["Shelf"], shelfScale);

            var crates = new (Vector3 position, float size, float yaw)[]
            {
                (new Vector3(-12.6f, 0.6f, -8.4f), 1.2f, 0f), (new Vector3(-11.3f, 0.6f, -8.5f), 1.2f, 8f), (new Vector3(-12f, 1.8f, -8.4f), 1.2f, -12f),
                (new Vector3(-13.6f, 0.5f, -6.9f), 1.0f, 20f),
                (new Vector3(11f, 0.6f, -6.5f), 1.2f, 5f), (new Vector3(11.2f, 0.6f, -5.1f), 1.2f, -10f), (new Vector3(11.1f, 1.8f, -5.8f), 1.2f, 30f),
                (new Vector3(4.5f, 0.5f, -6f), 1.0f, 35f), (new Vector3(-4.5f, 0.6f, 0.4f), 1.2f, 15f), (new Vector3(-3.3f, 0.45f, 0.6f), 0.9f, -20f),
                (new Vector3(13.3f, 0.6f, 9f), 1.2f, 0f), (new Vector3(-13.5f, 0.6f, 3.3f), 1.2f, 10f), (new Vector3(2.4f, 0.5f, 9.2f), 1.0f, 25f),
            };
            foreach (var (position, size, yaw) in crates)
                Box(props, "Caixote", position, Vector3.one * size, m["Crate"], fit: true, yaw: yaw);

            Box(props, "Porta dos fundos", new Vector3(0f, 1.2f, D / 2 - 0.05f), new Vector3(1.6f, 2.4f, 0.12f), m["Door"], fit: true);

            // Manchas de sangue no chão (quads com recorte).
            var decals = new GameObject("Sangue").transform;
            decals.SetParent(level, false);
            var stains = new (Vector3 position, float size, float yaw)[]
            {
                (new Vector3(1.5f, 0.012f, -3f), 1.6f, 20f), (new Vector3(2.2f, 0.013f, -1.9f), 0.8f, 70f), (new Vector3(-9.5f, 0.012f, -2.2f), 1.3f, 140f),
                (new Vector3(0.3f, 0.012f, 3.4f), 1.1f, 10f), (new Vector3(-8f, 0.012f, 7f), 1.8f, 95f), (new Vector3(8.6f, 0.012f, 7.1f), 1.2f, 200f),
                (new Vector3(0.2f, 0.012f, 8.6f), 2f, 45f),
            };
            foreach (var (position, size, yaw) in stains)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Mancha";
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(decals, false);
                quad.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(90f, yaw, 0f));
                quad.transform.localScale = Vector3.one * size;
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = m["Blood"];
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static void BuildLights(Transform level, Dictionary<string, Material> m)
        {
            var lights = new GameObject("Luzes").transform;
            lights.SetParent(level, false);

            // Lâmpadas de teto: posição, intensidade e "mau contato" (0 = estável, 1 = quase sempre apagada).
            var lamps = new (Vector3 position, float intensity, float brokenness, bool on)[]
            {
                (new Vector3(-7f, 4.35f, -6f), 6f, 0.1f, true),
                (new Vector3(7f, 4.35f, -6f), 6f, 0.55f, true),
                (new Vector3(0f, 4.35f, -1f), 5f, 0.3f, true),
                (new Vector3(-8.5f, 4.35f, 7f), 0f, 0f, false),
                (new Vector3(8.5f, 4.35f, 7f), 5f, 0.85f, true),
            };
            foreach (var (position, intensity, brokenness, on) in lamps)
            {
                var fixture = Box(lights, "Luminária", position, new Vector3(1.0f, 0.12f, 0.3f), on ? m["Lamp"] : m["Gun"], fit: true);
                fixture.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                fixture.GetComponent<BoxCollider>().enabled = false;
                if (!on) continue;
                var light = NewLight(lights, "Luz de teto", LightType.Point, position + Vector3.down * 0.25f, new Color(1f, 0.82f, 0.58f), intensity, 9f);
                light.shadows = LightShadows.None;
                var flicker = light.gameObject.AddComponent<FlickerLight>();
                flicker.brokenness = brokenness;
                flicker.bulb = fixture.GetComponent<MeshRenderer>();
            }

            // Luz de emergência vermelha sobre a porta dos fundos.
            var emergency = Box(lights, "Luz de emergência", new Vector3(0f, 2.75f, 9.9f), new Vector3(0.35f, 0.15f, 0.15f), m["EmergencyLamp"], fit: true);
            emergency.GetComponent<BoxCollider>().enabled = false;
            NewLight(lights, "Luz vermelha", LightType.Point, new Vector3(0f, 2.6f, 9.5f), new Color(1f, 0.08f, 0.04f), 3.5f, 7f).shadows = LightShadows.None;
        }

        static Light NewLight(Transform parent, string name, LightType type, Vector3 position, Color color, float intensity, float range)
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

        static (PlayerHealth, Pistol, Flashlight) BuildPlayer(Dictionary<string, Material> m)
        {
            var player = new GameObject("Player");
            player.transform.SetPositionAndRotation(new Vector3(0f, 0f, -8.5f), Quaternion.identity);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.35f;
            var controller = player.AddComponent<FirstPersonController>();
            var health = player.AddComponent<PlayerHealth>();

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

            // Pistola (modelo de primeira pessoa feito de blocos, sem sombra).
            var viewModel = new GameObject("ViewModel").transform;
            viewModel.SetParent(camGo.transform, false);
            // Mais longe da câmera, para a perspectiva não esticar a pistola como se fosse um fuzil.
            viewModel.localPosition = new Vector3(0.15f, -0.17f, 0.46f);
            viewModel.localRotation = Quaternion.Euler(-2f, -6f, 0f);
            GunPart(viewModel, "Ferrolho", new Vector3(0f, 0.012f, 0f), new Vector3(0.034f, 0.04f, 0.2f), 0f, m["Gun"]);
            GunPart(viewModel, "Cano", new Vector3(0f, 0.002f, 0.085f), new Vector3(0.02f, 0.02f, 0.05f), 0f, m["Gun"]);
            GunPart(viewModel, "Cabo", new Vector3(0f, -0.05f, -0.065f), new Vector3(0.03f, 0.09f, 0.045f), -15f, m["Gun"]);
            GunPart(viewModel, "Guarda-mato", new Vector3(0f, -0.022f, -0.02f), new Vector3(0.012f, 0.012f, 0.05f), 0f, m["Gun"]);
            GunPart(viewModel, "Mira", new Vector3(0f, 0.036f, 0.09f), new Vector3(0.006f, 0.008f, 0.008f), 0f, m["Gun"]);

            var flash = GameObject.CreatePrimitive(PrimitiveType.Quad);
            flash.name = "Clarão";
            Object.DestroyImmediate(flash.GetComponent<Collider>());
            flash.transform.SetParent(viewModel, false);
            flash.transform.localPosition = new Vector3(0f, 0.012f, 0.135f);
            flash.transform.localScale = Vector3.one * 0.11f;
            var flashRenderer = flash.GetComponent<MeshRenderer>();
            flashRenderer.sharedMaterial = m["MuzzleFlash"];
            flashRenderer.shadowCastingMode = ShadowCastingMode.Off;
            var muzzleLight = NewLight(viewModel, "Luz do tiro", LightType.Point, new Vector3(0f, 0.02f, 0.2f), new Color(1f, 0.75f, 0.4f), 10f, 8f);
            muzzleLight.shadows = LightShadows.None;

            var pistol = player.AddComponent<Pistol>();
            pistol.aimCamera = cam;
            pistol.controller = controller;
            pistol.health = health;
            pistol.viewModel = viewModel;
            pistol.muzzleLight = muzzleLight;
            pistol.muzzleFlash = flashRenderer;

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

            return (health, pistol, flashlight);
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

        static GameObject CreateZombieVariant()
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombieBasePrefab);
            if (basePrefab == null) throw new FileNotFoundException("Prefab do zumbi não encontrado", ZombieBasePrefab);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            instance.transform.localScale = Vector3.one * ZombieScale;
            if (instance.GetComponent<ZombieController>() == null) instance.AddComponent<ZombieController>();
            var variant = PrefabUtility.SaveAsPrefabAsset(instance, ZombieVariantPath);
            Object.DestroyImmediate(instance);
            return variant;
        }

        static void BuildZombies(GameObject prefab)
        {
            var parent = new GameObject("Zumbis").transform;
            var spots = new (Vector3 position, float yaw)[]
            {
                (new Vector3(6f, 0f, -2f), 20f),      // os dois da entrada começam de costas para o jogador
                (new Vector3(-10f, 0f, -1f), 330f),
                (new Vector3(-8.5f, 0f, 6.95f), 270f),
                (new Vector3(8f, 0f, 6.95f), 90f),
                (new Vector3(0.8f, 0f, 8.7f), 10f),
                (new Vector3(13f, 0f, 3.3f), 180f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var zombie = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                zombie.name = $"Zumbi {i + 1}";
                zombie.transform.SetPositionAndRotation(spots[i].position, Quaternion.Euler(0f, spots[i].yaw, 0f));
            }
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
