using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A pose do curativo (05/11/2026, a fase 3 do sistema de vida, a pedido: "animação individual e específica para cada
    /// membro"; feita por código, sem clipe novo), em todas as máquinas, pelo <see cref="VidaJogador.Tratando"/> que vem
    /// pela rede: depois da animação, o corpo se inclina e as mãos vão até a parte tratada, dando voltas como quem enrola a
    /// faixa. A perna: agachado, o tronco desce e as duas mãos vão à canela (a arma some da mão enquanto isso). O braço: ele estica o braço ferido para a frente e a outra
    /// mão dá a volta nele. A cabeça: as duas mãos na cabeça. O tronco: as mãos no peito. O que se toma: a mão direita na
    /// boca. No amigo: inclinado para a frente, as mãos na altura dele. As mãos chegam por um IK de dois ossos (o ombro, o
    /// cotovelo e o pulso), com o cotovelo para fora.
    /// <para>No dono, na primeira pessoa, os braços da lata aparecem com o curativo na mão (mesmo começado pela mochila,
    /// com a arma na mão) e descem um pouco, dando voltas; a câmera olha para a parte (<see cref="EfeitosDoCorpo"/>). No fim,
    /// a mão volta para o que estava nela.</para>
    /// </summary>
    [DefaultExecutionOrder(105)]   // depois do JogadorRede (que gira o peito e a cabeça pelo olhar), antes das peças nos ossos
    public class PoseDoCurativo : MonoBehaviour
    {
        const float VoltasPorSegundo = 0.9f;

        JogadorRede _rede;
        VidaJogador _vida;
        ArmaJogador _arma;
        InventarioJogador _inventario;
        readonly Dictionary<string, Transform> _ossos = new Dictionary<string, Transform>();
        float _peso, _fase;
        byte _qual;
        bool _maoEscondida;
        // o quadril agachado: se a animação não reescreve a posição dele, ela volta no quadro seguinte (não acumula)
        Transform _quadrilMexido;
        Vector3 _quadrilAntes, _quadrilDepois;

        // a primeira pessoa (no dono)
        GameObject _modeloMostrado;
        Vector3 _posBase;
        Quaternion _giroBase;
        float _pesoFP;

        void Start()
        {
            _rede = GetComponent<JogadorRede>();
            _vida = GetComponent<VidaJogador>();
            _arma = GetComponent<ArmaJogador>();
            _inventario = GetComponent<InventarioJogador>();
            if (_rede != null) _rede.CorpoMudou += LerOssos;
            LerOssos();
        }

        void OnDestroy()
        {
            if (_rede != null) _rede.CorpoMudou -= LerOssos;
        }

        void LerOssos()
        {
            _ossos.Clear();
            if (_rede == null || _rede.animadorCorpo == null) return;
            foreach (var t in _rede.animadorCorpo.GetComponentsInChildren<Transform>(true)) _ossos[t.name] = t;
        }

        Transform Osso(string nome) => _ossos.TryGetValue(nome, out var t) ? t : null;

        void LateUpdate()
        {
            if (_quadrilMexido != null)
            {
                if (_quadrilMexido.localPosition == _quadrilDepois) _quadrilMexido.localPosition = _quadrilAntes;
                _quadrilMexido = null;
            }
            if (_vida == null || !_vida.IsSpawned) return;
            byte v = _vida.Caido.Value || _vida.Desmaiado.Value || _vida.Dormindo.Value ? (byte)0 : _vida.Tratando.Value;
            if (v != 0) _qual = v;
            float dt = Time.deltaTime;
            _peso = Mathf.MoveTowards(_peso, v != 0 ? 1f : 0f, dt * 4f);
            _fase += dt * VoltasPorSegundo * 2f * Mathf.PI;
            if (_vida.IsOwner) PrimeiraPessoa(dt);
            EsconderOQueEstaNaMao(v != 0);
            if (_peso <= 0.001f || _ossos.Count == 0) return;
            if (_rede.Passageiro != null && _rede.Passageiro.NoCarro) return;
            Posar(_qual, Mathf.SmoothStep(0f, 1f, _peso));
        }

        // ------------------------------------------------ o corpo ------------------------------------------------

        void Posar(byte qual, float w)
        {
            var raiz = transform;
            Vector3 frente = raiz.forward, direita = raiz.right, cima = Vector3.up;
            bool perna = qual == (byte)ParteDoCorpo.PernaE + 1 || qual == (byte)ParteDoCorpo.PernaD + 1;
            bool braco = qual == (byte)ParteDoCorpo.BracoE + 1 || qual == (byte)ParteDoCorpo.BracoD + 1;
            bool cabeca = qual == (byte)ParteDoCorpo.Cabeca + 1;
            bool tronco = qual == (byte)ParteDoCorpo.Tronco + 1;
            bool amigo = qual == VidaJogador.TratandoOAmigo;
            bool boca = qual == VidaJogador.TratandoPelaBoca;

            // a perna: agachado (o quadril desce, os joelhos dobram e os pés ficam no chão)
            if (perna) Agachar(0.38f * w, frente);
            // o tronco desce para a perna e para o amigo; a cabeça olha para o que trata
            float inclinar = perna ? 58f : amigo ? 24f : tronco ? 10f : braco ? 6f : 0f;
            Inclinar("spine", inclinar * 0.45f * w, direita);
            Inclinar("chest", inclinar * 0.55f * w, direita);
            Inclinar("head", (perna ? 22f : braco ? 18f : tronco ? 24f : amigo ? 12f : boca ? -4f : 0f) * w, direita);

            // a volta da faixa: um círculo pequeno, de frente para quem enrola
            Vector3 volta = (direita * Mathf.Cos(_fase) + cima * Mathf.Sin(_fase)) * 0.045f;
            var peito = Osso("chest");
            var cabecaOsso = Osso("head");
            if (peito == null) return;

            if (perna)
            {
                string l = qual == (byte)ParteDoCorpo.PernaE + 1 ? ".L" : ".R";
                var canela = Osso("shin" + l);
                var pe = Osso("foot" + l);
                if (canela == null || pe == null) return;
                // o alto da canela, pela frente (logo abaixo do joelho)
                Vector3 meio = Vector3.Lerp(canela.position, pe.position, 0.15f) + frente * 0.08f;
                Mao(".L", meio - direita * 0.07f + volta, -direita + cima * 0.3f - frente * 0.4f, w);
                Mao(".R", meio + direita * 0.07f - volta, direita + cima * 0.3f - frente * 0.4f, w);
            }
            else if (braco)
            {
                bool esquerdo = qual == (byte)ParteDoCorpo.BracoE + 1;
                string ferido = esquerdo ? ".L" : ".R", outro = esquerdo ? ".R" : ".L";
                float lado = esquerdo ? -1f : 1f;
                // o braço ferido esticado à frente, um pouco para dentro
                Mao(ferido, peito.position + frente * 0.36f - cima * 0.16f + direita * (lado * 0.04f), -cima + direita * (lado * 0.4f), w);
                var antebraco = Osso("forearm" + ferido);
                var mao = Osso("hand" + ferido);
                if (antebraco == null || mao == null) return;
                // a outra mão dá a volta no antebraço
                Vector3 alvo = Vector3.Lerp(antebraco.position, mao.position, 0.5f) + volta * 0.9f + cima * 0.03f;
                Mao(outro, alvo, -cima + direita * (-lado * 0.5f), w);
            }
            else if (cabeca && cabecaOsso != null)
            {
                Mao(".L", cabecaOsso.position - direita * 0.12f + frente * 0.04f + cima * 0.04f + volta * 0.35f, -direita - cima * 0.4f, w);
                Mao(".R", cabecaOsso.position + direita * 0.12f + frente * 0.04f + cima * 0.04f - volta * 0.35f, direita - cima * 0.4f, w);
            }
            else if (boca && cabecaOsso != null)
            {
                Mao(".R", cabecaOsso.position + frente * 0.13f - cima * 0.07f, direita - cima, w);
            }
            else if (tronco)
            {
                Mao(".L", peito.position + frente * 0.2f - direita * 0.08f - cima * 0.05f + volta * 0.6f, -direita - cima, w);
                Mao(".R", peito.position + frente * 0.2f + direita * 0.08f - cima * 0.05f - volta * 0.6f, direita - cima, w);
            }
            else if (amigo)
            {
                Vector3 frenteBaixo = peito.position + frente * 0.55f - cima * 0.3f;
                Mao(".L", frenteBaixo - direita * 0.09f + volta, -direita - cima * 0.5f, w);
                Mao(".R", frenteBaixo + direita * 0.09f - volta, direita - cima * 0.5f, w);
            }
        }

        // agacha: o quadril desce (um pouco para trás) e as pernas dobram com os pés no lugar (o IK das pernas, com o joelho
        // para a frente)
        void Agachar(float quanto, Vector3 frente)
        {
            var quadril = Osso("hips");
            if (quadril == null || quanto <= 0.001f) return;
            Transform coxaE = Osso("thigh.L"), canelaE = Osso("shin.L"), peE = Osso("foot.L");
            Transform coxaD = Osso("thigh.R"), canelaD = Osso("shin.R"), peD = Osso("foot.R");
            if (coxaE == null || canelaE == null || peE == null || coxaD == null || canelaD == null || peD == null) return;
            Vector3 noChaoE = peE.position, noChaoD = peD.position;
            Quaternion giroE = peE.rotation, giroD = peD.rotation;
            _quadrilAntes = quadril.localPosition;
            quadril.position += Vector3.down * quanto - frente * (quanto * 0.35f);
            _quadrilDepois = quadril.localPosition;
            _quadrilMexido = quadril;
            DoisOssos(coxaE, canelaE, peE, noChaoE, frente, 1f);
            DoisOssos(coxaD, canelaD, peD, noChaoD, frente, 1f);
            peE.rotation = giroE;
            peD.rotation = giroD;
        }

        // o que está na mão do corpo (a arma) some enquanto trata; no fim, volta o que estiver na mão
        void EsconderOQueEstaNaMao(bool tratando)
        {
            var pecas = _rede != null ? _rede.naMaoCorpo : null;
            if (pecas == null) return;
            if (tratando)
            {
                _maoEscondida = true;
                foreach (var p in pecas) if (p != null && p.activeSelf) p.SetActive(false);
            }
            else if (_maoEscondida)
            {
                _maoEscondida = false;
                int m = Itens.MaoDe(_inventario != null ? _inventario.NaMao : Item.Nada);
                for (int i = 0; i < pecas.Length; i++)
                    if (pecas[i] != null) pecas[i].SetActive(i == m && !_rede.NaMaoEscondida);
            }
        }

        void Inclinar(string nome, float graus, Vector3 eixo)
        {
            var osso = Osso(nome);
            if (osso != null && Mathf.Abs(graus) > 0.01f) osso.rotation = Quaternion.AngleAxis(graus, eixo) * osso.rotation;
        }

        // a mão do lado (".L" ou ".R") até o alvo, com o cotovelo para o lado da dica, no peso w
        void Mao(string lado, Vector3 alvo, Vector3 dica, float w)
        {
            var ombro = Osso("upper_arm" + lado);
            var cotovelo = Osso("forearm" + lado);
            var pulso = Osso("hand" + lado);
            if (ombro == null || cotovelo == null || pulso == null) return;
            DoisOssos(ombro, cotovelo, pulso, alvo, dica, w);
        }

        /// <summary>
        /// O IK de dois ossos: dobra o meio para a distância do alvo (a lei dos cossenos), aponta a cadeia para o alvo e gira
        /// o cotovelo para o lado da <paramref name="dica"/>. Mistura com a pose da animação pelo <paramref name="peso"/>.
        /// </summary>
        public static void DoisOssos(Transform cima, Transform meio, Transform fim, Vector3 alvo, Vector3 dica, float peso)
        {
            if (peso <= 0f) return;
            Quaternion antesCima = cima.localRotation, antesMeio = meio.localRotation;
            Vector3 a = cima.position, b = meio.position, c = fim.position;
            float la = (b - a).magnitude, lb = (c - b).magnitude;
            if (la < 1e-4f || lb < 1e-4f) return;
            float d = Mathf.Clamp((alvo - a).magnitude, Mathf.Abs(la - lb) + 0.005f, la + lb - 0.005f);
            // 1) o cotovelo: o ângulo de dentro que chega à distância
            float atual = Vector3.Angle(a - b, c - b) * Mathf.Deg2Rad;
            float quer = Mathf.Acos(Mathf.Clamp((la * la + lb * lb - d * d) / (2f * la * lb), -1f, 1f));
            Vector3 eixo = Vector3.Cross(b - a, c - b);
            if (eixo.sqrMagnitude < 1e-8f) eixo = Vector3.Cross(b - a, dica);
            if (eixo.sqrMagnitude > 1e-8f) meio.rotation = Quaternion.AngleAxis((atual - quer) * Mathf.Rad2Deg, eixo.normalized) * meio.rotation;
            // 2) a cadeia toda aponta o pulso para o alvo
            c = fim.position;
            cima.rotation = Quaternion.FromToRotation(c - a, alvo - a) * cima.rotation;
            // 3) o cotovelo gira em volta do eixo ombro-alvo para o lado da dica
            Vector3 eixoDoAlvo = (alvo - a).normalized;
            Vector3 agora = Vector3.ProjectOnPlane(meio.position - a, eixoDoAlvo);
            Vector3 queroLado = Vector3.ProjectOnPlane(dica, eixoDoAlvo);
            if (agora.sqrMagnitude > 1e-6f && queroLado.sqrMagnitude > 1e-6f)
                cima.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(agora, queroLado, eixoDoAlvo), eixoDoAlvo) * cima.rotation;
            // a mistura com a animação (no espaço local: o meio é filho do de cima)
            Quaternion depoisCima = cima.localRotation, depoisMeio = meio.localRotation;
            cima.localRotation = Quaternion.Slerp(antesCima, depoisCima, peso);
            meio.localRotation = Quaternion.Slerp(antesMeio, depoisMeio, peso);
        }

        // ------------------------------------------------ a primeira pessoa (o dono) ------------------------------------------------

        void PrimeiraPessoa(float dt)
        {
            var t = Tratamento.Local;
            bool tratando = t != null && t.Ativo && _arma != null && _arma.modelos != null;
            // acabou ou parou: a mão volta na hora (antes de o jogador recarregar ou atirar de novo)
            if (!tratando)
            {
                _pesoFP = 0f;
                if (_modeloMostrado != null) DevolverAMao();
                return;
            }
            _pesoFP = Mathf.MoveTowards(_pesoFP, 1f, dt * 5f);
            if (_modeloMostrado == null) MostrarCurativo(t.Item);
            if (_modeloMostrado == null) return;
            // os braços se inclinam para baixo e dão voltas (enrolando); na perna, mais para o meio da tela (a câmera dos
            // braços não acompanha o olhar da câmera do mundo: eles ficam no lugar de sempre na tela)
            float w = Mathf.SmoothStep(0f, 1f, _pesoFP);
            bool fundo = (t.Parte == ParteDoCorpo.PernaE || t.Parte == ParteDoCorpo.PernaD) && t.Alvo == null;
            bool voltas = !PartesDoCorpo.PelaBoca(t.Item) && t.Item != Item.SprayAntisseptico;
            Vector3 volta = voltas ? new Vector3(Mathf.Cos(_fase), Mathf.Sin(_fase), 0f) * 0.025f : Vector3.zero;
            var tr = _modeloMostrado.transform;
            tr.localPosition = _posBase + (new Vector3(fundo ? -0.04f : 0f, 0f, 0.03f) + volta) * w;
            tr.localRotation = _giroBase * Quaternion.Euler(6f * w, 0f, 0f);
        }

        // os braços da lata com a malha do curativo, no lugar do que estava na mão (só a vista: o inventário não muda)
        void MostrarCurativo(Item item)
        {
            int m = Itens.ModeloNaMao(item);
            if (m < 0 || m >= _arma.modelos.Length || _arma.modelos[m] == null) return;
            for (int i = 0; i < _arma.modelos.Length; i++)
                if (_arma.modelos[i] != null) _arma.modelos[i].SetActive(i == m);
            ItemNaMaoProprio.Aplicar(_arma.modelos[m], item);
            _modeloMostrado = _arma.modelos[m];
            _posBase = _modeloMostrado.transform.localPosition;
            _giroBase = _modeloMostrado.transform.localRotation;
        }

        void DevolverAMao()
        {
            var tr = _modeloMostrado.transform;
            tr.localPosition = _posBase;
            tr.localRotation = _giroBase;
            _modeloMostrado = null;
            // a mão volta para o que está nela (sacando, como na troca de atalho)
            if (_arma != null && _inventario != null) _arma.Trocar(_arma.NaMao, _inventario.IdNaMao);
        }
    }
}
