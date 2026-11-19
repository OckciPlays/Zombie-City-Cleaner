using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Sentado no chão perto da fogueira (18/11/2026, a etapa 1 da fogueira): a mesma pose de quem vai sentado no piso da
    /// caçamba (uma perna esticada e a outra dobrada, as mãos na coxa e no joelho), com o quadril a uns 14 cm do chão e o
    /// tronco um pouco para a frente, para o fogo. Em todas as máquinas, pelo <see cref="JogadorRede.SentadoNoChao"/> (fora do
    /// carro: o LateUpdate daqui só cuida dela quando ele está a pé).
    /// </summary>
    public partial class PassageiroDoCarro
    {
        const float QuadrilNoChao = 0.14f;
        bool _noChaoAntes;

        void SentadoNoChao()
        {
            bool sentado = _rede != null && _rede.IsSpawned && _rede.SentadoNoChao.Value && _rede.animadorCorpo != null;
            if (!sentado)
            {
                if (_noChaoAntes)
                {
                    _noChaoAntes = false;
                    if (_rede != null && _rede.animadorCorpo != null) _rede.animadorCorpo.enabled = true;
                }
                return;
            }
            _noChaoAntes = true;
            PoseNoChao();
        }

        void PoseNoChao()
        {
            var animador = _rede.animadorCorpo;
            if (_repouso.Count == 0) return;
            if (animador.enabled) animador.enabled = false;
            foreach (var (osso, pos, rot) in _repouso) osso.SetLocalPositionAndRotation(pos, rot);
            Vector3 frente = transform.forward, cima = transform.up, direita = transform.right;
            // o quadril desce até o chão (a raiz fica em pé: é a cápsula do jogador)
            Vector3 assento = transform.position + cima * QuadrilNoChao;
            if (_ossos.TryGetValue("hips", out var quadril))
            {
                Vector3 doRepouso = transform.TransformPoint(_quadrilNaRaiz);
                quadril.position += assento - doRepouso;
            }
            if (_ossos.TryGetValue("spine", out var coluna))
                coluna.rotation = Quaternion.AngleAxis(8f, direita) * coluna.rotation;   // (um pouco para a frente, para o fogo)
            // a direita esticada, o calcanhar no chão
            Vector3 tornozeloD = assento + frente * 0.80f + direita * 0.13f - cima * (QuadrilNoChao - 0.06f);
            DoisOssos("thigh.R", "shin.R", tornozeloD, cima + frente * 0.2f);
            PeNoPlano(".R", frente * 0.35f + cima * 0.93f);
            // a esquerda dobrada: o pé no chão, o joelho para cima
            Vector3 tornozeloE = assento + frente * 0.52f - direita * 0.13f - cima * (QuadrilNoChao - 0.07f);
            DoisOssos("thigh.L", "shin.L", tornozeloE, cima * 0.95f - direita * 0.15f);
            PeNoPlano(".L", frente * 0.95f - cima * 0.1f);
            // as mãos: a direita na coxa esticada, a esquerda com o antebraço no joelho
            if (_ossos.TryGetValue("thigh.R", out var coxaD) && _ossos.TryGetValue("shin.R", out var joelhoD))
            {
                DoisOssos("upper_arm.R", "forearm.R", Vector3.Lerp(coxaD.position, joelhoD.position, 0.55f) + cima * 0.08f,
                          -cima * 0.6f - frente * 0.5f + direita * 0.3f);
                Palma(".R", -cima);
            }
            if (_ossos.TryGetValue("shin.L", out var joelhoE))
            {
                DoisOssos("upper_arm.L", "forearm.L", joelhoE.position + frente * 0.08f + cima * 0.02f, -cima * 0.3f - frente * 0.3f - direita * 0.6f);
                Palma(".L", -cima);
            }
            // (18/11/2026) o corpo fica parado: o olhar vira o peito um pouco e a cabeça (o giro pela rede) e inclina a
            // cabeça para cima e para baixo (a pose de repouso apagou o que o JogadorRede tinha posto)
            float giro = Mathf.Clamp(_rede.GiroSentado.Value, -MovimentoPrimeiraPessoa.GiroSentadoMaximo, MovimentoPrimeiraPessoa.GiroSentadoMaximo);
            var peito = _rede.ossoPeito;
            if (peito != null) peito.rotation = Quaternion.AngleAxis(giro * 0.35f, cima) * peito.rotation;
            if (_ossos.TryGetValue("head", out var cabeca))
            {
                cabeca.rotation = Quaternion.AngleAxis(Mathf.Clamp(giro * 0.65f, -GiroDaCabeca, GiroDaCabeca), cima) * cabeca.rotation;
                Vector3 lateral = Quaternion.AngleAxis(giro, cima) * direita;
                cabeca.rotation = Quaternion.AngleAxis(Mathf.Clamp(_rede.Pitch.Value, -60f, 60f) * 0.7f, lateral) * cabeca.rotation;
            }
        }
    }

    /// <summary>
    /// A pose de quem toca (18/11/2026, a etapa 4 da fogueira, a pedido: "fazer algumas animações para ficar legal também").
    /// Em todas as máquinas, depois da animação (e da pose de sentado no chão), pelo <see cref="JogadorRede.Tocando"/>: o
    /// instrumento sai da mão (o lugar da lata) e vai para o corpo, e os braços vão até ele com o IK de dois ossos daqui,
    /// no tempo da música (<see cref="MusicaDoJogador.Batida"/>):
    /// - o violão atravessado na barriga, o braço dele para a esquerda e um pouco para cima: a esquerda no braço (troca de
    ///   posição a cada compasso, como quem muda de acorde) e a direita batendo nas cordas a cada tempo (no dedilhado, os
    ///   dedos mexem de leve, em colcheias);
    /// - o pandeiro na esquerda, na frente do peito, a pele virada para a direita, que bate nele a cada tempo; ele chacoalha;
    /// - a gaita na boca, nas duas mãos em concha, correndo de um lado para o outro devagar;
    /// - a flauta na boca, para a frente e para baixo, as duas mãos nela, os dedos subindo e descendo.
    /// O dono, em primeira pessoa, vê o próprio corpo tocando (os braços de primeira pessoa somem, como na moto).
    /// </summary>
    public partial class PassageiroDoCarro
    {
        Transform _instrumento;
        MeshRenderer _rInstrumento;
        Item _instrumentoMontado;
        bool _tocandoAntes;
        GameObject _maoEscondidaPeloInstrumento;

        static Item ItemDoInstrumento(int i) => i switch { 1 => Item.Violao, 2 => Item.Pandeiro, 3 => Item.Gaita, 4 => Item.Flauta, _ => Item.Nada };

        void PoseDoInstrumento()
        {
            var t = _rede != null && _rede.IsSpawned ? _rede.Tocando.Value : default;
            bool tocando = t.instrumento != 0 && _rede.animadorCorpo != null && _ossos.Count > 0;
            if (!tocando)
            {
                if (_tocandoAntes) PararDeTocar();
                return;
            }
            var item = ItemDoInstrumento(t.instrumento);
            if (!_tocandoAntes || item != _instrumentoMontado) ComecarATocar(item);
            _tocandoAntes = true;
            // o que está na mão (o lugar da lata) some: o instrumento está no corpo
            int m = Itens.MaoDe(item);
            if (_rede.naMaoCorpo != null && m >= 0 && m < _rede.naMaoCorpo.Length && _rede.naMaoCorpo[m] != null && _rede.naMaoCorpo[m].activeSelf)
            {
                _rede.naMaoCorpo[m].SetActive(false);
                _maoEscondidaPeloInstrumento = _rede.naMaoCorpo[m];
            }
            // o dono em primeira pessoa vê o próprio corpo (sem a cabeça) e não os braços de primeira pessoa
            if (_rede.IsOwner && !_rede.EmTerceiraPessoa)
            {
                _rede.CorpoNoVeiculo(true);
                if (_rede.cameraBracos != null && _rede.cameraBracos.enabled) _rede.cameraBracos.enabled = false;
            }
            // o instrumento como a lança do corpo: escondido, ou só a sombra, como o corpo
            var pele = _rede.renderersCorpo != null && _rede.renderersCorpo.Length > 0 ? _rede.renderersCorpo[0] : null;
            if (pele != null)
            {
                _rInstrumento.shadowCastingMode = pele.shadowCastingMode;
                _rInstrumento.renderingLayerMask = pele.renderingLayerMask;
                _rInstrumento.enabled = pele.enabled;
            }
            float batida = MusicaDoJogador.Batida(t);
            switch (item)
            {
                case Item.Violao: PoseDoViolao(batida, t.musica); break;
                case Item.Pandeiro: PoseDoPandeiro(batida); break;
                case Item.Gaita: PoseDaGaita(batida); break;
                case Item.Flauta: PoseDaFlauta(batida); break;
            }
        }

        void ComecarATocar(Item item)
        {
            if (_instrumento == null)
            {
                var go = new GameObject("Instrumento tocando");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>();
                _rInstrumento = go.AddComponent<MeshRenderer>();
                _instrumento = go.transform;
            }
            _instrumento.gameObject.SetActive(true);
            _instrumento.GetComponent<MeshFilter>().sharedMesh = ItensDaCaca.MalhaDoItem(item);
            var itens = ItensNoMundo.Instancia;
            if (itens != null) _rInstrumento.sharedMaterial = itens.MaterialDe(item);
            if (_rede.animadorCorpo != null) _instrumento.gameObject.layer = _rede.animadorCorpo.gameObject.layer;
            _instrumentoMontado = item;
        }

        void PararDeTocar()
        {
            _tocandoAntes = false;
            if (_instrumento != null) _instrumento.gameObject.SetActive(false);
            // o que estava na mão volta (se ainda é o que está nela)
            if (_maoEscondidaPeloInstrumento != null && !_rede.NaMaoEscondida && _rede.Inventario != null && MusicaDoJogador.EhInstrumento(_rede.Inventario.NaMao))
                _maoEscondidaPeloInstrumento.SetActive(true);
            _maoEscondidaPeloInstrumento = null;
            if (_rede.IsOwner && _fase == Fase.Fora)
            {
                _rede.CorpoNoVeiculo(false);
                if (_rede.cameraBracos != null && !_rede.EmTerceiraPessoa) _rede.cameraBracos.enabled = true;
            }
        }

        // o peito, a boca e os eixos do corpo (o giro do corpo; o peito e a cabeça, da animação)
        Vector3 PeitoDeQuemToca => _rede.ossoPeito != null ? _rede.ossoPeito.position : transform.position + transform.up * 1.35f;
        Vector3 BocaDeQuemToca => _rede.ossoCabeca != null ? _rede.ossoCabeca.position + transform.up * 0.03f + transform.forward * 0.11f : transform.position + transform.up * 1.6f + transform.forward * 0.11f;

        // põe o instrumento: o ponto 'local' da malha vai em 'onde', com os eixos X e Y dela em 'x' e 'y' (no mundo)
        Quaternion PorOInstrumento(Vector3 local, Vector3 onde, Vector3 x, Vector3 y, float escala = 1f)
        {
            y = Vector3.ProjectOnPlane(y, x).normalized;
            x.Normalize();
            var rot = Quaternion.LookRotation(Vector3.Cross(x, y), y);
            _instrumento.localScale = Vector3.one * escala;
            _instrumento.SetPositionAndRotation(onde - rot * (local * escala), rot);
            return rot;
        }

        Vector3 NoInstrumento(Vector3 local) => _instrumento.TransformPoint(local);

        void Braco(string l, Vector3 pulso, Vector3 cotovelo, Vector3 palma)
        {
            DoisOssos("upper_arm" + l, "forearm" + l, pulso, cotovelo);
            Palma(l, palma);
        }

        void PoseDoViolao(float batida, int musica)
        {
            Vector3 frente = transform.forward, cima = transform.up, direita = transform.right;
            // atravessado na barriga: o braço para a esquerda, subindo 18° e vindo um pouco para a frente; o tampo para a
            // frente e um pouco para cima (18/11/2026, a pedido: "um pouco bugados": quem toca vê as cordas, e o fundo encosta
            // na barriga)
            Vector3 eixo = (-direita * Mathf.Cos(18f * Mathf.Deg2Rad) + cima * Mathf.Sin(18f * Mathf.Deg2Rad) + frente * 0.22f).normalized;
            Vector3 barriga = PeitoDeQuemToca - cima * 0.15f + frente * 0.11f + direita * 0.02f;
            var rot = PorOInstrumento(new Vector3(-0.06f, 0f, 0f), barriga, eixo, (frente + cima * 0.3f).normalized, 0.85f);
            Vector3 tampo = rot * Vector3.up, largura = rot * Vector3.forward;
            // a direita nas cordas: bate a cada tempo (no dedilhado, os dedos mexem de leve em colcheias)
            bool dedilhado = musica == 2;
            float f = batida - Mathf.Floor(batida);
            float bate = dedilhado ? 0.015f * Mathf.Sin(batida * Mathf.PI * 4f) : 0.07f * Mathf.Cos(f * Mathf.PI * 2f);
            Vector3 cordas = NoInstrumento(new Vector3(-0.12f, 0.12f, 0f));
            Vector3 pulsoD = cordas + largura * (0.09f + bate) + tampo * 0.05f;
            Braco(".R", pulsoD, direita * 0.7f - cima * 0.3f - frente * 0.35f, -tampo);
            // a esquerda no braço do violão: troca de lugar a cada compasso (os acordes)
            int compasso = Mathf.FloorToInt(batida / (musica == 1 ? 3f : 4f));
            float casa = 0.4f + 0.05f * Mathf.PingPong(compasso * 0.5f, 1f);
            Vector3 pulsoE = NoInstrumento(new Vector3(casa, 0.03f, -0.08f));
            Braco(".L", pulsoE, -direita * 0.3f - cima * 0.8f, largura * 0.7f + tampo * 0.3f);
        }

        void PoseDoPandeiro(float batida)
        {
            Vector3 frente = transform.forward, cima = transform.up, direita = transform.right;
            // em pé na frente do lado esquerdo do peito, a pele virada para a direita (para a mão que bate, que fica perto do
            // corpo), um pouco para a frente e para cima, chacoalhando (18/11/2026: com a pele para a frente, a mão ia bater
            // 42 cm à frente do peito, com o braço esticado, como quem segura uma bandeja)
            float chacoalha = 6f * Mathf.Sin(batida * Mathf.PI * 4f);
            Vector3 pele = Quaternion.AngleAxis(chacoalha, frente) * (direita * 0.85f + frente * 0.35f + cima * 0.25f).normalized;
            Vector3 meio = PeitoDeQuemToca + frente * 0.27f - direita * 0.13f + cima * 0.02f;
            var rot = PorOInstrumento(new Vector3(0f, 0.03f, 0f), meio, cima, pele, 1.05f);
            Vector3 normal = rot * Vector3.up;
            // a esquerda segura o aro por baixo
            Vector3 pulsoE = NoInstrumento(new Vector3(-0.17f, -0.03f, 0f));
            Braco(".L", pulsoE, -direita * 0.5f - cima * 0.8f, cima);
            // a direita bate na pele a cada tempo
            float f = batida - Mathf.Floor(batida);
            float sobe = 0.13f * Mathf.Pow(Mathf.Sin(f * Mathf.PI), 0.8f);
            Vector3 pulsoD = NoInstrumento(new Vector3(0.02f, 0.06f, 0f)) + normal * (0.06f + sobe);
            Braco(".R", pulsoD, direita * 0.7f - cima * 0.6f - frente * 0.1f, -normal);
        }

        void PoseDaGaita(float batida)
        {
            Vector3 frente = transform.forward, cima = transform.up, direita = transform.right;
            // na boca, correndo de um lado para o outro devagar (a cada 2 compassos)
            Vector3 boca = BocaDeQuemToca + frente * 0.035f + direita * (0.025f * Mathf.Sin(batida * Mathf.PI * 0.25f));
            PorOInstrumento(new Vector3(0f, 0.0125f, 0f), boca, direita, cima);
            // as mãos em concha, dos dois lados
            float tremor = 0.004f * Mathf.Sin(Time.time * 37f);
            Braco(".R", boca + direita * 0.07f + frente * 0.05f - cima * (0.05f + tremor), direita * 0.5f - cima * 0.8f, -direita * 0.6f - frente * 0.5f);
            Braco(".L", boca - direita * 0.07f + frente * 0.05f - cima * (0.05f - tremor), -direita * 0.5f - cima * 0.8f, direita * 0.6f - frente * 0.5f);
        }

        void PoseDaFlauta(float batida)
        {
            Vector3 frente = transform.forward, cima = transform.up, direita = transform.right;
            // na boca, para a frente e para baixo (50°, como a flauta doce; 18/11/2026: com 30° parecia um cachimbo),
            // balançando de leve com a música
            float inclina = (50f + 3f * Mathf.Sin(batida * Mathf.PI)) * Mathf.Deg2Rad;
            Vector3 saindo = (frente * Mathf.Cos(inclina) - cima * Mathf.Sin(inclina)).normalized;
            Vector3 furos = (cima * Mathf.Cos(inclina) + frente * Mathf.Sin(inclina)).normalized;
            var rot = PorOInstrumento(new Vector3(0.235f, 0.014f, 0f), BocaDeQuemToca + saindo * 0.01f, -saindo, furos);
            Vector3 cimaDela = rot * Vector3.up;
            // as duas mãos nela, os dedos subindo e descendo nos furos
            float dedos = 0.006f * Mathf.Sin(batida * Mathf.PI * 2f);
            Vector3 pulsoE = NoInstrumento(new Vector3(0.07f, 0.012f, 0f)) - cimaDela * (0.05f + dedos) - direita * 0.02f;
            Vector3 pulsoD = NoInstrumento(new Vector3(-0.08f, 0.012f, 0f)) - cimaDela * (0.05f - dedos) + direita * 0.02f;
            Braco(".L", pulsoE, -direita * 0.6f - cima * 0.8f, cimaDela);
            Braco(".R", pulsoD, direita * 0.6f - cima * 0.8f, cimaDela);
        }
    }
}
