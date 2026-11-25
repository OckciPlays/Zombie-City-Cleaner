using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// O limite da quarentena no Dia 0 e na noite da fuga (24/11/2026, o Pedro: "precisamos também criar um limite no mapa no
    /// d0, pois da para pegar a caminhonete e andar o mapa inteiro, vamos restringir a cidade local"): um círculo em volta
    /// da Woodcreek (o contorno da cidade e mais 70 m) que pega também o bunker da comunidade (o fim da fuga). Onde uma
    /// estrada ou rua sai do círculo, o bloqueio da polícia (os cavaletes, os cones e a placa "QUARENTENA"). Em cada
    /// máquina, o jogador desta máquina que passa do círculo volta para a beira dele, a pé ou dirigindo (o motorista é o
    /// dono do carro: o carro volta parado, virado para dentro), com o aviso na tela.
    /// </summary>
    public partial class WoodcreekDoDiaZero
    {
        /// <summary>O meio e o raio do limite (no mapa, m). Raio 0: ainda não montado.</summary>
        public Vector2 MeioDoLimite { get; private set; }
        public float RaioDoLimite { get; private set; }
        bool _limiteMontado;
        float _avisoAte;
        int _bloqueios;

        void AcompanharOLimite()
        {
            var lw = LakewoodNaCena.Instancia;
            var m = MundoDeLakewood.Instancia;
            if (lw == null || m == null || !(m.NoDiaZero || m.NaFuga)) return;
            if (!_limiteMontado) MontarOLimite(lw);
            if (RaioDoLimite <= 0f) return;
            var j = JogadorRede.Local;
            if (j == null || !j.Criado) return;
            var mov = j.GetComponent<MovimentoPrimeiraPessoa>();
            if (mov == null || mov.Voando) return;
            var noMapa = OrigemDeLakewood.NoMapa(j.transform.position);
            var p = new Vector2(noMapa.x, noMapa.z);
            var fora = p - MeioDoLimite;
            if (fora.magnitude <= RaioDoLimite) return;
            // de volta para a beira (2 m para dentro), virado para o meio
            var dentro = -fora.normalized;
            var beira = MeioDoLimite - dentro * (RaioDoLimite - 2f);
            var chao = lw.ChaoNoMapa;
            var mundo = OrigemDeLakewood.NoMundo(new Vector3(beira.x, chao.Altura(beira.x, beira.y), beira.y));
            float giro = Mathf.Atan2(dentro.x, dentro.y) * Mathf.Rad2Deg;
            var pass = j.Passageiro;
            if (pass != null && pass.NoCarro && pass.Carro != null)
            {
                // dirigindo: o carro volta (só o motorista, dono dele); de carona, quem dirige cuida
                var carro = pass.Carro;
                if (pass.Lugar == 0 && carro.IsOwner)
                {
                    var corpo = carro.GetComponent<Rigidbody>();
                    var pos = mundo + Vector3.up * 0.6f;
                    var rot = Quaternion.Euler(0f, giro, 0f);
                    if (corpo != null)
                    {
                        corpo.linearVelocity = Vector3.zero;
                        corpo.angularVelocity = Vector3.zero;
                        corpo.position = pos;
                        corpo.rotation = rot;
                    }
                    carro.transform.SetPositionAndRotation(pos, rot);
                }
            }
            else mov.Teleportar(mundo + Vector3.up * 0.3f, giro);
            _avisoAte = Time.time + 4f;
        }

        void MontarOLimite(LakewoodNaCena lw)
        {
            _limiteMontado = true;
            var chao = lw.ChaoNoMapa;
            if (chao == null || LoteDaFamilia == null) return;
            var cidade = lw.mapa.cidades.Find(c => c.nome == LoteDaFamilia.Lote.cidade);
            var meio = cidade != null ? cidade.centro : LoteDaFamilia.Lote.meio;
            float raio = 600f;
            if (cidade != null && cidade.contorno.Count > 0)
            {
                raio = 0f;
                foreach (var q in cidade.contorno) raio = Mathf.Max(raio, Vector2.Distance(meio, q));
                raio += 70f;
            }
            // o bunker da comunidade (o fim da fuga) fica dentro, com folga
            if (chao.Bunker != null) raio = Mathf.Max(raio, Vector2.Distance(meio, chao.Bunker.MeioDaPegada) + 90f);
            MeioDoLimite = meio;
            RaioDoLimite = raio;
            PorOsBloqueios(lw);
            Debug.Log($"[Dia 0] O limite da quarentena: {raio:F0} m em volta da {LoteDaFamilia.Lote.cidade} ({meio.x:F0}; {meio.y:F0}), com {_bloqueios} bloqueios nas estradas.");
        }

        // os bloqueios: onde cada estrada e cada rua cruza o círculo, os cavaletes de lado a lado, os cones e a placa
        void PorOsBloqueios(LakewoodNaCena lw)
        {
            var chao = lw.ChaoNoMapa;
            var pai = new GameObject("Os bloqueios da quarentena").transform;
            pai.SetParent(transform, false);
            var feitos = new List<Vector2>();
            void Linha(List<Vector2> pts, float largura)
            {
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    var a = pts[i];
                    var b = pts[i + 1];
                    bool da = Vector2.Distance(a, MeioDoLimite) <= RaioDoLimite, db = Vector2.Distance(b, MeioDoLimite) <= RaioDoLimite;
                    if (da == db) continue;
                    // o cruzamento com o círculo (a bissecção no trecho)
                    Vector2 de = da ? a : b, para = da ? b : a;
                    for (int k = 0; k < 20; k++)
                    {
                        var meio = (de + para) * 0.5f;
                        if (Vector2.Distance(meio, MeioDoLimite) <= RaioDoLimite) de = meio; else para = meio;
                    }
                    var dir = (para - de).normalized;
                    var ponto = de - dir * 5f;
                    bool perto = false;
                    foreach (var f in feitos) if ((f - ponto).sqrMagnitude < 40f * 40f) { perto = true; break; }
                    if (perto) continue;
                    feitos.Add(ponto);
                    Bloqueio(pai, chao, ponto, dir, Mathf.Max(7f, largura));
                }
            }
            foreach (var e in lw.mapa.estradas) if (e.pontos != null && e.pontos.Count >= 2) Linha(e.pontos, e.tipo == TipoDeVia.Rodovia ? 13f : 9f);
            foreach (var r in lw.mapa.ruas) if (r.pontos != null && r.pontos.Count >= 2) Linha(r.pontos, 8f);
            _bloqueios = feitos.Count;
        }

        void Bloqueio(Transform pai, ChaoDeLakewood chao, Vector2 ponto, Vector2 dir, float largura)
        {
            Vector3 W(Vector2 q, float acima = 0f) => OrigemDeLakewood.NoMundo(new Vector3(q.x, chao.Altura(q.x, q.y) + acima, q.y));
            var lado = new Vector2(dir.y, -dir.x);
            float giro = Mathf.Atan2(lado.x, lado.y) * Mathf.Rad2Deg;
            var go = new GameObject("Bloqueio da quarentena");
            go.transform.SetParent(pai, true);
            go.transform.position = W(ponto);
            // os cavaletes de lado a lado da pista, a cada 2,3 m
            int n = Mathf.Max(3, Mathf.CeilToInt(largura / 2.3f));
            for (int i = 0; i < n; i++)
            {
                var q = ponto + lado * ((i - (n - 1) * 0.5f) * 2.3f);
                Peca(go.transform, cavalete != null ? cavalete : barreira, W(q), giro);
            }
            // os cones antes dos cavaletes (do lado de dentro), em zigue-zague
            for (int i = 0; i < n + 1; i++)
            {
                var q = ponto - dir * (2.2f + (i % 2) * 0.8f) + lado * ((i - n * 0.5f) * 2f);
                Peca(go.transform, cone, W(q), giro);
            }
            // a placa no meio, de frente para quem vem de dentro
            var meio = ponto - dir * 0.6f;
            var placa = new GameObject("Placa da quarentena").transform;
            placa.SetParent(go.transform, true);
            placa.SetPositionAndRotation(W(meio, 1.55f), Quaternion.Euler(0f, Mathf.Atan2(-dir.x, -dir.y) * Mathf.Rad2Deg, 0f));
            var obra = new Obra(semente + 31, Medidas.DoJogo);
            obra.Caixa(new Vector3(0f, 0f, -0.04f), new Vector3(2.6f, 0.75f, 0.05f), "PlacaAmarela");
            obra.Caixa(new Vector3(-1.1f, -0.75f, -0.06f), new Vector3(0.08f, 1.5f, 0.08f), "Metal");
            obra.Caixa(new Vector3(1.1f, -0.75f, -0.06f), new Vector3(0.08f, 1.5f, 0.08f), "Metal");
            var malha = new GameObject("Placa");
            malha.transform.SetParent(placa, false);
            malha.AddComponent<MeshFilter>().sharedMesh = obra.vis.ParaMesh("Placa da quarentena");
            malha.AddComponent<MeshRenderer>().sharedMaterial = material;
            MalhaDeObra.Devolver(obra.vis);
            MalhaDeObra.Devolver(obra.col);
            Escrever(placa, new Vector3(0f, 0.13f, 0f), "QUARENTENA", 0.24f, ModelosDaEstrada.LetraVermelha);
            Escrever(placa, new Vector3(0f, -0.18f, 0f), "ESTRADA FECHADA PELA POLÍCIA", 0.11f, ModelosDaEstrada.LetraEscura);
        }

        void Peca(Transform pai, Mesh malha, Vector3 pos, float giro)
        {
            if (malha == null) return;
            var go = new GameObject(malha.name);
            go.transform.SetParent(pai, true);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, giro, 0f));
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        // o aviso na tela, embaixo do meio, como as falas
        void AvisoDoLimite()
        {
            if (Time.time > _avisoAte) return;
            float u = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            var estilo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(20 * u), alignment = TextAnchor.MiddleCenter, wordWrap = true, fontStyle = FontStyle.Bold };
            var r = new Rect(Screen.width * 0.15f, Screen.height * 0.62f, Screen.width * 0.7f, 70 * u);
            estilo.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), "A cidade está em quarentena: a polícia fechou as estradas. Não dá para sair da Woodcreek.", estilo);
            estilo.normal.textColor = new Color(1f, 0.85f, 0.4f);
            GUI.Label(r, "A cidade está em quarentena: a polícia fechou as estradas. Não dá para sair da Woodcreek.", estilo);
        }

        void OnGUI() => AvisoDoLimite();
    }
}
