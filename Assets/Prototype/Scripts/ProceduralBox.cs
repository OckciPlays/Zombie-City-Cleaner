using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>
    /// Caixa de cenário com faces subdivididas em células de ~1 m. No estilo PS1 a textura distorce
    /// (mapeamento afim), e a subdivisão deixa essa distorção pequena, como nos jogos da época.
    /// O UV segue a posição no mundo, então a textura continua de uma caixa para a outra.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
    public class ProceduralBox : MonoBehaviour
    {
        public Vector3 size = Vector3.one;
        public float cellSize = 1f;
        [Tooltip("Repetições da textura por metro.")]
        public float uvScale = 0.5f;
        [Tooltip("Estica a textura inteira em cada face (caixotes, pilastras), em vez de repetir pelo mundo.")]
        public bool fitTextureToFace;

        Mesh _mesh;
        bool _dirty = true;

        /// <summary>Cria uma caixa de cenário pronta (malha, colisor e material).</summary>
        public static GameObject Create(Transform parent, string name, Vector3 localCenter, Vector3 size, Material material,
            float uvScale = 0.5f, bool fitTextureToFace = false, float yaw = 0f)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider), typeof(ProceduralBox));
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(localCenter, Quaternion.Euler(0f, yaw, 0f));
            var box = go.GetComponent<ProceduralBox>();
            box.size = size;
            box.uvScale = uvScale;
            box.fitTextureToFace = fitTextureToFace;
            box.Rebuild();
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        void OnEnable() => Rebuild();
        void OnValidate() => _dirty = true;

        void OnDisable()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
            }
        }

        void Update()
        {
            if (transform.hasChanged) { _dirty = true; transform.hasChanged = false; }
            if (_dirty) Rebuild();
        }

        public void Rebuild()
        {
            _dirty = false;
            if (_mesh == null) _mesh = new Mesh { name = "ProceduralBox", hideFlags = HideFlags.DontSave };
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            Vector3 h = size * 0.5f;

            // Cada face: normal, eixo U e eixo V, com Cross(U, V) = normal.
            AddFace(new Vector3(h.x, 0, 0), Vector3.right, Vector3.back, Vector3.up, size.z, size.y, vertices, normals, uvs, triangles);
            AddFace(new Vector3(-h.x, 0, 0), Vector3.left, Vector3.forward, Vector3.up, size.z, size.y, vertices, normals, uvs, triangles);
            AddFace(new Vector3(0, 0, h.z), Vector3.forward, Vector3.right, Vector3.up, size.x, size.y, vertices, normals, uvs, triangles);
            AddFace(new Vector3(0, 0, -h.z), Vector3.back, Vector3.left, Vector3.up, size.x, size.y, vertices, normals, uvs, triangles);
            AddFace(new Vector3(0, h.y, 0), Vector3.up, Vector3.right, Vector3.back, size.x, size.z, vertices, normals, uvs, triangles);
            AddFace(new Vector3(0, -h.y, 0), Vector3.down, Vector3.right, Vector3.forward, size.x, size.z, vertices, normals, uvs, triangles);

            _mesh.Clear();
            _mesh.SetVertices(vertices);
            _mesh.SetNormals(normals);
            _mesh.SetUVs(0, uvs);
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;

            var box = GetComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = size;
        }

        void AddFace(Vector3 center, Vector3 normal, Vector3 u, Vector3 v, float uLength, float vLength,
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            int nu = Mathf.Max(1, Mathf.CeilToInt(uLength / cellSize));
            int nv = Mathf.Max(1, Mathf.CeilToInt(vLength / cellSize));
            int start = vertices.Count;
            Vector3 uWorld = transform.TransformDirection(u), vWorld = transform.TransformDirection(v);
            for (int j = 0; j <= nv; j++)
            {
                for (int i = 0; i <= nu; i++)
                {
                    Vector3 p = center + u * ((i / (float)nu - 0.5f) * uLength) + v * ((j / (float)nv - 0.5f) * vLength);
                    Vector3 world = transform.TransformPoint(p);
                    vertices.Add(p);
                    normals.Add(normal);
                    uvs.Add(fitTextureToFace
                        ? new Vector2(i / (float)nu, j / (float)nv)
                        : new Vector2(Vector3.Dot(world, uWorld), Vector3.Dot(world, vWorld)) * uvScale);
                }
            }
            for (int j = 0; j < nv; j++)
            {
                for (int i = 0; i < nu; i++)
                {
                    int a = start + j * (nu + 1) + i, b = a + 1, c = a + nu + 1, d = c + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
            }
        }
    }
}
