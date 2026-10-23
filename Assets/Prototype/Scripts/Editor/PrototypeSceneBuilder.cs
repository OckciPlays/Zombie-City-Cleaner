using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using static ZombieCityCleaner.Prototype.EditorTools.Ps1Assets;
using Object = UnityEngine.Object;

namespace ZombieCityCleaner.Prototype.EditorTools
{
    /// <summary>
    /// Monta a cena do galpão: um armazém escuro com cara de PS1, o jogador em primeira pessoa (SCAR,
    /// pistola e lanterna) e 6 zumbis. Menu: Zombie City Cleaner > Recriar cena do galpão.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Prototype_Galpao.unity";

        [MenuItem("Zombie City Cleaner/Recriar cena do galpão")]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        /// <summary>Monta e salva a cena sem perguntar nada (usado também por automação).</summary>
        public static void Build()
        {
            EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mats = CreateMaterials();
            var zombiePrefab = CreateZombieVariant();
            SetupAtmosphere(2f, 20f);

            var level = new GameObject("Level");
            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            BuildWarehouse(level.transform, mats);
            BuildLights(level.transform, mats);

            var (player, weapons, flashlight) = BuildPlayer(mats, new Vector3(0f, 0f, -8.5f), 0f);
            BuildZombies(zombiePrefab);

            var gameRoot = new GameObject("Game");
            gameRoot.AddComponent<PS1Atmosphere>();
            var game = gameRoot.AddComponent<PrototypeGame>();
            game.levelSurface = surface;
            game.particleMaterial = mats["Particles"];
            game.player = player;
            game.weapons = weapons;
            game.flashlight = flashlight;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Cena do galpão criada em {ScenePath}");
        }

        // ------------------------------------------------------------------ Materiais e texturas do galpão

        static Dictionary<string, Material> CreateMaterials()
        {
            var m = CommonMaterials();
            m["Floor"] = Ps1Material("Floor", MakeTexture("Floor", FloorTexture));
            m["Wall"] = Ps1Material("Wall", MakeTexture("Wall", WallTexture));
            m["Ceiling"] = Ps1Material("Ceiling", MakeTexture("Ceiling", CeilingTexture));
            m["Crate"] = Ps1Material("Crate", MakeTexture("Crate", CrateTexture));
            m["Shelf"] = Ps1Material("Shelf", MakeTexture("Shelf", ShelfTexture));
            m["Pillar"] = Ps1Material("Pillar", MakeTexture("Pillar", PillarTexture));
            m["Door"] = Ps1Material("Door", MakeTexture("Door", DoorTexture));
            AssetDatabase.SaveAssets();
            return m;
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

        // ------------------------------------------------------------------ Cena

        static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, float uvScale = 0.5f, bool fit = false, float yaw = 0f)
            => ProceduralBox.Create(parent, name, center, size, material, uvScale, fit, yaw);

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
    }
}
