using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// A queda do sobrevivente morto (02/11/2026): o corpo do jogador não tem uma animação de morte (o clipe "Cair" do FBX
    /// é a chegada no chão depois do pulo), então ele desaba com física, em cada máquina: um Rigidbody nos ossos grandes,
    /// ligados por juntas, e as partes do corpo que levavam tiro viram o corpo que bate no chão. O empurrão vem do tiro (ou
    /// do golpe) que matou. Em 8 s ele assenta (a física para) e fica ali.
    /// </summary>
    public partial class Sobrevivente
    {
        // o osso, o osso de cima (o que segura a junta), a massa e as folgas da junta (o giro e o balanço, em graus)
        static readonly (string osso, string pai, float massa, float giro, float balanco)[] OssosDaQueda =
        {
            ("hips", null, 10f, 0f, 0f),
            ("spine", "hips", 7f, 20f, 20f),
            ("chest", "spine", 8f, 20f, 20f),
            ("head", "chest", 4f, 35f, 30f),
            ("upper_arm.L", "chest", 2f, 50f, 50f),
            ("forearm.L", "upper_arm.L", 1.5f, 70f, 10f),
            ("upper_arm.R", "chest", 2f, 50f, 50f),
            ("forearm.R", "upper_arm.R", 1.5f, 70f, 10f),
            ("thigh.L", "hips", 5f, 40f, 35f),
            ("shin.L", "thigh.L", 3f, 70f, 8f),
            ("thigh.R", "hips", 5f, 40f, 35f),
            ("shin.R", "thigh.R", 3f, 70f, 8f),
        };

        List<Rigidbody> _boneco;
        float _assentaEm;

        void Desmoronar(bool ja)
        {
            if (corpo == null) return;
            if (corpo.animador != null) corpo.animador.enabled = false;
            var ossos = new Dictionary<string, Transform>();
            foreach (var t in corpo.GetComponentsInChildren<Transform>(true)) ossos.TryAdd(t.name, t);
            var corpos = new Dictionary<string, Rigidbody>();
            _boneco = new List<Rigidbody>();
            foreach (var (osso, pai, massa, giro, balanco) in OssosDaQueda)
            {
                if (!ossos.TryGetValue(osso, out var t)) continue;
                var rb = t.gameObject.AddComponent<Rigidbody>();
                rb.mass = massa;
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.8f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                corpos[osso] = rb;
                _boneco.Add(rb);
                if (pai == null || !corpos.TryGetValue(pai, out var dePai)) continue;
                var j = t.gameObject.AddComponent<CharacterJoint>();
                j.connectedBody = dePai;
                j.enableProjection = true;
                j.lowTwistLimit = new SoftJointLimit { limit = -giro };
                j.highTwistLimit = new SoftJointLimit { limit = giro };
                j.swing1Limit = new SoftJointLimit { limit = balanco };
                j.swing2Limit = new SoftJointLimit { limit = balanco };
            }
            // o quadril não tem parte própria: uma esfera para ele bater no chão
            if (ossos.TryGetValue("hips", out var quadril))
            {
                var q = new GameObject("Queda_Quadril") { layer = Camada };
                q.transform.SetParent(quadril, false);
                q.AddComponent<SphereCollider>().radius = 0.13f / Mathf.Max(0.0001f, quadril.lossyScale.x);
            }
            // as partes do corpo viram o corpo que cai (o tiro passa por elas: ArmaJogador confere o Morto)
            foreach (var p in _partes) if (p != null && p.colisor != null) p.colisor.enabled = true;
            Vector3 e = _empurrao.Value;
            if (!ja && e.sqrMagnitude > 0.01f && corpos.TryGetValue("chest", out var peito)) peito.AddForce(e, ForceMode.Impulse);
            _assentaEm = Time.time + (ja ? 3f : 8f);
        }

        // caído há um tempo: a física para (o corpo fica onde caiu)
        void Assentar()
        {
            if (_boneco == null || Time.time < _assentaEm) return;
            foreach (var rb in _boneco) if (rb != null) rb.isKinematic = true;
            _boneco = null;
        }
    }
}
