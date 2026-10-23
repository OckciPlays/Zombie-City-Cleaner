using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using static ZombieCityCleaner.Prototype.EditorTools.Ps1Assets;

namespace ZombieCityCleaner.Prototype.EditorTools
{
    /// <summary>
    /// Monta a cena do prédio: o elevador (fixo, com o jogador dentro) e o gerador de andares, que cria cada
    /// andar em tempo de jogo. Gera também as texturas e materiais do prédio.
    /// Menu: Zombie City Cleaner > Recriar cena do prédio.
    /// </summary>
    public static class TowerSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Prototype_Predio.unity";
        const float AgentRadius = 0.35f;

        [MenuItem("Zombie City Cleaner/Recriar cena do prédio")]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            EnsureFolders();
            ConfigureHumanoidAgent();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mats = CreateMaterials();
            var zombiePrefab = CreateZombieVariant();
            SetupAtmosphere(1.5f, 15f);

            // O prédio guarda o elevador e os andares gerados; o NavMesh é feito sobre os filhos dele.
            var building = new GameObject("Prédio");
            var surface = building.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            var floors = building.AddComponent<FloorGenerator>();
            floors.surface = surface;
            floors.zombiePrefab = zombiePrefab;
            floors.corridorFloor = mats["CorridorFloor"];
            floors.carpet = mats["Carpet"];
            floors.woodFloor = mats["WoodFloor"];
            floors.tile = mats["Tile"];
            floors.corridorWall = mats["CorridorWall"];
            floors.wallpaper = mats["Wallpaper"];
            floors.bathroomWall = mats["BathroomWall"];
            floors.ceiling = mats["CeilingTile"];
            floors.wood = mats["Wood"];
            floors.sofa = mats["Sofa"];
            floors.bed = mats["Bed"];
            floors.enamel = mats["Enamel"];
            floors.stairDoor = mats["StairDoor"];
            floors.window = mats["Window"];
            floors.lamp = mats["Lamp"];
            floors.lampOff = mats["LampOff"];
            floors.exitSign = mats["ExitSign"];
            floors.blood = mats["Blood"];

            var (player, weapons, flashlight) = BuildPlayer(mats, new Vector3(-1.7f, 0f, 0f), 90f);
            var elevator = BuildElevator(building.transform, mats);
            elevator.floors = floors;
            elevator.player = player.GetComponent<FirstPersonController>();

