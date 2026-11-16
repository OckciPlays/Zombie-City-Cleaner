using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A arma de quem vai sentado (16/11/2026, a pedido: "as mãos ficam parcialmente dentro do abdômen, a pistola fica muito
    /// próxima do colo, os braços apresentam ângulos artificiais, a animação parece misturar a pose de pilotagem com a de
    /// segurar uma pistola em pé"; as referências: GTA V, GTA IV e Days Gone). Em todas as máquinas, depois da pose de
    /// sentado (o Animator fica desligado na moto e na caçamba; no carro, por cima da animação), um rig feito no código com o
    /// IK de dois ossos do <see cref="PassageiroDoCarro"/> (o joelho e o cotovelo como dobradiças):
    /// <list type="bullet">
    /// <item><b>Os alvos</b> (<see cref="AlvosDosBracos"/>): a empunhadura (WeaponGripPoint), o pulso e o giro de cada mão
    /// (RightHandTarget, LeftHandTarget) e para onde vai cada cotovelo (RightElbowHint, LeftElbowHint).</item>
    /// <item><b>A empunhadura</b> (<see cref="EmpunhaduraDe"/>): a mão direita é posta pela arma do corpo (o vazio
    /// Arma_&lt;modelo&gt; no weapon.R e a boca, Boca_&lt;modelo&gt;): o cano na direção da mira e a arma em pé. Antes, os
    /// dedos é que apontavam para a mira, e a arma ficava torta.</item>
    /// <item><b>O garupa e os passageiros:</b> descansando, a arma fica com as duas mãos na frente do tronco, na altura do
    /// umbigo e 30 cm à frente dele (na moto, do lado direito, ao lado do piloto), o cano para a frente e para baixo; antes ela
    /// ficava no colo, e a mão esquerda entrava na barriga. Mirando para a frente, a arma sobe na frente do peito com as duas
    /// mãos e os cotovelos um pouco dobrados (na moto, pelo lado do piloto); para os lados (de 35° a 65°), a esquerda solta e
    /// a direita estica (como o garupa do GTA IV). O tronco vira com o olhar (até 60°).</item>
    /// <item><b>O piloto da moto:</b> sem mirar, as duas mãos no guidão e a arma guardada; mirando ou atirando, a direita sai
    /// do guidão e estica para o alvo (o cotovelo pouco dobrado), a esquerda continua no guidão e o tronco vira até 40° (como em
    /// Days Gone). A arma aparece com a mão fora do guidão (<see cref="AcertarNaMao"/>).</item>
    /// <item><b>As transições:</b> sacar e guardar em 0,25 s (as mãos saem das coxas, do guidão ou da animação e voltam para
    /// elas), levantar em 0,2 s e baixar em 0,5 s, a mão num arco pela frente do corpo (antes, em linha reta, ela cortava a
    /// barriga), e o coice do tiro (a mira sobe 7° e volta em 0,15 s). As pernas e o quadril não mudam.</item>
    /// <item>Os outros itens (a faca, a comida, os remédios) vão só na mão direita, que descansa na coxa e sobe para a frente.</item>
    /// </list>
    /// </summary>
    public partial class PassageiroDoCarro
    {
        /// <summary>Os alvos do rig dos braços sentado, a cada quadro.</summary>
        struct AlvosDosBracos
        {
            public Vector3 pegada;                         // WeaponGripPoint: onde fica a empunhadura da arma
            public Quaternion arma;                        // a arma no mundo: o cano no Z, o alto no Y
            public Vector3 maoDireita, maoEsquerda;        // RightHandTarget, LeftHandTarget (o pulso)
            public Quaternion giroDireita, giroEsquerda;   // o giro de cada mão
            public Vector3 cotoveloDireito, cotoveloEsquerdo;   // RightElbowHint, LeftElbowHint (para onde o cotovelo aponta)
            public float pesoDireita, pesoEsquerda;        // 0 a mão fica na pose de sentado, 1 na arma
        }

        /// <summary>A empunhadura de um item no espaço da mão direita do corpo: a pegada, o cano e o alto da arma.</summary>
        struct Empunhadura
        {
            public Vector3 pegada, cano, alto;
            public bool arma;   // uma arma de fogo (as duas mãos); senão, um item na mão direita
        }

        readonly Dictionary<Loot.Item, Empunhadura> _empunhaduras = new Dictionary<Loot.Item, Empunhadura>();
        AlvosDosBracos _alvos;
        Loot.Item _itemSentado = Loot.Item.Nada;
        // o quanto a arma está levantada (mirando, atirando) e sacada (na mão), de 0 a 1
        float _levantada, _armaLevantada, _sacada;
        int _levantadaNoQuadro = -1;

        /// <summary>Por quanto tempo (s) a arma de quem está sentado fica levantada depois de um tiro ou golpe.</summary>
        const float ArmaLevantadaDepoisDoAtaque = 1.6f;
        const float TempoDeSacar = 0.25f, TempoDeLevantar = 0.2f, TempoDeBaixar = 0.5f;
        /// <summary>O coice do tiro: a mira sobe isto (graus) e volta em <see cref="TempoDoCoice"/> segundos.</summary>
        const float Coice = 7f, TempoDoCoice = 0.15f;

        /// <summary>O peito do corpo (para a lanterna de quem vai na moto ou na caçamba sair dele), ou null.</summary>
        public Transform Peito => _ossos.TryGetValue("chest", out var p) ? p : null;

        /// <summary>
        /// Os braços com o que está na mão (o garupa, os passageiros, quem vai na caçamba e o piloto da moto armado). Vira o
        /// tronco e põe os dois braços; devolve false quando não há nada a fazer (os braços ficam na pose de sentado: no
        /// guidão, nas coxas ou na animação).
        /// </summary>
        bool BracosComArma(Carro c, int lugar)
        {
            if (c == null || !_ossos.TryGetValue("chest", out var peito) || !_ossos.TryGetValue("upper_arm.R", out var ombro)
                || !_ossos.TryGetValue("hand.R", out var maoD) || !_ossos.TryGetValue("hand.L", out var maoE)) return false;
            bool piloto = lugar == 0;
            if (piloto && !c.moto) return false;   // o motorista do carro: as mãos no volante
            var item = NaMao;
            bool serve = item != Loot.Item.Nada && !(c.moto && GrandeParaAMoto(item)) && (!piloto || ArmaDoPiloto(item));
            if (serve) _itemSentado = item;
            if (Time.frameCount != _levantadaNoQuadro)
            {
                _levantadaNoQuadro = Time.frameCount;
                float dt = Time.deltaTime;
                bool atacando = _rede.Mirando.Value || Time.time - _rede.UltimoAtaque < ArmaLevantadaDepoisDoAtaque;
                float alvo = serve && atacando ? 1f : 0f;
                _levantada = Mathf.MoveTowards(_levantada, alvo, dt / (alvo > _levantada ? TempoDeLevantar : TempoDeBaixar));
                _sacada = Mathf.MoveTowards(_sacada, serve ? 1f : 0f, dt / TempoDeSacar);
            }
            float k = Mathf.SmoothStep(0f, 1f, _levantada);
            _armaLevantada = k;
            // o piloto só tira a mão do guidão para mirar e atirar; os outros, assim que sacam
            float w = piloto ? k : Mathf.SmoothStep(0f, 1f, _sacada);
            if (w <= 0.001f || _itemSentado == Loot.Item.Nada) return false;
            // no dono, com os braços de primeira pessoa à vista, os do corpo estão encolhidos (BracosDoCorpoNaPrimeiraPessoa)
            if (_bracosDoCorpoEncolhidos) return true;

            Vector3 frente = transform.forward, cima = transform.up, direita = transform.right;
            var e = EmpunhaduraDe(_itemSentado, maoD);
            bool duasNaArma = e.arma && !piloto;

            // as poses sem a arma: no carro e na caçamba, onde a animação (ou a pose da caçamba) deixou as mãos, lidas antes de
            // o tronco virar; na moto, no guidão (o piloto) e nas coxas (o garupa), depois (as pernas e o guidão não viram)
            Vector3 livreD = maoD.position, livreE = maoE.position;
            Vector3 dobraLivreD = DobraDoCotovelo(".R"), dobraLivreE = DobraDoCotovelo(".L");
            Vector3 palmaLivreD = Vector3.zero, palmaLivreE = Vector3.zero;

            // o tronco vira para o lado do olhar com a arma levantada (os ombros vão junto); o piloto, menos (a mão esquerda
            // continua no guidão)
            float giro = piloto ? Mathf.Clamp(_rede.GiroSentado.Value, -60f, 110f)
                : Mathf.Clamp(_rede.GiroSentado.Value, -GiroDosBracos, GiroDosBracos);
            float tronco = (piloto ? Mathf.Clamp(giro * 0.45f, -25f, 40f) : Mathf.Clamp(giro * 0.6f, -60f, 60f)) * k;
            if (Mathf.Abs(tronco) > 0.5f && _ossos.TryGetValue("spine", out var coluna))
                coluna.rotation = Quaternion.AngleAxis(tronco, cima) * coluna.rotation;

            if (c.moto)
            {
                for (int s = 0; s < 2; s++)
                {
                    Vector3 p, d, palma = -cima;
                    if (piloto ? !NoGuidao(c, s, out p, out d, out palma) : !NaCoxa(s, out p, out d)) continue;
                    if (s == 0) { livreD = p; dobraLivreD = d; palmaLivreD = palma; }
                    else { livreE = p; dobraLivreE = d; palmaLivreE = palma; }
                }
            }

            // a mira: o olhar (o Pitch e o giro sentado, pela rede), com o coice do tiro
            float pitch = Mathf.Clamp(_rede.Pitch.Value, -50f, 50f);
            float desdeOTiro = Time.time - _rede.UltimoAtaque;
            if (e.arma && desdeOTiro >= 0f && desdeOTiro < TempoDoCoice) pitch -= Coice * (1f - desdeOTiro / TempoDoCoice);
            Vector3 miraAlta = transform.rotation * (Quaternion.Euler(pitch, giro, 0f) * Vector3.forward);
            Vector3 direitaDaMira = transform.rotation * (Quaternion.Euler(0f, giro, 0f) * Vector3.right);

            // levantada: as duas mãos na frente do peito (mirando para a frente) ou só a direita esticada (para os lados, e o
            // piloto sempre)
            float duasAlta = duasNaArma ? Mathf.Clamp01(1f - (Mathf.Abs(giro) - 35f) / 30f) : 0f;
            Vector3 ombros = peito.position + cima * 0.17f;
            Vector3 altaDuas = ombros + miraAlta * 0.40f + direitaDaMira * 0.03f - cima * 0.02f;
            Vector3 altaUma = ombro.position + miraAlta * (piloto ? 0.52f : 0.50f);
            Vector3 alta = Vector3.Lerp(altaUma, altaDuas, duasAlta);
            if (c.moto && !piloto)
            {
                // na garupa, olhando para a frente, a arma passa pelo lado direito do piloto, mais perto do peito (e não na
                // cabeça dele; da visão dele, "a arma fica no rosto do jogador")
                float paraAFrente = Mathf.Clamp01(1f - Mathf.Abs(giro) / 60f);
                alta += direita * (0.22f * paraAFrente) - miraAlta * (0.08f * paraAFrente);
            }

            // descansando: na frente do tronco, na altura do umbigo (o peito fica no meio do tronco), longe da barriga; na
            // garupa, do lado direito, ao lado do piloto. O cano para a frente e para baixo. Os itens: a mão na coxa direita
            Vector3 baixa, miraBaixa;
            if (piloto)
            {
                baixa = alta;   // (o piloto vem do guidão: o peso w é o quanto ele levantou)
                miraBaixa = miraAlta;
            }
            else if (!e.arma)
            {
                baixa = NaCoxa(0, out var coxa, out _) ? coxa + cima * 0.02f : peito.position - cima * 0.25f + frente * 0.3f;
                miraBaixa = (frente * 0.85f - cima * 0.35f - direita * 0.1f).normalized;
            }
            else if (c.moto)
            {
                // na garupa, só a direita, ao lado do corpo e por fora do tronco, a esquerda na coxa (16/11/2026, a pedido: "na
                // terceira pessoa ... os braços desaparecem e ficam apenas o tronco do personagem": com as duas mãos na frente
                // da barriga, de trás o tronco tapava os braços e a arma)
                baixa = peito.position - cima * 0.10f + frente * 0.12f + direita * 0.30f;
                miraBaixa = (frente * 0.85f - cima * 0.45f + direita * 0.12f).normalized;
            }
            else if (c.NaCacamba(lugar))
            {
                baixa = peito.position - cima * 0.12f + frente * 0.30f + direita * 0.05f;
                miraBaixa = (frente * 0.8f - cima * 0.6f).normalized;
            }
            else
            {
                // no banco do carro os joelhos ficam altos (a coxa sobe ~30°): na altura do umbigo, as mãos e a arma sumiam
                // entre as coxas; aqui, logo abaixo do peito, por cima dos joelhos
                baixa = peito.position + cima * 0.02f + frente * 0.26f + direita * 0.05f;
                miraBaixa = (frente * 0.85f - cima * 0.5f).normalized;
            }

            // entre as duas, um arco pela frente do corpo (em linha reta, a mão cortava a barriga)
            float arco = Mathf.Sin(k * Mathf.PI);
            var a = new AlvosDosBracos();
            a.pegada = Vector3.Lerp(baixa, alta, k) + frente * (0.08f * arco) + cima * (0.03f * arco);
            Vector3 mira = Vector3.Slerp(miraBaixa, miraAlta, k).normalized;
            // na garupa, a arma nunca chega perto da cabeça do piloto (ela passava na cara dele)
            float empurrao = 0f;
            if (c.moto && !piloto) a.pegada = ForaDaCabecaDoPiloto(c, a.pegada, mira, giro, out empurrao);
            Vector3 alto = Vector3.ProjectOnPlane(cima, mira);
            if (alto.sqrMagnitude < 1e-4f) alto = Vector3.ProjectOnPlane(-frente, mira);
            a.arma = Quaternion.LookRotation(mira, alto.normalized);
            Vector3 direitaDaArma = a.arma * Vector3.right, altoDaArma = a.arma * Vector3.up;

            // a direita: a mão posta pela empunhadura; o cotovelo para baixo e para fora (descansando, junto do corpo, sem
            // ir para trás: o antebraço ia para dentro da barriga)
            a.giroDireita = a.arma * Quaternion.Inverse(Quaternion.LookRotation(e.cano, e.alto));
            a.maoDireita = a.pegada - a.giroDireita * e.pegada;
            if (piloto) a.maoDireita += cima * (0.06f * arco);   // (saindo do guidão, ela sobe por cima dele)
            a.cotoveloDireito = Vector3.Lerp(-cima * 0.6f - frente * 0.15f + direita * 0.5f, -cima * 0.75f + direitaDaMira * 0.45f, k);
            a.pesoDireita = w;

            // a esquerda: com as duas mãos, abraça a empunhadura por baixo e pela esquerda (a palma para a arma e para cima, os
            // dedos por cima dos da direita); senão fica onde estava (o guidão, a coxa, a animação)
            // (empurrada para longe da cabeça do piloto, a esquerda não alcança: ele atira com uma mão, como o garupa do GTA IV)
            // (descansando na moto, uma mão só: a arma fica ao lado do corpo)
            float duasBaixa = c.moto ? 0f : 1f;
            float duas = duasNaArma ? Mathf.Lerp(duasBaixa, duasAlta, k) * Mathf.Clamp01(1f - (empurrao - 0.05f) / 0.10f) : 0f;
            Vector3 dedos = (direitaDaArma * 0.7f - altoDaArma * 0.55f + mira * 0.15f).normalized;
            Vector3 palmaE = Vector3.ProjectOnPlane(direitaDaArma * 0.6f + altoDaArma * 0.7f, dedos).normalized;
            Vector3 centroDaPalma = a.pegada - direitaDaArma * 0.04f - altoDaArma * 0.02f;
            a.maoEsquerda = centroDaPalma - dedos * 0.05f - palmaE * 0.015f;
            a.giroEsquerda = GiroDaMao(maoE, dedos, palmaE);
            a.cotoveloEsquerdo = Vector3.Lerp(-cima * 0.6f - frente * 0.15f - direita * 0.5f, -cima * 0.75f - direitaDaMira * 0.45f, k);
            a.pesoEsquerda = w * duas;
            _alvos = a;

            BracoPara(".R", livreD, dobraLivreD, palmaLivreD, a.maoDireita, a.cotoveloDireito, a.giroDireita, a.pesoDireita);
            BracoPara(".L", livreE, dobraLivreE, palmaLivreE, a.maoEsquerda, a.cotoveloEsquerdo, a.giroEsquerda, a.pesoEsquerda);
            return true;
        }

        bool _bracosDoCorpoEncolhidos;
        /// <summary>No dono: os braços do corpo estão encolhidos agora (na moto, na primeira pessoa, com os braços de primeira pessoa à vista).</summary>
        public bool BracosDoCorpoEncolhidos => _bracosDoCorpoEncolhidos;

        /// <summary>
        /// No dono, na moto e na primeira pessoa (onde o corpo dele aparece: as mãos no guidão e nas coxas), com os braços de
        /// primeira pessoa à vista (o garupa com algo na mão): os braços do corpo e o que está na mão dele somem (16/11/2026, a
        /// pedido: "parece que eu tenho um corpo 'molde' embaixo de mim": ele via a arma e as mãos duas vezes, as dos braços
        /// de primeira pessoa e as do corpo). O braço encolhe no ombro, como o pescoço do <see cref="JogadorRede.CorpoSemCabeca"/>.
        /// Os outros continuam vendo o corpo inteiro com a arma. O piloto armado não: ele vê o braço do corpo
        /// (<see cref="ArmaNoCorpo"/>).
        /// </summary>
        void BracosDoCorpoNaPrimeiraPessoa() =>
            EncolherBracosDoCorpo(_rede.IsOwner && Carro != null && Carro.moto && _fase == Fase.Dentro && !_terceira && _maosLivres && !ArmaNoCorpo);

        /// <summary>Encolhe (no ombro) ou devolve os braços do corpo. Quem mede os braços do corpo devolve antes e encolhe depois (<see cref="RoupasNosBracos"/>).</summary>
        public void EncolherBracosDoCorpo(bool encolher)
        {
            if (encolher == _bracosDoCorpoEncolhidos) return;
            _bracosDoCorpoEncolhidos = encolher;
            foreach (var l in new[] { ".L", ".R" })
                if (_ossos.TryGetValue("upper_arm" + l, out var braco)) braco.localScale = encolher ? Vector3.one * 0.001f : Vector3.one;
        }

        /// <summary>A distância mínima (m) de qualquer ponto da arma do garupa ao olho do piloto.</summary>
        const float FolgaDaCabecaDoPiloto = 0.42f;
        /// <summary>Quanto da arma vai à frente e atrás da pegada (m; a submetralhadora, a maior que cabe na moto: medida, de
        /// −0,27 com a coronha a 0,29 com o cano).</summary>
        const float FrenteDaArma = 0.30f, TrasDaArma = 0.28f;
        // o lado do piloto em que a arma do garupa passa (1 a direita, -1 a esquerda), suavizado na troca
        float _ladoDaArma = 1f, _ladoAlvo = 1f;
        int _ladoNoQuadro = -1;

        /// <summary>
        /// A arma do garupa fora da cabeça do piloto (16/11/2026, a pedido: "quando eu mirava com a arma, ela ultrapassava a
        /// cara do jogador que estava pilotando", com a pistola e com a submetralhadora; medido no Play: mirando para a frente
        /// e para a frente-esquerda, a arma chegava a 0–7 cm do olho do piloto). Nenhum ponto dela (da coronha até a boca da
        /// submetralhadora) chega a menos de <see cref="FolgaDaCabecaDoPiloto"/> do olho dele: ela vai para o lado (o direito
        /// mirando para a frente e para a direita, o esquerdo mirando para a esquerda, a partir de 35°). Trocando de lado, ela
        /// volta para perto do peito do garupa e passa por trás da cabeça do piloto, nunca na frente dela.
        /// </summary>
        Vector3 ForaDaCabecaDoPiloto(Carro c, Vector3 pegada, Vector3 mira, float giro, out float empurrao)
        {
            empurrao = 0f;
            if (c.assentos == null || c.assentos.Length == 0 || c.assentos[0] == null) return pegada;
            var banco = c.assentos[0];
            Vector3 olho = banco.position + banco.rotation * OlhoDe(c, 0);
            Vector3 lado = c.transform.right, frente = c.transform.forward, cima = c.transform.up;
            if (Time.frameCount != _ladoNoQuadro)
            {
                _ladoNoQuadro = Time.frameCount;
                if (giro < -35f) _ladoAlvo = -1f;
                else if (giro > -25f) _ladoAlvo = 1f;
                _ladoDaArma = Mathf.MoveTowards(_ladoDaArma, _ladoAlvo, Time.deltaTime * 2f / 0.3f);
            }
            float trocando = 1f - Mathf.Abs(_ladoDaArma);
            pegada -= frente * (0.3f * trocando) + cima * (0.12f * trocando);
            float x0 = Vector3.Dot(pegada - olho, lado), ml = Vector3.Dot(mira, lado);
            float r2 = FolgaDaCabecaDoPiloto * FolgaDaCabecaDoPiloto;
            float precisaDireita = float.MinValue, precisaEsquerda = float.MaxValue;
            for (int i = 0; i <= 8; i++)
            {
                float t = Mathf.Lerp(-TrasDaArma, FrenteDaArma, i / 8f);
                Vector3 d = pegada + mira * t - olho;
                float lateral = Vector3.Dot(d, lado);
                float resto = d.sqrMagnitude - lateral * lateral;
                if (resto >= r2) continue;
                float meia = Mathf.Sqrt(r2 - resto);
                precisaDireita = Mathf.Max(precisaDireita, meia - ml * t);
                precisaEsquerda = Mathf.Min(precisaEsquerda, -meia - ml * t);
            }
            float xDireita = Mathf.Max(x0, precisaDireita), xEsquerda = Mathf.Min(x0, precisaEsquerda);
            float x = Mathf.Lerp(xEsquerda, xDireita, (_ladoDaArma + 1f) * 0.5f);
            empurrao = Mathf.Abs(x - x0) + 0.3f * trocando;
            return pegada + lado * (x - x0);
        }

        /// <summary>
        /// Um braço entre a pose de sentado (<paramref name="livre"/>, com a palma para <paramref name="palmaLivre"/>, ou como
        /// estava, se zero) e a da arma (o pulso, o cotovelo e o giro da mão), pelo peso. Parte do giro da mão vai no antebraço
        /// (a pronação, até 80°) e o resto no pulso.
        /// </summary>
        void BracoPara(string l, Vector3 livre, Vector3 dobraLivre, Vector3 palmaLivre, Vector3 pulso, Vector3 dobra, Quaternion giro, float w)
        {
            DoisOssos("upper_arm" + l, "forearm" + l, Vector3.Lerp(livre, pulso, w), Vector3.Lerp(dobraLivre, dobra, w));
            if (palmaLivre.sqrMagnitude > 1e-6f) Palma(l, palmaLivre);
            if (w <= 0.001f || !_ossos.TryGetValue("hand" + l, out var mao) || !_ossos.TryGetValue("forearm" + l, out var antebraco)) return;
            var relativo = Quaternion.Inverse(mao.rotation) * giro;
            float torcao = Mathf.DeltaAngle(0f, 2f * Mathf.Atan2(relativo.y, relativo.w) * Mathf.Rad2Deg);
            antebraco.rotation = Quaternion.AngleAxis(Mathf.Clamp(torcao * 0.6f, -80f, 80f) * w, antebraco.up) * antebraco.rotation;
            mao.rotation = Quaternion.Slerp(mao.rotation, giro, w);
        }

        /// <summary>O giro da mão com os dedos em <paramref name="dedos"/> e a palma para <paramref name="palma"/> (no mundo).</summary>
        Quaternion GiroDaMao(Transform mao, Vector3 dedos, Vector3 palma)
        {
            if (!_palma.TryGetValue(mao, out var local)) return mao.rotation;
            Vector3 palmaLocal = Vector3.ProjectOnPlane(local, Vector3.up);   // os dedos vão ao longo do eixo Y da mão
            if (palmaLocal.sqrMagnitude < 1e-6f || palma.sqrMagnitude < 1e-6f) return mao.rotation;
            return Quaternion.LookRotation(palma, dedos) * Quaternion.Inverse(Quaternion.LookRotation(palmaLocal.normalized, Vector3.up));
        }

        // para onde o cotovelo aponta agora (do meio entre o ombro e a mão até ele)
        Vector3 DobraDoCotovelo(string l)
        {
            if (!_ossos.TryGetValue("upper_arm" + l, out var braco) || !_ossos.TryGetValue("forearm" + l, out var antebraco)
                || !_ossos.TryGetValue("hand" + l, out var mao)) return -transform.up;
            Vector3 d = antebraco.position - (braco.position + mao.position) * 0.5f;
            return d.sqrMagnitude > 1e-6f ? d : -transform.up;
        }

        /// <summary>A mão do piloto no punho do guidão (s: 0 o direito, 1 o esquerdo): o pulso, o cotovelo para fora e para baixo, a palma para baixo.</summary>
        bool NoGuidao(Carro c, int s, out Vector3 punho, out Vector3 dobra, out Vector3 palma)
        {
            float lado = s == 0 ? 1f : -1f;
            Vector3 frente = transform.forward, cima = transform.up, direita = transform.right;
            dobra = -cima * 0.55f + direita * (lado * 0.75f) - frente * 0.3f;
            palma = -cima + frente * 0.2f;
            punho = default;
            if (c == null || c.punhos == null || s >= c.punhos.Length || c.punhos[s] == null) return false;
            punho = c.punhos[s].position;
            return true;
        }

        /// <summary>A mão do garupa na coxa (s: 0 a direita, 1 a esquerda), perto do joelho, com o cotovelo para trás e para fora.</summary>
        bool NaCoxa(int s, out Vector3 naCoxa, out Vector3 dobra)
        {
            string l = s == 0 ? ".R" : ".L";
            float lado = s == 0 ? 1f : -1f;
            Vector3 frente = transform.forward, cima = transform.up, direita = transform.right;
            dobra = -frente * 0.6f + direita * (lado * 0.6f) - cima * 0.2f;
            naCoxa = default;
            if (!_ossos.TryGetValue("thigh" + l, out var coxa) || !_ossos.TryGetValue("shin" + l, out var joelho)) return false;
            naCoxa = Vector3.Lerp(coxa.position, joelho.position, 0.6f) + cima * 0.07f + direita * (lado * 0.02f);
            return true;
        }

        /// <summary>
        /// A empunhadura do item no espaço da mão direita (lida uma vez por item e por corpo). Numa arma de fogo, da arma do
        /// corpo: a pegada é o vazio Arma_&lt;modelo&gt; (preso no weapon.R), o cano é o eixo dele mais alinhado com a boca
        /// (Boca_&lt;modelo&gt;) e o alto, dos outros dois, o que vai para o lado da boca (o cano fica acima da pegada). Nos
        /// outros itens, a palma: os dedos para a frente e o polegar para cima.
        /// </summary>
        Empunhadura EmpunhaduraDe(Loot.Item item, Transform mao)
        {
            if (_empunhaduras.TryGetValue(item, out var pronta)) return pronta;
            var e = new Empunhadura { pegada = new Vector3(0f, 0.07f, 0f), cano = Vector3.up, alto = Vector3.right, arma = false };
            if (Loot.Itens.EhArma(item) && _rede.naMaoCorpo != null && _rede.bocasCorpo != null)
            {
                int m = Loot.Itens.ModeloNaMao(item), t = (int)Loot.Itens.Arma(item);
                var arma = m >= 0 && m < _rede.naMaoCorpo.Length ? _rede.naMaoCorpo[m] : null;
                var boca = t >= 0 && t < _rede.bocasCorpo.Length ? _rede.bocasCorpo[t] : null;
                Vector3 paraBoca = arma != null && boca != null ? boca.position - arma.transform.position : Vector3.zero;
                if (paraBoca.sqrMagnitude > 1e-6f)
                {
                    var p = arma.transform;
                    Vector3[] eixos = { p.right, p.up, p.forward };
                    int ic = 0;
                    float melhor = -1f;
                    for (int i = 0; i < 3; i++)
                    {
                        float d = Mathf.Abs(Vector3.Dot(eixos[i], paraBoca.normalized));
                        if (d > melhor) { melhor = d; ic = i; }
                    }
                    Vector3 cano = eixos[ic] * Mathf.Sign(Vector3.Dot(eixos[ic], paraBoca));
                    Vector3 resto = Vector3.ProjectOnPlane(paraBoca, cano);
                    int ia = ic == 0 ? 1 : 0;
                    melhor = -1f;
                    for (int i = 0; i < 3; i++)
                    {
                        if (i == ic) continue;
                        float d = Mathf.Abs(Vector3.Dot(eixos[i], resto));
                        if (d > melhor) { melhor = d; ia = i; }
                    }
                    Vector3 alto = eixos[ia] * (Vector3.Dot(eixos[ia], resto) >= 0f ? 1f : -1f);
                    e = new Empunhadura
                    {
                        pegada = mao.InverseTransformPoint(p.position),
                        cano = mao.InverseTransformDirection(cano).normalized,
                        alto = mao.InverseTransformDirection(alto).normalized,
                        arma = true,
                    };
                }
            }
            _empunhaduras[item] = e;
            return e;
        }

        /// <summary>
        /// Para os testes (e para desenhar): os alvos do rig sentado do último quadro: a empunhadura, o pulso de cada mão, para
        /// onde vai cada cotovelo e o peso de cada braço na arma.
        /// </summary>
        public (Vector3 pegada, Vector3 maoDireita, Vector3 maoEsquerda, Vector3 cotoveloDireito, Vector3 cotoveloEsquerdo, float pesoDireita, float pesoEsquerda) AlvosDaArma =>
            (_alvos.pegada, _alvos.maoDireita, _alvos.maoEsquerda, _alvos.cotoveloDireito, _alvos.cotoveloEsquerdo, _alvos.pesoDireita, _alvos.pesoEsquerda);

        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || _alvos.pesoDireita <= 0f) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_alvos.pegada, 0.02f);
            Gizmos.DrawRay(_alvos.pegada, _alvos.arma * Vector3.forward * 0.2f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_alvos.maoDireita, 0.025f);
            Gizmos.DrawRay(_alvos.maoDireita, _alvos.cotoveloDireito.normalized * 0.15f);
            if (_alvos.pesoEsquerda <= 0f) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_alvos.maoEsquerda, 0.025f);
            Gizmos.DrawRay(_alvos.maoEsquerda, _alvos.cotoveloEsquerdo.normalized * 0.15f);
        }
    }
}
