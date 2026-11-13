using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Vehicles;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O painel do Lakewood (a cena Lakewood, Plano_Lakewood), ao lado do menu do desenvolvedor: onde você está (o setor, a
    /// altura do chão, a água e a velocidade), quantos blocos estão montados; a neblina (ligada ou não, nesta máquina; o N);
    /// voar (F2); a régua (marcar um ponto e ver a distância e a diferença de altura até ele); ir para um ponto (as
    /// coordenadas, a partida, o meio, a barragem, as cidades, os lugares e as ilhas); e trazer o carro.
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        static LakewoodNaCena Lakewood => LakewoodNaCena.Instancia;

        Vector3? _marcaLw;
        Vector3 _ondeEstavaLw;
        float _velocidadeLw;
        List<(string nome, Vector2 ponto)> _lugaresDeLakewood;

        void AtalhosDeLakewood(Keyboard teclado)
        {
            if (Lakewood == null || Mapa != null) return;
            // a velocidade (km/h), a pé ou no carro
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                var agora = transform.position;
                float v = new Vector2(agora.x - _ondeEstavaLw.x, agora.z - _ondeEstavaLw.z).magnitude / dt * 3.6f;
                _velocidadeLw = Mathf.Lerp(_velocidadeLw, v < 1000f ? v : 0f, Mathf.Clamp01(dt * 4f));
                _ondeEstavaLw = agora;
            }
            if (teclado == null || MenuOnline.Aberto) return;
            if (teclado.f2Key.wasPressedThisFrame) Voar(!Movimento.Voando);
            if (teclado.nKey.wasPressedThisFrame) TrocarANeblinaDeLakewood();
        }

        void TrocarANeblinaDeLakewood()
        {
            AmbienteDoDia.SemNeblina = !AmbienteDoDia.SemNeblina;
            Avisar(AmbienteDoDia.SemNeblina ? "Sem neblina: a câmera vê o condado inteiro (N liga de novo)." : "Com a neblina.");
        }

        void IrParaEmLakewood(float x, float z)
        {
            var chao = Lakewood.Chao;
            var mapa = Lakewood.mapa;
            x = Mathf.Clamp(x, 30f, mapa.largura - 30f);
            z = Mathf.Clamp(z, 30f, mapa.comprimento - 30f);
            float h = chao.Altura(x, z);
            if (chao.Agua(x, z, out float nivel)) h = Mathf.Max(h, nivel);
            GetComponent<PassageiroDoCarro>().Soltar();
            // se a colisão ainda não estiver pronta, o LakewoodNaCena devolve o jogador à superfície
            Movimento.Teleportar(new Vector3(x, Movimento.Voando ? Mathf.Max(transform.position.y, h + 0.3f) : h + 0.3f, z), transform.eulerAngles.y);
            Avisar($"Em x {x:F0}, z {z:F0} (setor {mapa.NomeDoSetorEm(x, z)}).");
        }

        /// <summary>Os lugares do "ir para": a partida, o meio, a barragem, as cidades, os lugares e as ilhas.</summary>
        List<(string nome, Vector2 ponto)> LugaresDeLakewood()
        {
            if (_lugaresDeLakewood != null) return _lugaresDeLakewood;
            var d = Lakewood.mapa;
            var l = new List<(string, Vector2)> { ("A partida", new Vector2(Lakewood.partida.x, Lakewood.partida.z)), ("O meio do mapa", d.Meio) };
            if (d.represa.Tem) l.Add((d.represa.nome, d.represa.Meio + Lakewood.Chao.Relevo.Jusante * 120f));
            foreach (var c in d.cidades) l.Add((c.nome, c.centro));
            foreach (var lg in d.lugares) l.Add((lg.nome, lg.ponto));
            foreach (var il in d.ilhas)
                if (il.tipo != TipoDeIlha.Ilhote && il.contorno.Count >= 3) l.Add((il.nome, MapaDeLakewood.CentroDe(il.contorno)));
            _lugaresDeLakewood = l;
            return l;
        }

        /// <summary>Pede ao anfitrião o carro livre mais perto, 4,5 m à frente.</summary>
        [Rpc(SendTo.Server)]
        void TrazerOCarroEmLakewoodRpc(RpcParams parametros = default)
        {
            if (!Aceita(parametros) || Lakewood == null) return;
            Carro escolhido = null;
            float menor = float.MaxValue;
            foreach (var c in Carro.Todos)
            {
                if (c == null || !c.IsSpawned || c.Ocupado(0) || c.Ocupado(1) || c.Ocupado(2) || c.Ocupado(3)) continue;
                float d2 = (c.transform.position - transform.position).sqrMagnitude;
                if (d2 < menor) { menor = d2; escolhido = c; }
            }
            if (escolhido == null) return;
            var frente = transform.forward;
            frente.y = 0f;
            var ponto = transform.position + frente.normalized * 4.5f;
            ponto.y = Lakewood.Chao.Altura(ponto.x, ponto.z) + 0.6f;
            var corpo = escolhido.GetComponent<Rigidbody>();
            corpo.linearVelocity = Vector3.zero;
            corpo.angularVelocity = Vector3.zero;
            corpo.position = ponto;
            corpo.rotation = Quaternion.LookRotation(Vector3.Cross(frente, Vector3.up));
            escolhido.transform.SetPositionAndRotation(ponto, corpo.rotation);
        }

        /// <summary>O painel, à direita do menu (nada fora da cena do Lakewood). Devolve onde ele ficou.</summary>
        Rect PainelDeLakewood(float u, Rect menu)
        {
            var lw = Lakewood;
            if (lw == null || lw.Chao == null) return default;
            var lugares = LugaresDeLakewood();
            int linhasDeLugares = (lugares.Count + 1) / 2;
            var area = new Rect(menu.xMax + 8 * u, menu.y, 300 * u, (330 + 32 * linhasDeLugares) * u);
            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.72f, 0.58f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float x = area.x + 12 * u, w = area.width - 24 * u, y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "LAKEWOOD", _titulo);
            y += 26 * u;
            var p = transform.position;
            float chao = lw.Chao.Altura(p.x, p.z);
            string agua = lw.Chao.Agua(p.x, p.z, out float nivel) ? $", água {nivel:F1}" : "";
            GUI.Label(new Rect(x, y, w, 72 * u),
                $"Você: x {p.x:F0}, z {p.z:F0}, altura {p.y:F1} · setor {lw.SetorDe(p)}\nchão {chao:F1}{agua} · {_velocidadeLw:F0} km/h\n" +
                $"{lw.Blocos} blocos ({lw.BlocosComColisao} com colisão), {lw.PlantasNaVista} plantas", _texto);
            y += 76 * u;
            float bw = (w - 8 * u) / 2f;
            if (GUI.Button(new Rect(x, y, bw, 28 * u), AmbienteDoDia.SemNeblina ? "Ligar a neblina (N)" : "Tirar a neblina (N)", _botao)) TrocarANeblinaDeLakewood();
            if (GUI.Button(new Rect(x + bw + 8 * u, y, bw, 28 * u), Movimento.Voando ? "Parar de voar (F2)" : "Voar (F2)", _botao)) Voar(!Movimento.Voando);
            y += 36 * u;

            // a régua: marcar um ponto e medir até ele
            if (GUI.Button(new Rect(x, y, bw, 26 * u), "Marcar aqui", _botao))
            {
                _marcaLw = p;
                Avisar($"Marca em x {p.x:F0}, z {p.z:F0}.");
            }
            if (GUI.Button(new Rect(x + bw + 8 * u, y, bw, 26 * u), "Tirar a marca", _botao)) _marcaLw = null;
            y += 30 * u;
            string regua = "Marque um ponto para medir até ele.";
            if (_marcaLw is Vector3 m)
            {
                float dh = new Vector2(p.x - m.x, p.z - m.z).magnitude;
                regua = $"Até a marca: {dh:F1} m ({dh / 1609.34f:F2} mi), altura {p.y - m.y:+0.0;-0.0} m";
            }
            GUI.Label(new Rect(x, y, w, 22 * u), regua, _texto);
            y += 28 * u;

            GUI.Label(new Rect(x, y + 4 * u, 50 * u, 22 * u), "Ir para", _texto);
            GUI.Label(new Rect(x + 52 * u, y + 4 * u, 14 * u, 22 * u), "x", _texto);
            _irX = GUI.TextField(new Rect(x + 64 * u, y, 62 * u, 26 * u), _irX, 6, _campo);
            GUI.Label(new Rect(x + 132 * u, y + 4 * u, 14 * u, 22 * u), "z", _texto);
            _irZ = GUI.TextField(new Rect(x + 144 * u, y, 62 * u, 26 * u), _irZ, 6, _campo);
            if (GUI.Button(new Rect(x + 212 * u, y, w - 212 * u, 26 * u), "Ir", _botao))
            {
                if (float.TryParse(_irX, out float ix) && float.TryParse(_irZ, out float iz)) IrParaEmLakewood(ix, iz);
                else Avisar("Escreva os dois números (em metros; a origem é o canto sudoeste).");
            }
            y += 34 * u;
            for (int i = 0; i < lugares.Count; i++)
            {
                var r = new Rect(x + (i % 2) * (bw + 8 * u), y + (i / 2) * 32 * u, bw, 26 * u);
                if (!GUI.Button(r, lugares[i].nome, _botao)) continue;
                if (i == 0)
                {
                    var ponto = PontosDeNascimento.Ponto(OwnerClientId);
                    GetComponent<PassageiroDoCarro>().Soltar();
                    Movimento.Teleportar(ponto.position, ponto.rotation.eulerAngles.y);
                    Avisar("Na partida.");
                }
                else IrParaEmLakewood(lugares[i].ponto.x, lugares[i].ponto.y);
            }
            y += linhasDeLugares * 32 * u + 4 * u;
            if (GUI.Button(new Rect(x, y, w, 26 * u), "Trazer o carro", _botao))
            {
                TrazerOCarroEmLakewoodRpc();
                Avisar("O carro livre mais perto vem para a sua frente.");
            }
            return area;
        }
    }
}
