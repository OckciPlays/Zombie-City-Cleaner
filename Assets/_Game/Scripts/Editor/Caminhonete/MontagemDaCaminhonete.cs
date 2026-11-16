using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using ZombieCityCleaner.Vehicles;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A caminhonete (15/11/2026, a pedido: "essa caminhonete precisa ser boa, pois a gente vai começar o jogo com ela e vai
    /// ser o nosso primeiro carro"): uma picape grande de cabine simples dos anos 90, verde-escura com os frisos dourados,
    /// sem marca nem nome (`Art/Veiculos/Caminhonete/Caminhonete.fbx`, feita pelo `Blender/Caminhonete/caminhonete.py`). O
    /// prefab `Prefabs/Veiculos/Caminhonete.prefab` é uma cópia do da perua (`Carro.prefab`: a mesma física, rede, motor e
    /// chave, peças do capô, pneus, macaco, sons e luzes), com o visual dela e tudo o que depende do tamanho:
    ///  - <b>o chassi é outro</b>: rodas de 0,385 m (245/75 aro 16), entre-eixos de 2,98 m, bitola de 1,66 m, 1.900 kg, a
    ///    suspensão mais comprida e mais dura e a <b>tração atrás</b> (`Carro.tracaoTraseira`); o macaco encosta embaixo do
    ///    chassi e o pneu furado baixa na proporção da roda (`Carro.raioDaRoda`);
    ///  - <b>2 lugares na cabine e 2 na caçamba</b> (a pedido: "na cabine da frente vão ser apenas dois lugares ... e na
    ///    caçamba vão dar para sentar mais duas pessoas, encostadas ali dentro"): os da caçamba sentam no piso, encostados na
    ///    frente dela, olhando para trás (`Carro.cacamba`, `PassageiroDoCarro.PoseDaCacamba`), e sobem pela lateral, atrás
    ///    da roda ("[E] Subir na caçamba");
    ///  - <b>a caçamba aberta</b> ("pode fazer a caçamba aberta, sem a lona"): o porta-malas dela, sempre aberto, com o
    ///    <b>estepe de pé</b> do lado direito, logo atrás da cabine (a roda dela, que se tira e se põe) e o macaco deitado no
    ///    piso;
    ///  - pega com a chave dela (`Item.ChaveDaCaminhonete`, 73); sem ela, a ligação direta.
    /// Também põe a caminhonete na lista de prefabs da rede e no `MundoDaPartida.caminhonete` da cena Mundo (o carro da
    /// família no começo da partida, na vaga da perua no bunker). A perua e o cupê não mudam.
    /// Menu: Zombie City Cleaner > Veículos > Montar a caminhonete. Depois de um "Montar veículos", rode de novo.
    /// </summary>
    public static class MontagemDaCaminhonete
    {
        const string Pasta = "Assets/_Game/Art/Veiculos/Caminhonete";
        const string Fbx = Pasta + "/Caminhonete.fbx";
        const string PastaTexturas = Pasta + "/Texturas";
        const string PastaMateriais = Pasta + "/Materiais";
        const string PrefabPerua = "Assets/_Game/Prefabs/Veiculos/Carro.prefab";
        public const string Prefab = "Assets/_Game/Prefabs/Veiculos/Caminhonete.prefab";

        // as medidas da caminhonete (as do caminhonete.py, nos eixos da Unity)
        const float MeiaLargura = 0.97f, Eixo = 1.49f, XRoda = 0.83f, RaioRoda = 0.385f, MeiaLarguraDoPneu = 0.1225f;
        const float Suspensao = 0.22f, AlvoDaSuspensao = 0.5f;
        const float DY = 0.42f, DZ = -0.04f;            // o lugar do motorista é o da perua 42 cm mais alto e 4 cm para trás
        const float ZPorta = 0.95f, ZFimDaPorta = -0.37f;
        const float ZCapo = 1.06f;
        static float AlturaDoCapo(float z) => 1.275f - (z - ZCapo) * 0.155f;
        static readonly Vector3 PivoDaTampa = new Vector3(0f, 0.83f, -2.495f);
        const float PisoDaCacamba = 0.84f, ZCacamba = -0.52f, ZTraseira = -2.52f, XDentro = 0.85f, AltoDaCacamba = 1.38f;
        // o estepe (a pedido: "seria mais interessante ser mais lateral e não central, encostado em um dos lados"): de pé, do
        // lado direito, ao lado da caixa da roda, logo atrás da cabine, com o alto inclinado 6° para a lateral (o TOMBO_ESTEPE
        // do caminhonete.py)
        static readonly Vector3 Estepe = new Vector3(0.47f, 1.2397f, ZCacamba - 0.06f - RaioRoda - 0.02f);
        static readonly Quaternion GiroDoEstepe = Quaternion.Euler(0f, 0f, -6f);
        static readonly Vector3 Macaco = new Vector3(0.45f, PisoDaCacamba, -2.20f);
        // os lugares (o quadril de quem senta): os da cabine (os da perua, DY mais altos e DZ para trás) e os da caçamba
        // (sentados no piso, encostados na frente dela, lado a lado à esquerda do estepe, olhando para trás)
        static readonly Vector3[] Lugares =
        {
            new Vector3(-0.37f, 0.58f + DY, 0.06f + DZ), new Vector3(0.37f, 0.58f + DY, 0.06f + DZ),
            new Vector3(-0.52f, PisoDaCacamba + 0.11f, ZCacamba - 0.20f), new Vector3(-0.02f, PisoDaCacamba + 0.11f, ZCacamba - 0.20f),
        };
        // a força: 1.900 kg em rodas maiores. A perua (1.150 kg, rodas de 0,30) chega a ~110 km/h com 850 N·m e 44 m/s; a
        // caminhonete leva o torque na proporção da massa e do raio, para andar parecido (um pouco mais lenta no fim)
        public const float Massa = 1900f, Torque = 1750f, VelocidadeMaxima = 42f, FimDasMarchas = 30f;
        const float Freio = 4800f, FreioDeMao = 8500f;

        [MenuItem("Zombie City Cleaner/Veículos/Montar a caminhonete")]
        public static void Montar()
        {
            if (!File.Exists(Fbx)) throw new FileNotFoundException("Copie o FBX e as texturas da caminhonete (D:\\teste blender\\A Caminhonete\\export) antes.", Fbx);
            var mats = Materiais();
            Importar(mats);
            var prefab = MontarPrefab(mats);
            NaRede(prefab);
            NoMundo(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log($"Caminhonete montada ({Prefab}): na lista da rede e no MundoDaPartida da cena Mundo (o carro da família).");
        }

        /// <summary>
        /// Tudo de uma vez: as peças do carro com a chave nova (as malhas, os ícones e as fotos no jogador e no "Itens no
        /// mundo" da cena Mundo), a caminhonete e a conferência do prefab no Console. Também serve para a Unity sem janela:
        /// Unity -batchmode -quit -projectPath ... -executeMethod ZombieCityCleaner.EditorTools.MontagemDaCaminhonete.MontarTudo
        /// </summary>
        [MenuItem("Zombie City Cleaner/Veículos/Montar a caminhonete (com a chave)")]
        public static void MontarTudo()
        {
            EditorSceneManager.OpenScene(MontagemDoMundo.Cena, OpenSceneMode.Single);
            MontagemDasArmas.MontarAsPecasDoCarro();
            Montar();
            Conferir();
        }

        /// <summary>O que a montagem deixou no prefab e na cena Mundo (no Console).</summary>
        [MenuItem("Zombie City Cleaner/Veículos/Conferir a caminhonete")]
        public static void Conferir()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (go == null) { Debug.LogError("[Caminhonete] Sem o prefab."); return; }
            var c = go.GetComponent<Carro>();
            var sb = new System.Text.StringBuilder("[Caminhonete] ");
            sb.Append($"massa {go.GetComponent<Rigidbody>().mass:F0} kg, roda {c.raioDaRoda:F3}, tração atrás {c.tracaoTraseira}, caçamba {c.cacamba}, ");
            sb.Append($"lugares {c.lugaresUsados}, chave {c.chaveDoVeiculo}, lentes {c.submeshFarol}/{c.submeshLanterna}, tampa animada {c.tampaTraseira != null}; ");
            for (int i = 0; i < 4; i++)
                sb.Append($"lugar {i} {c.assentos[i].localPosition} giro {c.assentos[i].localEulerAngles.y:F0} porta {c.portas[i].name} (folha {(c.portas[i].folha != null ? c.portas[i].folha.name : "-")}, caçamba {c.portas[i].cacamba}); ");
            for (int i = 0; i < 4; i++)
                sb.Append($"roda {i} {c.rodas[i].transform.localPosition} r {c.rodas[i].radius:F3} modelo {c.rodasVisuais[i].GetComponent<MeshFilter>().sharedMesh.name}; ");
            int tris = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && mf.sharedMesh.name.StartsWith("Cam_"))
                    for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) tris += (int)mf.sharedMesh.GetIndexCount(s) / 3;
            sb.Append($"estepe {c.estepeNoPortaMalas.GetComponent<MeshFilter>().sharedMesh.name} em {c.estepeNoPortaMalas.transform.localPosition}; triângulos Cam_ {tris}; ");
            var partida = Object.FindAnyObjectByType<MundoDaPartida>();
            sb.Append(partida != null ? $"MundoDaPartida.caminhonete = {(partida.caminhonete != null ? partida.caminhonete.name : "vazio")}" : "(sem o MundoDaPartida na cena aberta)");
            Debug.Log(sb.ToString());
        }

        // ------------------------------------------------------------------------------------------ materiais e FBX
        class Mats { public Material lataria, interior, vidro, farol, lanterna, chaveiro; }

        static Mats Materiais()
        {
            foreach (var atlas in new[] { "Cam_Lataria", "Cam_Interior" })
            {
                Textura($"{PastaTexturas}/{atlas}_Cor.png", true, false);
                Textura($"{PastaTexturas}/{atlas}_MS.png", false, false);
                Textura($"{PastaTexturas}/{atlas}_Normal.png", false, true);
            }
            var r = new Mats
            {
                lataria = Pbr("Cam_Lataria", "Cam_Lataria", 0.85f),
                interior = Pbr("Cam_Interior", "Cam_Interior", 1f),
                lanterna = Pbr("Cam_Lanterna", "Cam_Lataria", 0.5f),
                vidro = Transparente("Cam_Vidro", new Color(0.09f, 0.11f, 0.13f, 0.36f), 0.93f, null),
                farol = Transparente("Cam_Farol", new Color(0.92f, 0.94f, 0.96f, 0.14f), 0.96f,
                    AssetDatabase.LoadAssetAtPath<Texture2D>($"{PastaTexturas}/Cam_Lataria_Normal.png")),
            };
            // o chaveiro da chave na ignição: o verde-escuro da chave da caminhonete (Blender/pecas.py)
            r.chaveiro = Novo("ChaveiroDaCaminhonete");
            r.chaveiro.SetColor("_BaseColor", new Color(0.18f, 0.29f, 0.23f));
            r.chaveiro.SetFloat("_Smoothness", 0.45f);
            r.chaveiro.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(r.chaveiro);
            AssetDatabase.SaveAssets();
            return r;
        }

        static void Importar(Mats m)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            imp.animationType = ModelImporterAnimationType.None;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.isReadable = false;
            imp.generateSecondaryUV = false;
            imp.bakeAxisConversion = false;
            imp.importNormals = ModelImporterNormals.Import;
            imp.importTangents = ModelImporterTangents.CalculateMikk;
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var (nome, mat) in new[] { ("Cam_Lataria", m.lataria), ("Cam_Interior", m.interior), ("Cam_Vidro", m.vidro), ("Cam_Farol", m.farol), ("Cam_Lanterna", m.lanterna) })
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), nome), mat);
            imp.SaveAndReimport();
        }

        static (Mesh malha, Material[] mats) Peca(string nome)
        {
            var mf = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx).GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(x => x.name == nome);
            if (mf == null) throw new System.Exception($"A peça {nome} não está no {Fbx}.");
            return (mf.sharedMesh, mf.GetComponent<MeshRenderer>().sharedMaterials);
        }

        // ------------------------------------------------------------------------------------------ o prefab
        static GameObject MontarPrefab(Mats m)
        {
            var raiz = PrefabUtility.LoadPrefabContents(PrefabPerua);
            try
            {
                raiz.name = "Caminhonete";
                var carro = raiz.GetComponent<Carro>();
                Transform T(string nome) => raiz.transform.Find(nome) ?? throw new System.Exception("Falta " + nome + " no prefab da perua.");

                // ---------------------------------------------------------------- o visual
                Trocar(T("Carroceria"), "Cam_Carroceria", Vector3.zero, Quaternion.identity);
                Trocar(T("Capô"), "Cam_Capo", new Vector3(0f, AlturaDoCapo(ZCapo), ZCapo), Quaternion.identity);
                // a tampa da caçamba é só desenho (a caçamba fica aberta: o Carro não a anima)
                var tampa = T("Tampa traseira");
                Trocar(tampa, "Cam_Tampa", PivoDaTampa, Quaternion.identity);
                tampa.name = "Tampa da caçamba";
                carro.tampaTraseira = null;
                Trocar(T("Volante"), "Cam_Volante", new Vector3(-0.37f, 0.86f + DY, 0.50f + DZ), Quaternion.Euler(25f, 0f, 0f));
                Trocar(T("Porta DE"), "Cam_Porta_E", new Vector3(-MeiaLargura, 0f, ZPorta), Quaternion.identity);
                Trocar(T("Porta DD"), "Cam_Porta_D", new Vector3(MeiaLargura, 0f, ZPorta), Quaternion.identity);
                string[] nomesDasRodas = { "DE", "DD", "TE", "TD" };
                for (int i = 0; i < 4; i++)
                {
                    var t = T("Roda " + nomesDasRodas[i] + " (modelo)");
                    int lado = i % 2 == 0 ? -1 : 1;
                    Trocar(t, "Cam_Roda", new Vector3(lado * XRoda, RaioRoda, i < 2 ? Eixo : -Eixo), Quaternion.Euler(0f, lado < 0 ? 180f : 0f, 0f));
                }
                // as portas de trás da perua não existem
                Object.DestroyImmediate(T("Porta TE").gameObject);
                Object.DestroyImmediate(T("Porta TD").gameObject);
                var lentes = carro.carroceria.sharedMaterials;
                carro.submeshFarol = System.Array.IndexOf(lentes, m.farol);
                carro.submeshLanterna = System.Array.IndexOf(lentes, m.lanterna);
                if (carro.submeshFarol < 0 || carro.submeshLanterna < 0) throw new System.Exception("Sem os submeshes das lentes na Cam_Carroceria.");

                // ---------------------------------------------------------------- a física: o corpo e as rodas
                var corpo = raiz.GetComponent<Rigidbody>();
                corpo.mass = Massa;
                carro.centroDeMassa = new Vector3(0f, 0.56f, 0.20f);
                carro.raioDaRoda = RaioRoda;
                carro.meiaLarguraDaRoda = MeiaLarguraDoPneu;
                carro.tracaoTraseira = true;
                carro.torque = Torque;
                carro.velocidadeMaxima = VelocidadeMaxima;
                carro.fimDasMarchas = FimDasMarchas;
                carro.freio = Freio;
                carro.freioDeMao = FreioDeMao;
                carro.giroMaximo = 30f;
                carro.antiCapotar = 12000f;
                for (int i = 0; i < 4; i++)
                {
                    var w = carro.rodas[i];
                    int lado = i % 2 == 0 ? -1 : 1;
                    // a roda pendura da posição do WheelCollider: em repouso (na metade do curso), ela fica no lugar do modelo
                    w.transform.localPosition = new Vector3(lado * XRoda, RaioRoda + Suspensao * (1f - AlvoDaSuspensao), i < 2 ? Eixo : -Eixo);
                    w.radius = RaioRoda;
                    w.mass = 30f;
                    w.suspensionDistance = Suspensao;
                    w.forceAppPointDistance = 0.15f;
                    w.suspensionSpring = new JointSpring { spring = 52000f, damper = 6000f, targetPosition = AlvoDaSuspensao };
                }

                // ---------------------------------------------------------------- as portas, a caçamba, os lugares e as saídas
                var folhas = new[] { T("Porta DE"), T("Porta DD") };
                string[] nomes = { "Porta E", "Porta D", "Caçamba E (subir)", "Caçamba D (subir)" };
                float meioDaPorta = (ZPorta + ZFimDaPorta) / 2f, compDaPorta = ZPorta - ZFimDaPorta;
                for (int i = 0; i < 4; i++)
                {
                    var porta = carro.portas[i];
                    int lado = i % 2 == 0 ? -1 : 1;
                    bool naCacamba = i >= 2;
                    porta.folha = naCacamba ? null : folhas[i];
                    porta.cacamba = naCacamba;
                    porta.lado = lado;
                    // cada subida da caçamba serve os dois lugares de lá: ocupado o do lado dela, o outro
                    porta.lugarDeTras = naCacamba ? (i == 2 ? 3 : 2) : -1;
                    porta.name = nomes[i];
                    var bc = porta.GetComponent<BoxCollider>();
                    bc.enabled = true;
                    // a porta: a chapa inteira; a caçamba: a lateral atrás da roda, embaixo da beirada (a mira acima dela
                    // fica para o estepe e o macaco)
                    bc.center = naCacamba ? new Vector3(lado * 1.05f, 0.92f, -2.17f) : new Vector3(lado * 1.05f, 0.98f, meioDaPorta);
                    bc.size = naCacamba ? new Vector3(0.16f, 0.72f, 0.58f) : new Vector3(0.14f, 0.95f, compDaPorta - 0.06f);
                    carro.assentos[i].localPosition = Lugares[i];
                    carro.assentos[i].localRotation = naCacamba ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                    carro.saidas[i].localPosition = new Vector3(lado * 1.65f, 0f, naCacamba ? -1.75f : 0.30f);
                }
                carro.lugaresUsados = 4;
                carro.cacamba = true;
                carro.primeiroLugarNaCacamba = 2;
                carro.pisoDaCacamba = PisoDaCacamba;
                carro.tetoPorDentro = 1.79f;

                // ---------------------------------------------------------------- a colisão
                // a parte de baixo (até o piso da caçamba), a frente (embaixo do capô), o painel, a cabine, o para-brisa
                // inclinado e as paredes da caçamba (aberta em cima: o que cai lá dentro fica)
                var colisao = T("Colisão");
                Caixa(colisao, "Baixo", new Vector3(0f, 0.63f, -0.165f), new Vector3(1.92f, 0.42f, 5.0f), Quaternion.identity);
                Caixa(colisao, "Frente", new Vector3(0f, 0.98f, 1.64f), new Vector3(1.92f, 0.28f, 1.32f), Quaternion.identity);
                Caixa(colisao, "Painel", new Vector3(0f, 1.10f, 0.70f), new Vector3(1.92f, 0.52f, 0.50f), Quaternion.identity);
                Caixa(colisao, "Cabine", new Vector3(0f, 1.35f, -0.015f), new Vector3(1.92f, 1.02f, 0.93f), Quaternion.identity);
                Caixa(colisao, "Para-brisa", new Vector3(0f, 1.60f, 0.70f), new Vector3(1.80f, 0.10f, 0.72f), Quaternion.Euler(42.7f, 0f, 0f));
                for (int s = -1; s <= 1; s += 2)
                    Caixa(colisao, s < 0 ? "Caçamba E" : "Caçamba D", new Vector3(s * (XDentro + MeiaLargura) / 2f, (PisoDaCacamba + AltoDaCacamba) / 2f, (ZCacamba + ZTraseira) / 2f),
                          new Vector3(MeiaLargura - XDentro, AltoDaCacamba - PisoDaCacamba, ZCacamba - ZTraseira), Quaternion.identity);
                Caixa(colisao, "Caçamba frente", new Vector3(0f, (PisoDaCacamba + AltoDaCacamba) / 2f, ZCacamba - 0.0325f), new Vector3(2f * XDentro, AltoDaCacamba - PisoDaCacamba, 0.065f), Quaternion.identity);
                Caixa(colisao, "Caçamba tampa", new Vector3(0f, (PisoDaCacamba + AltoDaCacamba) / 2f, ZTraseira + 0.025f), new Vector3(2f * XDentro, AltoDaCacamba - PisoDaCacamba, 0.05f), Quaternion.identity);
                var obst = raiz.GetComponent<NavMeshObstacle>();
                obst.center = new Vector3(0f, 0.95f, -0.165f);
                obst.size = new Vector3(1.96f, 1.90f, 5.05f);

                // ---------------------------------------------------------------- os gatilhos do capô, da caçamba e das rodas
                var capo = T("Capô (mira)").GetComponent<BoxCollider>();
                capo.center = new Vector3(0f, 1.27f, 1.62f);
                capo.size = new Vector3(1.55f, 0.12f, 1.05f);
                // a caçamba: um gatilho em volta dela toda (com as paredes: a mira o acha antes delas). Sem o estepe ou o
                // macaco na mira, ele não tem dica e a mira passa (para a subida, a tampa do tanque, as rodas)
                var malas = T("Porta-malas (mira)");
                malas.name = "Caçamba (mira)";
                var bcm = malas.GetComponent<BoxCollider>();
                bcm.center = new Vector3(0f, 1.15f, (ZCacamba + ZTraseira) / 2f);
                bcm.size = new Vector3(2.10f, 0.80f, 2.20f);
                carro.pontoDoPortaMalas = new Vector3(0f, 1.1f, -1.5f);
                foreach (Transform g in T("Rodas (mira)"))
                {
                    var bc = g.GetComponent<BoxCollider>();
                    bc.center = new Vector3(Mathf.Sign(bc.center.x) * 1.05f, 0.40f, Mathf.Sign(bc.center.z) * Eixo);
                    bc.size = new Vector3(0.16f, 0.78f, 0.96f);
                }
                // o estepe (a roda da caminhonete, de pé, do lado direito, logo atrás da cabine) e o macaco deitado no piso,
                // perto da tampa
                var estepe = T("Estepe");
                Trocar(estepe, "Cam_Roda", Estepe, GiroDoEstepe);
                T("Macaco").localPosition = Macaco;
                // o macaco apoiado encosta embaixo do chassi, ao lado da roda
                carro.macacoX = 0.62f;
                carro.baseDaLataria = 0.50f;
                carro.macacoDaRoda = 0.55f;
                carro.alturaDoMacaco = 0.30f;

                // ---------------------------------------------------------------- a chave, o tanque e o nome
                var so = new SerializedObject(carro);
                var chave = so.FindProperty("chaveDoVeiculo");
                if (chave != null)
                {
                    chave.intValue = (int)Loot.Item.ChaveDaCaminhonete;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                if (carro.chave != null)
                    foreach (var r in carro.chave.GetComponentsInChildren<MeshRenderer>(true))
                        if (r.name == "Chaveiro") r.sharedMaterial = m.chaveiro;
                carro.nomeDoVeiculo = "Caminhonete";
                carro.ondeFicaAIgnicao = "à direita do volante";
                // a tampa do tanque: a do modelo (à direita, atrás da roda de trás); um tanque grande
                carro.tampaDoTanque = new Vector3(MeiaLargura + 0.003f, 1.12f, -2.22f);
                carro.desenharTampa = false;
                carro.desgaste.tanque = 90f;
                carro.desgaste.reserva = 6f;

                // ---------------------------------------------------------------- o lugar do motorista, as luzes e o cofre
                T("Painel (mostradores)").localPosition = new Vector3(-0.37f, 0.85f + DY, 0.626f + DZ);
                T("Ignição").localPosition = new Vector3(-0.23f, 0.655f + DY, 0.62f + DZ);
                T("Luz do painel").localPosition = new Vector3(-0.25f, 1.64f, 0.32f);
                T("Lente da luz de teto").localPosition = new Vector3(0f, 1.79f, -0.05f);
                T("Luz de teto").localPosition = new Vector3(0f, 1.76f, -0.05f);
                T("Farol E").SetLocalPositionAndRotation(new Vector3(-0.675f, 0.965f, 2.22f), Quaternion.Euler(5f, -3f, 0f));
                T("Farol D").SetLocalPositionAndRotation(new Vector3(0.675f, 0.965f, 2.22f), Quaternion.Euler(5f, 3f, 0f));
                T("Buzina").localPosition = new Vector3(0f, 0.62f, 2.15f);
                T("Motor").localPosition = new Vector3(0f, 0.95f, 1.60f);
                T("Arranque").localPosition = new Vector3(0.25f, 0.85f, 1.45f);
                // o cofre (o mesmo de todos os carros), do tamanho de sempre, um pouco abaixo do fundo do cofre da caminhonete e
                // para trás: o capô dela desce para a frente, e o radiador (de pé, 0,355 m) passava do capô fechado (a pedido,
                // 15/11/2026: "o radiador fica para fora no capô"; com 15% a mais e o fundo em 0,811, o alto dele ficava em
                // 1,22 e o capô por baixo em 1,11). Agora o alto fica em 1,10, 1,5 cm abaixo do capô
                var cofre = T("Cofre do motor");
                cofre.localPosition = new Vector3(0f, 0.745f, 1.50f);
                cofre.localScale = Vector3.one;

                PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
                AssetDatabase.ImportAsset(Prefab, ImportAssetOptions.ForceUpdate);
                return AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
        }

        static void Trocar(Transform t, string peca, Vector3 pos, Quaternion rot)
        {
            var (malha, mats) = Peca(peca);
            t.SetLocalPositionAndRotation(pos, rot);
            t.GetComponent<MeshFilter>().sharedMesh = malha;
            t.GetComponent<MeshRenderer>().sharedMaterials = mats;
        }

        static void Caixa(Transform pai, string nome, Vector3 centro, Vector3 tamanho, Quaternion rot)
        {
            var t = pai.Find(nome);
            if (t == null)
            {
                t = new GameObject(nome) { layer = Carro.CamadaVeiculo }.transform;
                t.SetParent(pai, false);
                t.gameObject.AddComponent<BoxCollider>();
            }
            t.SetLocalPositionAndRotation(centro, rot);
            var bc = t.GetComponent<BoxCollider>();
            bc.center = Vector3.zero;
            bc.size = tamanho;
        }

        // ------------------------------------------------------------------------------------------ a rede e a cena
        static void NaRede(GameObject prefab)
        {
            var lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (lista != null && !lista.PrefabList.Any(p => p.Prefab == prefab))
            {
                lista.Add(new NetworkPrefab { Prefab = prefab });
                EditorUtility.SetDirty(lista);
            }
        }

        /// <summary>No `MundoDaPartida` da cena Mundo: a caminhonete é o carro da família no começo da partida.</summary>
        static void NoMundo(GameObject prefab)
        {
            var cena = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Mundo.unity", OpenSceneMode.Single);
            var partida = cena.GetRootGameObjects().Select(g => g.GetComponentInChildren<MundoDaPartida>(true)).FirstOrDefault(p => p != null);
            if (partida == null) { Debug.LogWarning("Sem o MundoDaPartida na cena Mundo: a caminhonete não foi posta no mundo."); return; }
            partida.caminhonete = prefab;
            EditorUtility.SetDirty(partida);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
        }

        // ------------------------------------------------------------------------------------------ texturas e materiais
        static void Textura(string caminho, bool srgb, bool normal)
        {
            var imp = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (imp == null) throw new FileNotFoundException("Textura da caminhonete não encontrada.", caminho);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = srgb;
            imp.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }

        static Material Novo(string nome)
        {
            Directory.CreateDirectory(PastaMateriais);
            string caminho = $"{PastaMateriais}/{nome}.mat";
            var s = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, caminho); }
            m.shader = s;
            m.shaderKeywords = new string[0];
            m.enableInstancing = true;
            return m;
        }

        static Material Pbr(string nome, string atlas, float oclusao)
        {
            var m = Novo(nome);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{PastaTexturas}/{atlas}_Cor.png"));
            m.SetColor("_BaseColor", Color.white);
            var ms = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PastaTexturas}/{atlas}_MS.png");
            m.SetTexture("_MetallicGlossMap", ms);
            m.SetFloat("_Metallic", 1f);
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{PastaTexturas}/{atlas}_Normal.png"));
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_OcclusionMap", ms);
            m.SetFloat("_OcclusionStrength", oclusao);
            m.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Transparente(string nome, Color cor, float suavidade, Texture normal)
        {
            var m = Novo(nome);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)CullMode.Back);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", cor);
            m.SetFloat("_Smoothness", suavidade);
            m.SetFloat("_Metallic", 0f);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("DepthNormals", false);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
