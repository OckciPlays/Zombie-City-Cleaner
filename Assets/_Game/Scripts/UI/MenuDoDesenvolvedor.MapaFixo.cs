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
    /// O painel do mapa fixo (a cena MapaFixo, 29/10/2026), ao lado do menu do desenvolvedor: onde você está (a altura do
    /// chão e a velocidade), quantos blocos estão montados; a neblina (ligada ou não, nesta máquina; o N); voar (F2); a
    /// régua (marcar um ponto e ver a distância e a diferença de altura até ele); ir para um ponto (as coordenadas, a
    /// partida, o meio do mapa, os lagos e os bloqueios); e trazer o carro.
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        static MapaFixoNaCena Fixo => MapaFixoNaCena.Instancia;

        Vector3? _marca;
        Vector3 _ondeEstava;
        float _velocidade;
        List<(string nome, Vector2 ponto)> _lugaresDoFixo;

        void AtalhosDoMapaFixo(Keyboard teclado)
        {
            if (Fixo == null || Mapa != null) return;
            // a velocidade (km/h), a pé ou no carro
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                var agora = transform.position;
                float v = new Vector2(agora.x - _ondeEstava.x, agora.z - _ondeEstava.z).magnitude / dt * 3.6f;
                _velocidade = Mathf.Lerp(_velocidade, v < 1000f ? v : 0f, Mathf.Clamp01(dt * 4f));
                _ondeEstava = agora;
            }
            if (teclado == null || MenuOnline.Aberto) return;
            if (teclado.f2Key.wasPressedThisFrame) Voar(!Movimento.Voando);
            if (teclado.nKey.wasPressedThisFrame) TrocarANeblina();
        }

        void TrocarANeblina()
        {
            AmbienteDoDia.SemNeblina = !AmbienteDoDia.SemNeblina;
            Avisar(AmbienteDoDia.SemNeblina ? "Sem neblina: a câmera vê o mapa inteiro (N liga de novo)." : "Com a neblina.");
        }

        void IrParaNoFixo(float x, float z)
        {
            var chao = Fixo.Chao;
            float limite = DesenhoDoMapa.Metade - 30f;
            x = Mathf.Clamp(x, -limite, limite);
            z = Mathf.Clamp(z, -limite, limite);
            float h = chao.Altura(x, z);
            if (chao.Agua(x, z, out float nivel)) h = Mathf.Max(h, nivel);
            GetComponent<PassageiroDoCarro>().Soltar();
            // se a colisão ainda não estiver pronta, o MapaFixoNaCena devolve o jogador à superfície
            Movimento.Teleportar(new Vector3(x, Movimento.Voando ? Mathf.Max(transform.position.y, h + 0.3f) : h + 0.3f, z), transform.eulerAngles.y);
            Avisar($"Em x {x:F0}, z {z:F0}.");
        }

        /// <summary>Os lugares do "ir para": a partida, o meio, a margem de cada lago grande e cada bloqueio.</summary>
        List<(string nome, Vector2 ponto)> LugaresDoFixo()
        {
            if (_lugaresDoFixo != null) return _lugaresDoFixo;
            var d = Fixo.desenho;
            var l = new List<(string, Vector2)> { ("A partida", new Vector2(Fixo.partida.x, Fixo.partida.z)), ("O meio do mapa", Vector2.zero) };
            var lagos = new List<LagoDoDesenho>(d.lagos);
            lagos.Sort((a, b) => Linhas.Area(b.contorno).CompareTo(Linhas.Area(a.contorno)));
            for (int i = 0; i < Mathf.Min(4, lagos.Count); i++)
            {
                // a margem leste: o ponto do contorno mais a leste, 40 m para fora
                var leste = lagos[i].contorno[0];
                foreach (var p in lagos[i].contorno) if (p.x > leste.x) leste = p;
                l.Add(($"Margem: {lagos[i].nome}", leste + new Vector2(40f, 0f)));
            }
            foreach (var b in d.bloqueios) l.Add(($"Bloqueio: {b.nome}", b.ponto));
            _lugaresDoFixo = l;
            return l;
        }

        /// <summary>Pede ao anfitrião o carro livre mais perto, 4,5 m à frente.</summary>
        [Rpc(SendTo.Server)]
        void TrazerOCarroDoFixoRpc(RpcParams parametros = default)
        {
            if (!Aceita(parametros) || Fixo == null) return;
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
            ponto.y = Fixo.Chao.Altura(ponto.x, ponto.z) + 0.6f;
            var corpo = escolhido.GetComponent<Rigidbody>();
            corpo.linearVelocity = Vector3.zero;
            corpo.angularVelocity = Vector3.zero;
            corpo.position = ponto;
            corpo.rotation = Quaternion.LookRotation(Vector3.Cross(frente, Vector3.up));
            escolhido.transform.SetPositionAndRotation(ponto, corpo.rotation);
        }

        /// <summary>O painel, à direita do menu (nada fora da cena do mapa fixo). Devolve onde ele ficou.</summary>
        Rect PainelDoMapaFixo(float u, Rect menu)
        {
            var fixo = Fixo;
            if (fixo == null || fixo.Chao == null) return default;
            var lugares = LugaresDoFixo();
            int linhasDeLugares = (lugares.Count + 1) / 2;
            var area = new Rect(menu.xMax + 8 * u, menu.y, 300 * u, (330 + 32 * linhasDeLugares) * u);
            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.72f, 0.58f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float x = area.x + 12 * u, w = area.width - 24 * u, y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "MAPA FIXO", _titulo);
            y += 26 * u;
            var p = transform.position;
            float chao = fixo.Chao.Altura(p.x, p.z);
            string agua = fixo.Chao.Agua(p.x, p.z, out float nivel) ? $", água {nivel:F1}" : "";
            GUI.Label(new Rect(x, y, w, 72 * u),
                $"Você: x {p.x:F0}, z {p.z:F0}, altura {p.y:F1}\nchão {chao:F1}{agua} · {_velocidade:F0} km/h\n" +
                $"{fixo.Blocos} blocos ({fixo.BlocosComColisao} com colisão), {fixo.PlantasNaVista} plantas", _texto);
            y += 76 * u;
            float bw = (w - 8 * u) / 2f;
            if (GUI.Button(new Rect(x, y, bw, 28 * u), AmbienteDoDia.SemNeblina ? "Ligar a neblina (N)" : "Tirar a neblina (N)", _botao)) TrocarANeblina();
            if (GUI.Button(new Rect(x + bw + 8 * u, y, bw, 28 * u), Movimento.Voando ? "Parar de voar (F2)" : "Voar (F2)", _botao)) Voar(!Movimento.Voando);
            y += 36 * u;

            // a régua: marcar um ponto e medir até ele
            if (GUI.Button(new Rect(x, y, bw, 26 * u), "Marcar aqui", _botao))
            {
                _marca = p;
                Avisar($"Marca em x {p.x:F0}, z {p.z:F0}.");
            }
            if (GUI.Button(new Rect(x + bw + 8 * u, y, bw, 26 * u), "Tirar a marca", _botao)) _marca = null;
            y += 30 * u;
            string regua = "Marque um ponto para medir até ele.";
            if (_marca is Vector3 m)
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
                if (float.TryParse(_irX, out float ix) && float.TryParse(_irZ, out float iz)) IrParaNoFixo(ix, iz);
                else Avisar("Escreva os dois números (em metros, o meio do mapa é 0, 0).");
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
                else IrParaNoFixo(lugares[i].ponto.x, lugares[i].ponto.y);
            }
            y += linhasDeLugares * 32 * u + 4 * u;
            if (GUI.Button(new Rect(x, y, w, 26 * u), "Trazer o carro", _botao))
            {
                TrazerOCarroDoFixoRpc();
                Avisar("O carro livre mais perto vem para a sua frente.");
            }
            return area;
        }
    }
}