            var gameRoot = new GameObject("Game");
            gameRoot.AddComponent<PS1Atmosphere>();
            var game = gameRoot.AddComponent<TowerGame>();
            game.elevator = elevator;
            game.floors = floors;
            game.player = player;
            game.weapons = weapons;
            game.flashlight = flashlight;
            game.particleMaterial = mats["Particles"];

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Cena do prédio criada em {ScenePath}");
        }

        // Os vãos de porta têm 1,1 m: o agente padrão (raio 0,5) não passaria. O raio do zumbi é 0,35 m.
        static void ConfigureHumanoidAgent()
        {
            var projectSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
            if (projectSettings.Length == 0) return;
            var settings = new SerializedObject(projectSettings[0]);
            var agent = settings.FindProperty("m_Settings").GetArrayElementAtIndex(0);
            agent.FindPropertyRelative("agentRadius").floatValue = AgentRadius;
            agent.FindPropertyRelative("agentHeight").floatValue = 1.8f;
            agent.FindPropertyRelative("agentClimb").floatValue = 0.4f;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ Elevador

        static Elevator BuildElevator(Transform building, Dictionary<string, Material> m)
        {
            var root = new GameObject("Elevador").transform;
            root.SetParent(building, false);
            const float H = 2.6f, cx = -1.4f, half = 1.35f;
            float uv = 1f / H;

            GameObject Box(string name, Vector3 center, Vector3 size, Material material, bool fit = false)
                => ProceduralBox.Create(root, name, center, size, material, fit ? 1f : uv, fit);

            // Paredes e piso grossos (crescendo para fora): zumbis espremidos contra o jogador não o empurram através deles.
            const float wall = 0.4f, outerX = -2.65f - wall, inner = half - 0.075f;
            float outerZ = inner + wall, length = -0.05f - outerX, midX = (outerX - 0.05f) / 2;
            Box("Piso da cabine", new Vector3(midX, -0.5f, 0f), new Vector3(length, 1f, 2 * outerZ), m["ElevatorFloor"]);
            Box("Teto da cabine", new Vector3(midX, H + 0.15f, 0f), new Vector3(length, 0.3f, 2 * outerZ), m["ElevatorMetal"]);
            Box("Fundo da cabine", new Vector3(outerX + wall / 2, H / 2, 0f), new Vector3(wall, H, 2 * outerZ), m["ElevatorMetal"]);
            Box("Lateral da cabine", new Vector3(midX, H / 2, inner + wall / 2), new Vector3(length, H, wall), m["ElevatorMetal"]);
            Box("Lateral da cabine", new Vector3(midX, H / 2, -inner - wall / 2), new Vector3(length, H, wall), m["ElevatorMetal"]);
            Box("Frente da cabine", new Vector3(-0.1f, H / 2, 1.05f), new Vector3(0.1f, H, 0.6f), m["ElevatorMetal"]);
            Box("Frente da cabine", new Vector3(-0.1f, H / 2, -1.05f), new Vector3(0.1f, H, 0.6f), m["ElevatorMetal"]);
            Box("Verga da cabine", new Vector3(-0.1f, 2.4f, 0f), new Vector3(0.1f, 0.4f, 1.5f), m["ElevatorMetal"]);
            Box("Corrimão", new Vector3(-2.6f, 0.95f, 0f), new Vector3(0.05f, 0.05f, 2.2f), m["ElevatorMetal"], true);

            // Portas deslizantes: ficam fora do NavMesh (o zumbi pode entrar quando elas abrem).
            var doorLeft = Box("Porta esquerda", new Vector3(-0.025f, 1.1f, 0.375f), new Vector3(0.04f, 2.2f, 0.75f), m["ElevatorDoor"], true);
            var doorRight = Box("Porta direita", new Vector3(-0.025f, 1.1f, -0.375f), new Vector3(0.04f, 2.2f, 0.75f), m["ElevatorDoor"], true);
            foreach (var door in new[] { doorLeft, doorRight })
                door.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            var panel = Box("Painel", new Vector3(-0.45f, 1.25f, -half + 0.165f), new Vector3(0.25f, 0.45f, 0.03f), m["ElevatorPanel"], true);
            panel.GetComponent<BoxCollider>().enabled = false;
            var indicator = Box("Indicador", new Vector3(-0.17f, 2.35f, 0f), new Vector3(0.03f, 0.1f, 0.5f), m["Indicator"], true);
            indicator.GetComponent<BoxCollider>().enabled = false;
            indicator.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var fixture = Box("Luz da cabine", new Vector3(cx, H - 0.02f, 0f), new Vector3(0.9f, 0.04f, 0.9f), m["Lamp"], true);
            fixture.GetComponent<BoxCollider>().enabled = false;
            fixture.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var light = NewLight(root, "Luz da cabine", LightType.Point, new Vector3(cx, H - 0.25f, 0f), new Color(1f, 0.9f, 0.75f), 1.4f, 4.5f);
            light.shadows = LightShadows.None;
            var flicker = light.gameObject.AddComponent<FlickerLight>();
            flicker.brokenness = 0.08f;
            flicker.bulb = fixture.GetComponent<MeshRenderer>();

            var elevator = root.gameObject.AddComponent<Elevator>();
            elevator.doorLeft = doorLeft.transform;
            elevator.doorRight = doorRight.transform;
            elevator.indicator = indicator.GetComponent<MeshRenderer>();
            return elevator;
        }

        // ------------------------------------------------------------------ Materiais e texturas do prédio

        static Dictionary<string, Material> CreateMaterials()
        {
            var m = CommonMaterials();
            m["CorridorFloor"] = Ps1Material("CorridorFloor", MakeTexture("CorridorFloor", CorridorFloorTexture));
            m["Carpet"] = Ps1Material("Carpet", MakeTexture("Carpet", CarpetTexture));
            m["WoodFloor"] = Ps1Material("WoodFloor", MakeTexture("WoodFloor", WoodFloorTexture));
            m["Tile"] = Ps1Material("Tile", MakeTexture("Tile", TileTexture));
            m["CorridorWall"] = Ps1Material("CorridorWall", MakeTexture("CorridorWall", CorridorWallTexture));
            m["Wallpaper"] = Ps1Material("Wallpaper", MakeTexture("Wallpaper", WallpaperTexture));
            m["BathroomWall"] = Ps1Material("BathroomWall", MakeTexture("BathroomWall", TileTexture));
            m["CeilingTile"] = Ps1Material("CeilingTile", MakeTexture("CeilingTile", CeilingTileTexture));
            var fabric = MakeTexture("Fabric", FabricTexture);
            m["Wood"] = Ps1Material("Wood", MakeTexture("Wood", WoodTexture));
            m["Sofa"] = Ps1Material("Sofa", fabric, baseColor: new Color(0.62f, 0.42f, 0.28f));
            m["Bed"] = Ps1Material("Bed", fabric, baseColor: new Color(0.42f, 0.5f, 0.62f));
            m["Enamel"] = Ps1Material("Enamel", MakeTexture("Enamel", EnamelTexture));
            m["StairDoor"] = Ps1Material("StairDoor", MakeTexture("StairDoor", StairDoorTexture));
            m["Window"] = Ps1Material("Window", MakeTexture("Window", WindowTexture), emission: Color.white * 1.2f);
            m["ExitSign"] = Ps1Material("ExitSign", MakeTexture("ExitSign", ExitSignTexture), emission: Color.white * 1.6f);
            m["ElevatorMetal"] = Ps1Material("ElevatorMetal", MakeTexture("ElevatorMetal", ElevatorMetalTexture));
            m["ElevatorDoor"] = Ps1Material("ElevatorDoor", MakeTexture("ElevatorDoor", ElevatorDoorTexture));
            m["ElevatorFloor"] = Ps1Material("ElevatorFloor", MakeTexture("ElevatorFloor", ElevatorFloorTexture));
            m["ElevatorPanel"] = Ps1Material("ElevatorPanel", MakeTexture("ElevatorPanel", ElevatorPanelTexture), emission: Color.white * 0.9f);
            m["Indicator"] = Ps1Material("Indicator", null, emission: new Color(1f, 0.1f, 0.05f) * 2f);
            AssetDatabase.SaveAssets();
            return m;
        }

        static Color[] CorridorFloorTexture(int s)
        {
            var p = Fill(s, (x, y) =>
            {
                bool dark = ((x / 32) + (y / 32)) % 2 == 0;   // linóleo xadrez
                Color baseColor = dark ? new Color(0.30f, 0.34f, 0.28f) : new Color(0.56f, 0.53f, 0.43f);
                float k = 0.85f + 0.25f * (Fbm(x, y, s, 4, 3, 101) - 0.5f);
                if (x % 32 == 0 || y % 32 == 0) k *= 0.75f;
                if (Hash(x, y, 103) > 0.95f) k *= 0.8f;
                if (Fbm(x, y, s, 2, 3, 105) > 0.64f) k *= 0.7f;   // sujeira
                return Shade(baseColor, k);
            });
            Crack(p, s, 107, 0.6f);
            return p;
        }

        static Color[] CarpetTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                float k = 0.8f + 0.2f * Hash(x, y, 111) + 0.25f * (Fbm(x, y, s, 4, 3, 113) - 0.5f);
                if (Fbm(x, y, s, 2, 3, 115) > 0.66f) k *= 0.6f;   // manchas
                return Shade(new Color(0.42f, 0.17f, 0.13f), k);
            });
        }

        static Color[] WoodFloorTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                int row = y / 8;
                int offset = (int)(Hash(row, 0, 121) * 64);
                bool seam = y % 8 == 0 || (x + offset) % 32 == 0;
                float grain = Fbm((x + offset) * 4 % (s * 4), y, s * 4, 4, 2, 123 + row);
                float k = 0.75f + 0.35f * (grain - 0.5f) + 0.12f * (Hash(row, (x + offset) / 32, 125) - 0.5f);
                if (seam) k *= 0.55f;
                return Shade(new Color(0.48f, 0.31f, 0.17f), k);
            });
        }

        static Color[] TileTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                bool grout = x % 16 == 0 || y % 16 == 0;
                float k = grout ? 0.55f : 0.92f + 0.08f * (Hash(x / 16, y / 16, 131) - 0.5f) - 0.06f * Hash(x, y, 133);
                if (Fbm(x, y, s, 2, 3, 135) > 0.62f) k *= 0.8f;   // encardido
                return Shade(new Color(0.78f, 0.80f, 0.77f), k);
            });
        }

        // A textura cobre do chão ao teto: rodapé, barrado pintado embaixo e reboco em cima.
        static Color[] CorridorWallTexture(int s)
        {
            var p = Fill(s, (x, y) =>
            {
                float k = 0.88f + 0.2f * (Fbm(x, y, s, 4, 3, 141) - 0.5f);
                if (y < 3) return Shade(new Color(0.18f, 0.14f, 0.10f), k);                  // rodapé
                if (y < 22) return Shade(new Color(0.20f, 0.30f, 0.23f), k * (Hash(x, y, 143) > 0.97f ? 1.4f : 1f));   // barrado
                if (y < 24) return Shade(new Color(0.30f, 0.22f, 0.14f), k);                 // friso
                Color plaster = Shade(new Color(0.62f, 0.58f, 0.48f), k);
                if (Fbm(x, y, s, 2, 3, 145) > 0.63f) plaster = Shade(plaster, 0.7f);          // infiltração
                return plaster;
            });
            Crack(p, s, 147, 0.6f);
            return p;
        }

        static Color[] WallpaperTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                float k = 0.9f + 0.15f * (Fbm(x, y, s, 4, 3, 151) - 0.5f);
                if (y < 3) return Shade(new Color(0.24f, 0.16f, 0.10f), k);                  // rodapé
                bool stripe = (x / 4) % 2 == 0;
                Color paper = stripe ? new Color(0.52f, 0.47f, 0.34f) : new Color(0.43f, 0.40f, 0.30f);
                if (!stripe && (y % 12 == 6) && (x % 8 == 6)) paper = new Color(0.35f, 0.22f, 0.18f);   // florzinha
                float damp = Fbm(x, y, s, 2, 3, 153) * (1.2f - y / (float)s);
                if (damp > 0.58f) paper = Shade(paper, 0.72f);                                // mofo perto do chão
                if (Fbm(x, y, s, 4, 2, 155) > 0.7f) paper = new Color(0.58f, 0.56f, 0.50f);   // papel descascado
                return Shade(paper, k);
            });
        }

        static Color[] CeilingTileTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                bool grid = x % 32 == 0 || y % 32 == 0;
                float k = grid ? 0.6f : 0.85f + 0.15f * Hash(x, y, 161);
                Color c = Shade(new Color(0.55f, 0.54f, 0.50f), k);
                float stain = Fbm(x, y, s, 2, 3, 163);
                if (stain > 0.6f) c = Color.Lerp(c, new Color(0.38f, 0.28f, 0.16f), stain > 0.66f ? 0.7f : 0.35f);   // goteira
                return c;
            });
        }

        static Color[] WoodTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                float grain = Fbm(x * 4, y, s * 4, 4, 3, 171);
                float k = 0.78f + 0.35f * (grain - 0.5f);
                if (x < 2 || x >= s - 2 || y < 2 || y >= s - 2) k *= 0.6f;
                return Shade(new Color(0.42f, 0.27f, 0.15f), k);
            });
        }

        static Color[] FabricTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                float k = ((x + y) % 2 == 0 ? 0.95f : 0.82f) + 0.2f * (Fbm(x, y, s, 4, 2, 181) - 0.5f);
                if (Fbm(x, y, s, 2, 3, 183) > 0.65f) k *= 0.65f;   // manchas
                if (x < 2 || x >= s - 2 || y < 2 || y >= s - 2) k *= 0.7f;
                return new Color(k, k, k, 1f);
            });
        }

        static Color[] EnamelTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                float k = 0.95f + 0.05f * Hash(x, y, 191) - 0.25f * Mathf.Max(0f, Fbm(x, y, s, 2, 3, 193) - 0.55f) * 4f;
                if (x < 2 || x >= s - 2 || y < 2 || y >= s - 2) k *= 0.75f;
                return Shade(new Color(0.80f, 0.79f, 0.73f), k);
            });
        }

        static Color[] StairDoorTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                float k = 0.85f + 0.2f * (Fbm(x, y, s, 4, 3, 201) - 0.5f);
                Color c = Shade(new Color(0.28f, 0.33f, 0.30f), k);
                if (x < 3 || x >= s - 3 || y >= s - 3) c = new Color(0.12f, 0.12f, 0.12f);
                if (y > 26 && y < 30 && x > 8 && x < 56) c = new Color(0.55f, 0.55f, 0.52f);   // barra antipânico
                if (y > 40 && y < 56 && x > 22 && x < 42) c = new Color(0.05f, 0.07f, 0.09f);   // visor
                return c;
            });
        }

        // Janela à noite: vidro escuro com reflexo de luar e prédios acesos lá fora (a emissão usa a textura).
        static Color[] WindowTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                if (x < 3 || x >= s - 3 || y < 3 || y >= s - 3 || Mathf.Abs(x - s / 2) < 2 || Mathf.Abs(y - s / 2) < 2)
                    return new Color(0.06f, 0.05f, 0.04f);
                Color glass = Color.Lerp(new Color(0.02f, 0.03f, 0.06f), new Color(0.07f, 0.1f, 0.18f), y / (float)s);
                bool building = y < 30 + (int)(Hash(x / 6, 0, 211) * 18);
                if (building) glass = new Color(0.015f, 0.015f, 0.025f);
                if (building && x % 6 > 2 && y % 5 > 2 && Hash(x / 6, y / 5, 213) > 0.8f) glass = new Color(0.55f, 0.45f, 0.18f);
                return glass;
            });
        }

        // Placa vermelha "SAIDA" em fonte de pixel 3x5 (esticada para a placa larga).
        static Color[] ExitSignTexture(int s)
        {
            string[] font =
            {
                "111100111001111", // S
                "111101111101101", // A
                "111010010010111", // I
                "110101101101110", // D
                "111101111101101", // A
            };
            const int sx = 2, sy = 5, gap = 2;
            int width = font.Length * 3 * sx + (font.Length - 1) * gap;
            int left = (s - width) / 2, bottom = (s - 5 * sy) / 2;
            return Fill(s, (x, y) =>
            {
                Color red = new Color(0.7f, 0.04f, 0.03f);
                int lx = x - left, ly = y - bottom;
                if (lx < 0 || ly < 0 || ly >= 5 * sy) return red;
                int letter = lx / (3 * sx + gap), inLetter = lx % (3 * sx + gap);
                if (letter >= font.Length || inLetter >= 3 * sx) return red;
                int col = inLetter / sx, row = 4 - ly / sy;
                return font[letter][row * 3 + col] == '1' ? new Color(1f, 0.95f, 0.9f) : red;
            });
        }

        static Color[] ElevatorMetalTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                float k = 0.85f + 0.12f * (Hash(x, 0, 221) - 0.5f) + 0.06f * (Hash(x, y, 223) - 0.5f);   // escovado vertical
                if (x % 32 == 0 || y % 32 == 0) k *= 0.6f;
                if ((x % 32 == 3 || x % 32 == 29) && (y % 32 == 3 || y % 32 == 29)) k = 1.2f;            // rebites
                if (Fbm(x, y, s, 2, 3, 225) > 0.66f) k *= 0.75f;
                return Shade(new Color(0.48f, 0.49f, 0.50f), k);
            });
        }

        static Color[] ElevatorDoorTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                float k = 0.9f + 0.1f * (Hash(x, 0, 231) - 0.5f);
                if (x < 2 || x >= s - 2) k *= 0.55f;
                if (Fbm(x, y, s, 2, 3, 233) > 0.62f) k *= 0.7f;
                return Shade(new Color(0.55f, 0.56f, 0.57f), k);
            });
        }

        static Color[] ElevatorFloorTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                float dx = (x % 8) - 3.5f, dy = (y % 8) - 3.5f;
                float k = dx * dx + dy * dy < 5f ? 1.5f : 1f;   // pinos de borracha
                return Shade(new Color(0.11f, 0.11f, 0.12f), k * (0.9f + 0.2f * Hash(x, y, 241)));
            });
        }

        static Color[] ElevatorPanelTexture(int s)
        {
            return Fill(s, (x, y) =>
            {
                Color plate = new Color(0.08f, 0.08f, 0.09f);
                int bx = (x - 12) / 14, by = (y - 6) / 10;
                float cx = 12 + bx * 14 + 6, cy = 6 + by * 10 + 4;
                bool button = bx >= 0 && bx < 3 && by >= 0 && by < 6 && (x - cx) * (x - cx) + (y - cy) * (y - cy) < 9f;
                return button ? (by == 5 ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.6f, 0.2f)) : plate;
            });
        }
    }
}
