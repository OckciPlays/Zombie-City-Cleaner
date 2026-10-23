using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>
    /// Gera um andar do prédio em volta do elevador (que fica parado em x &lt; 0, com a porta em x = 0):
    /// um corredor ao longo de +X com apartamentos dos dois lados (sala, quarto e banheiro, com móveis),
    /// luzes falhando, manchas de sangue e os zumbis. Só existe um andar por vez; o anterior é apagado.
    /// </summary>
    public class FloorGenerator : MonoBehaviour
    {
        [Header("Referências")]
        public NavMeshSurface surface;
        public GameObject zombiePrefab;

        [Header("Materiais")]
        public Material corridorFloor;
        public Material carpet, woodFloor, tile;
        public Material corridorWall, wallpaper, bathroomWall, ceiling;
        public Material wood, sofa, bed, enamel;
        public Material stairDoor, window, lamp, lampOff, exitSign, blood;

        [Header("Andar")]
        public int baseZombies = 30;
        public int extraZombiesPerFloor = 1;
        public int maxZombies = 45;
        public float ceilingHeight = 2.8f;
        [Range(0f, 1f)] public float corridorZombieShare = 0.18f;
        [Tooltip("Zumbis não nascem mais perto do que isso da porta do elevador.")]
        public float safeRadius = 5f;

        public GameObject CurrentFloor { get; private set; }
        public readonly List<ZombieController> Zombies = new List<ZombieController>();

        const float WallThickness = 0.15f, DoorWidth = 1.1f, DoorHeight = 2.1f, CorridorHalf = 1.25f;
        const float ElevatorHalfGap = 0.75f, ElevatorDoorHeight = 2.2f;

        class Room
        {
            public string kind;
            public Rect area;   // x, z
            public readonly List<Vector2> doors = new List<Vector2>();
            public readonly List<Rect> occupied = new List<Rect>();
        }

        System.Random _rng;
        Transform _geometry, _props, _lights, _zombiesRoot;
        readonly List<Room> _rooms = new List<Room>();
        Rect _corridor;
        static Mesh _quad;

        /// <summary>Apaga o andar atual e gera o andar <paramref name="floorNumber"/>. Retorna quantos zumbis nasceram.</summary>
        public int Generate(int floorNumber, int seed)
        {
            Clear();
            _rng = new System.Random(seed);
            _rooms.Clear();

            var root = new GameObject($"Andar {floorNumber}").transform;
            root.SetParent(transform, false);
            CurrentFloor = root.gameObject;
            _geometry = Child(root, "Estrutura");
            _props = Child(root, "Móveis");
            _lights = Child(root, "Luzes");
            _zombiesRoot = Child(root, "Zumbis");

            BuildLayout();
            if (surface != null) surface.BuildNavMesh();
            SpawnZombies(Mathf.Min(maxZombies, baseZombies + extraZombiesPerFloor * (floorNumber - 1)));
            return Zombies.Count;
        }

        public void Clear()
        {
            Zombies.Clear();
            if (CurrentFloor == null) return;
            // Desativa antes de destruir: o Destroy só acontece no fim do quadro, e o NavMesh do
            // andar novo é gerado neste mesmo quadro.
            CurrentFloor.SetActive(false);
            Destroy(CurrentFloor);
            CurrentFloor = null;
        }

        // ------------------------------------------------------------------ Planta

        void BuildLayout()
        {
            float h = ceilingHeight;
            int perSide = Range(3, 5);
            var widthsNorth = new float[perSide];
            var widthsSouth = new float[perSide];
            float length = 0f;
            for (int i = 0; i < perSide; i++) { widthsNorth[i] = Range(5.5f, 7.5f); length += widthsNorth[i]; }
            // O lado sul usa as mesmas larguras totais, mas divididas de outro jeito.
            float southTotal = 0f;
            for (int i = 0; i < perSide; i++) { widthsSouth[i] = Range(5.5f, 7.5f); southTotal += widthsSouth[i]; }
            for (int i = 0; i < perSide; i++) widthsSouth[i] *= length / southTotal;

            float depthNorth = Range(6.5f, 8f), depthSouth = Range(6.5f, 8f);
            float outerN = CorridorHalf + depthNorth, outerS = -CorridorHalf - depthSouth;
            _corridor = new Rect(0f, -CorridorHalf, length, CorridorHalf * 2f);

            // Laje (grossa, para ninguém ser empurrado através dela) e teto.
            Box(_geometry, "Piso", new Vector3(length / 2, -0.5f, (outerN + outerS) / 2), new Vector3(length + 0.3f, 1f, outerN - outerS + 0.3f), corridorFloor);
            Box(_geometry, "Teto", new Vector3(length / 2, h + 0.15f, (outerN + outerS) / 2), new Vector3(length + 0.3f, 0.3f, outerN - outerS + 0.3f), ceiling);

            // Parede do fundo do corredor (onde fica o elevador) e a do outro extremo, com a porta da escada.
            WallAlongZ("Parede do elevador", 0f, -CorridorHalf, CorridorHalf, new[] { (0f, ElevatorHalfGap * 2f) }, corridorWall, corridorWall, ElevatorDoorHeight);
            WallAlongZ("Parede da escada", length, -CorridorHalf, CorridorHalf, null, corridorWall, corridorWall);
            BuildStairDoor(length);

            BuildSide(+1, widthsNorth, depthNorth, length);
            BuildSide(-1, widthsSouth, depthSouth, length);
            BuildCorridorLights(length);
            Decals(Range(3, 6));
        }

        void BuildSide(int side, float[] widths, float depth, float length)
        {
            float h = ceilingHeight;
            float zFront = side * CorridorHalf, zBack = side * (CorridorHalf + depth);

            // Parede do corredor com uma porta por apartamento (a face do corredor tem outro papel de parede).
            var corridorGaps = new List<(float, float)>();
            float x = 0f;
            var doorXs = new float[widths.Length];
            for (int i = 0; i < widths.Length; i++)
            {
                doorXs[i] = x + Range(0.4f + DoorWidth / 2, widths[i] - 0.4f - DoorWidth / 2);
                corridorGaps.Add((doorXs[i], DoorWidth));
                x += widths[i];
            }
            if (side > 0) WallAlongX("Parede do corredor N", zFront, 0f, length, corridorGaps, wallpaper, corridorWall);
            else WallAlongX("Parede do corredor S", zFront, 0f, length, corridorGaps, corridorWall, wallpaper);

            // Fachada, paredes laterais e janelas.
            WallAlongX(side > 0 ? "Fachada N" : "Fachada S", zBack + side * WallThickness / 2, 0f, length, null, wallpaper, wallpaper);
            WallAlongZ("Parede externa", -WallThickness / 2, Mathf.Min(zFront, zBack), Mathf.Max(zFront, zBack), null, wallpaper, wallpaper);
            WallAlongZ("Parede externa", length + WallThickness / 2, Mathf.Min(zFront, zBack), Mathf.Max(zFront, zBack), null, wallpaper, wallpaper);

            x = 0f;
            for (int i = 0; i < widths.Length; i++)
            {
                float x0 = x, x1 = x + widths[i];
                if (i > 0) WallAlongZ("Parede entre apartamentos", x0, Mathf.Min(zFront, zBack), Mathf.Max(zFront, zBack), null, wallpaper, wallpaper);
                BuildApartment(side, x0, x1, zFront, zBack, doorXs[i]);
                x = x1;
            }
        }

        void BuildApartment(int side, float x0, float x1, float zFront, float zBack, float entranceX)
        {
            float depth = Mathf.Abs(zBack - zFront);
            float frontDepth = Range(3.0f, depth - 2.6f);
            float zPartition = zFront + side * frontDepth;

            // Fundos: quarto e banheiro (o banheiro fica num dos lados).
            float bathWidth = Range(1.9f, 2.4f);
            bool bathLeft = _rng.NextDouble() < 0.5;
            float xSplit = bathLeft ? x0 + bathWidth : x1 - bathWidth;
            float bedX0 = bathLeft ? xSplit : x0, bedX1 = bathLeft ? x1 : xSplit;
            float bathX0 = bathLeft ? x0 : xSplit, bathX1 = bathLeft ? xSplit : x1;

            float bedDoor = Range(bedX0 + 0.3f + DoorWidth / 2, bedX1 - 0.3f - DoorWidth / 2);
            float bathDoor = (bathX0 + bathX1) / 2f;

            // Divisória entre a sala e os fundos (em dois trechos, para o banheiro ter azulejo do lado de dentro).
            Material backSideBath = bathroomWall, backSideBed = wallpaper;
            if (side > 0)
            {
                WallAlongX("Divisória", zPartition, bedX0, bedX1, new[] { (bedDoor, DoorWidth) }, backSideBed, wallpaper);
                WallAlongX("Divisória", zPartition, bathX0, bathX1, new[] { (bathDoor, DoorWidth * 0.9f) }, backSideBath, wallpaper);
            }
            else
            {
                WallAlongX("Divisória", zPartition, bedX0, bedX1, new[] { (bedDoor, DoorWidth) }, wallpaper, backSideBed);
                WallAlongX("Divisória", zPartition, bathX0, bathX1, new[] { (bathDoor, DoorWidth * 0.9f) }, wallpaper, backSideBath);
            }
            float zMinBack = Mathf.Min(zPartition, zBack), zMaxBack = Mathf.Max(zPartition, zBack);
            Material bathFacingPlusX = bathLeft ? wallpaper : bathroomWall, bathFacingMinusX = bathLeft ? bathroomWall : wallpaper;
            WallAlongZ("Parede do banheiro", xSplit, zMinBack, zMaxBack, null, bathFacingPlusX, bathFacingMinusX);

            // Pisos e cômodos.
            float zMinFront = Mathf.Min(zFront, zPartition), zMaxFront = Mathf.Max(zFront, zPartition);
            var living = AddRoom("sala", new Rect(x0, zMinFront, x1 - x0, zMaxFront - zMinFront), _rng.NextDouble() < 0.5 ? carpet : woodFloor);
            var bedroom = AddRoom("quarto", new Rect(bedX0, zMinBack, bedX1 - bedX0, zMaxBack - zMinBack), carpet);
            var bathroom = AddRoom("banheiro", new Rect(bathX0, zMinBack, bathX1 - bathX0, zMaxBack - zMinBack), tile);
            living.doors.Add(new Vector2(entranceX, zFront));
            living.doors.Add(new Vector2(bedDoor, zPartition));
            living.doors.Add(new Vector2(bathDoor, zPartition));
            bedroom.doors.Add(new Vector2(bedDoor, zPartition));
            bathroom.doors.Add(new Vector2(bathDoor, zPartition));

            Furnish(living, new[] { ("Sofá", 2.0f, 0.85f, 0.85f, sofa), ("Poltrona", 0.9f, 0.85f, 0.85f, sofa), ("Rack da TV", 1.4f, 0.45f, 0.55f, wood),
                                    ("Balcão da cozinha", 2.2f, 0.6f, 0.9f, enamel), ("Geladeira", 0.75f, 0.7f, 1.8f, enamel), ("Mesa", 1.2f, 0.8f, 0.75f, wood) });
            Furnish(bedroom, new[] { ("Cama", 1.6f, 2.0f, 0.55f, bed), ("Guarda-roupa", 1.4f, 0.6f, 2.0f, wood), ("Criado-mudo", 0.5f, 0.45f, 0.55f, wood), ("Cômoda", 1.0f, 0.5f, 0.9f, wood) });
            Furnish(bathroom, new[] { ("Banheira", 1.5f, 0.75f, 0.55f, enamel), ("Vaso", 0.45f, 0.65f, 0.45f, enamel), ("Pia", 0.6f, 0.45f, 0.85f, enamel) });

            Windows(side, x0, x1, zBack);
            foreach (var room in new[] { living, bedroom })
                if (_rng.NextDouble() < 0.45) RoomLamp(room);
        }

        Room AddRoom(string kind, Rect area, Material floor)
        {
            var room = new Room { kind = kind, area = area };
            _rooms.Add(room);
            var overlay = Box(_geometry, $"Piso {kind}", new Vector3(area.center.x, 0.01f, area.center.y), new Vector3(area.width, 0.02f, area.height), floor);
            overlay.GetComponent<BoxCollider>().enabled = false;   // a laje já é o chão físico
            return room;
        }

        // ------------------------------------------------------------------ Paredes

        /// <summary>Parede ao longo de X, em z, de x0 a x1, com vãos (centro, largura) e verga em cima de cada vão.</summary>
        void WallAlongX(string name, float z, float x0, float x1, IList<(float center, float width)> gaps, Material facePlusZ, Material faceMinusZ, float gapHeight = DoorHeight)
        {
            foreach (var (a, b) in Segments(x0, x1, gaps))
                WallPiece(name, new Vector3((a + b) / 2, ceilingHeight / 2, z), new Vector3(b - a, ceilingHeight, WallThickness), true, facePlusZ, faceMinusZ);
            if (gaps == null) return;
            foreach (var (center, width) in gaps)
                WallPiece(name + " (verga)", new Vector3(center, (gapHeight + ceilingHeight) / 2, z), new Vector3(width, ceilingHeight - gapHeight, WallThickness), true, facePlusZ, faceMinusZ);
        }

        /// <summary>Parede ao longo de Z, em x, de z0 a z1.</summary>
        void WallAlongZ(string name, float x, float z0, float z1, IList<(float center, float width)> gaps, Material facePlusX, Material faceMinusX, float gapHeight = DoorHeight)
        {
            foreach (var (a, b) in Segments(z0, z1, gaps))
                WallPiece(name, new Vector3(x, ceilingHeight / 2, (a + b) / 2), new Vector3(WallThickness, ceilingHeight, b - a), false, facePlusX, faceMinusX);
            if (gaps == null) return;
            foreach (var (center, width) in gaps)
                WallPiece(name + " (verga)", new Vector3(x, (gapHeight + ceilingHeight) / 2, center), new Vector3(WallThickness, ceilingHeight - gapHeight, width), false, facePlusX, faceMinusX);
        }

        // Uma parede com materiais diferentes em cada face vira duas meias-paredes coladas.
        void WallPiece(string name, Vector3 center, Vector3 size, bool alongX, Material facePlus, Material faceMinus)
        {
            float uv = 1f / ceilingHeight;   // a textura cobre do chão ao teto
            if (facePlus == faceMinus)
            {
                Box(_geometry, name, center, size, facePlus, uv);
                return;
            }
            Vector3 half = alongX ? new Vector3(size.x, size.y, size.z / 2) : new Vector3(size.x / 2, size.y, size.z);
            Vector3 offset = alongX ? new Vector3(0, 0, size.z / 4) : new Vector3(size.x / 4, 0, 0);
            Box(_geometry, name, center + offset, half, facePlus, uv);
            Box(_geometry, name, center - offset, half, faceMinus, uv);
        }

        static List<(float, float)> Segments(float from, float to, IList<(float center, float width)> gaps)
        {
            var result = new List<(float, float)>();
            var sorted = new List<(float center, float width)>();
            if (gaps != null) sorted.AddRange(gaps);
            sorted.Sort((p, q) => p.center.CompareTo(q.center));
            float cursor = from;
            foreach (var (center, width) in sorted)
            {
                float a = center - width / 2, b = center + width / 2;
                if (a - cursor > 0.01f) result.Add((cursor, a));
                cursor = Mathf.Max(cursor, b);
            }
            if (to - cursor > 0.01f) result.Add((cursor, to));
            return result;
        }

        // ------------------------------------------------------------------ Móveis e detalhes

        void Furnish(Room room, (string name, float width, float depth, float height, Material material)[] items)
        {
            foreach (var item in items)
            {
                if (_rng.NextDouble() < 0.2) continue;   // nem todo apartamento tem tudo
                for (int attempt = 0; attempt < 14; attempt++)
                {
                    int wall = _rng.Next(4);   // 0 = z mínimo, 1 = z máximo, 2 = x mínimo, 3 = x máximo
                    bool alongX = wall < 2;
                    float along = alongX ? item.width : item.depth;   // tamanho em X
                    float across = alongX ? item.depth : item.width;  // tamanho em Z
                    Rect a = room.area;
                    float margin = WallThickness / 2 + 0.02f;
                    float cx, cz;
                    if (alongX)
                    {
                        if (a.width < along + 0.2f) continue;
                        cx = Range(a.xMin + along / 2 + margin, a.xMax - along / 2 - margin);
                        cz = wall == 0 ? a.yMin + margin + across / 2 : a.yMax - margin - across / 2;
                    }
                    else
                    {
                        if (a.height < across + 0.2f) continue;
                        cz = Range(a.yMin + across / 2 + margin, a.yMax - across / 2 - margin);
                        cx = wall == 2 ? a.xMin + margin + along / 2 : a.xMax - margin - along / 2;
                    }
                    var footprint = new Rect(cx - along / 2, cz - across / 2, along, across);
                    if (!Fits(room, footprint)) continue;
                    room.occupied.Add(footprint);
                    Box(_props, item.name, new Vector3(cx, item.height / 2, cz), new Vector3(along, item.height, across), item.material, fit: true);
                    break;
                }
            }
        }

        static bool Fits(Room room, Rect footprint)
        {
            if (!room.area.Contains(footprint.min) || !room.area.Contains(footprint.max)) return false;
            foreach (var door in room.doors)
            {
                var clearance = new Rect(door.x - 1.0f, door.y - 1.0f, 2.0f, 2.0f);   // deixa a porta livre
                if (clearance.Overlaps(footprint)) return false;
            }
            foreach (var other in room.occupied)
            {
                var padded = new Rect(other.x - 0.15f, other.y - 0.15f, other.width + 0.3f, other.height + 0.3f);
                if (padded.Overlaps(footprint)) return false;
            }
            return true;
        }

        void Windows(int side, float x0, float x1, float zBack)
        {
            int count = (x1 - x0) > 6.5f ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                float cx = x0 + (x1 - x0) * (i + 1) / (count + 1);
                var rotation = side > 0 ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
                Quad(_props, "Janela", new Vector3(cx, 1.45f, zBack - side * 0.01f), rotation, new Vector3(1.3f, 1.1f, 1f), window);
            }
        }

        void BuildStairDoor(float length)
        {
            var door = Box(_props, "Porta da escada", new Vector3(length - 0.12f, DoorHeight / 2, 0f), new Vector3(0.08f, DoorHeight, 1.0f), stairDoor, fit: true);
            door.GetComponent<BoxCollider>().enabled = false;
            var sign = Box(_props, "Placa de saída", new Vector3(length - 0.12f, DoorHeight + 0.22f, 0f), new Vector3(0.06f, 0.18f, 0.45f), exitSign, fit: true);
            sign.GetComponent<BoxCollider>().enabled = false;
            sign.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            NewLight("Luz da saída", new Vector3(length - 0.6f, DoorHeight + 0.2f, 0f), new Color(1f, 0.1f, 0.05f), 1.2f, 4f);
        }

        void BuildCorridorLights(float length)
        {
            for (float x = 2.5f; x < length - 0.8f; x += 4f)
            {
                double roll = _rng.NextDouble();
                bool on = roll > 0.25;
                var fixture = Box(_lights, "Luminária", new Vector3(x, ceilingHeight - 0.05f, 0f), new Vector3(0.9f, 0.08f, 0.25f), on ? lamp : lampOff, fit: true);
                fixture.GetComponent<BoxCollider>().enabled = false;
                fixture.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                if (!on) continue;
                var light = NewLight("Luz do corredor", new Vector3(x, ceilingHeight - 0.25f, 0f), new Color(1f, 0.85f, 0.62f), 2.4f, 6.5f);
                var flicker = light.gameObject.AddComponent<FlickerLight>();
                flicker.brokenness = roll > 0.85 ? 0.05f : roll > 0.55 ? 0.4f : 0.85f;
                flicker.bulb = fixture.GetComponent<MeshRenderer>();
            }
        }

        void RoomLamp(Room room)
        {
            var center = room.area.center;
            var fixture = Box(_lights, "Lustre", new Vector3(center.x, ceilingHeight - 0.05f, center.y), new Vector3(0.35f, 0.08f, 0.35f), lamp, fit: true);
            fixture.GetComponent<BoxCollider>().enabled = false;
            fixture.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var light = NewLight("Luz do cômodo", new Vector3(center.x, ceilingHeight - 0.3f, center.y), new Color(1f, 0.78f, 0.5f), Range(1.2f, 2.0f), 5f);
            var flicker = light.gameObject.AddComponent<FlickerLight>();
            flicker.brokenness = Range(0f, 0.6f);
            flicker.bulb = fixture.GetComponent<MeshRenderer>();
        }

        void Decals(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Rect area = _rng.NextDouble() < 0.4 || _rooms.Count == 0 ? _corridor : _rooms[_rng.Next(_rooms.Count)].area;
                var position = new Vector3(Range(area.xMin + 0.5f, area.xMax - 0.5f), 0.025f, Range(area.yMin + 0.3f, area.yMax - 0.3f));
                Quad(_props, "Mancha de sangue", position, Quaternion.Euler(90f, Range(0f, 360f), 0f), Vector3.one * Range(0.8f, 1.8f), blood);
            }
        }

        // ------------------------------------------------------------------ Zumbis

        void SpawnZombies(int total)
        {
            if (zombiePrefab == null) return;
            int inCorridor = Mathf.RoundToInt(total * corridorZombieShare);
            float totalArea = 0f;
            foreach (var room in _rooms) totalArea += room.area.width * room.area.height;
            var placed = new List<Vector3>();

            for (int i = 0; i < total; i++)
            {
                Rect area = _corridor;
                if (i >= inCorridor && _rooms.Count > 0)
                {
                    float pick = (float)_rng.NextDouble() * totalArea;
                    foreach (var room in _rooms)
                    {
                        pick -= room.area.width * room.area.height;
                        area = room.area;
                        if (pick <= 0f) break;
                    }
                }
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    var point = new Vector3(Range(area.xMin + 0.45f, area.xMax - 0.45f), 0f, Range(area.yMin + 0.45f, area.yMax - 0.45f));
                    if (!NavMesh.SamplePosition(point, out var hit, 0.5f, NavMesh.AllAreas)) continue;
                    if (new Vector2(hit.position.x, hit.position.z).magnitude < safeRadius) continue;
                    bool crowded = false;
                    foreach (var other in placed) if ((other - hit.position).sqrMagnitude < 0.75f * 0.75f) { crowded = true; break; }
                    if (crowded) continue;
                    placed.Add(hit.position);
                    var zombie = Instantiate(zombiePrefab, hit.position, Quaternion.Euler(0f, Range(0f, 360f), 0f), _zombiesRoot);
                    zombie.name = $"Zumbi {Zombies.Count + 1}";
                    Zombies.Add(zombie.GetComponent<ZombieController>());
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ Utilitários

        GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, float uvScale = 0.5f, bool fit = false)
            => ProceduralBox.Create(parent, name, center, size, material, uvScale, fit);

        void Quad(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            if (_quad == null) _quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = _quad;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        Light NewLight(string name, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_lights, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
        int Range(int min, int maxExclusive) => _rng.Next(min, maxExclusive);
    }
}
